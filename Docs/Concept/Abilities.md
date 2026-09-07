# Character abilities

## Status

This document defines the ability model. The six definitions and their
typed immutable content registry are implemented. Character ability values,
generation, advancement, races, heritages, and complete character composition
remain planned. Those character rules are defined in [`Races.md`](Races.md);
learned capabilities and action resolution are defined in [`Skills.md`](Skills.md).
The planned data definitions, runtime registry, and character-state layout are
specified in
[`../Architecture/CharacterCapabilities.md`](../Architecture/CharacterCapabilities.md).

## Purpose

Abilities express broad capability. They answer how a character approaches a
task, while skills answer what the character has learned to do. Spelljammer has
no character classes or global character level.

The normal player-facing ability range is 1–10. Permanent values change
rarely. Injuries, needs, equipment, supernatural conditions, assistance, and
the environment apply temporary modifiers without rewriting those permanent
values.

## Ability roster

| Ability | Stable ID | Governs |
| --- | --- | --- |
| Strength | `ability.strength` | Melee damage and hit chance, carry capacity, and weapon/armor weight requirements |
| Agility | `ability.agility` | Turn meter speed, action points, and dodge rate |
| Perception | `ability.perception` | Overall awareness, detecting hidden passages and objects, spotting stealthed creatures and NPCs, ranged weapon hit chance, and psionics |
| Toughness | `ability.toughness` | Overall health, stamina, physical defense, and resistance to physical effects |
| Willpower | `ability.willpower` | Effectiveness with psionics and magic, resistance to mental effects, and psi point/mana regeneration |
| Intelligence | `ability.intelligence` | Diagnosis and technical analysis, knowledge and lore recall, and effectiveness with enchantment and magic |

## Contextual use

Abilities are not permanently bound to skills. An action declares the
ability that matches its method and circumstances:

- Strength plus Engineering can force a warped drive housing into position.
- Intelligence plus Engineering can diagnose why the housing failed.
- Willpower plus Engineering can complete the repair during an aether storm.
- Toughness plus Athletics can sustain hard work in dangerous gravity.
- Strength plus Melee can deliver a forceful strike.
- Agility plus Melee can place a precise strike.
- Agility plus Archery can make a difficult shot.
- Willpower plus Magic can guide an enchanted arrow.
- Intelligence plus Enchantment can inscribe a stable magical binding.
- Intelligence plus Psionics can interpret an unfamiliar psionics signal.
- Willpower plus Psionics can maintain a shield against psionics intrusion.
- Perception plus Psionics can sense a faint emotion through a mindlink.
- Perception plus Salvage can expose a rare find on a careful sweep.
- Intelligence plus Merchant can recognize manipulated market records.
- Perception plus Negotiation can read a bluff during contract terms.
- Intelligence plus Ancient Lore can explain a discovery to a suspicious faction.

The interface must identify the chosen ability and explain why it applies.
Alternative approaches should be available when fiction, equipment, and known
techniques support them.

## Race, heritage, and abilities

Race may change physiological rules, while heritage may make an ability
cheaper to use in a particular environment. Neither assigns Intelligence,
morality, or a hard ability ceiling. For example, a heritage may reduce the
Toughness cost of high-gravity work without receiving a universal Toughness
bonus.

This distinction keeps race and heritage relevant to ship design and survival
while allowing any character to become an expert in any field through training
and experience.

## Permanent and temporary change

Permanent abilities may change through:

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

Ability definitions use stable canonical IDs and localized presentation keys.
Persistent character state stores permanent values separately from active
modifier records. Each modifier records a stable source ID, affected ability,
amount, stacking rule, start tick, and bounded duration or removal condition.

Character creation validates the allowed range and point budget before
publication. Runtime changes produce an event describing the old value, new
value, cause, and authoritative tick. Save migrations are required before
changing a released ability ID, scale, or meaning.

## First playable scope

The first crew-enabled slice should include all six abilities because they
form a small stable foundation. It only needs to exercise the abilities used
by navigation, salvage, repair, cooking, medicine, and the first ancient-lore
encounter. Ability advancement can remain deferred; temporary modifiers and
contextual ability-plus-skill selection must be visible from the start.
