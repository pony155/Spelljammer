# Character perks

## Status

Milestone 3 implements Perk definitions, Race and Heritage compatibility,
bounded grant graphs, provenance, and the first Race/Heritage grant roster.
Broader non-racial Perks remain planned.

## Purpose

A **Perk** is a named, discrete capability with a stable content identity. It
changes one or more documented rules; it is not a 0–100 rating, a character
class, or an opaque bundle of bonuses. A Perk must state its source, effects,
costs, requirements, and incompatibilities so its result can be inspected and
saved deterministically.

## Racial Perks

A **Racial Perk** is a Perk granted by Race or compatible Heritage. It can
represent physiology, supernatural nature, or a heritage-specific
adaptation, but never dictates personality, morality, profession, or faction.

At character generation, a character receives one Race Perk and one Heritage
Perk. Examples include the Human **Versatility**, the Elf
**Aether Sense**, and the Somnari **Mindwake**. A Racial Perk may grant innate
`access.magic` or `access.psionics`; this replaces the matching training Feat
only for access. It does not grant skill ranks, techniques, free resources, or
immunity to consequences.

The full roster and validation rules are in [`Races.md`](Races.md). Training,
skills, and learned Feats are defined in [`Skills.md`](Skills.md).

## Relationship to training

Racial Perks are granted, not trained. Learned Feats are earned through a
documented training project. A character has no class and may train any
eligible Feat regardless of Race or Heritage. A future non-racial Perk category
must declare its acquisition rule explicitly before it is added to content.

## Stable identity and migration

Planned content uses the technical `perk.*` namespace and the `grantedPerkIds`
field. These match the player-facing **Racial Perk** terminology. Any future
rename requires a versioned content-schema and save migration.

No Racial Perk definition may use localized text as its identity. Definitions
must reject duplicate grants, unknown effects or access IDs, incompatible
Race/Heritage pairs, and unbounded effect payloads before publication.
