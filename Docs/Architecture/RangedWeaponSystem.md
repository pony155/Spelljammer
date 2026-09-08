# Ranged Weapon System

## 1. Overview

The **Ranged Weapon System** defines all character-operated ranged weapons.

## Item-system ownership

`RangedWeaponDefinition` is a concrete item definition in the equipment
hierarchy. It is not a combat-rules record linked from a separate generic
equipment definition:

```text
ItemDefinition
└── EquipmentDefinition (abstract)
    └── WeaponDefinition (abstract)
        └── RangedWeaponDefinition
```

`ItemDefinition` owns the identity and fields common to all items.
`EquipmentDefinition` owns the equip rule and `occupiedSlotIds`.
`WeaponDefinition` is limited to rules genuinely shared by melee and ranged
weapons. This type owns the ranged-specific static combat data; an individual
equipped item's durability, ammunition, energy, and heat are runtime state
held by its item instance. It never uses an optional `rangedWeaponId` link.

A ranged weapon is defined along two independent dimensions:

1. **Weapon Family** — the physical form and tactical role of the weapon.
2. **Technology** — the technological or magical principle used by the weapon.

Example:

```text
Laser Rifle
Family: Rifle
Technology: Laser

Plasma Pistol
Family: Pistol
Technology: Plasma

Arcane Longbow
Family: Bow
Technology: Arcane

Rocket Launcher
Family: Launcher
Technology: Ballistic
```

Weapon Family and Technology must remain separate systems.

Technology does not represent a linear equipment tier.

A high-quality Ballistic weapon may outperform a poorly manufactured Laser or Plasma weapon depending on weapon quality, ammunition, condition, tactical situation, and character build.

---

# 2. Weapon Families

The standard ranged weapon families are:

```text
Bow
Crossbow
Pistol
Rifle
Shotgun
Heavy
Launcher
Special
```

---

## 2.1 Bow

Traditional projectile weapons powered by draw force.

Examples:

- Shortbow
- Longbow
- Composite Bow
- Arcane Bow
- Arcane Longbow

Typical characteristics:

- Quiet
- Reliable
- No firearm recoil
- Ammunition may be recoverable
- Compatible with Conventional and Arcane technology

---

## 2.2 Crossbow

Mechanically drawn projectile weapons.

Examples:

- Light Crossbow
- Heavy Crossbow
- Repeating Crossbow
- Arcane Crossbow
- Arcane Repeating Crossbow

Typical characteristics:

- Strong single-shot performance
- Mechanical or magical draw assistance
- Heavy variants require significant effort to reload
- Repeating variants trade power for rate of fire
- Compatible with Conventional and Arcane technology

---

## 2.3 Pistol

Compact ranged weapons designed primarily for short- and medium-range combat.

Examples:

- Revolver
- Autopistol
- Laser Pistol
- Plasma Pistol

Typical characteristics:

- Short to medium range
- Low equipment burden
- Mobile
- Common sidearm role
- Frequently one-handed

---

## 2.4 Rifle

Shoulder-fired ranged weapons optimized for medium- and long-range combat.

Examples:

- Service Rifle
- Assault Rifle
- Hunting Rifle
- Sniper Rifle
- Laser Rifle
- Plasma Rifle

Typical characteristics:

- Strong effective range
- Flexible firing actions
- Good sustained ranged combat capability
- Usually two-handed

---

## 2.5 Shotgun

Ballistic weapons specialized for close-range projectile spread or heavy single-projectile ammunition.

Examples:

- Double-Barrel Shotgun
- Pump Shotgun
- Combat Shotgun

Typical characteristics:

- Excellent close-range performance
- Scatter attacks
- Multiple ammunition types
- Can switch between pellet and slug ammunition
- Ballistic technology only

---

## 2.6 Heavy

Large ranged weapons intended for sustained fire, anti-armor fire, or heavy battlefield roles.

Examples:

- Heavy Machine Gun
- Autocannon
- Heavy Laser
- Plasma Cannon

Typical characteristics:

- High damage output
- High equipment weight
- High stamina requirements
- Often difficult to use while moving
- May support suppression or sustained fire

`Heavy` represents the physical weapon platform, not its technology.

For example:

```text
Heavy Machine Gun
Family: Heavy
Technology: Ballistic

Heavy Laser
Family: Heavy
Technology: Laser

Plasma Cannon
Family: Heavy
Technology: Plasma
```

---

## 2.7 Launcher

Weapons designed to launch explosive or specialized payloads.

The Launcher family includes:

```text
Grenade Launcher
Rocket Launcher
Nuclear Launcher
```

Launcher weapons normally use Ballistic technology.

Their primary tactical identity comes from their ammunition.

---

### Grenade Launcher

Core role:

**TACTICAL EXPLOSIVE**

Typical characteristics:

- Medium range
- Area attacks
- Ammunition versatility
- Can deliver utility payloads

Possible ammunition:

```text
Fragmentation Grenade
High-Explosive Grenade
Incendiary Grenade
Smoke Grenade
Shock Grenade
```

The Grenade Launcher is primarily defined by ammunition flexibility rather than maximum damage.

---

### Rocket Launcher

Core role:

**ANTI-ARMOR EXPLOSIVE**

Typical characteristics:

- High damage
- Strong armor performance
- Area damage
- Knockback
- Long range
- Minimum safe range

Possible ammunition:

```text
High-Explosive Rocket
Anti-Armor Rocket
Fragmentation Rocket
Incendiary Rocket
```

Grenade Launchers and Rocket Launchers should remain mechanically distinct.

```text
Grenade Launcher
→ Tactical AoE
→ Ammunition versatility
→ Utility payloads

Rocket Launcher
→ Direct fire
→ Anti-armor
→ Heavy explosive damage
```

---

### Nuclear Launcher

Core role:

**MASS DESTRUCTION**

The Nuclear Launcher fires extremely rare nuclear ammunition such as Mini-Nukes.

Typical effects include:

```text
Blast Damage
Thermal Damage
Radiation
Knockback
Fire
Environmental Destruction
Friendly Fire
```

Example traits:

```text
Nuclear
Explosive
AreaAttack
Radiation
Destructive
MinimumRange
```

Nuclear ammunition should be:

- Extremely rare
- Extremely expensive
- Dangerous to the user
- Dangerous to allies
- Capable of environmental destruction

The Nuclear Launcher should not simply function as a higher-damage Rocket Launcher.

Its defining characteristic is the extreme tactical and environmental consequence of firing it.

---

## 2.8 Special

The Special family contains ranged weapons that do not fit the standard projectile firing model.

Currently:

```text
Flamethrower
```

Special should remain a narrow category.

Weapons that can reasonably belong to Pistol, Rifle, Heavy, or Launcher should not be placed in Special.

---

### Flamethrower

Core role:

**AREA DENIAL**

```text
Family: Special
Technology: Ballistic
```

Although a flamethrower is not technically a ballistic weapon in the strict physical sense, Ballistic represents conventional industrial ranged weapon technology within the gameplay taxonomy.

The Flamethrower uses a cone or continuous-area attack rather than a normal projectile attack.

Example:

```text
        XXX
      XXXXX
    XXXXXXX
        @

@ = Shooter
```

Typical effects:

- Cone attack
- Area damage
- Burning
- Persistent fire
- Area denial
- Strong against lightly armored organic targets

Example traits:

```text
Cone
AreaAttack
Burning
```

Using a Flamethrower aboard a ship may create environmental hazards by igniting:

- Wooden structures
- Cargo
- Fuel
- Explosives
- Ship components
- Other flammable objects

---

# 3. Weapon Technologies

The standard ranged weapon technologies are:

```text
Conventional
Ballistic
Laser
Plasma
Arcane
```

These represent different technological approaches rather than equipment tiers.

---

## 3.1 Conventional

Conventional ranged weapons operate through mechanical force without firearms, energy cells, or magical enhancement.

Examples:

```text
Bow
Longbow
Crossbow
Heavy Crossbow
```

Core identity:

**RELIABLE**

Typical advantages:

- Reliable
- Low maintenance
- Quiet
- No energy requirement
- Ammunition can often be manufactured locally
- Ammunition may sometimes be recovered

Typical disadvantages:

- Lower technological performance ceiling
- Limited rate of fire
- Heavy crossbows may require significant reload effort

Conventional weapons are restricted primarily to:

```text
Bow
Crossbow
```

---

## 3.2 Ballistic

Ballistic weapons use physical ammunition, chemical propellants, fuel, or related industrial weapon systems.

Examples:

```text
Revolver
Autopistol
Shotgun
Service Rifle
Assault Rifle
Sniper Rifle
Machine Gun
Autocannon
Grenade Launcher
Rocket Launcher
Nuclear Launcher
Flamethrower
```

Core identity:

**PROJECTILE / IMPACT**

Ballistic weapons provide the greatest ammunition variety.

Possible ammunition characteristics include:

```text
Standard
Armor Piercing
Hollow Point
Incendiary
Explosive
Fragmentation
Subsonic
```

A major strength of Ballistic weapons is the ability to change battlefield performance through ammunition selection.

---

## 3.3 Laser

Laser weapons use directed energy.

Examples:

```text
Laser Pistol
Laser Carbine
Laser Rifle
Heavy Laser
```

Core identity:

**PRECISION**

Typical advantages:

- Long effective range
- Low recoil
- High precision
- Good penetration
- No conventional projectile drop

Typical disadvantages:

- Energy dependency
- Heat generation
- Specialized maintenance
- Potential weakness against specialized protective materials

Laser precision should not be represented through a universal weapon-level `accuracy` property.

Instead, Laser weapons may achieve their precision through:

- Reduced range penalties
- Specialized Actions
- Traits
- Targeting systems
- Low-recoil firing rules

---

## 3.4 Plasma

Plasma weapons fire high-energy plasma or equivalent high-energy projectiles.

Examples:

```text
Plasma Pistol
Plasma Rifle
Plasma Cannon
```

Core identity:

**DESTRUCTION**

Typical advantages:

- Very high damage
- High armor damage
- High armor penetration
- Strong against heavily protected targets

Typical disadvantages:

- Heat
- Overheating
- Expensive ammunition
- Specialized maintenance
- Potential instability

Plasma weapons should emphasize high-risk, high-output combat.

Example:

```text
Plasma Rifle

heat_capacity: 100
heat_per_shot: 20
```

Repeated firing:

```text
20
40
60
80
100 → Overheated
```

---

## 3.5 Arcane

Arcane ranged weapons integrate magical engineering into traditional projectile weapons.

Arcane technology is restricted to:

```text
Bow
Crossbow
```

Examples:

```text
Arcane Bow
Arcane Longbow
Arcane Crossbow
Arcane Repeating Crossbow
```

Arcane ranged weapons may use:

- Runes
- Mana conduits
- Magical crystals
- Enchanted strings
- Magical draw assistance
- Projectile acceleration
- Elemental enchantments

Arcane technology does not create Arcane Pistols, Arcane Rifles, or Arcane Launchers within this system.

Arcane ranged weapons preserve the identity of Bow and Crossbow weapons while enhancing them through magical engineering.

Possible Arcane ammunition includes:

```text
Flame Arrow
Frost Arrow
Shock Arrow
Ghost Arrow
Dispel Arrow
Explosive Rune Arrow
```

---

# 4. Family and Technology Compatibility

The primary legal combinations are:

| Family | Conventional | Ballistic | Laser | Plasma | Arcane |
|---|---:|---:|---:|---:|---:|
| Bow | Yes | No | No | No | Yes |
| Crossbow | Yes | No | No | No | Yes |
| Pistol | No | Yes | Yes | Yes | No |
| Rifle | No | Yes | Yes | Yes | No |
| Shotgun | No | Yes | No | No | No |
| Heavy | No | Yes | Yes | Yes | No |
| Launcher | No | Yes | No | No | No |
| Special | No | Yes | No | No | No |

These restrictions define the default weapon taxonomy.

Individual unique items should not violate these rules unless explicitly designed as exceptional artifacts.

---

# 5. Ranged Weapon Properties

The base ranged weapon definition is:

```text
RangedWeapon
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

    optimal_range
    max_range

    weight
    durability
    value

    ammunition_type        // optional
    magazine_capacity      // optional

    energy_capacity        // optional
    energy_per_shot        // optional

    heat_capacity          // optional
    heat_per_shot          // optional

    traits[]
    actions[]
}
```

---

# 6. Identity Properties

## id

Unique internal identifier.

Example:

```text
weapon_service_rifle
```

---

## name

Player-facing weapon name.

Example:

```text
Service Rifle
```

---

## description

Flavor and worldbuilding description.

Example:

```text
A rugged magazine-fed rifle widely used by colonial militias,
mercenaries, and shipboard security forces.
```

Description must not contain authoritative gameplay values.

Avoid:

```text
Deals 50 damage and gains +20% armor penetration.
```

Mechanical information must be generated from actual weapon, ammunition, trait, and action data.

---

# 7. Classification Properties

## family

Allowed values:

```text
Bow
Crossbow
Pistol
Rifle
Shotgun
Heavy
Launcher
Special
```

---

## technology

Allowed values:

```text
Conventional
Ballistic
Laser
Plasma
Arcane
```

---

## hands

Recommended values:

```text
OneHanded
TwoHanded
```

Most Rifles, Heavy weapons, Launchers, Bows, and Crossbows will normally be TwoHanded.

---

# 8. Damage

Ranged weapons use a base damage range:

```text
damage_min
damage_max
```

Example:

```text
damage_min: 45
damage_max: 60
```

A successful attack rolls base weapon damage within this range before ammunition and other combat modifiers are applied.

---

# 9. Armor Damage

`armor_damage` determines how effectively the weapon damages Armor itself.

Recommended representation:

```text
0.75 = 75%
1.00 = 100%
1.50 = 150%
```

Example:

```text
Base Damage: 50
Armor Damage: 1.50

50 × 1.50
= 75 Armor Damage
```

General technological tendencies may include:

```text
Ballistic → Medium
Laser     → Low / Medium
Plasma    → High
Launcher  → Primarily ammunition dependent
```

These are tendencies, not mandatory rules.

---

# 10. Armor Penetration

`armor_penetration` determines how much weapon damage can penetrate Armor and affect Health.

Recommended representation:

```text
0.20 = 20%
0.60 = 60%
```

Example:

```text
armor_penetration: 0.30
```

means the weapon has a base 30% Armor Penetration before ammunition and other modifiers.

Ammunition may modify this value.

---

# 11. Stamina Cost

`stamina_cost` represents the baseline physical effort required to operate and fire the weapon.

It may represent:

- Drawing a bow
- Controlling recoil
- Supporting a heavy weapon
- Maintaining firing posture
- Operating heavy mechanical systems

Example tendencies:

```text
Pistol             3
Rifle              5
Heavy Crossbow     8
Heavy Machine Gun 10
Rocket Launcher   12
```

Specific Actions may modify the final Stamina cost.

---

# 12. Range

Ranged weapons use two range properties:

```text
optimal_range
max_range
```

## optimal_range

The distance within which the weapon operates normally.

## max_range

The maximum tactical distance at which the weapon may attack.

Example:

```text
Shotgun

optimal_range: 3
max_range: 6
```

```text
Service Rifle

optimal_range: 8
max_range: 14
```

```text
Sniper Rifle

optimal_range: 12
max_range: 20
```

Beyond `optimal_range`, the combat system may apply:

- Range penalties
- Damage falloff
- Scatter
- Action-specific penalties

These effects should not require a universal weapon-level Accuracy property.

---

# 13. Weight

`weight` represents the physical mass and equipment burden of the weapon.

Weight may interact with:

```text
Carry Capacity
Equipment Load
Stamina
Initiative
Inventory Management
```

Weight should not directly increase weapon damage.

---

# 14. Durability

`durability` represents weapon condition.

Example:

```text
Durability:
100 / 100
```

Durability supports:

- Salvaging
- Damaged loot
- Repair
- Maintenance
- Long expeditions
- Technology differentiation
- Economy

Weapons should not degrade so rapidly that routine combat becomes constant repair micromanagement.

---

# 15. Value

`value` represents the base economic value of the weapon.

It may be used by:

```text
Trading
Loot Generation
Repair Costs
Crafting
Faction Economy
Item Rarity
```

Actual purchase and sale prices may be modified by the economy system.

---

# 16. Ammunition Type

`ammunition_type` defines the ammunition compatibility of the weapon.

Examples:

```text
Arrow
CrossbowBolt

PistolRound
RifleRound
ShotgunShell

Grenade
Rocket
MiniNuke

LaserCell
PlasmaCell
```

Example:

```text
Service Rifle

ammunition_type: RifleRound
```

Only compatible ammunition may be loaded.

---

# 17. Magazine Capacity

`magazine_capacity` defines how much ammunition the weapon can hold before requiring a reload.

Examples:

```text
Revolver:         6
Pistol:          12
Rifle:           20
SMG:             30
Shotgun:          8
Rocket Launcher:  1
Nuclear Launcher: 1
```

A Bow may not use a magazine:

```text
magazine_capacity: null
```

A standard Crossbow may use:

```text
magazine_capacity: 1
```

A Repeating Crossbow may use:

```text
magazine_capacity: 5
```

`magazine_capacity` is part of the weapon definition.

`current_ammo` is not.

---

# 18. Energy Properties

Energy properties are optional.

They are only used when a weapon has an internal energy storage system.

```text
energy_capacity
energy_per_shot
```

Example:

```text
energy_capacity: 100
energy_per_shot: 5
```

However, if Laser and Plasma weapons use replaceable Energy Cells as ammunition, the ammunition system should normally be preferred.

Avoid unnecessarily representing the same resource twice.

Two possible models exist:

```text
Model A

Laser Cell = Ammunition
magazine_capacity = 20
```

or:

```text
Model B

Internal Battery
energy_capacity = 100
energy_per_shot = 5
```

Use one model for a weapon unless there is a specific design reason to combine them.

---

# 19. Heat Properties

Weapons that generate significant heat may define:

```text
heat_capacity
heat_per_shot
```

Example:

```text
Plasma Rifle

heat_capacity: 100
heat_per_shot: 20
```

`heat_capacity` and `heat_per_shot` belong to the weapon definition.

The current heat value does not.

```text
WeaponDefinition
├── heat_capacity
└── heat_per_shot

WeaponInstance
└── current_heat
```

When current heat reaches the weapon's heat capacity, the weapon may enter an `Overheated` state.

Example:

```text
20
40
60
80
100 → Overheated
```

The consequences of overheating should be implemented through the combat/status system rather than hardcoded into every weapon.

Possible consequences include:

- Weapon temporarily cannot fire
- Forced cooldown
- Increased malfunction risk
- User damage for unstable weapons
- Reduced weapon performance

Plasma weapons are the primary users of the Heat system, although individual Laser or other weapons may also use it.

---

# 20. Traits

`traits[]` defines special weapon behavior that does not justify adding another universal property to every ranged weapon.

Examples:

```text
Burst
Automatic
Scatter
Silent
Scoped
Heavy
Explosive
AreaAttack
Cone
Burning
Overheat
AntiArmor
MinimumRange
IndirectFire
Suppressive
Radiation
Destructive
Repeating
```

Examples:

```text
Combat Shotgun

Traits:
[
    Scatter
]
```

```text
Flamethrower

Traits:
[
    Cone,
    AreaAttack,
    Burning
]
```

```text
Rocket Launcher

Traits:
[
    Explosive,
    AreaAttack,
    AntiArmor,
    MinimumRange
]
```

```text
Nuclear Launcher

Traits:
[
    Nuclear,
    Explosive,
    AreaAttack,
    Radiation,
    Destructive,
    MinimumRange
]
```

Traits should be preferred over continuously adding specialized fields to `RangedWeapon`.

---

# 21. Weapon Actions

`actions[]` defines the combat actions available while the weapon is equipped.

Example:

```text
Service Rifle

Actions:
[
    SnapShot,
    StandardShot,
    AimedShot,
    BurstFire,
    ReloadMagazine
]
```

Different weapons within the same family may provide different Actions.

Example:

```text
Sniper Rifle

Actions:
[
    StandardShot,
    AimedShot,
    PrecisionShot,
    ReloadMagazine
]
```

Example:

```text
Flamethrower

Actions:
[
    FlameBurst,
    SustainedFlame,
    ReloadFuel
]
```

Actions determine **how the weapon is used**, while the weapon definition describes the weapon itself.

---

# 22. Action Points

`ap_cost` is not a `RangedWeapon` property.

Do not implement:

```text
RangedWeapon
└── ap_cost
```

Instead:

```text
WeaponAction
└── ap_cost
```

The same weapon may support attacks with different AP requirements.

Example:

```text
Service Rifle

Snap Shot
AP Cost: 3

Standard Shot
AP Cost: 4

Aimed Shot
AP Cost: 6

Burst Fire
AP Cost: 6
```

This allows the action economy to emerge from the attack being performed rather than assigning a single artificial attack speed to the weapon.

---

# 23. Accuracy

`accuracy` is not a universal `RangedWeapon` property.

Do not implement:

```text
RangedWeapon
└── accuracy
```

Hit chance should be calculated by the combat system using factors such as:

```text
Ranged Skill
Action Modifier
Distance
Cover
Elevation
Shooter Status
Target Status
Environmental Conditions
```

Conceptually:

```text
Hit Chance
=
Ranged Skill
+ Action Hit Modifier
+ Situational Modifiers
- Target Defense
- Range Penalties
- Cover Penalties
```

Individual Actions may provide hit modifiers.

Example:

```text
Aimed Shot

AP Cost: 6
Hit Modifier: +15
```

```text
Snap Shot

AP Cost: 3
Hit Modifier: -10
```

The modifier belongs to the Action, not the base weapon.

---

# 24. Reload

Reload speed is not a base weapon property.

Do not implement:

```text
reload_speed
reload_time
```

Reloading is represented through Actions.

Examples:

```text
ReloadMagazine
ReloadSingleRound
CockCrossbow
LoadBolt
ReplaceEnergyCell
ReplacePlasmaCell
LoadGrenade
LoadRocket
LoadMiniNuke
```

Each Reload Action may define its own:

```text
ap_cost
stamina_cost
ammo_consumption
requirements
```

This allows different weapon mechanisms to use the same Action system.

For example:

```text
Revolver
→ ReloadSingleRound
```

```text
Service Rifle
→ ReloadMagazine
```

```text
Heavy Crossbow
→ CockCrossbow
→ LoadBolt
```

```text
Laser Rifle
→ ReplaceEnergyCell
```

```text
Rocket Launcher
→ LoadRocket
```

```text
Nuclear Launcher
→ LoadMiniNuke
```

---

# 25. Weapon Instance State

The base `RangedWeapon` definition contains static weapon data.

Runtime state belongs to a weapon instance.

Example:

```text
RangedWeaponDefinition
{
    magazine_capacity
    durability
    heat_capacity
}
```

Runtime:

```text
RangedWeaponInstance
{
    definition_id

    current_ammo
    current_durability
    current_heat

    loaded_ammo_id
}
```

Therefore, the following properties must not be stored as static weapon-definition values:

```text
current_ammo
current_heat
current_durability
```

This separation allows multiple instances of the same weapon definition to have different conditions and ammunition states.

---

# 26. Ammo System

Ammunition is defined independently from weapons.

The weapon provides the base combat characteristics.

Ammunition modifies those characteristics and may provide specialized payload effects.

Conceptually:

```text
Weapon
    ↓
Base Damage
Base Armor Damage
Base Armor Penetration
Base Range

        +

Ammo
    ↓
Damage Modifier
Armor Modifier
Penetration Modifier
Range Modifier
Special Payload

        +

Action
    ↓
AP Cost
Attack Pattern
Hit Modifier
Special Attack Behavior
```

---

# 27. Ammunition Definition

The standard ammunition definition is:

```text
AmmunitionDefinition : ItemDefinition
{
    id
    name
    description

    ammunitionType
    technology

    damagePercentage
    armorDamagePercentage
    armorPenetrationModifier

    rangeModifier

    maximumStackSize
    weightHundredthsOfPound
    value

    tags[]
}
```

---

# 28. Ammo Identity

## id

Unique internal ammunition identifier.

Example:

```text
ammo_rifle_ap
```

---

## name

Player-facing ammunition name.

Example:

```text
Armor-Piercing Rifle Round
```

---

## description

Flavor and worldbuilding description.

Example:

```text
A hardened rifle projectile designed to retain its shape
while penetrating heavy personal armor.
```

Description should not contain authoritative gameplay values.

---

# 29. Ammo Type

`ammo_type` determines weapon compatibility.

Standard ammunition types may include:

```text
Arrow
CrossbowBolt

PistolRound
RifleRound
ShotgunShell

Grenade
Rocket
MiniNuke

LaserCell
PlasmaCell
```

Example weapon:

```text
Service Rifle

ammunition_type: RifleRound
```

Compatible ammunition:

```text
Standard Rifle Round
ammo_type: RifleRound
```

```text
Armor-Piercing Rifle Round
ammo_type: RifleRound
```

Incompatible ammunition cannot be loaded.

---

# 30. Ammo Technology

Ammunition uses the same general technology taxonomy:

```text
Conventional
Ballistic
Laser
Plasma
Arcane
```

Examples:

```text
Standard Arrow
Technology: Conventional
```

```text
Arcane Arrow
Technology: Arcane
```

```text
Rifle Round
Technology: Ballistic
```

```text
Laser Cell
Technology: Laser
```

```text
Plasma Cell
Technology: Plasma
```

Ammo technology can be used by:

- Loot generation
- Crafting
- Merchant inventory
- Technology restrictions
- Faction equipment generation
- Repair and maintenance systems

It does not automatically modify damage.

---

# 31. Damage Modifier

`damage_modifier` modifies the weapon's base damage.

It is multiplicative.

Recommended representation:

```text
1.00 = 100%
1.20 = 120%
0.80 = 80%
```

Example:

```text
Weapon Damage:
50

Ammo Damage Modifier:
1.20

Final Base Damage:
50 × 1.20
= 60
```

Ammunition can therefore trade raw damage against other characteristics.

---

# 32. Armor Damage Modifier

`armor_damage_modifier` modifies the weapon's effectiveness against Armor.

It is multiplicative.

Example:

```text
Weapon Damage:
50

Weapon Armor Damage:
1.20

Ammo Armor Damage Modifier:
1.25

Final Armor Damage:
50 × 1.20 × 1.25
= 75
```

Recommended representation:

```text
1.00 = no modification
1.25 = +25%
0.75 = -25%
```

---

# 33. Armor Penetration Modifier

`armor_penetration_modifier` modifies base Armor Penetration.

Unlike Damage and Armor Damage modifiers, Armor Penetration is **additive**.

Example:

```text
Weapon Armor Penetration:
0.30

Ammo Armor Penetration Modifier:
+0.20

Final Armor Penetration:
0.50
```

Therefore:

```text
damage_modifier
→ multiplicative

armor_damage_modifier
→ multiplicative

armor_penetration_modifier
→ additive
```

This distinction must remain consistent throughout the combat system.

---

# 34. Range Modifier

`range_modifier` modifies the weapon's effective range.

Most ammunition should use:

```text
range_modifier: 0
```

Specialized ammunition may modify range.

Example:

```text
High-Velocity Rifle Round

range_modifier: +2
```

Example:

```text
Subsonic Rifle Round

range_modifier: -2
```

The exact interaction with `optimal_range` and `max_range` is determined by the combat system.

---

# 35. Stack Size

`maximumStackSize` determines how many units of ammunition may occupy a standard inventory stack.

Example tendencies:

```text
Arrow           20
Pistol Round    50
Rifle Round     40
Shotgun Shell   20

Grenade          6
Rocket           2
MiniNuke          1

Laser Cell       10
Plasma Cell       5
```

Stack size is primarily an inventory and logistics property.

---

# 36. Ammo Weight

`weight` represents the weight of a single ammunition unit.

Do not store total stack weight.

Example:

```text
Rifle Round

weight: 0.025
```

```text
Rocket

weight: 4.0
```

```text
MiniNuke

weight: 12.0
```

Total inventory weight is calculated dynamically:

```text
Total Ammo Weight
=
Quantity × Unit Weight
```

This makes ammunition logistics meaningful during long expeditions.

---

# 37. Ammo Value

`value` represents the base economic value of one ammunition unit.

Example:

```text
Standard Rifle Round
value: 2
```

```text
Plasma Cell
value: 25
```

```text
Rocket
value: 100
```

```text
MiniNuke
value: 1500
```

Exact values are subject to economic balancing.

Rare ammunition should create meaningful logistical and economic decisions.

---

# 38. Ammo Traits

`traits[]` defines specialized ammunition behavior.

Examples:

```text
ArmorPiercing
HollowPoint
Incendiary
Explosive
Tracer
Subsonic
Shock
Radiation
Burning
Fragmentation
Smoke
Arcane
AntiSpirit
Overcharged
Unstable
```

Traits should be used instead of adding specialized fields such as:

```text
fire_damage
radiation_damage
stun_chance
explosion_radius
```

to every `AmmunitionDefinition`.

When a Trait requires numerical configuration, it should reference or contain an appropriate Effect definition.

---

# 39. Standard Ballistic Ammunition

Example:

```text
Standard Rifle Round

ammo_type: RifleRound
technology: Ballistic

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

traits: []
```

This represents the baseline ammunition against which specialized ammunition can be compared.

---

# 40. Armor-Piercing Ammunition

Armor-Piercing ammunition trades some general damage efficiency for superior performance against armor.

Example:

```text
Armor-Piercing Rifle Round

ammo_type: RifleRound
technology: Ballistic

damage_modifier: 0.90
armor_damage_modifier: 1.10
armor_penetration_modifier: +0.20

range_modifier: 0

traits:
[
    ArmorPiercing
]
```

Core role:

**ANTI-ARMOR**

---

# 41. Hollow-Point Ammunition

Hollow-Point ammunition is optimized against unarmored or lightly armored organic targets.

Example:

```text
Hollow-Point Rifle Round

ammo_type: RifleRound
technology: Ballistic

damage_modifier: 1.25
armor_damage_modifier: 0.70
armor_penetration_modifier: -0.10

range_modifier: 0

traits:
[
    HollowPoint
]
```

Core role:

**ANTI-PERSONNEL**

This creates a direct tactical contrast:

```text
Armor Piercing
→ Lower flesh damage
→ Better against armor

Hollow Point
→ Higher flesh damage
→ Worse against armor
```

---

# 42. Shotgun Ammunition

Shotguns can significantly change tactical behavior through ammunition selection.

## Buckshot

```text
Buckshot

ammo_type: ShotgunShell
technology: Ballistic

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

traits:
[
    Pellet,
    Scatter
]
```

Core role:

- Close range
- Multiple projectiles
- Scatter
- Anti-personnel

---

## Slug

```text
Shotgun Slug

ammo_type: ShotgunShell
technology: Ballistic

damage_modifier: 1.10
armor_damage_modifier: 1.10
armor_penetration_modifier: +0.15

traits:
[
    Slug
]
```

A Slug removes or reduces the normal Scatter behavior and improves:

- Range
- Single-target damage
- Armor penetration

This allows one Shotgun to perform multiple tactical roles through ammunition selection.

---

# 43. Grenade Launcher Ammunition

The Grenade Launcher is heavily defined by ammunition flexibility.

Possible ammunition includes:

```text
Fragmentation Grenade
High-Explosive Grenade
Incendiary Grenade
Smoke Grenade
Shock Grenade
```

---

## Fragmentation Grenade

```text
40mm Fragmentation Grenade

ammo_type: Grenade
technology: Ballistic

traits:
[
    Explosive,
    Fragmentation,
    AreaAttack
]
```

Primary role:

**ANTI-PERSONNEL AoE**

---

## High-Explosive Grenade

```text
40mm High-Explosive Grenade

ammo_type: Grenade
technology: Ballistic

traits:
[
    Explosive,
    AreaAttack
]
```

Primary role:

**GENERAL EXPLOSIVE**

---

## Incendiary Grenade

```text
40mm Incendiary Grenade

ammo_type: Grenade
technology: Ballistic

traits:
[
    Explosive,
    Burning,
    AreaAttack
]
```

Primary role:

**AREA DENIAL**

---

## Smoke Grenade

```text
40mm Smoke Grenade

ammo_type: Grenade
technology: Ballistic

damage_modifier: 0.00

traits:
[
    Smoke
]
```

Primary role:

**UTILITY / COVER**

Smoke ammunition demonstrates that ammunition does not necessarily need to deal damage.

---

## Shock Grenade

```text
40mm Shock Grenade

ammo_type: Grenade
technology: Ballistic

traits:
[
    Shock,
    AreaAttack
]
```

Primary role:

**DISABLE**

Potential effects include:

- Stun
- Daze
- Stamina damage
- Machine disruption

---

# 44. Rocket Ammunition

Rocket Launchers can use multiple warhead types.

Examples:

```text
High-Explosive Rocket
Anti-Armor Rocket
Fragmentation Rocket
Incendiary Rocket
```

---

## High-Explosive Rocket

```text
High-Explosive Rocket

ammo_type: Rocket
technology: Ballistic

traits:
[
    Explosive,
    AreaAttack
]
```

Core role:

**GENERAL DESTRUCTION**

---

## Anti-Armor Rocket

```text
Anti-Armor Rocket

ammo_type: Rocket
technology: Ballistic

armor_penetration_modifier: +0.30

traits:
[
    Explosive,
    AntiArmor
]
```

Core role:

**ANTI-ARMOR**

It may sacrifice explosion radius or anti-personnel effectiveness for superior penetration.

---

## Fragmentation Rocket

```text
Fragmentation Rocket

ammo_type: Rocket
technology: Ballistic

traits:
[
    Explosive,
    Fragmentation,
    AreaAttack
]
```

Core role:

**ANTI-PERSONNEL**

---

## Incendiary Rocket

```text
Incendiary Rocket

ammo_type: Rocket
technology: Ballistic

traits:
[
    Explosive,
    Burning,
    AreaAttack
]
```

Core role:

**AREA DENIAL / BURNING**

---

# 45. Nuclear Ammunition

Nuclear ammunition is extremely rare strategic-level ammunition.

Example:

```text
Mini-Nuke

ammo_type: MiniNuke
technology: Ballistic

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

maximumStackSize: 1
weight: 12.0
value: 1500

traits:
[
    Nuclear,
    Explosive,
    Radiation,
    Destructive,
    AreaAttack
]
```

The base AmmunitionDefinition should not contain every nuclear explosion parameter.

The `Nuclear` payload/effect system should define:

```text
Blast Damage
Blast Radius
Thermal Damage
Radiation
Fire
Knockback
Terrain Damage
Environmental Effects
```

This prevents the base ammunition schema from becoming specialized around one weapon.

---

# 46. Laser Cells

Laser weapons may use replaceable energy cells as ammunition.

Example:

```text
Standard Laser Cell

ammo_type: LaserCell
technology: Laser

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

traits: []
```

A standard Laser Cell should normally provide energy rather than substantially altering the weapon's damage profile.

Most Laser weapon performance should come from the weapon itself.

---

## 46.1 Overcharged Laser Cell

Specialized Laser Cells may provide more energy per shot.

Example:

```text
Overcharged Laser Cell

ammo_type: LaserCell
technology: Laser

damage_modifier: 1.20
armor_damage_modifier: 1.10
armor_penetration_modifier: +0.05

range_modifier: 0

traits:
[
    Overcharged,
    HeatIncrease
]
```

Core tradeoff:

```text
More Energy
    ↓
More Damage
    ↓
More Heat
```

Overcharged ammunition should generally increase weapon stress, heat generation, or energy inefficiency rather than functioning as a simple damage upgrade.

---

# 47. Plasma Cells

Plasma weapons use specialized Plasma Cells or equivalent containment units.

Example:

```text
Standard Plasma Cell

ammo_type: PlasmaCell
technology: Plasma

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

traits: []
```

Plasma weapon damage should primarily come from the Plasma weapon itself.

The ammunition supplies the required energetic material or containment charge.

---

## 47.1 Overcharged Plasma Cell

Example:

```text
Overcharged Plasma Cell

ammo_type: PlasmaCell
technology: Plasma

damage_modifier: 1.25
armor_damage_modifier: 1.15
armor_penetration_modifier: +0.10

range_modifier: 0

traits:
[
    Overcharged,
    HeatIncrease,
    Unstable
]
```

Core tradeoff:

```text
Damage ↑
Armor Penetration ↑
Heat ↑
Instability ↑
```

An `Unstable` trait may interact with:

- Overheating
- Malfunctions
- Weapon damage
- User injury
- Catastrophic failure

These consequences should be handled by Trait and Effect systems rather than hardcoded into `AmmunitionDefinition`.

---

# 48. Conventional Arrows

Standard Bow ammunition uses the `Arrow` ammunition type.

Example:

```text
Standard Arrow

ammo_type: Arrow
technology: Conventional

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

maximumStackSize: 20
weight: 0.05
value: 1

traits: []
```

Different arrowheads may provide specialized effects.

Examples:

```text
Bodkin Arrow
Broadhead Arrow
Barbed Arrow
Fire Arrow
```

---

## 48.1 Bodkin Arrow

A narrow hardened arrowhead designed for armor penetration.

```text
Bodkin Arrow

ammo_type: Arrow
technology: Conventional

damage_modifier: 0.90
armor_damage_modifier: 1.00
armor_penetration_modifier: +0.15

range_modifier: 0

traits:
[
    ArmorPiercing
]
```

Core role:

**ANTI-ARMOR**

---

## 48.2 Broadhead Arrow

A wide cutting arrowhead optimized against exposed flesh.

```text
Broadhead Arrow

ammo_type: Arrow
technology: Conventional

damage_modifier: 1.20
armor_damage_modifier: 0.80
armor_penetration_modifier: -0.05

range_modifier: 0

traits:
[
    Bleeding
]
```

Core role:

**ANTI-PERSONNEL**

---

# 49. Conventional Crossbow Bolts

Crossbows use the `CrossbowBolt` ammunition type.

Example:

```text
Standard Crossbow Bolt

ammo_type: CrossbowBolt
technology: Conventional

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

maximumStackSize: 20
weight: 0.08
value: 2

traits: []
```

Crossbow Bolts may follow the same general specialization model as arrows.

Examples:

```text
Armor-Piercing Bolt
Barbed Bolt
Heavy Bolt
```

---

# 50. Arcane Ammunition

Arcane ammunition is restricted primarily to:

```text
Arrow
CrossbowBolt
```

Arcane ammunition may be fired from compatible Arcane weapons.

Depending on later balance decisions, some Arcane ammunition may also function with Conventional Bows or Crossbows.

Typical Arcane ammunition includes:

```text
Arcane Arrow
Arcane Bolt

Flame Arrow
Frost Arrow
Shock Arrow

Ghost Arrow
Dispel Arrow
Explosive Rune Arrow
```

Arcane ammunition should emphasize special magical effects rather than simply providing universally superior damage.

---

## 50.1 Arcane Arrow

A basic magically empowered projectile.

```text
Arcane Arrow

ammo_type: Arrow
technology: Arcane

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: +0.05

range_modifier: 0

traits:
[
    Arcane
]
```

Possible effects:

- Damage supernatural creatures
- Interact with magical barriers
- Ignore immunity to mundane projectiles

---

## 50.2 Flame Arrow

```text
Flame Arrow

ammo_type: Arrow
technology: Arcane

damage_modifier: 1.00
armor_damage_modifier: 0.90
armor_penetration_modifier: 0.00

range_modifier: 0

traits:
[
    Arcane,
    Burning
]
```

Core role:

**BURNING / AREA HAZARD**

---

## 50.3 Frost Arrow

```text
Frost Arrow

ammo_type: Arrow
technology: Arcane

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

traits:
[
    Arcane,
    Frost
]
```

Possible effects:

- Slow
- Reduced mobility
- Reduced Stamina recovery
- Freeze environmental surfaces

---

## 50.4 Shock Arrow

```text
Shock Arrow

ammo_type: Arrow
technology: Arcane

damage_modifier: 0.90
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

traits:
[
    Arcane,
    Shock
]
```

Possible effects:

- Stun
- Daze
- Stamina damage
- Machine disruption

---

## 50.5 Ghost Arrow

```text
Ghost Arrow

ammo_type: Arrow
technology: Arcane

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

traits:
[
    Arcane,
    AntiSpirit
]
```

Core role:

**SUPERNATURAL TARGETS**

Possible targets include:

```text
Ghosts
Spirits
Ethereal Creatures
Magical Entities
```

---

## 50.6 Dispel Arrow

```text
Dispel Arrow

ammo_type: Arrow
technology: Arcane

damage_modifier: 0.75
armor_damage_modifier: 0.75
armor_penetration_modifier: 0.00

range_modifier: 0

traits:
[
    Arcane,
    Dispel
]
```

Core role:

**ANTI-MAGIC UTILITY**

Possible effects:

- Damage magical barriers
- Remove magical buffs
- Disrupt summoned entities
- Disable magical objects

---

## 50.7 Explosive Rune Arrow

```text
Explosive Rune Arrow

ammo_type: Arrow
technology: Arcane

damage_modifier: 1.00
armor_damage_modifier: 1.20
armor_penetration_modifier: 0.00

range_modifier: 0

traits:
[
    Arcane,
    Explosive,
    AreaAttack
]
```

Core role:

**ARCANE AoE**

---

# 51. Ammunition Compatibility

Weapon and ammunition compatibility should primarily be determined through `ammunition_type`.

Example:

```text
Service Rifle
ammunition_type: RifleRound
```

Compatible:

```text
Standard Rifle Round
Armor-Piercing Rifle Round
Hollow-Point Rifle Round
Incendiary Rifle Round
```

Not compatible:

```text
PistolRound
ShotgunShell
Rocket
LaserCell
```

Technology may provide an additional compatibility restriction where appropriate.

For example:

```text
Laser Rifle

ammunition_type: LaserCell
technology: Laser
```

should not normally accept a Plasma Cell even if both are abstractly considered energy ammunition.

---

# 52. Ammunition and Weapon Damage Calculation

A simplified ranged attack calculation may begin with:

```text
Rolled Weapon Damage
=
Random(
    damage_min,
    damage_max
)
```

Then apply ammunition:

```text
Modified Damage
=
Rolled Weapon Damage
× Ammo Damage Modifier
```

Armor damage:

```text
Final Armor Damage
=
Modified Damage
× Weapon Armor Damage
× Ammo Armor Damage Modifier
```

Armor penetration:

```text
Final Armor Penetration
=
Weapon Armor Penetration
+ Ammo Armor Penetration Modifier
```

Final Armor Penetration should be clamped according to combat-system rules.

Example:

```text
Weapon Damage:
50

Weapon Armor Damage:
1.20

Weapon Armor Penetration:
0.30

AP Ammo:
damage_modifier: 0.90
armor_damage_modifier: 1.10
armor_penetration_modifier: +0.20
```

Calculation:

```text
Modified Damage
=
50 × 0.90
=
45
```

```text
Armor Damage
=
45 × 1.20 × 1.10
=
59.4
```

```text
Armor Penetration
=
0.30 + 0.20
=
0.50
```

The complete Health and Armor damage calculation belongs to the main Combat System rather than this document.

---

# 53. Range Calculation

The weapon defines:

```text
optimal_range
max_range
```

Ammo may define:

```text
range_modifier
```

A simple implementation may use:

```text
Final Optimal Range
=
Weapon Optimal Range
+ Ammo Range Modifier
```

```text
Final Max Range
=
Weapon Max Range
+ Ammo Range Modifier
```

More complex ammunition may instead modify only one range value through Traits or Effects.

Avoid adding additional universal range fields until they are required by gameplay.

---

# 54. Burst and Automatic Fire

Burst and Automatic firing behavior should not be represented through additional base weapon properties such as:

```text
rate_of_fire
shots_per_second
DPS
```

Instead, use Weapon Actions.

Example:

```text
BurstFire

AP Cost: 6
Ammo Cost: 3
Stamina Cost: +4

Shots:
3
```

Example:

```text
SuppressiveFire

AP Cost: 7
Ammo Cost: 8

Traits:
[
    Suppression
]
```

This ensures turn-based Action Points remain the primary representation of attack tempo.

---

# 55. Shotgun Scatter

Scatter should be implemented through ammunition, Action, or Trait behavior rather than a universal `spread` property on every Ranged Weapon.

Example:

```text
Buckshot

Traits:
[
    Pellet,
    Scatter
]
```

The `Scatter` effect may determine:

- Number of pellets
- Target distribution
- Range penalties
- Adjacent-target hits
- Damage falloff

A Shotgun firing a Slug would therefore not use Scatter behavior.

---

# 56. Explosive Attacks

Explosive ammunition should use Traits and payload Effects.

Example:

```text
Rocket

Traits:
[
    Explosive,
    AreaAttack
]
```

The explosion effect may define:

```text
blast_radius
blast_damage
armor_damage
knockback
terrain_damage
```

These values belong to the Explosion Effect or Payload Definition rather than the universal `AmmunitionDefinition`.

This allows the same base ammo schema to support:

```text
Grenades
Rockets
Mini-Nukes
Arcane Explosives
```

without adding explosive-specific properties to every ammunition item.

---

# 57. Flamethrower Fuel

The Flamethrower may use a specialized fuel ammunition type.

If fuel is tracked through the ammunition system:

```text
ammo_type: FlamethrowerFuel
technology: Ballistic
```

Example:

```text
Standard Flamethrower Fuel

ammo_type: FlamethrowerFuel
technology: Ballistic

damage_modifier: 1.00
armor_damage_modifier: 1.00
armor_penetration_modifier: 0.00

range_modifier: 0

traits:
[
    Burning
]
```

Potential specialized fuels:

```text
Incendiary Fuel
Sticky Fuel
Alchemical Fuel
```

However, specialized fuel types should only be added if they create meaningful tactical differences.

---

# 58. Minimum Range

Some heavy explosive weapons may be dangerous or ineffective at extremely short range.

Examples:

```text
Rocket Launcher
Nuclear Launcher
```

This behavior should be represented through:

```text
MinimumRange
```

Trait or Action requirements rather than a universal property added to all Ranged Weapons.

Possible rules include:

- Cannot target adjacent tiles
- Severe self-damage risk
- Reduced warhead arming
- Friendly-fire warning

---

# 59. Indirect Fire

Weapons such as Grenade Launchers may support indirect or arcing fire.

Use:

```text
IndirectFire
```

as an Action or Trait.

Possible characteristics:

- Fire over low obstacles
- Ignore direct line-of-fire requirements
- Reduced precision
- Requires target tile rather than target unit
- May interact with spotting systems

Do not make `trajectory_type` a universal Ranged Weapon property unless indirect-fire systems become sufficiently complex to require it.

---

# 60. Environmental Interaction

Certain ranged weapons should interact directly with the tactical environment.

Examples:

## Flamethrower

May ignite:

```text
Wood
Fuel
Cargo
Vegetation
Explosives
Ship Components
```

## Rocket Launcher

May damage:

```text
Doors
Barricades
Walls
Ship Components
Cover
```

## Nuclear Launcher

May cause:

```text
Large Terrain Destruction
Fire
Radiation Zones
Structural Damage
Persistent Environmental Hazards
```

## Arcane Ammunition

May interact with:

```text
Magical Barriers
Arcane Devices
Spirits
Summoned Creatures
Enchanted Terrain
```

Environmental effects should be handled through Traits and combat Effects rather than specialized weapon properties.

---

# 61. Example: Service Rifle

```text
RangedWeapon
{
    id: "weapon_service_rifle"

    name: "Service Rifle"

    description:
        "A rugged magazine-fed ballistic rifle commonly issued
        to colonial troops, mercenaries, and shipboard security."

    family: Rifle
    technology: Ballistic
    hands: TwoHanded

    damage_min: 45
    damage_max: 60

    armor_damage: 1.00
    armor_penetration: 0.30

    stamina_cost: 5

    optimal_range: 8
    max_range: 14

    weight: 4.2
    durability: 100
    value: 650

    ammunition_type: RifleRound
    magazine_capacity: 20

    traits:
    [
    ]

    actions:
    [
        SnapShot,
        StandardShot,
        AimedShot,
        BurstFire,
        ReloadMagazine
    ]
}
```

---

# 62. Example: Laser Rifle

```text
RangedWeapon
{
    id: "weapon_laser_rifle"

    name: "Laser Rifle"

    description:
        "A directed-energy rifle that focuses stored electrical
        power through a precision optical assembly."

    family: Rifle
    technology: Laser
    hands: TwoHanded

    damage_min: 40
    damage_max: 55

    armor_damage: 0.90
    armor_penetration: 0.40

    stamina_cost: 4

    optimal_range: 10
    max_range: 16

    weight: 4.8
    durability: 100
    value: 1200

    ammunition_type: LaserCell
    magazine_capacity: 20

    heat_capacity: 100
    heat_per_shot: 8

    traits:
    [
        LowRecoil
    ]

    actions:
    [
        StandardShot,
        AimedShot,
        PrecisionShot,
        ReloadEnergyCell
    ]
}
```

---

# 63. Example: Plasma Rifle

```text
RangedWeapon
{
    id: "weapon_plasma_rifle"

    name: "Plasma Rifle"

    description:
        "A heavy energy weapon that launches unstable packets
        of superheated plasma capable of devastating armor."

    family: Rifle
    technology: Plasma
    hands: TwoHanded

    damage_min: 65
    damage_max: 85

    armor_damage: 1.30
    armor_penetration: 0.55

    stamina_cost: 7

    optimal_range: 7
    max_range: 12

    weight: 6.5
    durability: 100
    value: 2400

    ammunition_type: PlasmaCell
    magazine_capacity: 10

    heat_capacity: 100
    heat_per_shot: 20

    traits:
    [
        Overheat
    ]

    actions:
    [
        PlasmaShot,
        ChargedShot,
        ReloadPlasmaCell
    ]
}
```

---

# 64. Example: Arcane Longbow

```text
RangedWeapon
{
    id: "weapon_arcane_longbow"

    name: "Arcane Longbow"

    description:
        "A rune-inscribed longbow reinforced by arcane conduits
        that channel magical energy through the bow and projectile."

    family: Bow
    technology: Arcane
    hands: TwoHanded

    damage_min: 40
    damage_max: 55

    armor_damage: 0.90
    armor_penetration: 0.25

    stamina_cost: 7

    optimal_range: 9
    max_range: 15

    weight: 2.0
    durability: 100
    value: 1000

    ammunition_type: Arrow
    magazine_capacity: null

    traits:
    [
        Arcane
    ]

    actions:
    [
        QuickShot,
        AimedShot,
        ArcaneShot
    ]
}
```

---

# 65. Example: Combat Shotgun

```text
RangedWeapon
{
    id: "weapon_combat_shotgun"

    name: "Combat Shotgun"

    description:
        "A rugged short-range firearm designed for boarding actions,
        urban fighting, and close-quarters combat."

    family: Shotgun
    technology: Ballistic
    hands: TwoHanded

    damage_min: 50
    damage_max: 70

    armor_damage: 1.10
    armor_penetration: 0.20

    stamina_cost: 6

    optimal_range: 3
    max_range: 6

    weight: 4.5
    durability: 100
    value: 700

    ammunition_type: ShotgunShell
    magazine_capacity: 8

    traits:
    [
    ]

    actions:
    [
        Fire,
        AimedShot,
        ReloadSingleRound
    ]
}
```

The loaded ammunition determines whether the attack behaves as Buckshot, Slug, or another specialized shell.

---

# 66. Example: Rocket Launcher

```text
RangedWeapon
{
    id: "weapon_rocket_launcher"

    name: "Rocket Launcher"

    description:
        "A shoulder-fired launcher designed to deliver heavy
        explosive and anti-armor warheads at battlefield ranges."

    family: Launcher
    technology: Ballistic
    hands: TwoHanded

    damage_min: 80
    damage_max: 100

    armor_damage: 1.20
    armor_penetration: 0.35

    stamina_cost: 12

    optimal_range: 10
    max_range: 16

    weight: 9.0
    durability: 100
    value: 1800

    ammunition_type: Rocket
    magazine_capacity: 1

    traits:
    [
        MinimumRange
    ]

    actions:
    [
        FireRocket,
        AimedRocket,
        LoadRocket
    ]
}
```

The loaded Rocket determines the primary explosion and payload behavior.

---

# 67. Example: Nuclear Launcher

```text
RangedWeapon
{
    id: "weapon_nuclear_launcher"

    name: "Nuclear Launcher"

    description:
        "An exceptionally rare heavy launcher designed to fire
        compact nuclear warheads against fortified targets."

    family: Launcher
    technology: Ballistic
    hands: TwoHanded

    damage_min: 100
    damage_max: 120

    armor_damage: 1.50
    armor_penetration: 0.50

    stamina_cost: 16

    optimal_range: 10
    max_range: 16

    weight: 14.0
    durability: 100
    value: 8000

    ammunition_type: MiniNuke
    magazine_capacity: 1

    traits:
    [
        Heavy,
        MinimumRange
    ]

    actions:
    [
        FireMiniNuke,
        LoadMiniNuke
    ]
}
```

The majority of its destructive behavior comes from the `MiniNuke` payload rather than the launcher itself.

---

# 68. Example: Flamethrower

```text
RangedWeapon
{
    id: "weapon_flamethrower"

    name: "Flamethrower"

    description:
        "A fuel-fed industrial weapon that projects a stream
        of burning liquid across a wide area."

    family: Special
    technology: Ballistic
    hands: TwoHanded

    damage_min: 25
    damage_max: 40

    armor_damage: 0.80
    armor_penetration: 0.10

    stamina_cost: 8

    optimal_range: 3
    max_range: 5

    weight: 8.0
    durability: 100
    value: 900

    ammunition_type: FlamethrowerFuel
    magazine_capacity: 20

    traits:
    [
        Cone,
        AreaAttack,
        Burning
    ]

    actions:
    [
        FlameBurst,
        SustainedFlame,
        ReloadFuel
    ]
}
```

---

# 69. Properties Explicitly Excluded from RangedWeapon

The base `RangedWeapon` definition should not contain:

```text
accuracy
ap_cost
reload_speed
reload_time

current_ammo
current_heat
current_durability

critical_chance
critical_damage

attack_speed
rate_of_fire
DPS
```

These concepts belong elsewhere or are intentionally omitted.

---

## accuracy

Handled by:

```text
Character Skill
Weapon Action
Range
Cover
Conditions
Target Defense
```

---

## ap_cost

Handled by:

```text
WeaponAction
```

---

## reload_speed / reload_time

Handled by:

```text
Reload Action AP Cost
```

---

## current_ammo

Handled by:

```text
RangedWeaponInstance
```

---

## current_heat

Handled by:

```text
RangedWeaponInstance
```

---

## current_durability

Handled by:

```text
RangedWeaponInstance
```

---

## attack_speed / rate_of_fire

Represented through:

```text
Action AP Cost
Ammo Cost
Number of Shots
```

---

## DPS

DPS is not a meaningful core property in the turn-based tactical combat system.

---

# 70. Data Responsibility

The ranged combat data model should maintain clear responsibility boundaries.

```text
RangedWeapon
    ↓
Defines the weapon itself
```

Responsible for:

```text
Damage
Armor Damage
Armor Penetration
Range
Stamina Cost
Magazine Capacity
Heat Limits
Technology
Family
Traits
Available Actions
```

---

```text
AmmunitionDefinition
    ↓
Defines what is loaded
```

Responsible for:

```text
Damage Modification
Armor Modification
Penetration Modification
Range Modification
Payload
Special Effects
Logistics
```

---

```text
WeaponAction
    ↓
Defines how the weapon is used
```

Responsible for:

```text
AP Cost
Action Stamina Modifier
Hit Modifier
Ammo Consumption
Number of Shots
Attack Pattern
Targeting Rules
```

---

```text
RangedWeaponInstance
    ↓
Defines runtime state
```

Responsible for:

```text
Current Ammo
Loaded Ammo
Current Heat
Current Durability
Instance-Specific Modifications
```

---

# 71. Design Principles

## Family and Technology are independent

The weapon's physical form and its technology are separate concepts.

Example:

```text
Ballistic Rifle
Laser Rifle
Plasma Rifle
```

All belong to:

```text
Family: Rifle
```

but use different technologies.

---

## Technology is not a linear tier

Do not implement:

```text
Conventional
    ↓
Ballistic
    ↓
Laser
    ↓
Plasma
```

These are different technological solutions.

A high-quality Ballistic Rifle may remain useful even when Laser and Plasma weapons are available.

---

## Arcane Ranged Weapons remain Bow and Crossbow based

Arcane technology is intentionally restricted to:

```text
Bow
Crossbow
```

Do not introduce standard:

```text
Arcane Pistol
Arcane Rifle
Arcane Shotgun
Arcane Launcher
```

unless the setting is deliberately changed later.

This restriction preserves the technological identity of Arcane ranged combat.

---

## Ballistic weapons emphasize ammunition diversity

Ballistic technology should provide the broadest ammunition selection.

Examples:

```text
Armor Piercing
Hollow Point
Incendiary
Fragmentation
Explosive
Smoke
Anti-Armor
```

This gives Ballistic weapons long-term tactical relevance.

---

## Laser weapons emphasize precision

Laser weapons should generally emphasize:

```text
Range
Low Recoil
Precision
Energy Efficiency
```

without requiring a base `accuracy` property.

---

## Plasma weapons emphasize destruction

Plasma weapons should generally emphasize:

```text
High Damage
Armor Damage
Armor Penetration
Heat
Risk
```

Their power is balanced by heat, ammunition cost, and technological complexity.

---

## Launcher weapons are payload platforms

Grenade, Rocket, and Nuclear Launchers should derive much of their combat identity from ammunition.

```text
Launcher
+
Payload
=
Combat Function
```

This allows one launcher platform to support multiple tactical roles.

---

## Special weapons should remain exceptional

`Special` is not a miscellaneous dumping category.

Only weapons that fundamentally do not fit the normal weapon model should use it.

Currently:

```text
Flamethrower
```

---

## AP belongs to Actions

The same weapon may perform fast, normal, aimed, burst, or specialized attacks.

Therefore:

```text
Weapon
≠
AP Cost
```

Instead:

```text
Action
=
AP Cost
```

---

## Accuracy belongs to the combat and Action systems

Weapon-level Accuracy is intentionally omitted.

This prevents redundant numerical properties and keeps weapon handling tied to actual firing methods and character skill.

---

## Runtime state is separate from definitions

Do not mix static item definitions with individual weapon state.

```text
Definition
=
What this weapon type is

Instance
=
What is happening to this particular weapon
```

---

## Prefer Traits over universal properties

When only a minority of weapons require a mechanic, implement it as a Trait or Effect rather than adding another field to every weapon.

Examples:

```text
Scatter
Cone
Explosive
Radiation
Burning
Overheat
MinimumRange
IndirectFire
```

---

# 72. Final RangedWeapon Schema

```text
RangedWeapon
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

    optimal_range
    max_range

    weight
    durability
    value

    ammunition_type        // optional
    magazine_capacity      // optional

    energy_capacity        // optional
    energy_per_shot        // optional

    heat_capacity          // optional
    heat_per_shot          // optional

    traits[]
    actions[]
}
```

---

# 73. Final AmmunitionDefinition Schema

```text
AmmunitionDefinition : ItemDefinition
{
    id
    name
    description

    ammunitionType
    technology

    damagePercentage
    armorDamagePercentage
    armorPenetrationModifier

    rangeModifier

    maximumStackSize
    weightHundredthsOfPound
    value

    tags[]
}
```

Modifier rules:

```text
damagePercentage
→ multiplicative

armorDamagePercentage
→ multiplicative

armorPenetrationModifier
→ additive

rangeModifier
→ additive
```

---

# 74. Final RangedWeaponInstance Schema

```text
RangedWeaponInstance
{
    definition_id

    current_durability

    loaded_ammo_id
    current_ammo

    current_heat
}
```

Optional future fields may include:

```text
installed_modifications[]
custom_name
quality
condition_effects[]
```

These should only be added when the corresponding systems are implemented.

---

# 75. Final System Structure

```text
Ranged Combat Equipment
│
├── RangedWeapon
│   │
│   ├── Family
│   │   ├── Bow
│   │   ├── Crossbow
│   │   ├── Pistol
│   │   ├── Rifle
│   │   ├── Shotgun
│   │   ├── Heavy
│   │   ├── Launcher
│   │   └── Special
│   │
│   └── Technology
│       ├── Conventional
│       ├── Ballistic
│       ├── Laser
│       ├── Plasma
│       └── Arcane
│
├── AmmunitionDefinition
│   ├── Arrow
│   ├── CrossbowBolt
│   ├── PistolRound
│   ├── RifleRound
│   ├── ShotgunShell
│   ├── Grenade
│   ├── Rocket
│   ├── MiniNuke
│   ├── FlamethrowerFuel
│   ├── LaserCell
│   └── PlasmaCell
│
├── WeaponAction
│   ├── Fire
│   ├── SnapShot
│   ├── AimedShot
│   ├── BurstFire
│   ├── AreaAttack
│   └── Reload
│
└── RangedWeaponInstance
    ├── Current Durability
    ├── Loaded Ammo
    ├── Current Ammo
    └── Current Heat
```

This separation should remain the foundation of the Ranged Weapon System.

---

# 76. Implementation Status

The first data-driven ranged weapon slice is implemented. Authored JSON uses
the repository's camel-case convention while keeping separate
`RangedWeaponDefinition`, `AmmunitionDefinition`,
`RangedWeaponActionDefinition`, and `RangedWeaponState` responsibilities.

`RangedWeaponDefinition : WeaponDefinition` owns family, technology,
handedness, governing Skill, base damage and armor behavior, Stamina cost,
optimal and maximum range, weight, durability, value, optional ammunition or
internal energy, optional heat, traits, and allowed actions. Weight is stored
as an integer count of hundredths of a pound. It inherits item fields from
`ItemDefinition` and equip slots from `EquipmentDefinition`; there is no second
ranged definition containing duplicate weight, value, traits, or action IDs.
The content compiler enforces the family-and-technology compatibility table in
section 4 and rejects weapons that model both replaceable ammunition and
internal energy.

Ammunition definitions use integer percentages for multiplicative damage and
Armor Damage modifiers. Armor Penetration and range modifiers are additive.
Action definitions own AP, Stamina and hit modifiers, shot count, ammunition
consumption, energy and heat adjustments, durability cost, range penalties,
damage falloff, reload quantity, and effects. Attack and reload actions have
different validated field requirements.

`RangedWeaponSystem` validates actor ownership, content fingerprint, that the
equipped item resolves to `RangedWeaponDefinition`, action membership, target
legality, range, cover, AP, Stamina, ammunition, energy, heat, durability, and
Skill before reserving an attack. Each shot uses an explicit seed and sequence,
including multi-shot actions. Damage modifiers are applied in the order
specified by section 52, and burst results reduce Armor between resolved hits.
Accepted attempts spend their costs even when every shot misses; rejected
attempts leave all input state unchanged.

Reloading is a separate transactional operation addressed by the weapon's
`ItemInstanceId` and the ammunition's `InventoryEntryId`. It resolves the
ammunition definition through the unified item catalog, validates container
ownership and compatibility, prevents replacement of a non-empty incompatible
magazine, and respects both magazine capacity and stack quantity. Stack
consumption, magazine replacement, AP, and Stamina are committed atomically;
rejection leaves all input state unchanged. Attack, reload, and `Cool` publish
ammunition, energy, heat, and durability back into the owning character's item
state.

The base pack demonstrates the contract with the Ballistic Service Pistol,
Standard Pistol Rounds, Standard Shot, Aimed Shot, and Reload Magazine. Other
families, technologies, ammunition payloads, burst fire, area attacks, and
environmental interactions are schema-supported concepts that still require
authored definitions. A hit now emits deterministic armor-damage and
physical-damage `EffectRequest` values plus action-authored Effects;
`ResolveEffects` applies the batch atomically through the shared Effect system.

Encounter units and campaign save schema 13 persist the item instance and its
`RangedWeaponState`. Publishing the resolved Effect target snapshot back into
encounter actor state, AI action selection, line-of-fire, and terrain effects
remain planned.
