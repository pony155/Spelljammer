# Battle Unit

## Implemented boundary

`BattleUnitState` is the authoritative encounter-scoped representation of one
participant on a personal `TacticalBoard`. It owns battlefield position,
team, Turn Meter, Action Points, character resources, equipment, Statuses,
injuries, defense, surrender, prisoner, and reaction state while the encounter
is active. `PersonalEncounterState.Units` owns the bounded unit collection;
`CombatSystem` only validates and coordinates replacement states.

`BattleUnitId` uses the `unit.*` namespace. Authored encounters may supply a
stable ID directly. Procedural encounters must use `BattleUnitId.Derive` with
the encounter ID, stable source ID, and spawn ordinal so replay and UI timing
cannot change identity or equal-tick ordering.

## Character projection

`BattleUnitProjection.Project` creates a unit from `CharacterState` at the
encounter boundary. It copies combat-owned resources, items, Statuses, and
injuries, initializes the unit turn state from the active data-driven resource
profile, and retargets Status instances from character identity to unit
identity. The projection validates the complete character against the active
catalog and rejects characters that cannot act before creating any state.

`BattleUnitProjection.Commit` is the reverse transaction. It verifies the
character association, returns resources, items, Statuses, and injuries to the
persistent character, derives whether the character can still act, retargets
Statuses to `CharacterId`, and validates the complete replacement before it is
published. Commit also rejects mismatched Unit identity, Status ownership, or
turn state instead of silently normalizing it. Rejected projection or commit
work never mutates either input.

Non-character units may leave `CharacterId` empty, but they cannot be committed
through the character projection boundary.

## Board, commands, and persistence

`TacticalBoard.Occupants`, `PersonalEncounterState.Units`, and the World's
Ready-unit queue use `BattleUnitId`. Personal `WorldCommand` issuer and target
fields remain general `ContentId` values because the same command envelope also
addresses ships, cells, objectives, and objects; personal command validation
must parse their `unit.*` values before execution.

`StatusTargetId` is the explicit Status ownership ID. Projection is responsible
for changing it between character and battle-unit identities.

Campaign save schema 13 writes `units` and `readyUnitIds`. Schema 12 remains a
supported read format: the bounded in-place migration renames the old actor
properties, rewrites `actor.*` identities to `unit.*`, records
`migration.save.v13-battle-units`, and publishes only the current schema.
