# Spelljammer

Spelljammer is an in-development 2D outer-space sandbox roguelike built with
the [SpriteForge](https://github.com/pony155/SpriteForge) engine. The player
commands a small voidfaring ship, explores a seeded star chart, takes risks for
salvage, and tries to bring enough of the expedition home to finance the next
voyage.

Spelljammer is the requested working project name. The current implementation
draws inspiration from the broad fantasy of age-of-sail adventure among the
stars and from systemic roguelikes, while its universe, terminology,
characters, rules, content, code, artwork, and sound remain original.

> [!IMPORTANT]
> Spelljammer is at an early vertical-slice stage, not a content-complete game.
> The current shell exposes the main menu, game settings, audio options, and
> character creation. Headless systems implement the eleven-character roster,
> protagonist/NPC recruitment, typed personal combat, modular ship encounters,
> content-locked saves, and data-driven gameplay definitions. These systems are
> not yet composed into a complete playable campaign. The retired 4-by-4
> expedition and native renderer demonstration have been removed so new
> gameplay follows one authoritative `World` path.

## Current implementation

Implemented foundations include:

- a headless `Spelljammer.Simulation` project with immutable voyage,
  character, item, encounter, Status, and Effect state, typed commands, stable
  identities, explicit rejection reasons, and bounded collections;
- a bounded gameplay-content foundation with validated stable IDs, strict JSON
  pack loading, deterministic dependency ordering, immutable snapshots,
  canonical SHA-256 fingerprints, transactional publication, and production
  registries for seven Abilities, 29 Skills, the first character slice, and
  passive and active Feats including spell and psionic rules;
- deterministic immutable character creation for eleven races, an eight-member
  scenario-authored active-crew limit with protagonist-led NPC recruitment, bounded
  capability/grant storage, action eligibility and resolution, Feat training,
  mixed-crew support validation, and localization-ready roster
  inspection;
- a headless 20 Hz `World` with tactical pause, bounded ordered commands,
  immutable snapshots and replay logs, continuous ship combat, modular damage,
  data-driven personal Turn Meters and Action Points, shared character
  resources, stamina speed penalties, reactions, injuries, objectives, and
  encounter cleanup;
- a versioned, bounded campaign-save envelope with content preflight, stable-ID
  reconstruction, transactional publication, durable replacement and recovery,
  and deterministic explicit migrations;
- a versioned local settings profile with strict bounded JSON, schema-1
  migration, durable replacement/recovery, English/French/Traditional Chinese
  language and safe resolution choices, stable diagnostics, and a
  keyboard-operable localized in-window SpriteForge UI modal;
- a localized SpriteForge UI main menu using the base-pack background asset,
  with bounded mouse actions for New Game, Game Settings, and Quit
  Game;
- an in-window SpriteForge character-creation UI for choosing among the 11
  authored first-voyage captain profiles and an explicit rerollable voyage
  seed; campaign construction and launch remain planned;
- authored equipment, a six-zone Glass Observatory ruin, a Wayfarer ship frame,
  Arcane and Industrial module packages, and two cannon configurations;
- a .NET 10, C# 14, Windows x64 WPF host for the current menu, settings, and
  character-creation flows;
- a narrow managed/native interop layer for SpriteForge UI and audio;
- versioned game-owned localization catalogs, typed message formatting,
  explicit fallback, pinned plural/number profiles, pseudo-locales, and offline
  catalog tooling; and
- MSTest contract suites executed through Microsoft Testing Platform.

## Product direction

- **A ship is a home:** its hull, cargo space, modules, crew, and damage persist
  through a voyage and force meaningful tradeoffs.
- **The chart is a gamble:** routes reveal hazards, opportunities, factions,
  strange environments, and shortcuts one decision at a time.
- **Systems tell the story:** crew needs, ship failures, weather, pursuit,
  resources, and encounters combine without a prescribed plot.
- **Characters remain classless:** abilities shape broad capability while
  skills improve independently through use, instruction, and experience.
- **Retreat is a decision:** a modest return keeps a campaign alive; greed can
  strand a run in the void.
- **Runs are reproducible:** explicit seeds and command streams make simulation
  outcomes testable and debuggable.

These are product goals, not claims that every system is implemented. See
[`Docs/Concept/Vision.md`](Docs/Concept/Vision.md).
Player preferences, accessibility, campaign rules, and their persistence
boundaries are defined in
[`Docs/Concept/GameSettings.md`](Docs/Concept/GameSettings.md). The planned
Arcane-Industrial setting, including dieselpunk and atompunk technology, is
defined in [`Docs/Concept/History.md`](Docs/Concept/History.md). The planned
crew races, heritages, physiology, and character-generation boundaries are
defined in [`Docs/Concept/Races.md`](Docs/Concept/Races.md). The
classless capability model is split into
[`Docs/Concept/Abilities.md`](Docs/Concept/Abilities.md) and
[`Docs/Concept/Skills.md`](Docs/Concept/Skills.md); learned and Racial
Feat rules are defined in [`Docs/Concept/Feats.md`](Docs/Concept/Feats.md), and
the protagonist, NPC recruitment, and active-roster limit are defined in
[`Docs/Concept/Crew.md`](Docs/Concept/Crew.md).
Implemented first-slice and planned personal weapons, armor, tools, and relics are defined in
[`Docs/Concept/Equipments.md`](Docs/Concept/Equipments.md).
Planned spellcasting rules,
the authored spell catalog, and psionics systems are defined in
[`Docs/Concept/Spells.md`](Docs/Concept/Spells.md), and
[`Docs/Concept/Psionics.md`](Docs/Concept/Psionics.md).
Ship engagements, boarding, ruin expeditions, EVA fighting, injuries, and
tactical resolution are defined in
[`Docs/Concept/Battle.md`](Docs/Concept/Battle.md). First-slice and planned ship frames,
modules, networks, damage, and refits are defined in
[`Docs/Concept/Ships.md`](Docs/Concept/Ships.md). The versioned procedural galaxy
graph, Starways, system generation, and discovery model are defined in
[`Docs/Concept/GalaxyMap.md`](Docs/Concept/GalaxyMap.md). Seeded random
events during interstellar travel are defined in
[`Docs/Concept/Events.md`](Docs/Concept/Events.md). Planned faction
membership, standing, diplomacy, territory, laws, markets, and conflict are
defined in [`Docs/Concept/Factions.md`](Docs/Concept/Factions.md). Optional
late-campaign threats, escalation, alternative resolutions, and aftermath are
defined in
[`Docs/Concept/Endgame_Crisis.md`](Docs/Concept/Endgame_Crisis.md).

## Repository layout

| Path | Purpose |
| --- | --- |
| [`Source/Spelljammer.App/`](Source/Spelljammer.App/README.md) | WPF host, current main menu, settings and character-creation presentation, and SpriteForge interop. |
| `Source/Spelljammer.Simulation/` | Headless authoritative voyage, character, item, Effect, and encounter state and commands. |
| `Source/Spelljammer.Content/` | Pack loading, validation, immutable registries, and semantic fingerprints. |
| `Source/Spelljammer.Persistence/` | Content-locked campaign saves, validation, recovery, and migrations. |
| `Source/Spelljammer.Settings/` | Local player preferences, validation, transactional publication, and durable persistence. |
| `Source/Spelljammer.Storage/` | Shared durable staging, atomic replacement, recovery, and exact-file cleanup primitives. |
| `Source/Spelljammer.Localization/` | Game-owned localization runtime and message formatter. |
| `Tools/Spelljammer.Content.Compiler/` | Offline gameplay-pack validation tool. |
| `Tools/Spelljammer.Localization.Compiler/` | Source-catalog compiler and validation tools. |
| `Content/Packs/base/` | Built-in capability, Race, Heritage, learned and race- or heritage-granted Feat, training, and first-roster definitions with localization. |
| `Content/Localization/` | Pinned locale-data inputs and third-party notices. |
| `Tests/Spelljammer.Simulation.Tests/` | MSTest deterministic simulation contracts. |
| `Tests/Spelljammer.Localization.Tests/` | MSTest localization contracts. |
| `Tests/Spelljammer.Content.Tests/` | MSTest gameplay content and rollback contracts plus frozen v1 fixtures. |
| `Tests/Spelljammer.Persistence.Tests/` | MSTest save, preflight, replacement, recovery, and migration contracts. |
| `Tests/Spelljammer.Settings.Tests/` | MSTest settings codec, rollback, recovery, and cleanup contracts. |
| `Docs/Concept/` | Current vision and playable-slice scope. |
| `Docs/Architecture/` | Implemented and planned subsystem boundaries. |
| `Docs/Archive/` | Historical explorations; not current product authority. |
| `Build/` | Focused CMake declarations included by the root project. |

Implemented and planned contracts are documented in the
[`Spelljammer.Content` source briefing](Source/Spelljammer.Content/README.md),
[`Spelljammer.Persistence` source briefing](Source/Spelljammer.Persistence/README.md),
[`Docs/Architecture/Simulation.md`](Docs/Architecture/Simulation.md),
[`Docs/Architecture/GameSettings.md`](Docs/Architecture/GameSettings.md),
[`Docs/Architecture/MainMenu.md`](Docs/Architecture/MainMenu.md),
[`Docs/Architecture/Modding.md`](Docs/Architecture/Modding.md), and
[`Docs/Architecture/BattleMapRoadMap.md`](Docs/Architecture/BattleMapRoadMap.md).

## Build and run

Install the .NET 10 SDK and build the managed solution:

```powershell
dotnet build .\Spelljammer.slnx
```

The WPF host also needs a built sibling SpriteForge checkout. Supply its native
output without committing a developer-specific path:

```powershell
$env:SPRITEFORGE_ROOT = (Resolve-Path ..\SpriteForge)
$nativeDir = Join-Path $env:SPRITEFORGE_ROOT 'build\windows-msvc-debug\release\bin'

dotnet build .\Spelljammer.slnx -p:SpriteForgeNativeDir="$nativeDir"
dotnet run --project .\Source\Spelljammer.App\Spelljammer.App.csproj `
    -p:SpriteForgeNativeDir="$nativeDir"
```

Follow SpriteForge's README to configure and install the native engine. The
current host is Windows-only even though reusable SpriteForge components may
target other platforms. The settings dialog requires SpriteForge UI interop
version 1 in addition to the existing renderer ABI.

Run all managed MSTest suites through Microsoft Testing Platform with:

```powershell
dotnet test .\Spelljammer.slnx
```

The command reports both per-assembly results and the aggregate discovered and
executed test count.

## Localization

`en-US` is the current source locale. Compile a source catalog with:

```powershell
dotnet run --project .\Tools\Spelljammer.Localization.Compiler\Spelljammer.Localization.Compiler.csproj -- `
    compile `
    .\Content\Packs\base\Localization\en-US\core.sfloc.json `
    .\out\Localization\en-US\core.sfloc
```

See [`Source/Spelljammer.Localization/README.md`](Source/Spelljammer.Localization/README.md)
for catalog syntax and runtime behavior.

Validate one gameplay content pack without modifying its source:

```powershell
dotnet run --project .\Tools\Spelljammer.Content.Compiler\Spelljammer.Content.Compiler.csproj -- `
    validate .\Content\Packs\base
```

Use `report` instead of `validate` to list compiled IDs, source packs,
revisions, dense indices, and registry counts.

## Contributing

Keep game rules and content in Spelljammer and reusable engine behavior in
SpriteForge. Authoritative state must remain independent of WPF, renderer
handles, localized strings, frame rate, and wall-clock timing. Describe roadmap
work as planned until source and verification exist. Pull requests run a GitHub
Actions workflow that performs a Windows `dotnet build` solution check and a
non-blocking `codespell` pass.
