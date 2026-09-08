# Melee Weapons System

## 1. Overview

The Melee Weapons System defines all character-operated close-combat weapons.

## Item-system ownership

`MeleeWeaponDefinition` is a concrete item definition in the equipment
hierarchy. It is not a combat-rules record linked from a separate generic
equipment definition:

```text
ItemDefinition
└── EquipmentDefinition (abstract)
    └── WeaponDefinition (abstract)
        └── MeleeWeaponDefinition
```

`ItemDefinition` owns the identity and fields common to all items.
`EquipmentDefinition` owns the equip rule and `occupiedSlotIds`.
`WeaponDefinition` is limited to rules genuinely shared by melee and ranged
weapons. This type owns the melee-specific static combat data; an individual
equipped item's durability and energy are runtime state held by its item
instance. It never uses an optional `meleeWeaponId` link.

A melee weapon is defined along two independent dimensions:

1. **Weapon Family** — the physical form and combat role of the weapon.
2. **Technology** — the technological or magical system used by the weapon.

For example:

```text
Power Sword
Family: Blade
Technology: Powered

Chain Axe
Family: Axe
Technology: Chain

Shock Maul
Family: Blunt
Technology: Shock

Arcane Glaive
Family: Polearm
Technology: Arcane
```

Weapon Family and Technology must remain separate systems.

Technology does not represent a linear equipment tier. A high-quality Conventional weapon may be superior to a poorly manufactured Powered or Arcane weapon.

---

# 2. Weapon Families

The standard melee weapon families are:

```text
Blade
Dagger
Axe
Blunt
Polearm
Fist
```

## Blade

General-purpose cutting weapons.

Examples:

- Sword
- Longsword
- Cutlass
- Sabre
- Machete
- Greatsword

Typical characteristics:

- Balanced damage
- Moderate armor damage
- Moderate armor penetration
- Moderate stamina requirements
- Broad selection of combat actions

---

## Dagger

Small stabbing and cutting weapons optimized for close combat and attacks against vulnerable areas.

Examples:

- Knife
- Dagger
- Stiletto
- Dirk
- Punch Dagger

Typical characteristics:

- Low raw damage
- Low stamina cost
- Strong penetration-oriented actions
- Concealable
- Effective for finishing vulnerable targets

---

## Axe

Heavy cutting weapons designed to inflict severe structural damage.

Examples:

- Hand Axe
- Battle Axe
- Boarding Axe
- Greataxe
- Chain Axe

Typical characteristics:

- High damage
- High armor damage
- Strong against shields and equipment
- Higher stamina requirements

Boarding axes may additionally function as utility tools for destroying doors, barricades, ship structures, or environmental objects.

---

## Blunt

Impact weapons designed to crush armor and incapacitate targets.

Examples:

- Club
- Mace
- Warhammer
- Maul
- Sledgehammer
- Shock Maul
- Power Hammer

Typical characteristics:

- Strong performance against armored targets
- Stagger and stun potential
- High stamina requirements for heavier variants
- Often associated with control-oriented actions

---

## Polearm

Long weapons designed around reach and battlefield control.

Examples:

- Spear
- Pike
- Halberd
- Glaive
- Poleaxe

Typical characteristics:

- Extended attack range
- Formation fighting
- Zone control
- Thrust, brace, hook, sweep, or push actions

Spears are included within the Polearm family rather than existing as a separate weapon family.

---

## Fist

Weapons mounted directly on or used through the hands.

Examples:

- Knuckles
- Cestus
- Combat Gauntlet
- Shock Gauntlet
- Power Fist

Natural attacks may use related combat systems but do not necessarily need to be represented as equippable melee weapons.

---

# 3. Weapon Technology

Every melee weapon belongs to one of five technology categories:

```text
Conventional
Chain
Shock
Powered
Arcane
```

These technologies represent different engineering principles and combat roles rather than sequential equipment tiers.

---

# 4. Conventional Weapons

Conventional weapons rely primarily on their physical construction.

Examples:

```text
Sword
Cutlass
Boarding Axe
Warhammer
Spear
Combat Knife
```

Core identity:

**RELIABLE**

Conventional weapons generally provide:

- Low maintenance requirements
- No energy requirement
- Low operating cost
- High reliability
- Easy repair
- Broad availability

Their disadvantage is the absence of specialized powered effects.

Conventional weapons must remain viable throughout the game.

Advanced materials, superior craftsmanship, rare alloys, or masterwork construction can produce extremely powerful Conventional weapons without changing their Technology classification.

Example:

```text
Masterwork Adamantine Sword

Family: Blade
Technology: Conventional
```

---

# 5. Chain Weapons

Chain weapons use motor-driven cutting teeth or similar mechanical cutting systems.

Examples:

```text
Chain Knife
Chainsword
Chain Axe
Chain Glaive
```

Core identity:

**SHRED**

Chain weapons specialize in physically destroying the target.

Typical strengths:

- High armor damage
- High physical damage
- Bleeding
- Severe injuries
- Dismemberment potential

Typical disadvantages:

- Heavy
- High stamina requirements
- Loud
- High maintenance requirements
- May require energy or fuel

Chain weapons primarily defeat armor by **destroying it**.

Conceptually:

```text
Chain
    ↓
SHRED
    ↓
Armor Destruction
Bleeding
Injury
Dismemberment
```

A Chain weapon may remain usable while inactive if its physical construction permits it, but loses its powered cutting benefits.

---

# 6. Shock Weapons

Shock weapons use electrical discharge systems to incapacitate targets.

Examples:

```text
Shock Baton
Shock Mace
Shock Spear
Shock Gauntlet
```

Core identity:

**DISABLE**

Shock weapons emphasize battlefield control rather than maximum physical damage.

Potential effects include:

- Stun
- Daze
- Stamina damage
- Action disruption
- Electrical damage
- Machine disruption

Shock weapons may receive specialized effectiveness against:

```text
Robots
Constructs
Machines
Powered equipment
```

Conceptually:

```text
Shock
    ↓
DISABLE
    ↓
Stun
Daze
Stamina Damage
Machine Disruption
```

Shock weapons should generally have lower raw destructive power than dedicated Chain or Powered weapons.

---

# 7. Powered Weapons

Powered weapons surround or reinforce a physical weapon with an energy or disruption field.

Examples:

```text
Power Sword
Power Axe
Power Hammer
Power Fist
Power Glaive
```

The weapon remains physically recognizable as a sword, axe, hammer, or other melee weapon.

The energy field enhances its ability to defeat durable materials and armor.

Core identity:

**DISRUPT**

Typical strengths:

- Very high armor penetration
- Strong performance against heavy armor
- Effective against heavily protected targets
- High-end military combat performance

Typical disadvantages:

- Expensive
- Requires energy
- More difficult to maintain
- Less widely available

Powered weapons primarily defeat armor by **penetrating or bypassing it**, rather than simply destroying it.

The distinction between Chain and Powered weapons must remain mechanically meaningful:

```text
Chain
→ destroys Armor

Powered
→ penetrates Armor
```

Conceptually:

```text
Powered
    ↓
DISRUPT
    ↓
Armor Penetration
Heavy Armor Defeat
Material Disruption
```

---

# 8. Arcane Weapons

Arcane weapons integrate magic into engineered weapon systems.

They may use:

```text
Runes
Mana conduits
Arcane crystals
Enchantments
Magical circuits
Focus components
Bound magical effects
```

Examples:

```text
Arcane Sword
Runic Axe
Arcane Glaive
Enchanted Spear
Arcane Staff
```

Core identity:

**ENCHANT**

Arcane weapons emphasize special effects and supernatural interactions rather than simply possessing higher physical damage.

Possible Arcane effects include:

```text
Flaming
Frost
Lightning
Mana Drain
Ghost Touch
Anti-Magic
Soulbound
Vampiric
Arcane Disruption
```

Arcane weapons may also be capable of harming entities that conventional physical weapons cannot effectively interact with.

Examples include:

```text
Spirits
Ghosts
Ethereal creatures
Magical constructs
Arcane barriers
Supernatural entities
```

Arcane does not mean "highest tier."

An Arcane weapon can be crude, unstable, common, rare, ancient, or masterfully constructed depending on its individual item definition.

---

# 9. Technology Roles

The five technologies should maintain distinct gameplay identities.

| Technology | Core Identity | Primary Role |
|---|---|---|
| Conventional | Reliable | Dependable physical combat |
| Chain | Shred | Armor destruction and injury |
| Shock | Disable | Control and incapacitation |
| Powered | Disrupt | Armor penetration |
| Arcane | Enchant | Magical and supernatural effects |

Simplified:

```text
Conventional
    └── RELIABLE

Chain
    └── SHRED

Shock
    └── DISABLE

Powered
    └── DISRUPT

Arcane
    └── ENCHANT
```

These categories are not a linear progression.

Do not implement:

```text
Conventional
    ↓
Chain
    ↓
Shock
    ↓
Powered
    ↓
Arcane
```

Instead, they represent parallel technological approaches.

---

# 10. Melee Weapon Properties

The base melee weapon data structure is:

```text
MeleeWeapon
{
    id
    name
    description

    family
    technology
    hands

    damage_min
    damage_max

    armor_damage
    armor_penetration

    stamina_cost
    range

    weight
    durability
    value

    energy_capacity      // optional
    energy_per_attack    // optional

    traits[]
    actions[]
}
```

---

# 11. Identity Properties

## id

Unique internal identifier.

Example:

```text
weapon_chain_sword
```

---

## name

Player-facing weapon name.

Example:

```text
Chainsword
```

---

## description

Flavor and worldbuilding description of the weapon.

Example:

```text
A brutal motor-driven blade lined with rotating teeth,
designed to tear through armor, flesh, and bone.
```

Description must not be used to store mechanical information.

Avoid descriptions such as:

```text
Deals +50% armor damage and has a 20% chance to cause Bleeding.
```

Mechanical information must be generated from actual weapon properties, traits, and actions so that UI text cannot become inconsistent with gameplay data.

---

# 12. Classification Properties

## family

Defines the physical weapon family.

Allowed values:

```text
Blade
Dagger
Axe
Blunt
Polearm
Fist
```

---

## technology

Defines the weapon technology.

Allowed values:

```text
Conventional
Chain
Shock
Powered
Arcane
```

---

## hands

Defines equipment requirements.

Recommended values:

```text
OneHanded
TwoHanded
```

Two-handed weapons prevent normal use of an off-hand weapon or shield unless another rule explicitly overrides this restriction.

---

# 13. Damage Properties

## damage_min

Minimum base physical damage.

## damage_max

Maximum base physical damage.

Example:

```text
damage_min: 45
damage_max: 60
```

When an attack successfully hits, base weapon damage is rolled within this range before relevant modifiers are applied.

---

# 14. Armor Damage

`armor_damage` determines the weapon's effectiveness at damaging Armor.

Recommended representation:

```text
1.00 = 100%
1.50 = 150%
0.75 = 75%
```

Example:

```text
damage = 50
armor_damage = 1.50

Armor Damage = 75
```

Chain and Axe-type weapons will commonly have high Armor Damage values.

Armor Damage and Armor Penetration are separate mechanics.

---

# 15. Armor Penetration

`armor_penetration` determines how much weapon damage can penetrate Armor and affect Health.

Recommended representation:

```text
0.20 = 20%
0.60 = 60%
```

Powered weapons will commonly have high Armor Penetration.

Conceptually:

```text
High Armor Damage
→ destroys protection

High Armor Penetration
→ bypasses protection
```

This distinction is fundamental to melee weapon balance.

---

# 16. Stamina Cost

`stamina_cost` represents the baseline physical effort required to attack with the weapon.

Examples:

```text
Dagger        4
Sword         7
Axe          10
Chainsword   12
Power Hammer 16
```

Heavier and mechanically demanding weapons generally require more Stamina.

Specific weapon Actions may modify the final stamina cost.

Example:

```text
Base Weapon Stamina Cost: 8

Slash:
+0

Heavy Strike:
+5

Final Heavy Strike Cost:
13 Stamina
```

---

# 17. Range

`range` represents the weapon's normal tactical attack distance.

Examples:

```text
Dagger     1
Sword      1
Axe        1
Mace       1

Spear      2
Halberd    2
Glaive     2
```

Polearms generally use extended range as one of their primary tactical advantages.

---

# 18. Weight

`weight` represents the physical mass and equipment burden of the weapon.

Weight should not directly increase weapon damage.

It may instead interact with systems such as:

```text
Carry Capacity
Equipment Load
Stamina
Initiative
Inventory Management
```

This avoids unnecessary coupling between inventory simulation and damage calculation.

---

# 19. Durability

`durability` represents weapon condition.

Example:

```text
Durability:
100 / 100
```

Technology can influence maintenance requirements.

General tendency:

```text
Conventional → Very Reliable
Chain        → High Maintenance
Shock        → Moderate Maintenance
Powered      → Moderate / High Maintenance
Arcane       → Specialized Maintenance
```

Weapons should not degrade so rapidly that routine combat becomes constant repair micromanagement.

Durability exists primarily to support:

- Salvaging
- Maintenance
- Damaged loot
- Long expeditions
- Technology differences
- Economy

---

# 20. Value

`value` represents the weapon's base economic value.

It may be used by:

```text
Trading
Loot generation
Repair costs
Crafting
Faction economy
Item rarity calculations
```

Actual purchase and sale prices may be modified by other economic systems.

---

# 21. Energy Properties

Energy properties are optional.

Only weapons that require stored power need them.

## energy_capacity

Maximum stored energy.

Example:

```text
energy_capacity: 30
```

## energy_per_attack

Baseline energy consumed when performing a powered attack.

Example:

```text
energy_per_attack: 2
```

These properties are primarily relevant to:

```text
Chain
Shock
Powered
```

and some Arcane weapons where appropriate.

Conventional weapons normally do not have energy properties.

An individual weapon may remain physically usable without power if its design allows it.

Example:

```text
Power Sword — Powered

Damage: 50–60
Armor Penetration: 60%
```

When depleted:

```text
Power Sword — Unpowered

Damage: reduced
Armor Penetration: greatly reduced
```

The exact unpowered behavior is defined by the weapon or its traits.

---

# 22. Traits

Traits define special weapon behaviors that should not require additional universal numerical properties.

Examples:

```text
Bleeding
Shred
Stun
Knockback
Reach
Cleave
Sweep
Hook
Disarm
AntiMachine
AntiSpirit
Concealable
Unwieldy
Reliable
Loud
```

Technology-specific traits can establish weapon identity.

Example:

```text
Chainsword

Traits:
- Shred
- Bleeding
- Loud
```

Traits should be preferred over adding large numbers of specialized fields to every weapon.

---

# 23. Weapon Actions

`actions[]` defines the combat actions available while the weapon is equipped.

Example:

```text
Sword

Actions:
- Slash
- Thrust
- Heavy Strike
```

Different weapons within the same family may provide different Actions.

Example:

```text
Halberd

Actions:
- Thrust
- Sweep
- Hook
- Brace
```

Weapon Actions are responsible for defining the actual method of attack.

---

# 24. Action Points

**AP Cost is not a MeleeWeapon property.**

The same weapon may support attacks with different AP requirements.

Therefore:

```text
MeleeWeapon
    ❌ ap_cost
```

Instead:

```text
WeaponAction
    ✓ ap_cost
```

Example:

```text
Longsword

Slash:
AP Cost: 4

Thrust:
AP Cost: 4

Heavy Strike:
AP Cost: 6
```

This allows attack speed and action economy to emerge from individual combat actions instead of being permanently attached to the weapon.

---

# 25. Accuracy

Melee weapons do not have a universal weapon-level `accuracy` property.

Therefore:

```text
MeleeWeapon
    ❌ accuracy
```

Hit chance should be determined by the combat system using character skills, target defenses, conditions, and specific Action modifiers where appropriate.

For example:

```text
Hit Chance
=
Attacker Melee Skill
+ Action Modifier
+ Situational Modifiers
- Target Defense
```

A specific Action may still apply an accuracy modifier.

Example:

```text
Precise Thrust
Hit Modifier: +10

Wild Swing
Hit Modifier: -15
```

The modifier belongs to the Action, not the weapon.

---

# 26. Example: Chainsword

```text
MeleeWeapon
{
    id: "weapon_chain_sword"

    name: "Chainsword"

    description:
        "A brutal motor-driven blade lined with rotating teeth,
        designed to tear through armor, flesh, and bone."

    family: Blade
    technology: Chain
    hands: OneHanded

    damage_min: 45
    damage_max: 60

    armor_damage: 1.50
    armor_penetration: 0.25

    stamina_cost: 12
    range: 1

    weight: 6.5
    durability: 100
    value: 850

    energy_capacity: 30
    energy_per_attack: 1

    traits:
    [
        Shred,
        Bleeding,
        Loud
    ]

    actions:
    [
        Slash,
        RevvedStrike
    ]
}
```

---

# 27. Design Principles

The Melee Weapons System should follow these principles:

### Weapon shape and technology are independent.

A Blade can be Conventional, Chain, Powered, or Arcane without creating separate weapon skill categories.

### Technology is not equipment tier.

Conventional weapons remain viable.

### Armor Damage and Armor Penetration are different.

Chain weapons tend toward Armor Damage.

Powered weapons tend toward Armor Penetration.

### AP belongs to Actions.

Weapons themselves do not have a universal AP Cost.

### Accuracy does not belong to the base weapon.

Accuracy differences should normally originate from character ability, combat conditions, or individual Actions.

### Avoid unnecessary weapon stats.

Do not add universal properties such as:

```text
Attack Speed
DPS
Critical Chance
Critical Damage
Block Chance
```

unless later combat design demonstrates that the property is required by multiple weapon types and cannot be represented more cleanly through Actions, Traits, character attributes, or the combat system.

The base weapon should describe what the item is. Weapon Actions should describe how it is used. Traits should describe exceptional behavior. Combat systems should calculate outcomes from those inputs rather than duplicating rules in item descriptions.

# 28. Implementation Status

The first data-driven melee slice is implemented. Authored JSON uses the
repository's camel-case source convention while preserving the concepts in
this document: `damageMinimum`/`damageMaximum`, `armorDamagePercentage`,
`armorPenetrationPercentage`, `staminaCost`, `maximumDurability`, optional
energy behavior expressed by zero or positive capacities, `traits`, and
`actionIds`.

`MeleeWeaponDefinition : WeaponDefinition` owns family, technology,
handedness, governing Skill and Ability, damage, armor interaction, range,
weight, durability, value, energy behavior, unpowered fallback percentages,
Strength scaling, traits, and its allowed action IDs. Weight is stored as an
integer count of hundredths of a pound. It inherits item fields from
`ItemDefinition` and equip slots from `EquipmentDefinition`; there is no second
melee definition containing duplicate weight, value, traits, or action IDs.
`MeleeWeaponActionDefinition` separately owns AP cost, accuracy, stamina,
damage, armor, range, energy, durability, and effect modifiers.

`MeleeWeaponSystem` validates actor ownership, content fingerprint, that the
equipped item resolves to `MeleeWeaponDefinition`, action membership, target
legality and range, AP, Stamina, durability, energy, Skill, and Ability before
reserving an attack. Resolution uses explicit seed and sequence values, integer
percentage arithmetic, and returns the new character and turn state. The new
weapon state is committed inside the addressed `ItemInstance`. Rejection does
not mutate any input. AP and Stamina are paid on an
accepted attempt even when it misses; durability and energy are likewise
consumed by the attempt. Effects apply only on a hit.

The base pack currently demonstrates this contract with the conventional,
one-handed Boarding Blade and Slash, Thrust, and Heavy Strike actions. The
other families and technologies are supported by the schema but await authored
weapons and technology-specific effects.

Character and encounter state persist the same item-instance ownership model,
and campaign save schema 9 stores melee durability and energy. Applying the
returned Health and Armor damage to encounter targets and AI action selection
remain encounter-system work.
