# Authoritative simulation

## Implemented boundary

`Source/Spelljammer.Simulation` is the headless authoritative gameplay
project. It does not reference WPF, SpriteForge, localization, wall-clock APIs,
filesystem APIs, or mutable presentation objects.

`World` owns the fixed-tick voyage timeline, ship and personal
encounters, typed commands, scheduled action phases, replay history, and
bounded events. Accepted transitions publish replacement immutable state.
Rejected commands preserve the prior world and return stable rejection codes.

Simulation cadence and catch-up policy come from the fingerprinted
`WorldTimeDefinition` selected when a `World` is created. The world also owns a
`CampaignClockState`; each accepted simulation tick advances campaign seconds
through a fingerprinted `TimeScaleDefinition`. A `CalendarDefinition` and
pure `WorldTimeQueries` projection provide the world date without duplicating
year, month, or day in mutable state. The Elven Astral Calendar (EAC) is the
setting's shared interstellar date standard, paired with Elven Astral Time
(EAT) as its 24-hour time standard. Local calendars and civil times must
project from the same absolute campaign time. See [World time](WorldTime.md)
and the setting history in
[History](../Concept/History.md#elven-astral-calendar).

The base pack authors these rules under `Definitions/WorldTimes`,
`Definitions/TimeScales`, and `Definitions/Calendars`. Changing cadence,
campaign-time rate, or calendar structure does not require recompiling
simulation code. Command order is deterministic by target tick, priority,
issuer ID, sequence, and command ID. Random outcomes use explicit world or
action seeds and owned sequence values.

The retired `ExpeditionSimulation` prototype, its 4-by-4 chart, and its WPF
host have been removed. New gameplay must integrate with `World` or a
more focused authoritative subsystem rather than recreate a parallel
simulation.

The authoritative source is separated by responsibility:

```text
Characters/  character state, creation, progression, resources, and recruitment
Combat/      character-combat definitions, action systems, and resolution contracts
Content/     stable content IDs, common definition metadata, and aggregate catalog
Effects/     Effect and Status definitions, state, queries, and systems
Encounters/  encounter definitions, battle-unit projections, tactical board, and lifecycle
Galaxy/      deterministic topology, knowledge, validation, and bounded route planning
Items/       item definitions, inventory state, equipment, and mutations
Ships/       ship definitions, state, loadout, power, damage, and combat geometry
World/       world state, time, geometry, commands, scheduled actions, and events
```

Public world contracts are named `World`, `WorldCommand`, `WorldEvent`,
`WorldSnapshot`, and `ScheduledAction`. The explicit `World` prefix prevents
command and event types from colliding with framework or UI abstractions.
The immutable `World` record is implemented as one partial type split by
responsibility: `World.cs` owns state creation and snapshots,
`World.Commands.cs` owns queueing and cancellation,
`World.Advancement.cs` owns fixed-tick scheduling,
`World.ShipCommands.cs` owns ship transitions, and
`World.PersonalCommands.cs` owns encounter transitions and the personal
timeline. This is a source-level separation only; it does not create parallel
world state or alter the public simulation contract.

## Personal combat authority

`World` schedules personal actions but does not calculate weapon,
spell, psionic, Status, or Effect results. At commit time, melee, ranged,
spell, and psionic commands require an `IPersonalCombatResolver`. The resolver
uses the typed combat systems and returns one `PersonalCombatResolution`
containing the committed `BattleUnitState` actor and target states.

The implemented [`CombatSystem`](CombatSystem.md) is the standard resolver. It
routes each character-combat command to a registered
`ICharacterCombatActionSystem`, validates the replacement states, and rejects
partial or structurally invalid results before `World` commits them.

The world validates battle-unit actor and target identities before publishing that result.
A missing, rejected, or structurally invalid resolution leaves encounter state
unchanged and records a failed event. This keeps combat formulas in their
domain systems while preserving one atomic world commit boundary.

Movement, defend, reaction reservation, medicine, interaction, engineering,
surrender, retreat, and activation lifecycle remain direct encounter
operations because they do not duplicate weapon or supernatural resolution.
Persistent characters enter encounters through `BattleUnitProjection.Project`;
an explicit `BattleUnitProjection.Commit` transaction returns resources,
items, Statuses, injuries, and action availability to `CharacterState`.

## Content views

Gameplay consumers depend on narrow read-only catalog views:

- `ICharacterDefinitionCatalog` for capabilities, progression, and recruitment;
- `ICharacterCreationCatalog` for creation plus initial inventory validation;
- `ICombatContentCatalog` for items, weapons, Feats, Statuses, and Effects;
- `IEncounterContentCatalog` for boards and encounter definitions; and
- `IShipContentCatalog` for ship frames, modules, and weapons; and
- `IWorldContentCatalog` for fixed-tick, campaign time-scale, and calendar
  definitions.

`IGameContentCatalog` composes these interfaces for application
composition and persistence. Gameplay systems should request only the narrowest
view they consume.

## Persistence and content

The persistence project serializes `World`, character, encounter, item,
Status, and command state only after content preflight. See the
[Persistence source briefing](../../Source/Spelljammer.Persistence/README.md).

Gameplay definitions are discovered, linked, validated, and compiled by the
content pipeline described in the
[Content source briefing](../../Source/Spelljammer.Content/README.md).
Character concepts live in [Abilities](../Concept/Abilities.md),
[Skills](../Concept/Skills.md), and [Feats](../Concept/Feats.md). The planned
untrusted package boundary is defined in [Modding](Modding.md).

## Verification ownership

Builds may compile the test projects to verify contracts and public API shape.
Execution of test runners remains user-owned unless the user explicitly asks
for tests to run.
