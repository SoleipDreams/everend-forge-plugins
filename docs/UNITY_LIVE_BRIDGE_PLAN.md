# Everend Unity Live Bridge v0.1

## Document status

This is the implementation plan for the first live connection between Everend PathBranching and Unity. The first compatibility profile targets the current **Sintoma de Poder (SINPO)** Unity project.

The plan is intentionally split across independently versioned repositories. Everend Spec remains the source of truth for shared contracts; PathBranching owns narrative authoring; this repository owns engine adapters; SINPO only contains the project-specific integration needed to consume generated native assets.

## Planning baseline

The following state was observed on 2026-07-16:

- `plugins` contains documentation only. No engine adapter implementation has started.
- `pathbranching` is on `release/v0.5.0-alpha` with a substantial uncommitted working tree covering graph, dialogue, persistence, validation, and export work.
- PathBranching typechecking passes.
- The PathBranching core verification run currently stops in `verify-dialogue-nodes.mjs` with `Expected the trigger to render independently from Dialogue containers.`
- SINPO is clean on `Dialogue-Action-Features` and uses Unity `6000.3.19f1`, Addressables `2.9.1`, Ink, native branching ScriptableObjects, and the current narrative editor tooling.
- SINPO event IDs are legacy, sequence-scoped identifiers. Values such as `0` and `001` are repeated for different characters and cannot serve as global identities.

The PathBranching worktree must be stabilized and committed before any bridge implementation is based on it. This documentation branch does not contain or modify that work.

## Product outcome

The v0.1 bridge must let an author:

1. Open a story in PathBranching.
2. Pair a Unity project with the PathBranching desktop app.
3. Export or live-sync the story into native SINPO assets.
4. Play the synchronized story through the existing SINPO `BranchingManager` and `DialogueManager` flow.
5. See Unity-owned asset assignments and validation findings in PathBranching.
6. Reconnect automatically after a Unity assembly/domain reload.
7. Fall back to an offline bundle import when the live server is unavailable.

The target is editor-time synchronization in under two seconds on the same machine. In Play Mode, synchronized content becomes available after explicitly restarting the current event. State migration or mid-line hot patching is not part of v0.1.

## Architectural decisions

### Projection pipeline

~~~text
WorldNotion canon
       |
       v
PathBranching story and narrative graph
       |
       | runtime package + SINPO projection + change sets
       v
PathBranching local bridge server
       |
       | WebSocket over loopback
       v
Everend Unity package
       |
       | AssetDatabase + SerializedObject + Addressables
       v
SINPO native assets and existing runtime
~~~

The Unity adapter is a projection target. Unity ScriptableObjects must not become the Everend canonical model, and the adapter must not depend on WorldNotion or PathBranching at player runtime.

### Authority by property

The bridge is bidirectional, but it is not a dual-master system. Every synchronized property has one owner.

| Owner | Data |
| --- | --- |
| PathBranching | Stable IDs, sequences, branches, graph structure, event text, dialogue beats, decisions, conditions, consequences, transitions, localization, and canon references. |
| Unity | Asset GUIDs and paths, sprites, backgrounds, scenes, FMOD references, timelines, and other presentation references. |
| Adapter | Addressable entries and labels, generated file locations, content hashes, revision mappings, and projection metadata. |

Changes made on the non-owning side are proposals. They must be shown as conflicts or pending changes and may not silently overwrite the owning side.

### Native SINPO output

The first adapter generates and updates the structures already consumed by SINPO:

- `SequenceData`
- `BranchData`
- `EventsData`
- `DecisionsData`
- per-event Ink source and compiled JSON
- Addressables entries and character labels

The adapter does not replace `BranchingManager`, `DialogueManager`, save data, the dialogue canvas, or the existing external Ink functions in v0.1.

### Package boundary

The reusable Unity integration is an editor-only UPM package named `com.everendforge.unity`.

SINPO types currently compile into predefined Unity assemblies. An assembly definition inside a package cannot directly reference types from `Assembly-CSharp`. The SINPO profile therefore resolves configured types at editor time and writes them through `SerializedObject` and `SerializedProperty`. It must never edit Unity YAML files directly.

A minimal SINPO-side playtest hook may be added in the SINPO integration branch to expose a supported "restart current event" operation. It must not contain import, mapping, or transport logic.

## Shared contracts

### Runtime package

The v0.1 bridge continues to consume Everend runtime package `specVersion: "0.1"`. The live transport has its own version so transport changes do not force a runtime-package migration.

The runtime snapshot must contain or project:

- package, project, and story IDs
- entry node and entry sequence
- canon references
- variables and localization
- sequences, branches, events, decisions, and transitions
- script documents or script references
- external function mappings
- Unity target and SINPO projection settings

### Sync envelope

Every WebSocket message uses a common envelope:

~~~json
{
  "syncProtocolVersion": "0.1",
  "messageId": "uuid",
  "type": "changeSet",
  "sessionId": "uuid",
  "projectId": "project:sinpo",
  "storyId": "story:ridina",
  "baseRevision": 41,
  "revision": 42,
  "origin": "pathbranching",
  "payload": {}
}
~~~

Required message types:

| Type | Direction | Purpose |
| --- | --- | --- |
| `hello` | Unity -> PathBranching | Adapter, engine, project, profile, and capability handshake. |
| `welcome` | PathBranching -> Unity | Accepted versions, session, current revision, and permissions. |
| `snapshot` | PathBranching -> Unity | Complete runtime package and projection state. |
| `changeSet` | Either | Stable-ID-addressed changes since a known revision. |
| `inventory` | Unity -> PathBranching | Existing native assets and mappings discovered in Unity. |
| `validationReport` | Either | Contract, projection, asset, Ink, or runtime findings. |
| `applyResult` | Either | Applied, rejected, conflicted, or partially staged result. |
| `playtestEvent` | Unity -> PathBranching | Current event, choice, transition, and runtime diagnostics. |
| `heartbeat` | Either | Connection liveness and last acknowledged revision. |

Change operations address data by `entityKind` plus stable Everend `entityId`. Array indexes must not be used as identities. Supported operations are `upsert`, `set`, `remove`, and `propose`; every operation declares its property owner.

### Revision and conflict rules

- PathBranching maintains a monotonic story revision for the live session.
- A change set must name the revision it was based on.
- If `baseRevision` is stale, the receiver rejects the mutation and requests a fresh snapshot or rebase.
- `messageId`, `origin`, revision, and per-entity hashes prevent echo loops.
- Adapter-owned updates are acknowledged only after Unity finishes its asset transaction and validation pass.
- Deletions are staged as orphans and never physically delete assets automatically in v0.1.

### Adapter manifest

Unity stores a version-controlled manifest at `ProjectSettings/EverendForgeSync.json`. Each mapping records:

~~~json
{
  "stableId": "event:ridina:fines",
  "entityKind": "event",
  "unityGuid": "1e271105e8627b5468390a2498bad787",
  "assetPath": "Assets/GameData/Branching/Events/Ridina/FINES_RID_001.asset",
  "unityType": "Assembly-CSharp::EventsData",
  "legacyId": "001",
  "sequenceId": "sequence:ridina",
  "generationMode": "generated",
  "lastAppliedRevision": 42,
  "contentHash": "sha256:..."
}
~~~

Legacy IDs are scoped by sequence. The Everend stable ID is the only cross-engine identity.

## Connection server

PathBranching's Tauri backend hosts the bridge:

- bind only to `127.0.0.1`
- default port `47831`, configurable when occupied
- `GET /health` for diagnostics
- `GET /v1/session` for paired-client metadata
- `GET /v1/package` for the current complete snapshot
- `WS /v1/sync` for live messages

The first pairing uses a short code shown in PathBranching. Successful pairing issues a random persistent token stored in PathBranching application data and Unity project settings. Tokens are scoped to the paired project, can be revoked, and are never included in exported runtime packages.

Unity uses `System.Net.WebSockets.ClientWebSocket`, serializes send operations, runs receive work outside the UI thread, and marshals asset mutations onto the Unity editor main thread. It reconnects with exponential backoff after domain reloads and requests either changes since its last revision or a full snapshot.

## PathBranching changes

### Connect workspace

Replace the current "Coming soon" Unity card with:

- start/stop server controls
- bind address and port
- pairing code and paired-project list
- connection, adapter, engine, profile, and revision status
- last sent and acknowledged revision
- pause/resume auto-sync
- send snapshot and request inventory actions
- validation and conflict summaries
- token revocation and troubleshooting information

The UI must call a transport service; protocol and mutation logic must not live in React components.

### SINPO export projection

Keep the generic Ink exporter unchanged and add a dedicated SINPO bundle exporter.

SINPO currently assigns one compiled Ink `TextAsset` to each `EventsData`. The exporter must therefore create one `.ink` file per top-level event, not one file per sequence. Each generated file must:

- include the configured `Globals.ink` relative path
- have a deterministic start knot
- preserve stable IDs in generated comments/metadata
- project dialogue beats, tags, choices, conditions, and consequences
- call configured external functions by semantic mapping
- call `SetNextEvent(legacyId)` for cross-event transitions
- end cleanly when the event is terminal

The offline bundle contains:

~~~text
runtime-package.json
sinpo-projection.json
adapter-manifest.json
validation-report.json
Ink/<sequence>/<event>.ink
~~~

Export is blocked by errors such as missing entry event, unresolved speaker mapping, duplicate legacy ID within a sequence, unsupported external function, missing transition target, or missing required Ink global definitions.

## Unity package

### Editor surface

Add `Window > Everend Forge > Connect` with:

- connection and pairing controls
- current project/story/revision
- auto-sync toggle
- dry-run import preview
- apply/reject controls
- conflict and validation lists
- "Adopt existing SINPO content"
- "Restart current event" during Play Mode
- offline bundle import

### Import transaction

For every snapshot or accepted change set:

1. Validate protocol and runtime-package versions.
2. Validate the SINPO profile and required Unity types.
3. Build an import plan without changing assets.
4. Show or log creates, updates, preserved fields, conflicts, and orphans.
5. Apply changes through `AssetDatabase`, `Undo`, and `SerializedObject` in one bounded transaction.
6. Preserve existing paths and `.meta` files for mapped assets.
7. Import Ink and wait for the Ink Unity integration to produce its JSON `TextAsset`.
8. Wire ScriptableObject references only after all assets exist.
9. Update Addressables entries and configured labels.
10. Save, refresh once, validate, update the manifest, and acknowledge the revision.

If any required stage fails, the transaction must report the failure and must not advance the manifest revision.

### SINPO projection profile

`sinpo-v0.1` maps the Everend model onto these current fields:

- sequence: `characterRef`, `startingEvent`, `events`, `branches`
- branch: `branchID`, `title`, `description`, `events`
- event: `eventID`, `EventName`, `BranchRef`, `EventType`, `InkJSON`, `Description`, `decisions`, `worldDataGiven`, `nextEvents`
- decision: `DecisionID`, `DecisionName`, `Description`, `_decisionType`, `estados`

Unity-owned fields on `EventsData`, including exploration scene, FMOD settings, cover image, and event backgrounds, are preserved unless a future contract explicitly assigns them to PathBranching.

The profile also configures generated roots, Addressables group names, label rules, character enum mappings, Ink locations, global include path, and external function names.

### Asset lifecycle

- Existing mapped assets are updated in place.
- New assets receive a GUID once and are immediately added to the manifest.
- Renames use `AssetDatabase.MoveAsset` so GUIDs survive.
- Unmatched generated assets are moved to `Assets/Everend/Orphans/<timestamp>/` after confirmation.
- Linked assets are never regenerated or orphaned automatically.
- Hand-authored Unity fields are never cleared because a PathBranching property is absent.

## Existing-content adoption

Adoption is optional and always starts with a dry run.

The wizard scans current `SequenceData`, `BranchData`, `EventsData`, `DecisionsData`, Ink, compiled JSON, and Addressables metadata. Matching order is:

1. existing Everend manifest entry
2. Unity GUID
3. explicit user mapping
4. suggested sequence + legacy ID + asset path match

Suggested matches require confirmation. Ambiguous matches are conflicts.

Adoption sends an `inventory` followed by a proposed PathBranching change set. It imports structure, names, legacy IDs, references, and script paths. Existing Ink is registered as `generationMode: "linked"`; v0.1 does not promise lossless conversion of arbitrary Ink into editable PathBranching dialogue nodes.

After adoption, authors can explicitly convert an individual linked event to generated ownership. Conversion requires a preview and preserves the previous Ink file as a backup/orphan until confirmed.

## Live synchronization behavior

- PathBranching mutations are debounced before emitting a change set.
- Unity asset modifications are observed, classified by ownership, and debounced before reporting.
- Unity-owned changes are accepted into engine target metadata.
- Changes to PathBranching-owned fields made through existing Unity narrative tools are sent as proposals.
- Disconnected changes remain queued with their base revision.
- Reconnection rebases safe owner-specific changes and surfaces all others as conflicts.
- No side may apply its own echoed message.

During Play Mode, imported assets may refresh, but the active Ink `Story` instance is not patched. The SINPO hook exposes a deliberate restart of the current event, which rebuilds the story from the new compiled JSON. The user is warned that event-local transient state resets.

## Implementation sequence

### Phase 0: stabilize PathBranching

- Create `stabilize/v0.5-dialogue-base` from the current PathBranching work.
- Commit the existing WIP separately from bridge work.
- Fix the dialogue trigger verification failure.
- Require typecheck and every core/persistence verification to pass.
- Create `feat/unity-live-bridge` from that stabilized commit.

### Phase 1: contract-first work in Everend Spec

- Branch: `feat/engine-sync-protocol-v0.1` from `main`.
- Add sync envelope, change-set, validation report, and adapter-manifest schemas.
- Add valid and invalid examples.
- Document version negotiation, ownership, revision, and error behavior.
- Merge and tag the contract before dependent implementation PRs.

### Phase 2: Unity adapter foundation

- Branch in this repository: `feat/unity-adapter-v0.1` from updated `main`.
- Scaffold `com.everendforge.unity` as an editor-only UPM package.
- Implement protocol models, offline import, manifest, dry-run planning, and idempotent asset projection.
- Add the `sinpo-v0.1` profile and EditMode fixtures.
- Add live WebSocket transport only after offline import is deterministic.

### Phase 3: PathBranching bridge

- Add the contract types and SINPO exporter.
- Implement the Tauri loopback server and pairing store.
- Replace the Connect placeholder with operational UI.
- Emit snapshots and granular changes from the same normalized project state used for export.

### Phase 4: SINPO integration

- Branch: `integrations/everend-unity-v0.1` from `Dialogue-Action-Features`.
- Install the Unity package by Git URL or local development path.
- Add only the playtest restart hook and required configuration.
- Adopt a small existing route, then generate a new route from PathBranching.
- Validate playthrough through the existing SINPO runtime.

### Phase 5: bidirectional validation and release

- Exercise Unity-owned field updates, PathBranching proposals, conflicts, reconnects, and offline bundles.
- Publish the compatibility matrix and migration notes.
- Tag `protocol-v0.1.0` and `unity-v0.1.0` after acceptance.

## Testing and acceptance

### Contract tests

- Accept all valid message and manifest fixtures.
- Reject unsupported protocol versions, missing identities, stale base revisions, invalid ownership, and malformed changes.
- Verify runtime-package compatibility independently of sync protocol compatibility.

### PathBranching tests

- Existing checks remain green.
- Golden SINPO exports cover a normal event, final event, decision, conditional choice, external function, cross-event transition, localization, and nested dialogue.
- Duplicate legacy IDs are allowed across sequences but rejected within one sequence.
- Server tests cover loopback binding, pairing, token rejection, reconnect, revision mismatch, and snapshot fallback.

### Unity EditMode tests

- First import creates the expected native assets.
- Re-import is idempotent.
- Updating content preserves GUIDs, paths, manual presentation fields, and linked Ink.
- Reference wiring works regardless of creation order.
- Addressables groups, addresses, and character labels are deterministic.
- A failed import does not advance the manifest revision.
- Removed generated entities become staged orphans, not deletions.
- A domain reload reconnects and resumes from the last acknowledged revision.

### SINPO end-to-end acceptance

Using Unity `6000.3.19f1` and Addressables `2.9.1`:

1. Pair Unity and PathBranching.
2. Sync a route with at least two events and one decision.
3. Confirm native assets, GUID mappings, Addressables, and Ink JSON references.
4. Load the route through `BranchingManager`.
5. Play dialogue and select a choice that transitions through `SetNextEvent`.
6. Change dialogue text in PathBranching and observe the editor update in under two seconds.
7. Restart the current event and observe the new text.
8. Change a Unity-owned presentation reference and observe it in PathBranching.
9. Create an ownership conflict and confirm neither side overwrites it silently.
10. Disconnect the server and import the same story through the offline bundle.

## Failure handling

- **Port occupied:** show the owner/port error and allow a configured alternate port.
- **Invalid token:** reject before returning project data and offer re-pairing.
- **Unsupported version:** report both supported ranges; do not partially import.
- **Ink compile failure:** keep the prior `InkJSON` reference and report compiler output.
- **Missing SINPO type/field:** mark the projection profile incompatible and stop before mutation.
- **Stale revision:** reject, request snapshot, and preserve the local proposal.
- **Unity domain reload:** reconnect and reconcile from last acknowledged revision.
- **Partial asset failure:** do not advance revision; show affected entities and recovery instructions.
- **Delete request:** stage an orphan and require explicit confirmation.

## Repository, branch, and release strategy

Keep engine adapters in this monorepo as independently versioned packages. Do not use permanent `unity`, `godot`, or `unreal` branches; they would cause the shared protocol and fixtures to diverge.

Initial branches:

| Repository | Branch |
| --- | --- |
| `spec` | `feat/engine-sync-protocol-v0.1` |
| `plugins` | `feat/unity-adapter-v0.1` |
| `pathbranching` | `feat/unity-live-bridge` |
| SINPO | `integrations/everend-unity-v0.1` from `Dialogue-Action-Features` |

Merge order is contract, adapter, PathBranching bridge, then SINPO integration. Contract and app changes should remain separate pull requests.

Version each dimension independently:

- runtime package spec version
- sync protocol version
- Unity adapter SemVer
- SINPO projection profile version
- verified Unity editor versions

Use tags such as `protocol-v0.1.0` and `unity-v0.1.0`. Breaking wire or manifest changes increment the relevant major version; additive capabilities increment minor; compatible fixes increment patch. Release branches are created only when an already tagged line needs maintenance.

A repository split may be reconsidered only when engines have genuinely independent maintainers or release cadence. It is not part of v0.1.

## Explicit non-goals for v0.1

- Godot or Unreal implementation
- a generic Everend runtime replacing SINPO managers
- remote/network collaboration outside the local machine
- canon editing in Unity
- full bidirectional visual Ink editing
- automatic deletion of Unity assets
- automatic Git branch creation from the Unity plugin
- hot patching the active Ink story or migrating live Play Mode state
- changing the current SINPO save format

## Handoff checklist

Before implementation begins on another machine:

- fetch the latest branches in each independent repository
- stabilize and commit the PathBranching WIP before branching the bridge
- merge the spec contract before coding dependent transports
- keep SINPO work based on `Dialogue-Action-Features`
- preserve the current native SINPO runtime as the v0.1 playback target
- use dry runs and manifests before touching existing Unity assets
- record every verified version in the compatibility matrix
