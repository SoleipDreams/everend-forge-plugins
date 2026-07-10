<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/everend-forge-logo-on-dark.png">
    <img src="assets/everend-forge-logo-on-light.png" width="110" alt="Everend Forge mark">
  </picture>
</p>

<h1 align="center">Everend Plugins</h1>
<p align="center">
  Runtime engine adapters for <a href="https://github.com/Everendforge/everend-forge">Everend Forge</a>.<br />
  Consumes exported runtime packages without owning canon or authoring.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-MIT%20OR%20Apache--2.0-blue.svg" alt="License">
  <a href="https://github.com/Everendforge/everend-forge"><img src="https://img.shields.io/badge/Everend%20Forge-open%20core%20suite-0a0e1a.svg" alt="Part of Everend Forge"></a>
</p>

---

Everend Plugins coordinates runtime adapters for game engines and other execution environments.

Plugins consume Everend runtime packages exported by PathBranching. They should not own canon, replace WorldNotion, or become the primary branching authoring tool.

## Current Status

This repository currently contains roadmap and API documentation only. Engine plugin implementations have not started yet.

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

- [Everend Forge portal](https://github.com/Everendforge/everend-forge)
- [Everend Spec](https://github.com/Everendforge/spec)
- [Everend PathBranching](https://github.com/Everendforge/pathbranching)

## License

Code is licensed under MIT OR Apache-2.0. Documentation is licensed under CC BY 4.0 unless stated otherwise.
