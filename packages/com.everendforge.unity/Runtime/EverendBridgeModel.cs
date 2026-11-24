using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EverendForge.Unity
{
    public enum EverendBridgeMode { Live, Manual }
    public enum EverendBridgeConnectionStatus { Disconnected, Connecting, Connected, Reconnecting, Error }
    public enum EverendBridgeServerStatus { Unknown, Offline, Online }

    [Serializable]
    public sealed class EverendBridgeServerState
    {
        public EverendBridgeServerStatus Status;
        public string Message;
        public string Endpoint;
    }

    [Serializable]
    public sealed class EverendBridgeSessionState
    {
        public EverendBridgeConnectionStatus Status;
        public string Message;
        public string Endpoint;
        public string ProjectId;
        public string StoryId;
        public string SessionId;
        public string PairingCode;
        public int LocalRevision;
        public int RemoteRevision;
        public string LastMessageType;
        public bool UpdatePendingInPlayMode;
    }

    [Serializable]
    public sealed class EverendBridgeReport
    {
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> Creates = new List<string>();
        public List<string> Updates = new List<string>();
        public List<string> Preserved = new List<string>();
        public List<string> Conflicts = new List<string>();
        public List<string> Orphans = new List<string>();
        public bool Success { get { return Errors.Count == 0 && Conflicts.Count == 0; } }
        public override string ToString() { return "Everend Bridge report: " + (Success ? "OK" : "FAILED") + ", creates=" + Creates.Count + ", updates=" + Updates.Count + ", warnings=" + Warnings.Count + ", conflicts=" + Conflicts.Count + ", orphans=" + Orphans.Count; }
    }

    [Serializable]
    public sealed class EverendChangeSet
    {
        public List<EverendChangeOperation> Operations = new List<EverendChangeOperation>();
        public string Description;
    }

    [Serializable]
    public sealed class EverendChangeOperation
    {
        public string Operation;
        public string EntityType;
        public string EntityId;
        public string PropertyPath;
        public string Ownership;
        public string ValueJson;
        public string Reason;
    }

    /// <summary>Conceptual bridge API. The editor assembly supplies the transport implementation.</summary>
    public interface IEverendBridgeClient
    {
        Task StartServiceAsync();
        Task StopServiceAsync();
        Task ConnectAsync(string endpoint, string pairingCode);
        Task DisconnectAsync();
        Task RequestSnapshotAsync();
        Task SendInventoryAsync();
        Task SendChangeSetAsync(EverendChangeSet changeSet);
    }

    public sealed class EverendSnapshot
    {
        public string RuntimePackageJson;
        public string SourceDescription;
        public int Revision;
        public bool IsLive;
    }
}
