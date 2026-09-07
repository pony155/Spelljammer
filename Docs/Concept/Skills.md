
# Skills

## Status

This document defines the planned skill roster and the relationship between
skills, abilities, training, and action resolution. The current repository has
the content and character-capability foundations for this model, but the full
learned-skill progression system remains planned.

The initial crew-enabled slice should use a small subset of this roster. New
skills should be added only when they create a distinct player decision,
support a recurring activity, or express a meaningful character identity.

## Purpose

Skills represent what a character has learned to do. They are narrower than
the six broad abilities and are not character classes, professions, or fixed
roles. Any eligible character may train a skill regardless of Race, Heritage,
or previous specialization.

An action combines one skill with the ability that best describes the current
approach and circumstances. The same skill can therefore produce different
outcomes when used by different characters, with different equipment, or in a
different environment.

Examples:

- Strength plus Melee Weapons can deliver a forceful strike.
- Agility plus Melee Weapons can place a precise strike.
- Agility plus Archery can make a difficult shot.
- Intelligence plus Engineering can diagnose a damaged drive.
- Strength plus Engineering can force a warped housing into position.
- Perception plus Salvage can identify a valuable component in wreckage.
- Intelligence plus Merchant can recognize manipulated cargo records.
- Perception plus Negotiation can read a bluff during contract terms.
- Willpower plus Magic can stabilize a failing ward.
- Intelligence plus Ancient Lore can explain a dangerous relic.

The interface must show the selected skill, the selected ability, relevant
modifiers, and the reason the approach is available. Alternative ability
approaches should be offered when fiction, equipment, training, and current
conditions support them.

## Skill principles

- Skills use stable IDs and localized presentation keys.
- Skill ranks describe learned capability; they do not directly replace an
  ability or grant a universal bonus.
- Skills are resolved through explicit commands and fixed simulation ticks.
- Equipment, assistance, fatigue, wounds, morale, environment, and supplies
  modify an action without silently changing the character's learned rank.
- A skill must have a visible use in exploration, ship operation, combat,
  care, social interaction, crafting, or knowledge play.
- Failure should produce an inspectable consequence, such as lost time,
  resource cost, injury risk, detection, damage, or a changed relationship.
- Skills must not be used as hidden requirements for content that has no
  player-facing decision.

## Skill roster

### Combat

| Skill | Stable ID | Scope |
| --- | --- | --- |
| Melee Weapons | `skill.melee` | Swords, axes, spears, improvised melee weapons, and close-range attacks |
| Archery | `skill.archery` | Bows, crossbows, and other non-firearm projectile weapons |
| Firearms | `skill.firearms` | Pistols, rifles, and conventional personal firearms |
| Heavy Weapons | `skill.heavy-weapons` | Portable launchers, crew-served weapons, and oversized battlefield weapons |
| Energy Weapons | `skill.energy-weapons` | Personal arcane, plasma, crystal, and other directed-energy weapons |
| Throwing | `skill.throwing` | Thrown weapons, grenades, bottles, and improvised projectiles |

Ship cannons do not require a separate Gunnery skill in the first design. Their
accuracy and timing are properties of the cannon configuration, ship systems,
and command order. A future Gunnery skill would require a distinct player
decision that cannot be expressed by those systems.

### Supernatural and chemical practice

| Skill | Stable ID | Scope |
| --- | --- | --- |
| Magic | `skill.magic` | Direct spellwork, magical effects, wards, and controlled use of Aether |
| Psionics | `skill.psionics` | Mindlinks, mental influence, psionic sensing, and resistance techniques |
| Alchemy | `skill.alchemy` | Reagents, medicines, compounds, toxins, and volatile mixtures |
| Enchantment | `skill.enchantment` | Persistent magical bindings, resonators, runes, and enchanted equipment |

Magic and Psionics are learned skills. Race or Heritage may grant access to
one of them through a Feat, but access does not grant ranks, techniques, free
resources, or immunity to consequences.

### Ship operation and expedition work

| Skill | Stable ID | Scope |
| --- | --- | --- |
| Piloting | `skill.piloting` | Ship handling, docking, evasion, close maneuvering, and emergency control |
| Astrogation | `skill.astrogation` | Route plotting, position fixes, star charts, and anomaly-aware travel |
| Sensors | `skill.sensors` | Detecting contacts, hazards, emissions, survey targets, and hidden objects |
| Rigging | `skill.rigging` | Sails, lines, external work, heavy lifting, and emergency deployment |
| Salvage | `skill.salvage` | Recovering wreckage, cargo, components, and dangerous materials |
| Engineering | `skill.engineering` | Diagnosing, operating, and repairing propulsion, power, atmosphere, and ship modules |
| Crafting | `skill.crafting` | Manufacturing tools, fittings, ammunition, replacement parts, and modifications |

These skills are deliberately separate. Piloting controls the vessel, while
Astrogation determines where it can safely travel. Engineering understands
how a system works, while Crafting produces or modifies the physical thing.
Rigging and Salvage focus on hazardous external work and recovery rather than
general repair.

### Survival and care

| Skill | Stable ID | Scope |
| --- | --- | --- |
| Athletics | `skill.athletics` | Climbing, bracing, carrying, swimming, gravity changes, and sustained exertion |
| Survival | `skill.survival` | Staying alive with limited supplies in unfamiliar or hostile environments |
| Medicine | `skill.medicine` | Diagnosis, treatment, surgery, quarantine, and recovery |
| Cooking | `skill.cooking` | Preparing safe meals, preserving provisions, and handling different diets |
| Xenology | `skill.xenology` | Alien biology, species behavior, ecosystems, toxins, and biological hazards |

### Social and knowledge skills

| Skill | Stable ID | Scope |
| --- | --- | --- |
| Merchant | `skill.merchant` | Appraisal, cargo, markets, contracts, and trade opportunities |
| Negotiation | `skill.negotiation` | Bargaining, diplomacy, docking terms, surrender, and dispute resolution |
| Insight | `skill.insight` | Reading emotion, detecting pressure, understanding motives, and mediating conflict |
| Leadership | `skill.leadership` | Directing crew, maintaining morale, coordinating emergencies, and taking responsibility |
| Language and Literacy | `skill.language-literacy` | Translation, records, signals, inscriptions, and formal documents |
| Ancient Lore | `skill.ancient-lore` | Lost civilizations, relics, myths, ruins, and ancient technologies |
| Research | `skill.research` | Comparing evidence, maintaining records, testing hypotheses, and building knowledge |

## Skill and ability combinations

Skills do not have one permanently assigned ability. The action definition
chooses the ability according to method and circumstances.

| Activity | Possible combinations |
| --- | --- |
| Repair a damaged module | Intelligence + Engineering; Strength + Engineering; Toughness + Engineering |
| Plot a route through an anomaly | Intelligence + Astrogation; Perception + Astrogation; Willpower + Astrogation |
| Recover a wreck component | Perception + Salvage; Toughness + Salvage; Strength + Salvage |
| Operate a damaged sail or external fitting | Strength + Rigging; Agility + Rigging; Toughness + Rigging |
| Prepare a crew meal | Intelligence + Cooking; Perception + Cooking; Toughness + Cooking |
| Treat an injured crew member | Intelligence + Medicine; Willpower + Medicine; Perception + Medicine |
| Negotiate with a faction | Intelligence + Negotiation; Perception + Negotiation; Willpower + Negotiation |
| Interpret a relic | Intelligence + Ancient Lore; Perception + Ancient Lore; Willpower + Ancient Lore |
| Use a supernatural technique | Willpower + Magic or Psionics; Intelligence + Magic or Psionics; Perception + Psionics |

The combination table is guidance, not a fixed class matrix. Content may define
additional combinations when the action provides a clear explanation.

## Training and advancement

Skills are learned through training projects, instruction, recovered knowledge,
faction teaching, or authored consequences. A training project must
declare:

- the skill ID and target rank;
- required time, facilities, materials, or instructor tags;
- possible risks or interruptions;
- the event or command that grants the result; and
- any prerequisite Feat or access Feat.

Training is committed transactionally. An interrupted project keeps its
documented partial state or fails according to its definition; it never grants
an undocumented rank. Racial and Heritage Feats are granted separately and do
not count as skill ranks.

## First playable scope

The first crew-enabled voyage should exercise a focused subset:

| Area | Skills |
| --- | --- |
| Travel | Piloting, Astrogation, Sensors |
| Ship work | Engineering, Rigging, Salvage |
| Crew care | Medicine, Cooking, Athletics |
| Contact | Negotiation, Merchant, Language and Literacy |
| Discovery | Ancient Lore |
| Combat and supernatural action | Melee Weapons, Archery or Firearms, Magic, Psionics |

Crafting, Survival, Xenology, Leadership, Research, Heavy Weapons, Energy
Weapons, Throwing, Alchemy, and Enchantment may remain available as authored
definitions but do not need complete progression or encounter coverage in the
first slice.

## Data and persistence

Skill definitions use stable canonical IDs and localized presentation keys.
Persistent character state stores learned ranks, training projects, granted
access, and learned Feat IDs separately. Save data never stores a translated
skill name as identity.

An authored skill definition should resemble:

```json
{
  "schemaVersion": 1,
  "id": "skill.engineering",
  "nameKey": "skill.engineering.name",
  "descriptionKey": "skill.engineering.description",
  "category": "ship-operation",
  "maxRank": 10,
  "tags": ["repair", "ship-module"]
}
```

Content loading rejects duplicate IDs, missing localization keys, invalid
categories, negative or unbounded ranks, unknown tags, and references to
unknown skill IDs. Changing a released skill ID or its meaning requires a
content-schema revision and save migration.

## Boundaries and deferred skills

Skills should remain character-facing capabilities. Ship cannon statistics,
module power allocation, and fixed resource consumption belong to ship and
combat definitions rather than hidden skill checks.

The following are intentionally deferred unless future gameplay gives them a
distinct role:

- Gunnery, because ship cannons currently resolve through ship configuration;
- Lockpicking, if Security can cover access and containment decisions;
- Acrobatics, if Athletics can cover movement and balance;
- Navigation as a separate skill, if Astrogation already covers all route and
  position decisions; and
- separate weapon specializations, unless the roster becomes too broad for a
  single combat skill to support meaningful builds.
crafting

Negotiation
Merchant
Navigation
