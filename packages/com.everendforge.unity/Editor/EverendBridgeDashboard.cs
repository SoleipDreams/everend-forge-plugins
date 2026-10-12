using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EverendForge.Unity.Editor
{
    public sealed class EverendBridgeDashboard : EditorWindow
    {
        private EverendBridgeSettings settings;
        private EverendBridgeClient client;
        private EverendSyncCoordinator coordinator;
        private EverendImportPlan plan;
        private EverendProjectionProfile profile;
        private EverendBridgeServerStatus serverStatus = EverendBridgeServerStatus.Unknown;
        private Label serviceState;
        private Label connectionState;
        private Label lastMessage;
        private Label serverDetails;
        private Label serverStatusIndicator;
        private Button serviceToggleButton;
        private TextField endpointField;
        private TextField projectIdField;
        private TextField storyIdField;
        private TextField daemonField;
        private TextField packageField;
        private TextField bundleField;
        private TextField pairingField;
        private Toggle autoSyncToggle;
        private VisualElement dashboardTab;
        private VisualElement connectionTab;
        private VisualElement syncTab;
        private VisualElement diagnosticsTab;

        [MenuItem("Window/Everend Forge/Bridge Dashboard")]
        public static void Open() { GetWindow<EverendBridgeDashboard>("Everend Forge Bridge"); }

        private void CreateGUI()
        {
            settings = EverendBridgeSettings.Load();
            if (string.IsNullOrWhiteSpace(settings.daemonExecutable)) settings.daemonExecutable = settings.ResolveDaemonExecutable();
            profile = settings.Profile;
            coordinator = new EverendSyncCoordinator(settings);
            client = new EverendBridgeClient(settings);
            client.StateChanged += OnStateChanged;
            client.MessageReceived += OnMessageReceived;
            EverendAssetChangeObserver.AssetsChanged += OnAssetsChanged;
            BuildUi();
            OnStateChanged(client.State);
            _ = RefreshServerStatusAsync(false);
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            rootVisualElement.Add(new Label("Everend Forge Bridge") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 16 } });
            rootVisualElement.Add(new Label("Control the local daemon and the PathBranching ↔ Unity connection."));

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexWrap = Wrap.Wrap;
            tabs.style.marginTop = 8;
            tabs.style.marginBottom = 8;
            Button dashboardButton = null; Button connectionButton = null; Button syncButton = null; Button diagnosticsButton = null;
            dashboardButton = TabButton("Dashboard", () => SelectTab(dashboardTab, dashboardButton));
            connectionButton = TabButton("Connection", () => SelectTab(connectionTab, connectionButton));
            syncButton = TabButton("Sync", () => SelectTab(syncTab, syncButton));
            diagnosticsButton = TabButton("Diagnostics", () => SelectTab(diagnosticsTab, diagnosticsButton));
            tabs.Add(dashboardButton); tabs.Add(connectionButton); tabs.Add(syncButton); tabs.Add(diagnosticsButton);
            rootVisualElement.Add(tabs);

            dashboardTab = BuildDashboardTab();
            connectionTab = BuildConnectionTab();
            syncTab = BuildSyncTab();
            diagnosticsTab = BuildDiagnosticsTab();
            rootVisualElement.Add(dashboardTab); rootVisualElement.Add(connectionTab); rootVisualElement.Add(syncTab); rootVisualElement.Add(diagnosticsTab);
            SelectTab(dashboardTab, dashboardButton);
        }

        private Button TabButton(string label, Action action)
        {
            var button = new Button(action) { text = label };
            button.style.marginRight = 4; button.style.marginBottom = 3; button.style.minHeight = 28;
            return button;
        }

        private void SelectTab(VisualElement selected, Button activeButton)
        {
            if (dashboardTab != null) dashboardTab.style.display = selected == dashboardTab ? DisplayStyle.Flex : DisplayStyle.None;
            if (connectionTab != null) connectionTab.style.display = selected == connectionTab ? DisplayStyle.Flex : DisplayStyle.None;
            if (syncTab != null) syncTab.style.display = selected == syncTab ? DisplayStyle.Flex : DisplayStyle.None;
            if (diagnosticsTab != null) diagnosticsTab.style.display = selected == diagnosticsTab ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var button in rootVisualElement.Query<Button>().ToList())
            {
                if (button == activeButton) button.style.backgroundColor = new Color(0.18f, 0.34f, 0.58f, 1f);
                else if (button.text == "Dashboard" || button.text == "Connection" || button.text == "Sync" || button.text == "Diagnostics") button.style.backgroundColor = new Color(0.16f, 0.16f, 0.18f, 1f);
            }
        }

        private VisualElement BuildDashboardTab()
        {
            var tab = new VisualElement();
            tab.style.flexGrow = 1;
            tab.style.maxWidth = 520;
            tab.style.paddingTop = 8;
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.alignItems = Align.Center;
            card.style.paddingLeft = 10; card.style.paddingRight = 10; card.style.paddingTop = 10; card.style.paddingBottom = 10;
            card.style.marginBottom = 8;
            card.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            var copy = new VisualElement(); copy.style.flexGrow = 1;
            copy.Add(new Label("Estado del servidor") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } });
            serverDetails = new Label(); serverDetails.style.marginTop = 3; copy.Add(serverDetails);
            card.Add(copy);
            serverStatusIndicator = new Label("● Apagado");
            serverStatusIndicator.style.unityFontStyleAndWeight = FontStyle.Bold;
            serverStatusIndicator.style.paddingLeft = 9; serverStatusIndicator.style.paddingRight = 9; serverStatusIndicator.style.paddingTop = 5; serverStatusIndicator.style.paddingBottom = 5;
            card.Add(serverStatusIndicator);
            tab.Add(card);
            serviceToggleButton = Button("Start service", ToggleService);
            serviceToggleButton.style.minHeight = 38;
            serviceToggleButton.style.flexGrow = 1;
            tab.Add(serviceToggleButton);
            return tab;
        }

        private VisualElement BuildConnectionTab()
        {
            var tab = new VisualElement();
            tab.style.flexGrow = 1;
            serviceState = new Label(); connectionState = new Label(); lastMessage = new Label();
            var source = new Foldout { text = "Connection settings", value = true };
            var mode = new EnumField("Mode", settings.IsLive ? EverendBridgeMode.Live : EverendBridgeMode.Manual);
            mode.RegisterValueChangedCallback(change => { settings.mode = ((EverendBridgeMode)change.newValue).ToString().ToLowerInvariant(); settings.Save(); });
            endpointField = Field("Endpoint", settings.endpoint, value => settings.endpoint = value);
            projectIdField = Field("Project ID", settings.projectId, value => settings.projectId = value);
            storyIdField = Field("Story ID", settings.storyId, value => settings.storyId = value);
            daemonField = Field("Daemon executable", settings.daemonExecutable, value => settings.daemonExecutable = value);
            var daemonActions = new VisualElement();
            daemonActions.style.flexDirection = FlexDirection.Row;
            daemonActions.Add(Button("Choose daemon executable", ChooseDaemonExecutable));
            daemonActions.Add(Button("Auto-detect daemon", AutoDetectDaemon));
            pairingField = Field("Unity pairing code", string.Empty, value => { });
            autoSyncToggle = new Toggle("Auto-sync") { value = settings.autoSync };
            autoSyncToggle.RegisterValueChangedCallback(change => { settings.autoSync = change.newValue; settings.Save(); });
            var showTerminalToggle = new Toggle("Mostrar terminal del daemon") { value = settings.showTerminal };
            showTerminalToggle.RegisterValueChangedCallback(change => { settings.showTerminal = change.newValue; settings.Save(); });
            source.Add(mode); source.Add(endpointField); source.Add(projectIdField); source.Add(storyIdField); source.Add(daemonField); source.Add(daemonActions); source.Add(pairingField); source.Add(autoSyncToggle); source.Add(showTerminalToggle);
            var profileField = new ObjectField("Projection profile") { objectType = typeof(EverendProjectionProfile), allowSceneObjects = false, value = profile };
            profileField.RegisterValueChangedCallback(change => { profile = change.newValue as EverendProjectionProfile; settings.ApplyProfileDefaults(profile); settings.Save(); });
            source.Add(profileField);
            tab.Add(source);
            var state = new Foldout { text = "Connection state", value = true };
            state.Add(serviceState); state.Add(connectionState); state.Add(lastMessage);
            var actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row; actions.style.flexWrap = Wrap.Wrap;
            actions.Add(Button("Connect", Connect)); actions.Add(Button("Disconnect", Disconnect)); actions.Add(Button("Request snapshot", RequestSnapshot)); actions.Add(Button("Send inventory", SendInventory)); actions.Add(Button("Restart Current Event", RestartCurrentEvent));
            state.Add(actions); tab.Add(state);
            return tab;
        }

        private VisualElement BuildSyncTab()
        {
            var tab = new VisualElement(); tab.style.flexGrow = 1;
            var manual = new Foldout { text = "Manual Import / Sync", value = true };
            packageField = Field("runtime-package.json", settings.manualPackagePath, value => settings.manualPackagePath = value);
            bundleField = Field("Offline bundle folder", settings.manualBundlePath, value => settings.manualBundlePath = value);
            manual.Add(packageField); manual.Add(Button("Choose runtime-package.json", ChoosePackage)); manual.Add(bundleField); manual.Add(Button("Choose offline bundle", ChooseBundle));
            manual.Add(Button("Analyze", Analyze)); manual.Add(Button("Dry-run", DryRun)); manual.Add(Button("Apply", Apply)); manual.Add(Button("Export report", ExportReport));
            tab.Add(manual); return tab;
        }

        private VisualElement BuildDiagnosticsTab()
        {
            var tab = new VisualElement(); tab.style.flexGrow = 1;
            var adoption = new Foldout { text = "Adoption & Mapping", value = true };
            adoption.Add(new Label("The baseline scanner reports existing ScriptableObjects and GUIDs. Confirmed mapping and linked/generated adoption remains explicit and never deletes assets."));
            adoption.Add(Button("Scan Unity inventory", SendInventory)); tab.Add(adoption);
            var diagnostics = new Foldout { text = "History & Diagnostics", value = true };
            diagnostics.Add(Button("Open generated folder", OpenGeneratedFolder)); diagnostics.Add(Button("Copy endpoint", CopyEndpoint)); tab.Add(diagnostics);
            return tab;
        }

        private TextField Field(string label, string value, Action<string> onChange)
        {
            var field = new TextField(label) { value = value ?? string.Empty };
            field.RegisterValueChangedCallback(change => { onChange(change.newValue); settings.Save(); });
            return field;
        }

        private Button Button(string label, Action action)
        {
            var button = new Button(() => { try { action(); } catch (Exception exception) { Debug.LogException(exception); SetNotice(exception.Message); } }) { text = label };
            button.style.marginTop = 3; button.style.marginBottom = 3; return button;
        }

        private async void ToggleService()
        {
            try
            {
                if (serverStatus == EverendBridgeServerStatus.Online) await StopServiceAsync();
                else await StartServiceAsync();
            }
            catch (Exception exception) { SetNotice(exception.Message); }
        }

        private async Task StartServiceAsync()
        {
            await client.StartServiceAsync();
            SetServerStatus(EverendBridgeServerStatus.Unknown, "Start requested. Checking the daemon…");
            await Task.Delay(350);
            await RefreshServerStatusAsync(false);
            if (serverStatus == EverendBridgeServerStatus.Online)
            {
                try { await client.ConnectAsync(settings.endpoint, pairingField == null ? string.Empty : pairingField.value); SetNotice("Daemon started and Unity connected."); }
                catch (Exception exception) { SetNotice("Daemon started, but Unity connection failed: " + exception.Message); }
            }
        }

        private async Task StopServiceAsync()
        {
            await client.StopServiceAsync();
            await client.DisconnectAsync();
            SetServerStatus(EverendBridgeServerStatus.Offline, "The daemon was stopped.");
            SetNotice("Daemon stopped.");
        }

        private async void Connect()
        {
            try { settings.Save(); await client.ConnectAsync(settings.endpoint, pairingField == null ? string.Empty : pairingField.value); SetNotice("Connected; the daemon will send the latest PathBranching snapshot."); }
            catch (Exception exception) { SetNotice(exception.Message); }
        }
        private async void Disconnect() { try { await client.DisconnectAsync(); } catch (Exception exception) { SetNotice(exception.Message); } }
        private async void RequestSnapshot() { try { await client.RequestSnapshotAsync(); SetNotice("Snapshot request sent."); } catch (Exception exception) { SetNotice(exception.Message); } }
        private async void SendInventory()
        {
            try { var inventory = EverendInventoryScanner.Scan(profile); await client.SendInventoryAsync(); SetNotice("Inventory scan completed: " + inventory.Items.Count + " assets. Transport inventory is ready for the adoption phase."); } catch (Exception exception) { SetNotice(exception.Message); }
        }

        private async void OnAssetsChanged(string[] paths)
        {
            if (client == null || client.State.Status != EverendBridgeConnectionStatus.Connected || paths == null || paths.Length == 0) return;
            try
            {
                var proposal = EverendAssetChangeObserver.BuildUnityOwnedProposal(paths);
                if (proposal.Operations.Count > 0) await client.SendChangeSetAsync(proposal);
            }
            catch (Exception exception) { SetNotice("Unity change proposal was not sent: " + exception.Message); }
        }

        private void ChoosePackage()
        {
            var path = EditorUtility.OpenFilePanel("Select runtime-package.json", Application.dataPath, "json");
            if (string.IsNullOrWhiteSpace(path)) return;
            packageField.value = path; settings.manualPackagePath = path; settings.Save();
        }
        private void ChooseDaemonExecutable()
        {
            var initialDirectory = Path.GetDirectoryName(settings.ResolveDaemonExecutable());
            if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory)) initialDirectory = Directory.GetParent(Application.dataPath).FullName;
            var path = EditorUtility.OpenFilePanel("Select everend-forge-bridge launcher", initialDirectory, string.Empty);
            if (string.IsNullOrWhiteSpace(path)) return;
            daemonField.value = path; settings.daemonExecutable = path; settings.Save(); SetNotice("Bridge daemon executable configured.");
        }
        private void AutoDetectDaemon()
        {
            var path = EverendBridgeSettings.FindDaemonExecutable();
            if (string.IsNullOrWhiteSpace(path)) { SetNotice("Bridge daemon not found automatically. Choose everend-forge-bridge.cmd manually."); return; }
            daemonField.value = path; settings.daemonExecutable = path; settings.Save(); SetNotice("Bridge daemon found and configured: " + path);
        }
        private void ChooseBundle()
        {
            var path = EditorUtility.OpenFolderPanel("Select offline bundle folder", Application.dataPath, string.Empty);
            if (string.IsNullOrWhiteSpace(path)) return;
            bundleField.value = path; settings.manualBundlePath = path; settings.Save();
        }
        private void Analyze()
        {
            plan = LoadPlan(); SetNotice(plan.Analysis.Summary + (plan.Analysis.Warnings.Count == 0 ? string.Empty : " Warnings: " + plan.Analysis.Warnings.Count));
            foreach (var error in plan.Analysis.Errors) Debug.LogError(error);
            foreach (var warning in plan.Analysis.Warnings) Debug.LogWarning(warning);
        }
        private void DryRun()
        {
            if (plan == null) Analyze();
            if (plan == null) return;
            var result = coordinator.DryRun(plan); SetNotice(result.Message); Debug.Log(result.Message);
        }
        private void Apply()
        {
            if (plan == null) Analyze();
            if (plan == null) return;
            var result = coordinator.Apply(plan); SetNotice(result.Message); if (result.Catalog != null) Selection.activeObject = result.Catalog; Debug.Log(result.Message);
        }
        private EverendImportPlan LoadPlan()
        {
            if (!string.IsNullOrWhiteSpace(settings.manualBundlePath)) return coordinator.AnalyzeBundle(settings.manualBundlePath);
            if (string.IsNullOrWhiteSpace(settings.manualPackagePath)) throw new InvalidOperationException("Select a runtime-package.json or offline bundle folder.");
            return coordinator.Analyze(new EverendSnapshot { RuntimePackageJson = File.ReadAllText(settings.manualPackagePath), SourceDescription = settings.manualPackagePath, IsLive = false });
        }
        private void ExportReport()
        {
            var path = Path.Combine(Application.dataPath, "EverendForge", "Generated", "everend-bridge-report.json");
            var directory = Path.GetDirectoryName(path); if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(plan == null ? new EverendBridgeReport() : plan.Report, true)); AssetDatabase.Refresh(); SetNotice("Report exported to " + path);
        }

        private async void RefreshServerStatus() { await RefreshServerStatusAsync(true); }
        private async Task RefreshServerStatusAsync(bool notify)
        {
            if (client == null) return;
            var result = await client.CheckServerAsync();
            SetServerStatus(result.Status, result.Message);
            if (notify) SetNotice("Server status refreshed.");
        }
        private void SetServerStatus(EverendBridgeServerStatus status, string message)
        {
            serverStatus = status;
            var online = status == EverendBridgeServerStatus.Online;
            var unknown = status == EverendBridgeServerStatus.Unknown;
            if (serverStatusIndicator != null)
            {
                serverStatusIndicator.text = online ? "● Activo" : unknown ? "● Comprobando…" : "● Apagado";
                serverStatusIndicator.style.backgroundColor = online ? new Color(0.14f, 0.38f, 0.2f, 1f) : unknown ? new Color(0.42f, 0.3f, 0.08f, 1f) : new Color(0.48f, 0.12f, 0.12f, 1f);
                serverStatusIndicator.style.color = Color.white;
            }
            if (serviceToggleButton != null)
            {
                serviceToggleButton.text = online ? "Stop service" : "Start service";
                serviceToggleButton.style.backgroundColor = online ? new Color(0.48f, 0.12f, 0.12f, 1f) : new Color(0.14f, 0.38f, 0.2f, 1f);
                serviceToggleButton.style.color = Color.white;
            }
            if (serverDetails != null) serverDetails.text = message ?? string.Empty;
        }
        private void OpenGeneratedFolder() { EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, settings.outputRoot))); }
        private void CopyEndpoint() { GUIUtility.systemCopyBuffer = settings.endpoint; SetNotice("Endpoint copied."); }
        private async void RestartCurrentEvent() { if (!EditorApplication.isPlaying) { SetNotice("Enter Play Mode to use Restart Current Event."); return; } try { await client.RestartCurrentEventAsync(); SetNotice("Pending snapshot applied. The active Ink story must be restarted by the SINPO runtime hook; transient event state is considered lost."); } catch (Exception exception) { SetNotice(exception.Message); } }
        private void OnStateChanged(EverendBridgeSessionState state)
        {
            if (connectionState == null) return;
            connectionState.text = "Connection: " + state.Status + " — " + state.Message;
            serviceState.text = "Daemon endpoint: " + (string.IsNullOrWhiteSpace(state.Endpoint) ? "not connected" : state.Endpoint);
            lastMessage.text = "Revision: " + state.RemoteRevision + " | Last message: " + (state.LastMessageType ?? "none");
            if (state.Status == EverendBridgeConnectionStatus.Connected) SetServerStatus(EverendBridgeServerStatus.Online, "The local daemon is connected to Unity.");
            else if (state.Status == EverendBridgeConnectionStatus.Disconnected || state.Status == EverendBridgeConnectionStatus.Error) SetServerStatus(EverendBridgeServerStatus.Offline, "The server or Unity connection is offline.");
        }
        private void OnMessageReceived(EverendBridgeWireMessage message) { SetNotice("Received " + message.MessageType + " revision " + message.Revision + "."); }
        private void SetNotice(string value) { if (lastMessage != null) lastMessage.text = value; if (serverDetails != null && serverStatus != EverendBridgeServerStatus.Online) serverDetails.text = value; }
        private void OnDisable() { EverendAssetChangeObserver.AssetsChanged -= OnAssetsChanged; if (client != null) { client.StateChanged -= OnStateChanged; client.MessageReceived -= OnMessageReceived; client.Dispose(); client = null; } if (settings != null) settings.Save(); }
    }
}
