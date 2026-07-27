# Everend Forge Narrative Gateway for Unity

`com.everendforge.unity` imports a portable PathBranching `runtime-package.json`, stores its exact source snapshot, and exposes a normalized `EverendNarrativeCatalog` through `IEverendNarrativeGateway`.

The Runtime assembly has no dependency on SINPO, Ink, Addressables or `Assembly-CSharp`. Interpreter-specific work belongs to editor projection profiles.

## Install

Add this folder through Unity Package Manager using **Add package from disk** and select `package.json`, or add it as a local package in the consuming project's `manifest.json`.

Create a **SINPO v0.1 Profile** with `Everend Forge > Create SINPO v0.1 Profile`. Configure its output directory, its `Globals.ink` asset path, and one character mapping for each exported PathBranching sequence. The mapping's Unity enum value must match the existing `playableCharacters` enum name.

Then open `Everend Forge > Import RuntimePackage...`, select the exported JSON, and run **Analyze import**. The window offers:

- **Import catalog**: creates or updates only the neutral RuntimePackage and NarrativeCatalog assets.
- **Dry-run SINPO projection**: reports files and assets that would be created or updated.
- **Apply SINPO projection**: writes generated Ink and creates/updates the isolated projection folder.

## Text-only Ridina smoke test

`Samples~/RidinaTextOnly/ridina-text-only.runtime-package.json` is a self-contained, non-canonical smoke-test package for the Ridina route. It intentionally has no Ink, Addressables, portraits, backgrounds or audio. Copy it into a Unity project's `Assets/` folder, open **Everend Forge > Import RuntimePackage...**, and use **Import catalog**.

In SINPO, assign that JSON (or the imported `EverendNarrativeCatalog`) to `EverendTextStoryPlayer`, set **Start Branch Id** to `branch:ridina:mvp`, and enter Play Mode. The player renders only plain text and choices. Its branch boundary prevents a transition from silently continuing into another route. A real Ridina export from PathBranching can replace this sample without changing the setup, as long as it retains the portable `runtime-package.json` contract.

## SINPO v0.1 contract

The adapter resolves `SequenceData`, `BranchData`, and `EventsData` by type name at editor time and writes only these narrative fields:

- `SequenceData`: `characterRef`, `startingEvent`, `events`, `branches`
- `BranchData`: `branchID`, `title`, `description`, `events`
- `EventsData`: `eventID`, `EventName`, `BranchRef`, `EventType`, `InkJSON`, `Description`, `nextEvents`

Everything else on the generated ScriptableObjects remains Unity-owned and is preserved across reimport. The projection manifest preserves the Everend ID → native asset/ID mapping, so reimports keep the same GUIDs and `eventID` values.

Generated Ink supports narration/dialogue beats, one choice group per event, simple variable comparisons, `setVariable` / `incrementVariable`, same-sequence event transitions through `SetNextEvent`, and final events. Unsupported graph shapes, missing fallbacks, unmapped characters, script blocks, cross-sequence jumps and unsupported conditions/consequences are rejected before assets are changed.

The generated `Ink` folder is PathBranching-owned. Do not edit it manually.
