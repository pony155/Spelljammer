# Authoritative simulation

## Implemented boundary

`Source/Spelljammer.Simulation` is the headless authoritative gameplay
project. It does not reference WPF, SpriteForge, localization, wall-clock APIs,
filesystem APIs, or mutable presentation objects.

`VoyageWorld` owns the fixed-tick voyage timeline, ship and personal
encounters, typed commands, scheduled action phases, replay history, and
bounded events. Accepted transitions publish replacement immutable state.
Rejected commands preserve the prior world and return stable rejection codes.

Simulation advances at 20 fixed ticks per second and processes at most eight
catch-up ticks per call. Command order is deterministic by target tick,
priority, issuer ID, sequence, and command ID. Random outcomes use explicit
world or action seeds and owned sequence values.

The retired `ExpeditionSimulation` prototype, its 4-by-4 chart, and its WPF
host have been removed. New gameplay must integrate with `VoyageWorld` or a
more focused authoritative subsystem rather than recreate a parallel
simulation.

The authoritative source is separated by responsibility:

```text
World/       world state, commands, scheduled actions, and events
Encounters/  encounter state, tactical board, and encounter lifecycle
Combat/      character-combat coordination and resolution contracts
Ships/       ship state and ship-combat rules
```

The public type names retain the `Voyage` prefix where it clarifies their
scope, while their namespaces follow these ownership boundaries.

## Personal combat authority

`VoyageWorld` schedules personal actions but does not calculate weapon,
spell, psionic, Status, or Effect results. At commit time, melee, ranged,
spell, and psionic commands require an `IPersonalCombatResolver`. The resolver
uses the typed combat systems and returns one `PersonalCombatResolution`
containing the committed actor and target states.

The implemented [`CombatSystem`](CombatSystem.md) is the standard resolver. It
routes each character-combat command to a registered
`ICharacterCombatActionSystem`, validates the replacement states, and rejects
partial or structurally invalid results before `VoyageWorld` commits them.

The world validates actor and target identities before publishing that result.
A missing, rejected, or structurally invalid resolution leaves encounter state
unchanged and records a failed event. This keeps combat formulas in their
domain systems while preserving one atomic world commit boundary.

Movement, defend, reaction reservation, medicine, interaction, engineering,
surrender, retreat, and activation lifecycle remain direct encounter
operations because they do not duplicate weapon or supernatural resolution.

## Content views

Gameplay consumers depend on narrow read-only catalog views:

- `ICharacterDefinitionCatalog` for capabilities, progression, and recruitment;
- `ICharacterCreationCatalog` for creation plus initial inventory validation;
- `ICombatContentCatalog` for items, weapons, Feats, Statuses, and Effects;
- `IEncounterContentCatalog` for boards and encounter definitions; and
- `IShipContentCatalog` for ship frames, modules, and weapons.

`ICharacterContentCatalog` composes these interfaces for application
composition and persistence. Gameplay systems should request only the narrowest
view they consume.

## Persistence and content

The persistence project serializes `VoyageWorld`, character, encounter, item,
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
