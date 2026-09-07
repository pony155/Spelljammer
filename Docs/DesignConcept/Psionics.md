# Psionics

## Status

Design concept only. No psionics technique is implemented yet. This document
defines the intended shape for `PsychicAbilities.md`
referenced from [`Skills.md`](Skills.md); it will be renamed or merged there
once technique content is authored.

## Simple psionics rule

Psionics creates memorable choices, not psychic-power accounting. A technique
tooltip shows its **Strain cost**, **range**, **cast time**, and **effect**; it
may also show a resistance or consent rule. Strain is the personal cost of
pushing a mind past its comfort; Psionics Strain accumulates and imposes
consequences before it becomes a hard stop, rather than acting as a second
mana pool with its own economy.

Using a technique is one action: choose a legal target, preview the effect,
pay the Strain cost, and resolve it. A failed technique still spends its
stated cost and clearly says why it failed. Only a sustained technique can be
interrupted. There are no separate reservation, meditation, or detailed ritual
systems.

## Access and learning

Characters have no psion class. To use a technique, a character needs:

1. `access.psionics`, from `feat.access.psionics` (Psionic Training) or a
   Racial Perk that explicitly grants it;
2. the technique's stable ID in their known-technique collection; and
3. enough Strain capacity and a legal target.

The Somnari Race Perk **Mindwake** is the initial innate source of
`access.psionics`. It grants basic psionics contact, not free Psionics ranks or
known techniques. A high Psionics skill never bypasses the access requirement.

Learning a technique is a short, explicit training project: find a source,
meet its stated requirement, spend downtime, and add its technique ID.
Sources can be teachers, psychic contacts, ruins, factions, or discoveries.

## Disciplines

There are four Psionic Disciplines. Disciplines organize discovery and
teaching; they are not classes and do not add separate resource systems.

| Discipline | Purpose |
| --- | --- |
| Telepathy | Mental contact, communication, emotion and thought reading, and influence |
| Telekinesis | Remote physical force and fine manipulation |
| Clairsentience | Detection, sensing, and brief precognition |
| Discipline | Self-directed mental defense and control |

## Technique definition

Every technique definition contains a stable ID, localized name and
description, required access ID, Strain cost, range, cast time, one bounded
effect, and an optional consent or resistance rule. It may additionally
declare legal target tags or a governing Ability other than Willpower.
Definitions must have bounded targets and a clear end condition.

## First playable techniques

The first character-combat slice uses four Tier 1 techniques:

| Technique | Stable ID | Strain | Range | Cast time | Effect |
| --- | --- | ---: | --- | --- | --- |
| Mindlink Contact | `psi.telepathy.mindlink-contact` | Low | Near | Instant | Open a two-way mental link with a willing or resisting target. |
| Mind Shield | `psi.discipline.mind-shield` | Low | Self | Instant | Raise personal resistance to mental intrusion for a short duration. |
| Telekinetic Force | `psi.telekinesis.force` | Low | Near | Instant | Apply remote force to one visible target or object, choosing to push it away or pull it toward the caster. |
| Detect Life | `psi.clairsentience.detect-life` | Low | Far | Instant | Sense the presence, rough count, and direction of any creature with Willpower 3 or higher within range. Governed by Intelligence, not Willpower. |

## Catalog

The remaining entries are planned content candidates. Their final numbers are
balance data; their intended use stays short and readable.

### Telepathy

| Technique | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |
| Psychic Blast | `psi.telepathy.psychic-blast` | 2 | Deal direct psychic damage to one target's mind; resistible by Willpower. |
| Mental Domination | `psi.telepathy.mental-domination` | 3 | Seize control of a target's actions for a limited duration; resisted and broken by significant harm or counter-psionics. |
| Psychic Scan | `psi.telepathy.psychic-scan` | 2 | Sweep a wide area to detect and locate all conscious minds within range, revealing their general position and relative mental strength. |

Telepathy never rewrites memory, forces permanent belief, or grants
unrestricted mind reading. A resisted attempt still creates Psionics Strain,
distorted impressions, or detectable feedback for the target.

### Telekinesis

| Technique | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |
| Telekinetic Force | `psi.telekinesis.force` | 1 | Apply remote force to one visible target or object, choosing to push it away or pull it toward the caster. |
| Force Shield | `psi.telekinesis.force-shield` | 2 | Raise a short-duration telekinetic barrier that absorbs or deflects incoming physical force for the caster or a nearby ally. |

### Clairsentience

| Technique | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |

Detect Life is a pure Willpower-score check against the target, not a
life-versus-undead check: a lich or vampire with Willpower 3 or higher
registers the same as any living creature. It does not classify targets by
creature type.

### Discipline

| Technique | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |


## Limits, saves, and delivery

Psionics cannot freely create wealth, resurrect the dead, read any mind
without limit, time travel, or bypass the galaxy map. Saves store known
technique IDs and active effects that matter: source ID, target, remaining
duration, and caster when relevant.

Content validation rejects duplicate IDs, unknown access or target tags,
negative costs, unbounded targets or durations, and effects that bypass these
limits. Implement the four first-playable techniques first; add the
remaining ones only when they make combat or exploration more interesting.
