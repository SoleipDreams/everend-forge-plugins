# Everend Forge Unity Bridge — implemented baseline

This document records the current implementation boundary for the Unity bridge described in [UNITY_LIVE_BRIDGE_PLAN.md](UNITY_LIVE_BRIDGE_PLAN.md).

## Current flow

```text
PathBranching --WebSocket--> everend-forge-bridge --WebSocket--> Unity Editor
       |                              |
       +-- runtime-package.json       +-- /health, /v1/session
       +-- offline bundle              +-- pairing, revision, stale-base rejection
```

The daemon binds to `127.0.0.1` only. The default port is `47831`; if it is occupied, the daemon probes the next loopback ports and reports the selected endpoint. The local state and token files are kept outside the repositories in the user application-data state directory. The daemon is distributed as a shared sidecar runtime: either Unity or PathBranching can launch the same bundled bridge, and both clients first detect an existing instance before attempting to start another one.

For Windows service controls, `everend-forge-bridge` generates a runtime bundle
containing the bridge entry point, launcher, and local Node runtime. PathBranching
stages it into Tauri resources; the Unity package stages it into
`Editor/BridgeRuntime`. The terminal is visible by default and can be hidden
from each client's Connection settings.

## Implemented

- Versioned sync contract and offline bundle manifest in `everend-forge-specs`.
- Standalone TypeScript daemon with `start`, `status`, and `stop` commands.
- Health/session HTTP endpoints and WebSocket pairing for one author and one Unity client.
- Persistent local token issued after pairing; pairing code remains a local bootstrap credential.
- Snapshot forwarding, change-set transport, duplicate message rejection, heartbeat, revision stamping, and stale base rejection.
- PathBranching client with manual connection, exponential reconnect, snapshot publish, auto-sync debounce, inventory reception, and Tauri service control.
- PathBranching offline bundle export with runtime package, projection descriptor, adapter manifest, validation report, and Ink files.
- Unity `ProjectSettings/EverendForgeSync.json` configuration without tokens. Tokens use `EditorPrefs`.
- Unity dashboard with Dashboard/Connection/Sync/Diagnostics tabs. Dashboard starts with only the server status card and one dynamic Start/Stop button; starting the daemon then checks its health and attempts the Unity connection. The persisted `showTerminal` preference defaults to visible.
- Unity Editor WebSocket client, common sync coordinator, manual JSON/bundle analysis, dry-run, apply, report, manifest revision metadata, and UI Toolkit dashboard.
- Unity inventory scanner for ScriptableObjects, Ink, and JSON assets; initial adoption suggestions from manifests and unique GUID/name matches.
- Unity asset change observer that classifies Unity-owned changes as proposals and never silently edits narrative-owned fields.
- Play Mode pending snapshot state and explicit `Restart Current Event` action. The active `Ink.Runtime.Story` is intentionally not hot-patched.

## Known v0.1 limits

- PathBranching receives Unity inventory and proposals, but a full review/apply UI for Unity-owned changes is still the next bidirectional phase.
- The SINPO projection uses the existing adapter implementation. Its dry-run is non-mutating; a complete filesystem/AssetDatabase transaction with physical rollback remains to be hardened before production use.
- Orphan staging and explicit mapping confirmation are represented by the inventory/adoption boundary but are not yet a full asset-moving workflow.
- The first client does not patch a running Ink story. Restarting an event reapplies the pending projection and reports the runtime restart requirement.
