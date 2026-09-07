# Character attributes

## Status

This document defines the attribute model. The seven definitions and their
typed immutable content registry are implemented. Character attribute values,
generation, advancement, races, heritages, and complete character composition
remain planned. Those character rules are defined in [`Races.md`](Races.md);
learned capabilities and action resolution are defined in [`Skills.md`](Skills.md).
The planned data definitions, runtime registry, and character-state layout are
specified in
[`../Architecture/CharacterCapabilities.md`](../Architecture/CharacterCapabilities.md).

## Purpose

Attributes express broad capability. They answer how a character approaches a
task, while skills answer what the character has learned to do. Spelljammer has
no character classes or global character level.

The normal player-facing attribute range is 1–10. Permanent values change
rarely. Injuries, needs, equipment, supernatural conditions, assistance, and
the environment apply temporary modifiers without rewriting those permanent
values.

## Attribute roster

| Attribute | Stable ID | Governs |
| --- | --- | --- |
| Strength | `attribute.strength` | Physical force, lifting, carrying capacity, heavy weapons, and resisting forced movement |
| Agility | `attribute.agility` | Coordination, balance, reflexes, precise manipulation, movement, and aim |
| Toughness | `attribute.toughness` | Health, stamina, pain tolerance, physical recovery, and environmental resistance |
| Willpower | `attribute.willpower` | Concentration, courage, self-control, and magical or psychic control and resistance |
| Intelligence | `attribute.intelligence` | Observation, analysis, memory, diagnosis, planning, and technical learning |
| Charisma | `attribute.charisma` | Leadership, empathy, intimidation, performance, deception, and negotiation |
| Luck | `attribute.luck` | Bounded fortunate outcomes, rare opportunities, and narrow escapes where chance is relevant |

## Contextual use

Attributes are not permanently bound to skills. An action declares the
attribute that matches its method and circumstances:

- Strength plus Engineering can force a warped drive housing into position.
- Intelligence plus Engineering can diagnose why the housing failed.
- Willpower plus Engineering can complete the repair during an aether storm.
- Toughness plus Athletics can sustain hard work in dangerous gravity.
- Strength plus Melee can deliver a forceful strike.
- Agility plus Melee can place a precise strike.
- Agility plus Archery can make a difficult shot.
- Willpower plus Magic can guide an enchanted arrow.
- Intelligence plus Enchantment can inscribe a stable magical binding.
- Intelligence plus Psionics can interpret an unfamiliar psychic signal.
- Willpower plus Psionics can maintain a shield against psychic intrusion.
- Charisma plus Psionics can send a clear emotion through a mindlink.
- Luck plus Salvage can expose a rare find on a chance-driven sweep.
- Intelligence plus Merchant can recognize manipulated market records.
- Charisma plus Negotiation can bargain for safer contract terms.
- Charisma plus Ancient Lore can explain a discovery to a suspicious faction.

The interface must identify the chosen attribute and explain why it applies.
Alternative approaches should be available when fiction, equipment, and known
techniques support them.

## Race, heritage, and attributes

Race may change physiological rules, while heritage may make an attribute
cheaper to use in a particular environment. Neither assigns Intelligence,
morality, or a hard attribute ceiling. For example, a heritage may reduce the
Toughness cost of high-gravity work without receiving a universal Toughness
bonus.

This distinction keeps race and heritage relevant to ship design and survival
while allowing any character to become an expert in any field through training
and experience.

## Luck and determinism

Luck is not a universal substitute for another attribute. An authored action
must explicitly allow Luck because fortune materially affects its outcome. Luck
then adjusts a bounded result, opportunity table, or consequence; it does not
erase requirements or guarantee success.

Luck uses the simulation's owned seeded random stream. Replaying the same seed
and committed commands therefore produces the same fortunate or unfortunate
result. UI timing, render cadence, save reloading, and repeated previews cannot
reroll an outcome.

## Permanent and temporary change

Permanent attributes may change through:

- prolonged focused training;
- a major campaign milestone;
- aging or recovery from a lasting condition;
- a permanent injury, prosthetic, or bodily modification;
- magical transformation; or
- a rare authored story consequence.

Temporary modifiers come from current state such as fatigue, hunger, thirst,
fear, morale, wounds, gravity, atmosphere, lighting, medication, equipment, and
crew assistance. Sources stack only through explicit bounded rules, and the UI
must show every applied source.

## Data and persistence

Attribute definitions use stable canonical IDs and localized presentation keys.
Persistent character state stores permanent values separately from active
modifier records. Each modifier records a stable source ID, affected attribute,
amount, stacking rule, start tick, and bounded duration or removal condition.

Character creation validates the allowed range and point budget before
publication. Runtime changes produce an event describing the old value, new
value, cause, and authoritative tick. Save migrations are required before
changing a released attribute ID, scale, or meaning.

## First playable scope

The first crew-enabled slice should include all seven attributes because they
form a small stable foundation. It only needs to exercise the attributes used
by navigation, salvage, repair, cooking, medicine, and the first ancient-lore
encounter. Attribute advancement can remain deferred; temporary modifiers and
contextual attribute-plus-skill selection must be visible from the start.
