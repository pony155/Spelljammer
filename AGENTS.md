# AGENTS.md

This file defines repository-wide guidance for contributors and coding agents
working on Spelljammer. If a more specific `AGENTS.md` is added under a
subdirectory, the closest file takes precedence for that subtree.

## Mission and current state

Spelljammer is the requested working name for an original outer-space sandbox
roguelike inspired by the broad fantasy of age-of-sail adventure among the
stars and by systemic games. It must develop its own setting, names, cosmology,
visual identity, rules, content, and assets. Do not assume the working name
licenses proprietary game data, lore, characters, creatures, text, artwork,
audio, code, trade dress, or branding.

The repository is at an early prototype stage. It currently contains a Windows
x64 .NET 10 WPF host, a narrow native rendering bridge to SpriteForge,
Game-owned localization runtime/tooling, and a small deterministic
space-expedition loop. It does not yet contain the planned crew, encounter,
ship-interior, campaign, or save systems. Describe features according to their
implemented state and label future-facing design as planned.

`Docs/Archive/LargeScaleRtsConcept.md` predates the current sandbox direction.
Treat it as historical exploration; it is not the authority for new gameplay
work. Current product direction lives under `Docs/DesignConcept`.

## Repository and engine boundary

- Spelljammer owns all game-specific voyage and crew simulation, rules,
  content, UI flows, saves, scenarios, localization keys, and presentation
  decisions.
- SpriteForge is a separate reusable engine repository. In the standard local
  layout it is the sibling directory `../SpriteForge`.
- Do not copy SpriteForge source into this repository or depend on private
  engine implementation headers. Consume deliberate public APIs and the
  versioned native C ABI.
- Do not modify the sibling SpriteForge repository unless the user explicitly
  requests an engine change. When both repositories must change, keep the
  commits independently buildable and document the required engine revision or
  interop version.
- Reusable rendering, platform, input, audio, text shaping, world/ECS, asset,
  and framework capabilities belong in SpriteForge. A mechanic belongs in
  Spelljammer until a concrete reusable engine requirement is demonstrated.
- Never commit a developer-specific absolute SpriteForge path. Accept it
  through an environment variable, MSBuild property, or local untracked
  configuration.

## Product pillars

- Systemic voyage simulation in which ship, crew, environment, resources, and
  factions combine into unexpected situations and stories.
- Crew members with understandable needs, capabilities, relationships, tasks,
  and consequences rather than opaque scripted outcomes.
- A persistent ship whose modules, cargo, damage, and history interact with a
  dangerous seeded star chart and a recurring home anchorage.
- Player freedom to choose routes, configure the ship, assign priorities,
  bargain, fight, retreat, and recover without one mandatory solution.
- Data-driven definitions and stable identities that support balancing,
  content growth, save migration, and eventual modding without hard-coding
  presentation text into simulation state.

These pillars define direction, not current feature claims. Implement the
smallest playable vertical slice before broadening the simulation surface.

## Architecture invariants

- Advance authoritative gameplay on an explicit fixed simulation tick.
  Rendering cadence, WPF layout, animation interpolation, and wall-clock timing
  must not change simulation results.
- Keep simulation state separate from presentation state. Renderer handles,
  controls, localized strings, and native pointers are never save-game or
  gameplay identity.
- Use stable IDs for persistent entities and content. Version serialized data,
  validate it before publication, and define migrations before changing a
  released save contract.
- Make randomness reproducible through explicitly owned seeded streams. Do not
  let thread completion order or UI event timing determine outcomes.
- Prefer commands, events, snapshots, and documented commit boundaries over
  direct cross-system mutation or hidden global state.
- Bound collections, queues, path searches, per-tick work, and external input.
  Capacity failures need explicit, observable behavior.
- Publish replacement content or state transactionally: validate the new value
  before retiring the previous working value.
- Keep player-visible text in localization catalogs. Simulation, persistence,
  sorting, and command dispatch use stable keys or IDs, never translated text.

## Managed/native boundary

- `Source/Spelljammer.App/Interop/SpriteForgeNative.cs` owns the low-level
  SpriteForge imports. Keep the ABI narrow, versioned, blittable, and explicit
  about ownership, thread affinity, units, ranges, and error status.
- Do not allow managed exceptions to escape callbacks into native code, or C++
  exceptions to cross the C ABI.
- Copy values across the boundary or use explicitly scoped borrowed views. Do
  not retain managed references in native serialized state or expose native
  ECS/resource pointers to managed gameplay.
- WPF controls and window operations stay on the UI thread. Native renderer and
  service calls must follow SpriteForge's documented owner-thread contract.
- Keep high-volume simulation work on the appropriate native or bounded batch
  path. Avoid per-entity managed/native calls in hot loops.

## Source conventions

- The managed projects target .NET 10, C# 14, x64 for the WPF host, nullable
  reference types, implicit usings, and warnings as errors. Do not silently
  lower or raise those baselines.
- Follow `.editorconfig` when present. Use four spaces, clear ownership, small
  focused types, and actionable errors at I/O and interop boundaries.
- Avoid synchronous blocking on the UI thread. Make cancellation and lifetime
  ownership explicit for background work.
- Treat UTF-8 as the authored catalog encoding. Preserve the localization
  runtime's stable keys, typed arguments, bounded formatting, fallback rules,
  and copy-only language-profile boundary.
- Do not add an online fetch to ordinary configure, build, catalog compilation,
  or runtime startup.

## Repository map

| Path | Responsibility |
| --- | --- |
| `Source/Spelljammer.App/` | Current WPF shell, native interop, and expedition presentation host. |
| `Source/Spelljammer.Simulation/` | Headless authoritative voyage state, seeded sector generation, and typed commands. |
| `Source/Spelljammer.Localization/` | Game-owned catalog runtime, formatting, identity, and limits. |
| `Tools/Spelljammer.Localization.Compiler/` | Offline source-catalog parser and compiler. |
| `Content/Packs/base/` | Built-in gameplay definitions and their authored localization catalogs. |
| `Content/Localization/` | Pinned locale-data inputs and notices. |
| `Tests/Spelljammer.Localization.Tests/` | Headless localization contracts and corruption/formatting coverage. |
| `Tests/Spelljammer.Simulation.Tests/` | Compile-only determinism, rejection, bounds, and resource-loop contracts. |
| `Docs/DesignConcept/` | Current product direction and playable-slice scope. |
| `Docs/Architecture/` | Implemented and planned subsystem architecture. |
| `Docs/Archive/` | Historical design material that is not current product authority. |
| `Build/` | Focused CMake declarations included by the standalone root project. |

Add new gameplay code under a clearly named game-owned directory rather than
placing it in `Localization`, the WPF shell, or the native interop layer.

## Change workflow

1. Inspect `git status`, the nearest documentation, project files, and related
   tests before editing. Preserve unrelated user changes.
2. Trace the complete ownership path before changing a public contract,
   serialization format, localization artifact, or native interop structure.
3. Make the smallest coherent change and add focused coverage for success,
   failure, bounds, and rollback behavior.
4. Update `Spelljammer.slnx`, the relevant `.csproj`, or CMake declaration when
   adding or moving compiled sources.
5. Update README or design status when setup, supported behavior, limitations,
   or public workflows change.
6. Review the final diff for generated files, machine paths, secrets, stale
   claims, and accidental changes outside Spelljammer.

## Build and verification

The expected local engine checkout is supplied through `SPRITEFORGE_ROOT`:

```powershell
$env:SPRITEFORGE_ROOT = (Resolve-Path ..\SpriteForge)
```

Build and install SpriteForge before launching the WPF host, then pass its
native output directory through `SpriteForgeNativeDir`. Do not hard-code that
path in the project.

For Spelljammer changes:

- Run `dotnet build .\Spelljammer.slnx` with the appropriate
  `SpriteForgeNativeDir` property when native runtime copying is required.
- Compile affected localization CMake or .NET targets when their sources or
  build declarations change.
- Unit-test execution is user/CI-owned. Coding agents may compile test targets
  but must not run the localization test executable, `ctest`, or equivalent
  local unit-test commands.
- Run formatting/static checks applicable to touched files and
  `git diff --check` before handoff.
- Report engine build, game build, and CI-owned test status separately.
