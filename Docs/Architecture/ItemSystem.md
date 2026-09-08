# Item System

## Status and scope

This document defines the planned item-system foundation for Spelljammer. The
first implementation slice is deliberately Equipment-first: it must support
equipping, unequipping, transferring, and persisting distinct pieces of
equipment without coupling inventory identity to WPF, rendering, or localized
text.

Existing melee and ranged weapon rules remain their own authoritative combat
definitions. This system supplies the item identity, ownership, placement, and
per-instance state that those rules need. Consumables, materials, trade goods,
crafting, merchants, loot generation, and ground containers are planned
extensions rather than implemented features.

## Core model

The item system has three separate responsibilities:

```text
ItemDefinition
    Abstract static definition shared by every item kind.

EquipmentDefinition : ItemDefinition
    An equippable item kind and its equip rules.

WeaponDefinition : EquipmentDefinition
    Abstract base for weapons.

MeleeWeaponDefinition : WeaponDefinition
RangedWeaponDefinition : WeaponDefinition
ArmorDefinition : EquipmentDefinition
GearDefinition : EquipmentDefinition

ItemInstance
    The state of one distinct physical item.

InventoryContainer / EquipmentLoadout
    Who owns the item and where it is placed.
```

Definitions are immutable compiled content. Instances and containers are
authoritative saveable simulation state. A localized name, renderer handle,
native pointer, or UI selection is never an item identity.

## Item definition

`ItemDefinition` is an abstract immutable content definition. It owns the
fields common to every item kind; specialized definitions inherit those fields
instead of duplicating or linking back to a separate generic item record.

```text
abstract ItemDefinition : ContentDefinition
{
    category
    weightHundredthsOfPound
    value
    maximumStackSize

    tags[]
}
```

The first supported categories are:

```text
Equipment
Ammunition
Consumable
Material
Commodity
Miscellaneous
```

`quest-item`, `unique`, `contraband`, `stolen`, and comparable concepts are
tags or ownership/status data, not primary categories. A quest object can be a
weapon, a material, or a commodity without requiring a parallel hierarchy.

An item instance resolves its `definitionId` directly to a derived
`ItemDefinition`. There is no parallel generic item record and no optional
`equipmentId`, `ammunitionId`, or similar cross-link on the base definition.

## Equipment definition

`EquipmentDefinition : ItemDefinition` is the abstract base for every
equippable item. It defines how an item occupies slots, while concrete derived
definitions define weapon, armor, or gear behavior. Its inherited `category`
is always `Equipment`. It does not own the runtime condition of a specific
item instance.

```text
abstract EquipmentDefinition : ItemDefinition
{
    occupiedSlotIds[]
}
```

The first equipment hierarchy is:

```text
ItemDefinition
└── EquipmentDefinition (abstract)
    ├── WeaponDefinition (abstract)
    │   ├── MeleeWeaponDefinition
    │   └── RangedWeaponDefinition
    ├── ArmorDefinition
    └── GearDefinition
```

### Weapon

`WeaponDefinition : EquipmentDefinition` is the abstract common base for all
weapons. It remains intentionally small: only data genuinely shared by melee
and ranged weapons belongs there. The concrete types directly own their
distinctive combat data:

```text
abstract WeaponDefinition : EquipmentDefinition

MeleeWeaponDefinition : WeaponDefinition
RangedWeaponDefinition : WeaponDefinition
```

`MeleeWeaponDefinition` retains melee damage, action, energy, durability, and
combat-resolution rules. `RangedWeaponDefinition` retains range, ammunition,
energy, heat, and ranged-resolution rules. Both inherit `occupiedSlotIds`
from `EquipmentDefinition`.

A future hybrid weapon must be an explicit concrete definition with defined
equip, action, and state rules. It must not be created through optional links
that let an ordinary weapon refer to both weapon systems.

### Armor

Armor is passive protective equipment. The initial armor placement model is:

```text
Armor
├── Helm       → equipment-slot.head
└── BodyArmor  → equipment-slot.body
```

This is modeled by `occupiedSlotIds`, not by hard-coding a fixed Helmet enum
into every inventory operation. `ArmorDefinition : EquipmentDefinition` carries
its defensive rules directly:

```text
ArmorDefinition : EquipmentDefinition
{
    armorValue
    durabilityMaximum
    resistanceIds[]
    coverageTags[]
    traits[]
}
```

An armored helmet occupies `Head`; an armored suit occupies `Body`; both may
be equipped at the same time. A pressure helmet that provides atmosphere
support but no Armor can instead be Gear occupying `Head`.

Future slots such as hands, legs, feet, back, and accessory slots must be
added as authored slot definitions with explicit capacity and compatibility
rules. They must not be assumed by code before gameplay requires them.

### Gear

`GearDefinition : EquipmentDefinition` covers equippable objects that are
neither Weapons nor Armor:

- tools and scanners;
- medical kits;
- casting foci and psionic foci;
- survival and EVA devices;
- relics;
- shields and wards.

Gear normally supplies Actions, Effects, tags, charges, or specialized state.
Shields belong to Gear in the first slice because their defining behavior is
an active defense or temporary effect, rather than a universal passive Armor
value.

## Equipment slots and loadout

The initial authored slots are:

```text
equipment-slot.head
equipment-slot.body
equipment-slot.main-hand
equipment-slot.off-hand
equipment-slot.utility
equipment-slot.relic
```

An `EquipmentLoadout` maps each slot to one `ItemInstanceId`.

```text
EquipmentLoadout
{
    ownerId
    slotAssignments[]
}

SlotAssignment
{
    slotId
    itemInstanceId
}
```

An equipped instance must be owned by the same actor's inventory container.
An instance that occupies multiple slots has one assignment for every occupied
slot, all pointing to the same instance. For example, a two-handed weapon
occupies both Main Hand and Off Hand.

The loadout must reject a publication when any of these conditions is true:

- a required slot is unknown or assigned more than once;
- the referenced item instance is missing or belongs to another owner;
- the instance definition is not Equipment;
- the equipment does not declare that slot;
- one item has only part of its required slot set assigned;
- a slot conflicts with another equipped item;
- a broken or context-incompatible item is used where its specialized rules
  prohibit it.

Unequipping removes every assignment for that instance atomically. Dropping,
trading, destroying, or transferring an equipped instance must likewise clear
its loadout assignments in the same commit.

## Item instances

An `ItemInstance` represents one non-stackable item with its own persistent
identity.

```text
ItemInstance
{
    instanceId
    definitionId
    ownerContainerId

    currentDurability?
    qualityId?
    customName?
    stateVersion
}
```

Specialized state is composed with the instance rather than duplicated in the
definition:

```text
ItemInstance
├── RangedWeaponState
│   ├── loadedAmmunitionId
│   ├── currentAmmunition
│   ├── currentEnergy
│   └── currentHeat
├── MeleeWeaponState
│   ├── currentDurability
│   └── currentEnergy
└── ArmorState (planned)
    └── currentDurability
```

The final ownership boundary must select one source of truth for durability.
The preferred model is item-instance durability, with weapon and armor state
projecting that value for their resolution systems. The current melee and
ranged standalone state records are transitional until this persistence
contract is migrated together.

## Inventory containers

An inventory container is a bounded owner of item entries. Characters, ships,
loot caches, merchants, and ground drops can use the same future abstraction.

```text
InventoryContainer
{
    containerId
    ownerId
    maximumWeightHundredthsOfPound
    maximumEntries
    entries[]
}

InventoryEntry
{
    itemDefinitionId
    itemInstanceId?
    quantity
}
```

The initial capacity model is authored maximum weight plus a bounded entry
count. Item shapes and grid-packing are explicitly out of scope.

Stackable entries use `itemDefinitionId` and a positive `quantity` no greater
than the definition's `maximumStackSize`. Distinct equipment uses one
`itemInstanceId`, quantity one, and cannot be merged merely because it shares
a definition ID.

Weight uses an integer fixed-point unit of one hundredth of a pound. For
example, `125` represents 1.25 lb and `5` represents 0.05 lb. Simulation,
sorting, capacity checks, and save data use only these integers; they never use
floating-point weight values. Player-facing UI formats the integer quotient
and remainder as a decimal pound value, such as `1.25 lb`.

Weight is calculated from the active compiled definition and quantity. It is
not copied into each entry. Container capacity, stack limits, and entry limits
must be validated before a state replacement is published.

## Commands and commit boundaries

All mutations use typed commands and publish replacement state only after full
validation.

```text
TransferItem
EquipItem
UnequipItem
SplitStack
MergeStack
DropItem
DestroyItem
```

`TransferItem` validates source ownership, destination capacity, stack rules,
and equipment references before changing either container. `EquipItem`
validates inventory ownership and every requested slot before replacing the
loadout. Failure is observable and leaves all source state unchanged.

Commands must use stable IDs, bounded quantities, deterministic ordering, and
explicit failure codes. UI drag-and-drop is only a command producer; it must
not mutate inventory or loadout state directly.

## Equipment-first implementation plan

1. Add typed IDs and compiled definitions for Item, abstract Equipment,
   abstract Weapon, MeleeWeapon, RangedWeapon, Armor, Gear, and equipment
   slots.
2. Introduce saveable `ItemInstance`, `InventoryContainer`, and
   `EquipmentLoadout` state with versioned DTOs and validation.
3. Migrate the current encounter `PersonalLoadout` from definition IDs to item
   instance IDs while retaining content-fingerprint validation.
4. Connect equipped melee and ranged instances to their persistent durability,
   energy, heat, and loaded-ammunition state.
5. Add transactional Equip, Unequip, and Transfer commands with focused
   success, conflict, capacity, ownership, and rollback contracts.
6. Add Helm and BodyArmor definitions and authored Head/Body slot examples.

No inventory, item, or equipment change may require hard-coded absolute paths,
localized text as identity, unbounded collections, or UI-timing-dependent
simulation behavior.
