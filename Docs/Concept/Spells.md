# Spells

## Status

Magic Missile, its learning project, active Feat definition, and deterministic
headless execution phases are implemented. The remaining catalog and visual
encounter integration are planned.

## Simple magic rule

Magic creates memorable choices, not aether chemistry or ritual accounting. A
spell tooltip shows its **Mana cost**, **range**, **cast time**, and **effect**;
it may also show a short cooldown. Arcane is the overall magical practice;
Aether is the non-material medium that carries its energy. A personal spell uses
Mana, while an item or ship spell may explicitly consume Aether charge.

Casting is one action: choose a legal target, preview the effect, pay the cost,
and resolve it. A failed spell spends its stated cost and clearly says why it
failed. Only a channeled spell can be interrupted. There are no separate
reservation, preparation, refund, contamination, or detailed ritual systems.

Magic belongs to the advanced Arcane-Industrial setting. Wards and spells can
exist beside engines, reactors, sensors, armor, and energy weapons without
requiring a second layer of simulation. See [`History.md`](History.md).

## Access and learning

Characters have no mage class. To cast, a character needs:

1. `access.magic`, from `feat.access.magic` (Spellcasting Training) or a
   Racial Feat that explicitly grants it;
2. the spell's active `FeatId` in their Feats collection; and
3. enough Mana and a legal target.

The Elf Race Feat **Aether Sense** is the initial innate source of
`access.magic`. It grants the ability to perceive nearby Aether patterns, but
no free Magic ranks or known spells. A high Magic skill, spellbook, or item
never bypasses the access requirement.

Learning a spell is a short, explicit training project: find a source, meet its
stated requirement, spend downtime, and add its active Feat ID. Sources can be
teachers, books, ruins, factions, or discoveries.

## Schools

There are four Arcane Traditions. Traditions organize discovery and
teaching; they are not classes and do not add separate resource systems.

| School | Purpose |
| --- | --- |
| Elemental | Fire, lightning, and other direct natural-energy effects |
| Spirit | Arcane force, perception, invisible presences, and false sights |
| Hex | Temporary curses and disruptive effects with a clear counter or end condition |
| Nature | Living systems, plants, ecosystems, and cooperation with a local environment |

## Spell definition

Every active Spell Feat contains a stable ID, localized name and description,
required access ID, Mana cost, range, cast time, one bounded effect, and an
optional cooldown. It may additionally declare legal target tags, damage type,
or resistance. Definitions must have bounded targets and a clear end condition.

## First playable spells

The first character-combat slice uses four Tier 1 spells:

| Spell | Stable ID | Mana | Range | Cast time | Effect |
| --- | --- | ---: | --- | --- | --- |
| Magic Missile | `feat.active.spell.spirit.magic-missile` | Low | Far | Instant | Deal reliable Arcane damage to one visible target. |
| Burning Hands | `feat.active.spell.elemental.burning-hands` | Low | Near | Instant | Deal fire damage in a short previewed cone. |
| Phantasmal Image | `feat.active.spell.spirit.phantasmal-image` | Low | Near | Instant | Create one visual decoy that distracts or misleads. |
| Detect Invisibility | `feat.active.spell.spirit.detect-invisibility` | Low | Near | Instant | Reveal nearby invisible subjects or their outline briefly. |

## Catalog

The remaining entries are planned content candidates. Their final numbers are
balance data; their intended use stays short and readable.

### Elemental

| Spell | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |
| Burning Hands | `feat.active.spell.elemental.burning-hands` | 1 | Deal fire damage in a short, previewed cone. |
| Lightning Bolt | `feat.active.spell.elemental.lightning-bolt` | 2 | Deal lightning damage along a previewed line. |

Burning Hands and Lightning Bolt show their area before casting, including
allies.

### Spirit

| Spell | Stable ID | Tier | Intended effect |
| --- | --- | ---: | --- |
| Magic Missile | `feat.active.spell.spirit.magic-missile` | 1 | Deal reliable Arcane damage to one visible target. |
| Magic Missile Storm | `feat.active.spell.spirit.magic-missile-storm` | 3 | Split Arcane missiles among several visible targets. |
| Invisibility | `feat.active.spell.spirit.invisibility` | 2 | Hide one subject until it attacks, casts, or the short duration ends. |
| Phantasmal Image | `feat.active.spell.spirit.phantasmal-image` | 1 | Create one visual decoy that can distract or mislead. |
| Detect Invisibility | `feat.active.spell.spirit.detect-invisibility` | 1 | Reveal nearby invisible subjects or their outline briefly. |

Spirit effects change perception or strike through Arcane force; they do not rewrite
memory, create physical cover, or force belief. A successful inspection or
Spirit detection effect reveals an illusion or invisible subject. Magic Missile
still needs a legal visible target, Mana, and any defense declared by its
definition.

### Hex

Hex is a planned tradition for temporary curses, misfortune, weakening, and
disruption. It has no base spell in the first catalog yet. Every future Hex
must show its duration, counterplay, and clear end condition; it cannot impose
permanent mind control or an unexplained irreversible penalty.

### Nature

Nature is a planned tradition for living habitats, plants, beasts, recovery,
and environmental adaptation. It does not grant automatic control over animals
or create unlimited food, oxygen, or healing. The first catalog contains no
Nature spell yet; each future spell must create a clear exploration, survival,
or support choice with a visible limit.

## Limits, saves, and delivery

Magic cannot freely create wealth, resurrect the dead, read any mind, time
travel, or bypass the galaxy map. Saves store known active Feat IDs and active effects
that matter: source ID, target, remaining duration, and caster when relevant.

Content validation rejects duplicate IDs, unknown access or target tags,
negative costs, unbounded targets or durations, and effects that bypass these
limits. Implement the four first-playable spells first; add the remaining three
only when they make combat or exploration more interesting.
