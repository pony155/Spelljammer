# Character Feats

## Status

The content compiler, immutable character capability state, bounded grant
graphs, training projects, Race and Heritage grants, and the first Feat roster
are implemented. Broader learned Feats and complete advancement presentation
remain planned.

## Purpose

A **Feat** is a named, discrete capability with a stable content identity. It
changes one or more documented rules; it is not a Skill rank, character class,
or opaque bundle of bonuses. Every Feat declares its acquisition source,
effects, requirements, and incompatibilities so its result can be inspected,
validated, and saved deterministically.

Spelljammer distinguishes two acquisition categories while presenting both as
Feats:

| Category | Source type | Example | Technical identity |
| --- | --- | --- | --- |
| Learned Feat | Training project or authored reward | Spellcasting Training | `feat.access.magic` |
| Racial Feat | Race or compatible Heritage | Aether Sense | `feat.race.elf.aether-sense` |
| Heritage Feat | Compatible Heritage | Dawnweave | `feat.heritage.elf.dawnweave` |

The source code uses `FeatDefinition` for trained Feats and
`RacialFeatDefinition` for Race and Heritage Feats because their validation and
acquisition contracts differ. Both use the player-facing Feat terminology and
the `feat.*` ID namespace.

## Learned Feats

A learned Feat is earned through a documented training project or another
explicit campaign reward. It may grant access such as `access.magic` or
`access.psionics`, but it does not automatically grant Skill ranks, techniques,
free resources, or immunity to consequences.

Training completion validates prerequisites, facilities, safety, work, and
resource costs before granting the Feat and its access sources atomically.
Partial training never provides partial access.

## Racial and Heritage Feats

A Racial Feat represents physiology, supernatural nature, or a distinct racial
adaptation. A Heritage Feat represents a compatible environmental, cultural,
or physiological inheritance. Neither category dictates personality,
intelligence, morality, profession, faction, or a hard Skill ceiling.

At character creation, a character receives:

1. the Racial Feats granted by their Race definition; and
2. the Heritage Feats granted by their compatible Heritage definition.

A Racial or Heritage Feat may grant innate supernatural access. Innate access
replaces the matching learned access Feat only for access validation; it does
not count as having completed that training project.

## Grant provenance

Every effective capability records a stable source chain. If a learned Feat and
a Racial Feat both grant `access.magic`, removing either source leaves access
available while the other valid source remains. Removing a source also removes
only capabilities that no longer have another retained source.

Grant graphs are bounded and validated before publication. Content rejects
unknown references, duplicate grants, incompatible Race links, cycles, and
graphs exceeding the configured depth or entry limits.

## Data and persistence

Learned Feat definitions live under `Definitions/Feats` and use `FeatId`.
Race and Heritage Feat definitions live under `Definitions/RacialFeats` and use
`RacialFeatId`. Example IDs are:

```text
feat.access.magic
feat.access.psionics
feat.race.elf.aether-sense
feat.heritage.elf.dawnweave
```

Race and Heritage definitions reference `grantedRacialFeatIds`. Training
projects reference `grantedFeatIds`. Persistent character state stores learned
Feat IDs and Racial Feat IDs separately so acquisition rules and provenance can
be reconstructed and validated.

No Feat uses localized text as identity. Changing a released Feat ID, category,
or meaning requires a content-schema revision and save migration.

## First playable scope

The first crew-enabled slice needs:

- the Race and Heritage Feats defined in [`Races.md`](Races.md);
- Spellcasting Training and Psionics Awakening as learned access Feats;
- independent provenance when innate and trained access overlap;
- one action enabled by a Racial Feat;
- one effect removed without removing an independent grant source; and
- inspectable rejection reasons for missing or incompatible Feats.

Additional combat, social, exploration, crafting, and ship-operation Feats
remain planned until their rules create choices that cannot be expressed by
Skills, equipment, techniques, or temporary status effects.
