# Equipment

## Status

Milestone 5 implements bounded personal loadouts with Main hand, Off hand,
Body, Utility, and Relic slots plus Ready, Depleted, and Damaged states. Five
first-encounter item definitions are data-authored. General inventories,
economy, crafting, and the broader catalog in this document remain planned.

## Design goals

- Make gear create a clear tactical, exploration, or social option.
- Support Arcane, dieselpunk, and atompunk equipment in the same setting.
- Keep inventory and item values readable; equipment is not an engineering or
  accounting simulation.
- Let any classless character use compatible equipment when its explicit Skill,
  Feat, Racial Perk, or physical requirement is met.
- Keep stable IDs and data-authored definitions for saves, balancing, and
  eventual modding.

The setting baseline is in [`History.md`](History.md), combat contexts are in
[`Battle.md`](Battle.md), and Skills and supernatural access are in
[`Skills.md`](Skills.md).

## Simple equipment rule

An equipment tooltip normally shows only:

1. its category and slot;
2. one primary effect, such as damage, Armor Value, range, healing support, or
   an exploration action;
3. an optional energy, ammunition, or Focus cost; and
4. an explicit requirement when one exists.

Items are **Ready**, **Depleted**, or **Damaged**. There is no routine tracking
of item weight, detailed durability, ammunition caliber, recoil, component
quality, or individual battery chemistry. Cargo capacity and a small personal
equipment capacity provide the only inventory limits needed for the first
playable game.

## Equipment slots

| Slot | Examples | Rule |
| --- | --- | --- |
| Main hand | melee weapon, pistol, tool, casting focus | One equipped active item |
| Off hand | shield, sidearm, scanner, compact tool | Optional complementary item |
| Body | suit, armor, pressure gear, robes | One protective outfit |
| Utility | medkit, breaching kit, climbing line, charm | A small bounded set of quick-use items |
| Relic | enchanted or advanced unique item | Optional unique item with an explicit effect |

Two-handed equipment occupies Main hand and Off hand. The interface rejects an
incompatible loadout before it changes state. A character can carry other
found items as cargo, but must equip them to use their active effect.

## Categories

| Category | Examples | Typical visible effect |
| --- | --- | --- |
| Melee weapons | blade, axe, shock baton, Arcane staff | Damage type and one combat tag |
| Ranged weapons | bow, pistol, rifle, Aether projector, laser carbine, plasma rifle | Damage, range, and ammunition or charge cost |
| Armor and suits | leather rig, pressure suit, plated coat, reactor suit, powered armor | Armor Value and one environmental protection tag |
| Shields and wards | riot shield, buckler, ward charm | A brief defense or protection effect |
| Tools | repair kit, cutter, survey scanner, lock kit | Enables or improves one declared action |
| Medical equipment | medkit, trauma injector, diagnostic wand | Stabilize, treat, or diagnose within Medicine rules |
| Arcane equipment | focus, wand, grimoire, enchanted tool | Spell support, Aether use, or a bounded magical action |
| Industrial equipment | breaching charge, sensor kit, power cell, radiation suit | Engineering, combat, or environmental support |
| Relics | ancient key, psychic lens, star compass | A unique authored action with clear limits |

## Technology and magic

Arcane is the overall magical practice; Aether is the non-material medium that
carries its energy. Arcane equipment uses Focus, Aether charge, a
known spell, or `access.magic` only when its definition says so. Industrial
equipment may use ammunition or stored energy. Atompunk equipment can be powerful and rare, but it remains a simple
item with a visible effect; radiation, reactor management, and exotic fuel
chemistry are not ordinary equipment statistics.

The latest Industrial equipment includes powered armor, laser weapons, and
plasma weapons. Powered armor is Body-slot armor with a visible Armor Value and
an optional energy cost; it does not model servos, heat, batteries, or joint
maintenance. Laser and plasma weapons use a displayed charge cost and their
declared damage type; they do not need ammunition caliber, cooling, or recoil
statistics.

An item never grants unrestricted spellcasting, psychic influence, or immunity
to all hazards. A casting focus helps an authorized caster; it does not replace
Spellcasting Training or an innate Racial Perk. A weapon's technology path does
not make it automatically stronger than the other path.

## Resonance and industrial craft

Elven resonance craft makes a small number of Arcane masterworks: tuned blades,
armor, Aether resonators, wards, and starwood fittings. These items emphasize
one distinctive, inspectable effect. Dwarven industrial craft makes repeatable
equipment with standardized parts: armor, tools, engines, laser weapons, and
plasma weapons. These items emphasize reliability and straightforward repair.

Both traditions use the same simple equipment rules. They are cultural craft
styles, not equipment restrictions by Race; any compatible character can use,
study, buy, repair, or improve either kind of item.

## Definition contract

Every equipment definition declares:

- a stable ID in the form `item.<category>.<name>` and localized keys;
- category, compatible slots, and a small set of tags;
- one primary effect and optional cost;
- requirements and incompatibilities;
- Ready, Depleted, and Damaged behavior; and
- a bounded stack or unique-item rule.

Definitions may use damage type, range, condition, resource, spell, or module
IDs defined elsewhere. They cannot embed player-visible text, arbitrary rules,
or unlimited generated effects.

## Initial equipment slice

The first personal-combat slice needs only a small mixed set:

| Equipment | Stable ID | Purpose |
| --- | --- | --- |
| Boarding blade | `item.weapon.boarding-blade` | Reliable melee weapon |
| Service pistol | `item.weapon.service-pistol` | Short-range industrial sidearm |
| Aether projector | `item.weapon.aether-projector` | Short-range Arcane energy weapon using charge |
| Laser carbine | `item.weapon.laser-carbine` | Atompunk ranged weapon with laser damage and charge cost |
| Plasma rifle | `item.weapon.plasma-rifle` | Atompunk ranged weapon with plasma damage and charge cost |
| Pressure suit | `item.armor.pressure-suit` | Basic armor and vacuum protection |
| Powered armor | `item.armor.powered-armor` | Atompunk Body armor with high Armor Value and optional energy cost |
| Repair kit | `item.tool.repair-kit` | Support Engineering repair actions |
| Survey scanner | `item.tool.survey-scanner` | Support Sensors and ruin exploration |
| Field medkit | `item.medical.field-medkit` | Stabilize or treat an injury |
| Casting focus | `item.arcane.casting-focus` | Support a known spell without granting access |

These IDs are planned design content, not a claim that an item registry or
inventory UI exists yet. More equipment is added only when it creates a new
meaningful choice rather than a small numerical upgrade.
