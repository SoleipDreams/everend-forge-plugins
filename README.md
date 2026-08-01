<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/everend-forge-logo-on-dark.png">
    <img src="assets/everend-forge-logo-on-light.png" width="110" alt="Everend Forge mark">
  </picture>
</p>

<h1 align="center">Everend Plugins</h1>
<p align="center">
  Runtime engine adapters for <a href="https://github.com/SoleipDreams/everend-forge">Everend Forge</a>.<br />
  Consumes exported runtime packages without owning canon or authoring.
</p>

## Unity Narrative Gateway MVP

[`packages/com.everendforge.unity`](packages/com.everendforge.unity) is a local UPM package that imports the portable `RuntimePackage` contract into a neutral catalog and offers a configurable SINPO v0.1 projection. See its [installation and contract notes](packages/com.everendforge.unity/README.md).

<p align="center">
  <img src="https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue.svg" alt="License">
  <a href="https://github.com/SoleipDreams/everend-forge"><img src="https://img.shields.io/badge/Everend%20Forge-open%20core%20suite-0a0e1a.svg" alt="Part of Everend Forge"></a>
</p>

---

Everend Plugins coordinates runtime adapters for game engines and other execution environments.

Plugins consume Everend runtime packages exported by PathBranching. They should not own canon, replace WorldNotion, or become the primary branching authoring tool.

## Current Status

This repository currently contains roadmap and API documentation only. Engine plugin implementations have not started yet.

The decision-complete implementation plan for the first PathBranching-to-Unity connection is documented in [Unity Live Bridge v0.1](docs/UNITY_LIVE_BRIDGE_PLAN.md). The initial compatibility profile targets the existing SINPO Unity and Ink workflow while preserving a portable engine-adapter boundary.

## Targets

1. Unity
2. Godot
3. Unreal

## Minimal Runtime API

- Load package
- Start node
- Get current line
- Get choices
- Select choice
- Get/set variables
- Emit events
- Save/load state

## Related Repositories

- [Everend Forge portal](https://github.com/SoleipDreams/everend-forge)
- [Everend Spec](https://github.com/SoleipDreams/spec)
- [Everend PathBranching](https://github.com/SoleipDreams/pathbranching)

## License

Code is licensed under MIT OR Apache-2.0. Documentation is licensed under CC BY 4.0 unless stated otherwise.

## Support

If Everend Forge is useful to you, you can support its development on [Ko-fi](https://ko-fi.com/heinzdbv).
