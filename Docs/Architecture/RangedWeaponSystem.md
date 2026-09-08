# Ranged Weapon System

## 1. Overview

The **Ranged Weapon System** defines all character-operated ranged weapons.

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
