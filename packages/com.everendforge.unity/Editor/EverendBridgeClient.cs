using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendBridgeWireMessage
    {
        public string MessageType;
        public string MessageId;
        public string SessionId;
        public string ProjectId;
        public string StoryId;
        public int BaseRevision;
        public int Revision;
        public string Origin;
        public string PayloadJson;
        public string RuntimePackageJson;
        public string Token;

        internal static EverendBridgeWireMessage Parse(string json)
        {
            var root = EverendJson.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Bridge message must be an object.");
            var result = new EverendBridgeWireMessage
            {
                MessageType = String(root, "messageType"), MessageId = String(root, "messageId"), SessionId = String(root, "sessionId"),
                ProjectId = String(root, "projectId"), StoryId = String(root, "storyId"), BaseRevision = Int(root, "baseRevision"), Revision = Int(root, "revision"),
                Origin = String(root, "origin")
            };
            var payload = Object(root, "payload");
            result.PayloadJson = EverendJsonWriter.Write(payload ?? new Dictionary<string, object>());
            result.Token = payload == null ? string.Empty : String(payload, "token");
            var runtimePackage = payload == null ? null : Object(payload, "runtimePackage");
            if (runtimePackage != null) result.RuntimePackageJson = EverendJsonWriter.Write(runtimePackage);
            return result;
        }

        private static Dictionary<string, object> Object(Dictionary<string, object> source, string key) { object value; return source != null && source.TryGetValue(key, out value) ? value as Dictionary<string, object> : null; }
        private static string String(Dictionary<string, object> source, string key) { object value; return source != null && source.TryGetValue(key, out value) && value != null ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : string.Empty; }
        private static int Int(Dictionary<string, object> source, string key) { object value; if (source == null || !source.TryGetValue(key, out value) || value == null) return 0; if (value is long) return (int)(long)value; if (value is double) return (int)(double)value; int result; return int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out result) ? result : 0; }
    }

    public sealed class EverendBridgeClient : IEverendBridgeClient, IDisposable
    {
        private readonly EverendBridgeSettings settings;
        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private ClientWebSocket socket;
        private CancellationTokenSource cancellation;
        private string sessionId;
        private int revision;
        private EverendSnapshot pendingSnapshot;
        private bool disposed;

        public EverendBridgeSessionState State { get; private set; } = new EverendBridgeSessionState { Status = EverendBridgeConnectionStatus.Disconnected, Message = "Disconnected." };
        public event Action<EverendBridgeSessionState> StateChanged;
        public event Action<EverendBridgeWireMessage> MessageReceived;

        public EverendBridgeClient(EverendBridgeSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            EditorApplication.update += PumpMessages;
        }

        public async Task StartServiceAsync()
        {
            var running = await CheckServerAsync();
            if (running.Status == EverendBridgeServerStatus.Online)
            {
                SetState(EverendBridgeConnectionStatus.Connecting, "Bridge already running; connecting Unity to the existing service.");
                return;
            }
            var executable = settings.ResolveDaemonExecutable();
            if (string.IsNullOrWhiteSpace(executable)) throw new InvalidOperationException("Bridge daemon executable not found. Open the Connection tab and choose everend-forge-bridge.cmd.");
            settings.daemonExecutable = executable;
            var port = Port(settings.endpoint);
            StartProcess("start --port " + port + " --project-id " + Quote(settings.projectId) + " --story-id " + Quote(settings.storyId));
            await Task.Delay(250);
            SetState(EverendBridgeConnectionStatus.Connecting, "Bridge service start requested.");
        }

        public async Task<EverendBridgeServerState> CheckServerAsync()
        {
            var endpoint = NormalizeEndpoint(settings.endpoint);
            try
            {
                await GetAsync(endpoint + "/health");
                return new EverendBridgeServerState { Status = EverendBridgeServerStatus.Online, Endpoint = endpoint, Message = "The local daemon is running." };
            }
            catch (Exception exception)
            {
                return new EverendBridgeServerState { Status = EverendBridgeServerStatus.Offline, Endpoint = endpoint, Message = "The local daemon is not reachable: " + exception.Message };
            }
        }

        public async Task StopServiceAsync()
        {
            var executable = settings.ResolveDaemonExecutable();
            if (string.IsNullOrWhiteSpace(executable)) throw new InvalidOperationException("Bridge daemon executable not found. Open the Connection tab and choose everend-forge-bridge.cmd.");
            settings.daemonExecutable = executable;
            StartProcess("stop --port " + Port(settings.endpoint));
            await Task.Delay(100);
            await DisconnectAsync();
        }

        public async Task ConnectAsync(string endpoint, string pairingCode)
        {
            await DisconnectAsync();
            SetState(EverendBridgeConnectionStatus.Connecting, "Reading bridge session.");
            var sessionJson = await GetAsync(NormalizeEndpoint(endpoint) + "/v1/session");
            var session = EverendJson.Parse(sessionJson) as Dictionary<string, object>;
            if (session == null) throw new InvalidOperationException("Bridge session response is not an object.");
            sessionId = String(session, "sessionId"); revision = Int(session, "revision");
            State.Endpoint = NormalizeEndpoint(endpoint); State.SessionId = sessionId; State.ProjectId = settings.projectId; State.StoryId = settings.storyId; State.PairingCode = String(session, "pairingCode"); State.RemoteRevision = revision;
            cancellation = new CancellationTokenSource();
            socket = new ClientWebSocket();
            var wsUri = new Uri(ToWebSocketEndpoint(endpoint) + "/v1/sync");
            await socket.ConnectAsync(wsUri, cancellation.Token);
            var token = settings.GetToken();
            await SendAsync(BuildEnvelope("hello", "{\"role\":\"unity\",\"clientId\":\"unity-editor\",\"pairingCode\":" + JsonString(string.IsNullOrWhiteSpace(pairingCode) ? State.PairingCode : pairingCode) + ",\"token\":" + JsonString(token) + "}"));
            SetState(EverendBridgeConnectionStatus.Connected, "Connected. Waiting for PathBranching snapshot.");
            _ = ReceiveLoop(cancellation.Token);
        }

        public async Task DisconnectAsync()
        {
            cancellation?.Cancel();
            if (socket != null && socket.State == WebSocketState.Open)
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None); } catch { }
            }
            socket = null; cancellation = null; SetState(EverendBridgeConnectionStatus.Disconnected, "Disconnected.");
        }

        public Task RequestSnapshotAsync() { return SendEnvelopeAsync("inventory", "{\"requestSnapshot\":true}"); }

        public async Task RestartCurrentEventAsync()
        {
            if (pendingSnapshot == null) throw new InvalidOperationException("There is no pending snapshot to restart.");
            var coordinator = new EverendSyncCoordinator(settings);
            var result = coordinator.Apply(coordinator.Analyze(pendingSnapshot));
            pendingSnapshot = null;
            State.UpdatePendingInPlayMode = false;
            SetState(result.Success ? EverendBridgeConnectionStatus.Connected : EverendBridgeConnectionStatus.Error, result.Message);
            await SendApplyResultAsync(result);
        }

        public Task SendInventoryAsync()
        {
            var inventory = EverendInventoryScanner.Scan(settings.Profile);
            var payload = "{\"client\":\"unity-editor\",\"inventory\":" + EverendInventoryScanner.ToJson(inventory) + "}";
            return SendEnvelopeAsync("inventory", payload);
        }

        public Task SendChangeSetAsync(EverendChangeSet changeSet)
        {
            var operations = new List<string>();
            foreach (var operation in changeSet == null ? new List<EverendChangeOperation>() : changeSet.Operations)
                operations.Add("{\"operation\":" + JsonString(operation.Operation) + ",\"entityType\":" + JsonString(operation.EntityType) + ",\"entityId\":" + JsonString(operation.EntityId) + ",\"propertyPath\":" + JsonString(operation.PropertyPath) + ",\"ownership\":" + JsonString(operation.Ownership) + ",\"value\":" + (string.IsNullOrWhiteSpace(operation.ValueJson) ? "null" : operation.ValueJson) + ",\"reason\":" + JsonString(operation.Reason) + "}");
            return SendEnvelopeAsync("changeSet", "{\"operations\":[" + string.Join(",", operations.ToArray()) + "],\"description\":" + JsonString(changeSet == null ? string.Empty : changeSet.Description) + "}");
        }

        public async Task ApplySnapshotAsync(EverendSnapshot snapshot, EverendSyncCoordinator coordinator)
        {
            var result = await coordinator.ApplySnapshotAsync(snapshot);
            SetState(result.Success ? EverendBridgeConnectionStatus.Connected : EverendBridgeConnectionStatus.Error, result.Message);
            await SendApplyResultAsync(result);
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (!token.IsCancellationRequested && socket != null && socket.State == WebSocketState.Open)
                {
                    var builder = new StringBuilder();
                    WebSocketReceiveResult received;
                    do
                    {
                        received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (received.MessageType == WebSocketMessageType.Close) return;
                        builder.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
                    } while (!received.EndOfMessage);
                    incoming.Enqueue(builder.ToString());
                }
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested) SetState(EverendBridgeConnectionStatus.Error, exception.Message);
            }
        }

        private void PumpMessages()
        {
            string json;
            while (incoming.TryDequeue(out json))
            {
                try
                {
                    var message = EverendBridgeWireMessage.Parse(json);
                    if (!string.IsNullOrWhiteSpace(message.Token)) settings.SetToken(message.Token);
                    revision = Math.Max(revision, message.Revision); State.RemoteRevision = revision; State.LastMessageType = message.MessageType;
                    if (message.MessageType == "error") SetState(EverendBridgeConnectionStatus.Error, message.PayloadJson);
                    else if (message.MessageType == "snapshot" && !string.IsNullOrWhiteSpace(message.RuntimePackageJson))
                    {
                        var snapshot = new EverendSnapshot { RuntimePackageJson = message.RuntimePackageJson, SourceDescription = "live:" + State.Endpoint, Revision = message.Revision, IsLive = true };
                        MessageReceived?.Invoke(message);
                        var coordinator = new EverendSyncCoordinator(settings);
                        if (EditorApplication.isPlaying) { pendingSnapshot = snapshot; State.UpdatePendingInPlayMode = true; SetState(EverendBridgeConnectionStatus.Connected, "Snapshot received; apply is pending until Play Mode stops."); }
                        else _ = ApplySnapshotOnMainThread(snapshot, coordinator);
                    }
                    else MessageReceived?.Invoke(message);
                }
                catch (Exception exception) { SetState(EverendBridgeConnectionStatus.Error, exception.Message); }
            }
        }

        private async Task ApplySnapshotOnMainThread(EverendSnapshot snapshot, EverendSyncCoordinator coordinator)
        {
            var result = coordinator.Analyze(snapshot);
            var apply = coordinator.Apply(result);
            SetState(apply.Success ? EverendBridgeConnectionStatus.Connected : EverendBridgeConnectionStatus.Error, apply.Message);
            await SendApplyResultAsync(apply);
        }

        private Task SendApplyResultAsync(EverendApplyResult result)
        {
            var payload = "{\"success\":" + (result.Success ? "true" : "false") + ",\"dryRun\":" + (result.DryRun ? "true" : "false") + ",\"revision\":" + result.Revision + ",\"message\":" + JsonString(result.Message) + ",\"errors\":" + JsonString(string.Join(" | ", result.Report.Errors.ToArray())) + "}";
            return SendEnvelopeAsync("applyResult", payload);
        }

        private async Task SendEnvelopeAsync(string type, string payload)
        {
            if (socket == null || socket.State != WebSocketState.Open) throw new InvalidOperationException("Everend Forge Bridge is not connected.");
            await SendAsync(BuildEnvelope(type, payload));
        }

        private async Task SendAsync(string json) { var bytes = Encoding.UTF8.GetBytes(json); await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation == null ? CancellationToken.None : cancellation.Token); }

        private string BuildEnvelope(string type, string payload)
        {
            return "{\"syncProtocolVersion\":\"0.1\",\"messageType\":" + JsonString(type) + ",\"messageId\":" + JsonString(Guid.NewGuid().ToString("N")) + ",\"sessionId\":" + JsonString(sessionId) + ",\"projectId\":" + JsonString(settings.projectId) + ",\"storyId\":" + JsonString(settings.storyId) + ",\"baseRevision\":" + revision + ",\"revision\":" + revision + ",\"origin\":\"unity\",\"payload\":" + payload + "}";
        }

        private void StartProcess(string arguments) { Process.Start(new ProcessStartInfo { FileName = settings.daemonExecutable, Arguments = arguments, UseShellExecute = true, CreateNoWindow = !settings.showTerminal, WindowStyle = settings.showTerminal ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden }); }
        private static async Task<string> GetAsync(string url) { var request = WebRequest.CreateHttp(url); request.Method = "GET"; using (var response = await request.GetResponseAsync()) using (var reader = new StreamReader(response.GetResponseStream())) return await reader.ReadToEndAsync(); }
        private static string NormalizeEndpoint(string endpoint) { return (endpoint ?? string.Empty).Trim().TrimEnd('/').Replace("ws://", "http://").Replace("wss://", "https://"); }
        private static string ToWebSocketEndpoint(string endpoint) { return NormalizeEndpoint(endpoint).Replace("http://", "ws://").Replace("https://", "wss://"); }
        private static int Port(string endpoint) { Uri uri; return Uri.TryCreate(NormalizeEndpoint(endpoint), UriKind.Absolute, out uri) && uri.Port > 0 ? uri.Port : 47831; }
        private static string Quote(string value) { return "\"" + (value ?? string.Empty).Replace("\"", "") + "\""; }
        private static string String(Dictionary<string, object> source, string key) { object value; return source != null && source.TryGetValue(key, out value) && value != null ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : string.Empty; }
        private static int Int(Dictionary<string, object> source, string key) { object value; if (source == null || !source.TryGetValue(key, out value) || value == null) return 0; if (value is long) return (int)(long)value; if (value is double) return (int)(double)value; int result; return int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out result) ? result : 0; }
        private static string JsonString(string value) { return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\""; }
        private void SetState(EverendBridgeConnectionStatus status, string message) { State.Status = status; State.Message = message ?? string.Empty; StateChanged?.Invoke(State); }
        public void Dispose() { if (disposed) return; disposed = true; EditorApplication.update -= PumpMessages; cancellation?.Cancel(); socket?.Dispose(); }
    }

    internal static class EverendJsonWriter
    {
        public static string Write(object value)
        {
            if (value == null) return "null";
            var text = value as string; if (text != null) return JsonString(text);
            if (value is bool) return ((bool)value) ? "true" : "false";
            if (value is long || value is double || value is int || value is float || value is decimal) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            var dictionary = value as Dictionary<string, object>; if (dictionary != null) { var values = new List<string>(); foreach (var pair in dictionary) values.Add(JsonString(pair.Key) + ":" + Write(pair.Value)); return "{" + string.Join(",", values.ToArray()) + "}"; }
            var list = value as List<object>; if (list != null) { var values = new List<string>(); foreach (var item in list) values.Add(Write(item)); return "[" + string.Join(",", values.ToArray()) + "]"; }
            return JsonString(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }
        private static string JsonString(string value) { return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\""; }
    }
}
