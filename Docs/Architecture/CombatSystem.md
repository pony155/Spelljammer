# Character Combat System

## Purpose

`CombatSystem` is the authoritative coordinator for character combat. It is
used for personal melee, ranged, spell, and psionic commands; ship combat
remains owned by the separate `Spelljammer.Simulation.Ships` namespace.

The coordinator does not contain weapon formulas, spell rules, or psionic
rules. Those remain in focused action systems such as `MeleeWeaponSystem`,
`RangedWeaponSystem`, `SpellActionSystem`, and the psionic systems.

## Implemented boundary

`CombatSystem` implements `IPersonalCombatResolver`, which lets `World`
schedule and commit character combat without calculating combat outcomes.
Each supported `CommandKind` is registered through an
`ICharacterCombatActionSystem` adapter.

The coordinator and result contracts live in `Spelljammer.Simulation.Combat`.
They consume encounter state from `Spelljammer.Simulation.Encounters` and
world commands from `Spelljammer.Simulation.World`; neither lower-level
namespace owns the other.

Resolution follows this transaction boundary:

```text
PersonalCombatContext
        |
        v
CombatSystem validation
        |
        v
Registered action system
        |
        v
Replacement actor and target states
        |
        v
CombatSystem result validation
        |
        v
World atomic commit
```

The system rejects unknown or unregistered actions, actors that cannot act,
illegal targets, exceptions from an action system, and structurally invalid
results. Rejection always returns the original actor and target state, so a
partially resolved action cannot escape into the encounter.

The result validator preserves actor identity, team, character association,
and board cell; prevents action points from increasing; enforces Status and
item collection shape limits; and requires valid event and damaged-object
data. `World` performs a second validation before publishing the
replacement encounter state.

## Composition

The application composition root should construct one `CombatSystem` from the
available action adapters and pass it to `World.Advance`. An adapter owns
translation between `PersonalCombatContext` and its typed domain request. For
example, a melee adapter selects the commanded item/action, derives target
distance and defenses from the board and combatant state, invokes
`MeleeWeaponSystem`, applies its Effect requests, and returns one
`PersonalCombatResolution`.

This keeps action-specific mechanics testable in isolation while providing a
single character-combat authority and commit boundary.
