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
> Spelljammer is at a playable-prototype stage, not a content-complete game.
> The current shell exposes a small deterministic expedition loop and a native
> renderer demonstration. A headless eleven-character capability roster now
> exists with trained and innate supernatural access, Magic Missile, and
> consensual Mindlink execution. Milestone 5 adds a headless modular ship
> engagement and six-zone personal encounter slice, but these are not yet
> connected to the WPF shell. Milestone 6 adds headless content-locked campaign
> saves, validation, recovery, and migration. A first localized SpriteForge UI
> game-settings dialog now persists bounded local preferences transactionally,
> and the application now opens on a background-art main menu whose current
> actions are Game Settings and Quit Game. The expedition prototype remains in
> source but is not currently reachable from that menu. WPF save controls,
> campaign-rule settings, crew
> schedules and relationships, trading, procedural ship interiors, and a full
> game UI remain planned.

## Current prototype

Each new chart creates a deterministic 4 × 4 region from an explicit seed.
Travel consumes fuel and can damage the hull; time consumes supplies. A sector
can be salvaged only once, recovered cargo can patch the hull, and a successful
run requires returning to the free anchorage with at least eight cargo.

Implemented foundations include:

- a headless `Spelljammer.Simulation` project with immutable expedition state,
  typed commands, stable sector identities, explicit rejection reasons, bounded
  maps, and seed-derived hazards and rewards;
- a bounded gameplay-content foundation with validated stable IDs, strict JSON
  pack loading, deterministic dependency ordering, immutable snapshots,
  canonical SHA-256 fingerprints, transactional publication, and production
  registries for seven Attributes, 29 Skills, the first character slice, one
  Spell, and one psychic technique;
- deterministic immutable character creation for eleven races, bounded
  capability/grant storage, action eligibility and resolution, Skill practice,
  Feat training, mixed-crew support validation, and localization-ready roster
  inspection;
- a headless 20 Hz `VoyageWorld` with tactical pause, bounded ordered commands,
  immutable snapshots and replay logs, continuous ship combat, modular damage,
  personal Turn Meters and Action Points, reactions, injuries, objectives, and
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
- a .NET 10, C# 14, Windows x64 WPF host that presents the expedition loop;
- a child Win32 viewport rendered through SpriteForge's native D3D12 sprite
  renderer and a narrow managed/native interop layer;
- versioned game-owned localization catalogs, typed message formatting,
  explicit fallback, pinned plural/number profiles, pseudo-locales, and offline
  catalog tooling; and
- compile-only simulation and localization contract targets for CI execution.

## Product direction

- **A ship is a home:** its hull, cargo space, modules, crew, and damage persist
  through a voyage and force meaningful tradeoffs.
- **The chart is a gamble:** routes reveal hazards, opportunities, factions,
  strange environments, and shortcuts one decision at a time.
- **Systems tell the story:** crew needs, ship failures, weather, pursuit,
  resources, and encounters combine without a prescribed plot.
- **Characters remain classless:** attributes shape broad capability while
  skills improve independently through use, instruction, and experience.
- **Retreat is a decision:** a modest return keeps a campaign alive; greed can
  strand a run in the void.
- **Runs are reproducible:** explicit seeds and command streams make simulation
  outcomes testable and debuggable.

These are product goals, not claims that every system is implemented. See
[`Docs/DesignConcept/Vision.md`](Docs/DesignConcept/Vision.md) and
[`Docs/DesignConcept/VerticalSlice.md`](Docs/DesignConcept/VerticalSlice.md).
Player preferences, accessibility, campaign rules, and their persistence
boundaries are defined in
[`Docs/DesignConcept/GameSettings.md`](Docs/DesignConcept/GameSettings.md). The planned
Arcane-Industrial setting, including dieselpunk and atompunk technology, is
defined in [`Docs/DesignConcept/History.md`](Docs/DesignConcept/History.md). The planned
crew races, heritages, physiology, and character-generation boundaries are
defined in [`Docs/DesignConcept/Races.md`](Docs/DesignConcept/Races.md). The
classless capability model is split into
[`Docs/DesignConcept/Attributes.md`](Docs/DesignConcept/Attributes.md) and
[`Docs/DesignConcept/Skills.md`](Docs/DesignConcept/Skills.md); Perk and Racial
Perk rules are defined in [`Docs/DesignConcept/Perks.md`](Docs/DesignConcept/Perks.md).
Implemented first-slice and planned personal weapons, armor, tools, and relics are defined in
[`Docs/DesignConcept/Equipments.md`](Docs/DesignConcept/Equipments.md).
Planned spellcasting rules,
the authored spell catalog, and psychic systems are defined in
[`Docs/DesignConcept/Spells.md`](Docs/DesignConcept/Spells.md), and
[`Docs/DesignConcept/PsychicAbilities.md`](Docs/DesignConcept/PsychicAbilities.md).
Ship engagements, boarding, ruin expeditions, EVA fighting, injuries, and
tactical resolution are defined in
[`Docs/DesignConcept/Battle.md`](Docs/DesignConcept/Battle.md). First-slice and planned ship frames,
modules, networks, damage, and refits are defined in
[`Docs/DesignConcept/Ships.md`](Docs/DesignConcept/Ships.md). The versioned procedural galaxy
graph, Starways, system generation, and discovery model are defined in
[`Docs/DesignConcept/GalaxyMap.md`](Docs/DesignConcept/GalaxyMap.md). Seeded random
events during interstellar travel are defined in
[`Docs/DesignConcept/Events.md`](Docs/DesignConcept/Events.md). Planned faction
membership, standing, diplomacy, territory, laws, markets, and conflict are
defined in [`Docs/DesignConcept/Factions.md`](Docs/DesignConcept/Factions.md). Optional
late-campaign threats, escalation, alternative resolutions, and aftermath are
defined in
[`Docs/DesignConcept/Endgame_Crisis.md`](Docs/DesignConcept/Endgame_Crisis.md).

## Repository layout

| Path | Purpose |
| --- | --- |
| [`Source/Spelljammer.App/`](Source/Spelljammer.App/README.md) | WPF host, current main menu and settings presentation, retained expedition prototype, and SpriteForge interop. |
| `Source/Spelljammer.Simulation/` | Headless authoritative expedition and character-capability state and commands. |
| `Source/Spelljammer.Content/` | Pack loading, validation, immutable registries, and semantic fingerprints. |
| `Source/Spelljammer.Persistence/` | Content-locked campaign saves, validation, recovery, and migrations. |
| `Source/Spelljammer.Settings/` | Local player preferences, validation, transactional publication, and durable persistence. |
| `Source/Spelljammer.Localization/` | Game-owned localization runtime and message formatter. |
| `Tools/Spelljammer.Content.Compiler/` | Offline gameplay-pack validation tool. |
| `Tools/Spelljammer.Localization.Compiler/` | Source-catalog compiler and validation tools. |
| `Content/Packs/base/` | Built-in capability, Race, Heritage, Perk, training, and first-roster definitions with localization. |
| `Content/Localization/` | Pinned locale-data inputs and third-party notices. |
| `Tests/Spelljammer.Simulation.Tests/` | Compile-only deterministic simulation contracts. |
| `Tests/Spelljammer.Localization.Tests/` | Compile-only localization contracts. |
| `Tests/Spelljammer.Content.Tests/` | Compile-only gameplay content and rollback contracts plus frozen v1 fixtures. |
| `Tests/Spelljammer.Persistence.Tests/` | Compile-only save, preflight, replacement, recovery, and migration contracts. |
| `Tests/Spelljammer.Settings.Tests/` | Compile-only settings codec, rollback, recovery, and cleanup contracts. |
| `Docs/DesignConcept/` | Current vision and playable-slice scope. |
| `Docs/Architecture/` | Implemented and planned subsystem boundaries. |
| `Docs/Archive/` | Historical explorations; not current product authority. |
| `Build/` | Focused CMake declarations included by the root project. |

Planned implementation contracts are documented in
[`Docs/Architecture/ContentPacks.md`](Docs/Architecture/ContentPacks.md),
[`Docs/Architecture/CharacterCapabilities.md`](Docs/Architecture/CharacterCapabilities.md),
[`Docs/Architecture/CampaignSaves.md`](Docs/Architecture/CampaignSaves.md),
[`Docs/Architecture/GameSettings.md`](Docs/Architecture/GameSettings.md),
[`Docs/Architecture/MainMenu.md`](Docs/Architecture/MainMenu.md),
[`Docs/Architecture/Modding.md`](Docs/Architecture/Modding.md), and
[`Docs/Architecture/ImplementationRoadmap.md`](Docs/Architecture/ImplementationRoadmap.md).
The frozen version 1 identity, serialization, bounds, and diagnostic contracts
are in
[`Docs/Architecture/ContentContractsV1.md`](Docs/Architecture/ContentContractsV1.md)
and
[`Docs/Architecture/ContentLimitsAndDiagnostics.md`](Docs/Architecture/ContentLimitsAndDiagnostics.md).

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
work as planned until source and verification exist.
