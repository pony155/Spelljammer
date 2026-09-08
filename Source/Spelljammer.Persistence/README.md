# Spelljammer.Persistence

This headless project owns the versioned, content-locked campaign save format,
bounded preflight and reconstruction, transactional campaign publication,
durable same-directory replacement, recovery artifacts, and explicit
migrations. The public `CampaignSaveCodec` facade delegates envelope,
content-lock, write-mapping, and read-mapping work to focused partial
implementations without changing the save schema.

Durable stage, read-back validation, atomic replacement, and recovery mechanics
come from the game-owned `Spelljammer.Storage` project. Persistence retains
ownership of campaign encoding, validation, limits, and diagnostics. It has no
WPF, localization, SpriteForge, or native dependency.
Callers supply the immutable `GameContentSnapshot` selected for a load; saved
stable IDs are resolved and runtime indices are reconstructed only inside that
validated boundary.

The codec accepts only the current save schema (12); older schema conversion is
not retained during this prototype phase. Schema 12 preserves campaign time,
campaign and encounter character resources, turn state, and complete
item-instance ownership: containers, equipped slots, durability, melee energy,
ranged ammunition, energy, heat, and stackable inventory entries. It also
persists authored Status instances, including source, target, duration, stacks,
potency, and definition revision.

The pre-EAC base content fingerprint is intentionally unsupported. The
shipping prototype retains neither its old content snapshot nor a migration to
the current EAC/EAT calendar definition. A caller that must preserve such a
development save must supply both an exact old snapshot and an explicit,
verified migration path.
