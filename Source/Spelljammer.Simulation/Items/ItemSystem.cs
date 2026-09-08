using System.Collections.Frozen;
using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

public enum ItemCategory : byte
{
    Equipment,
    Ammunition,
    Consumable,
    Material,
    Commodity,
    Miscellaneous,
}

/// <summary>Immutable static data shared by all instances of an item kind.</summary>
public abstract record ItemDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ItemCategory Category,
    int WeightHundredthsOfPound,
    int Value,
    int MaximumStackSize,
    ImmutableArray<string> Tags)
    : ContentDefinition(Id, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>Base definition for an item which may occupy authored equipment slots.</summary>
public abstract record EquipmentDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    ImmutableArray<string> Tags,
    ImmutableArray<ContentId> OccupiedSlotIds)
    : ItemDefinition(Id, SchemaVersion, Revision, NameKey, DescriptionKey,
        ItemCategory.Equipment, WeightHundredthsOfPound, Value, 1, Tags);

/// <summary>Common base for equipment that provides weapon actions.</summary>
public abstract record WeaponDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    ImmutableArray<string> Tags,
    ImmutableArray<ContentId> OccupiedSlotIds,
    ImmutableArray<ContentId> ActionIds)
    : EquipmentDefinition(Id, SchemaVersion, Revision, NameKey, DescriptionKey,
        WeightHundredthsOfPound, Value, Tags, OccupiedSlotIds);

public sealed record MeleeWeaponDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    ImmutableArray<string> Tags,
    ImmutableArray<ContentId> OccupiedSlotIds,
    ImmutableArray<ContentId> ActionIds)
    : WeaponDefinition(Id, SchemaVersion, Revision, NameKey, DescriptionKey,
        WeightHundredthsOfPound, Value, Tags, OccupiedSlotIds, ActionIds);

public sealed record RangedWeaponDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    ImmutableArray<string> Tags,
    ImmutableArray<ContentId> OccupiedSlotIds,
    ImmutableArray<ContentId> ActionIds)
    : WeaponDefinition(Id, SchemaVersion, Revision, NameKey, DescriptionKey,
        WeightHundredthsOfPound, Value, Tags, OccupiedSlotIds, ActionIds);

public sealed record ArmorDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    ImmutableArray<string> Tags,
    ImmutableArray<ContentId> OccupiedSlotIds,
    int ArmorValue,
    int DurabilityMaximum,
    ImmutableArray<ContentId> ResistanceIds,
    ImmutableArray<string> CoverageTags,
    ImmutableArray<string> Traits)
    : EquipmentDefinition(Id, SchemaVersion, Revision, NameKey, DescriptionKey,
        WeightHundredthsOfPound, Value, Tags, OccupiedSlotIds);

public sealed record GearDefinition(
    ContentId Id,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    ImmutableArray<string> Tags,
    ImmutableArray<ContentId> OccupiedSlotIds,
    ImmutableArray<ContentId> ActionIds,
    ImmutableArray<ContentId> EffectIds)
    : EquipmentDefinition(Id, SchemaVersion, Revision, NameKey, DescriptionKey,
        WeightHundredthsOfPound, Value, Tags, OccupiedSlotIds);

public readonly record struct ItemInstanceId(Guid Value)
{
    public bool IsValid => Value != Guid.Empty;
    public override string ToString() => Value.ToString("N");
}

public readonly record struct InventoryContainerId(Guid Value)
{
    public bool IsValid => Value != Guid.Empty;
    public override string ToString() => Value.ToString("N");
}

/// <summary>Persistent state belonging to one non-stackable item.</summary>
public sealed record ItemInstance(
    ItemInstanceId InstanceId,
    ContentId DefinitionId,
    InventoryContainerId OwnerContainerId,
    int? CurrentDurability,
    ContentId? QualityId,
    string? CustomName,
    int StateVersion);

/// <summary>A bounded container which owns distinct item instances.</summary>
public sealed record InventoryContainer(
    InventoryContainerId ContainerId,
    ContentId OwnerId,
    int MaximumWeightHundredthsOfPound,
    int MaximumEntries,
    ImmutableArray<ItemInstanceId> ItemInstanceIds);

public sealed record SlotAssignment(ContentId SlotId, ItemInstanceId ItemInstanceId);

/// <summary>The equipment slots currently occupied by one owner.</summary>
public sealed record EquipmentLoadout(ContentId OwnerId, ImmutableArray<SlotAssignment> SlotAssignments);

/// <summary>Read-only definition lookup used by item-state validation.</summary>
public sealed class ItemDefinitionCatalog
{
    private readonly FrozenDictionary<ContentId, ItemDefinition> definitions;
    private readonly FrozenSet<ContentId> equipmentSlots;

    public ItemDefinitionCatalog(IEnumerable<ItemDefinition> definitions, IEnumerable<ContentId> equipmentSlots)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(equipmentSlots);
        this.definitions = definitions.ToFrozenDictionary(value => value.Id);
        this.equipmentSlots = equipmentSlots.ToFrozenSet();
    }

    public bool TryGetDefinition(ContentId id, out ItemDefinition? definition) =>
        definitions.TryGetValue(id, out definition);

    public bool IsKnownEquipmentSlot(ContentId id) => equipmentSlots.Contains(id);
}

/// <summary>Whole immutable item state, used as the transaction publication boundary.</summary>
public sealed record ItemSystemState(
    ImmutableArray<ItemInstance> ItemInstances,
    ImmutableArray<InventoryContainer> InventoryContainers,
    ImmutableArray<EquipmentLoadout> EquipmentLoadouts);

public sealed record ItemSystemResult(ItemSystemState State, bool Accepted, string RejectionCode)
{
    public static ItemSystemResult Rejected(ItemSystemState state, string code) => new(state, false, code);
    public static ItemSystemResult AcceptedState(ItemSystemState state) => new(state, true, ItemRejectionCodes.None);
}

public static class ItemRejectionCodes
{
    public const string None = "none";
    public const string InvalidState = "item.invalid-state";
    public const string ContainerMissing = "item.container-missing";
    public const string ItemMissing = "item.missing";
    public const string DefinitionMissing = "item.definition-missing";
    public const string OwnershipMismatch = "item.ownership-mismatch";
    public const string CapacityExceeded = "item.capacity-exceeded";
    public const string EquipmentRequired = "item.equipment-required";
    public const string SlotUnknown = "item.slot-unknown";
    public const string SlotConflict = "item.slot-conflict";
    public const string LoadoutMissing = "item.loadout-missing";
}

/// <summary>Atomic item, inventory, and equipment-loadout mutations.</summary>
public static class ItemSystem
{
    public static ItemSystemResult Create(ItemSystemState candidate, ItemDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(catalog);
        return Validate(candidate, catalog, out string rejection)
            ? ItemSystemResult.AcceptedState(Normalize(candidate))
            : ItemSystemResult.Rejected(candidate, rejection);
    }

    public static ItemSystemResult Equip(
        ItemSystemState state,
        ContentId ownerId,
        InventoryContainerId containerId,
        ItemInstanceId instanceId,
        ItemDefinitionCatalog catalog)
    {
        if (!Validate(state, catalog, out string rejection))
        {
            return ItemSystemResult.Rejected(state, rejection);
        }

        if (!TryFindContainer(state, containerId, out InventoryContainer? container))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.ContainerMissing);
        }

        if (container!.OwnerId != ownerId)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.OwnershipMismatch);
        }

        if (!TryFindItem(state, instanceId, out ItemInstance? item))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.ItemMissing);
        }

        if (item!.OwnerContainerId != containerId || !container.ItemInstanceIds.Contains(instanceId))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.OwnershipMismatch);
        }

        if (!catalog.TryGetDefinition(item.DefinitionId, out ItemDefinition? definition))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.DefinitionMissing);
        }

        if (definition is not EquipmentDefinition equipment)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.EquipmentRequired);
        }

        if (equipment.OccupiedSlotIds.IsDefaultOrEmpty || equipment.OccupiedSlotIds.Distinct().Count() != equipment.OccupiedSlotIds.Length ||
            equipment.OccupiedSlotIds.Any(slot => !catalog.IsKnownEquipmentSlot(slot)))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.SlotUnknown);
        }

        EquipmentLoadout? existing = state.EquipmentLoadouts.SingleOrDefault(value => value.OwnerId == ownerId);
        if (existing is null)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.LoadoutMissing);
        }

        ImmutableArray<SlotAssignment> retained = existing.SlotAssignments
            .Where(value => value.ItemInstanceId != instanceId)
            .ToImmutableArray();
        if (retained.Any(value => equipment.OccupiedSlotIds.Contains(value.SlotId)))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.SlotConflict);
        }

        EquipmentLoadout replacement = existing with
        {
            SlotAssignments = retained
                .AddRange(equipment.OccupiedSlotIds.Select(slot => new SlotAssignment(slot, instanceId)))
                .OrderBy(value => value.SlotId)
                .ToImmutableArray(),
        };
        ItemSystemState candidate = state with
        {
            EquipmentLoadouts = state.EquipmentLoadouts
                .Select(value => value.OwnerId == ownerId ? replacement : value)
                .ToImmutableArray(),
        };
        return Create(candidate, catalog);
    }

    public static ItemSystemResult Unequip(
        ItemSystemState state,
        ContentId ownerId,
        ItemInstanceId instanceId,
        ItemDefinitionCatalog catalog)
    {
        if (!Validate(state, catalog, out string rejection))
        {
            return ItemSystemResult.Rejected(state, rejection);
        }

        EquipmentLoadout? existing = state.EquipmentLoadouts.SingleOrDefault(value => value.OwnerId == ownerId);
        if (existing is null)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.LoadoutMissing);
        }

        ItemSystemState candidate = state with
        {
            EquipmentLoadouts = state.EquipmentLoadouts.Select(value => value.OwnerId == ownerId
                ? value with { SlotAssignments = value.SlotAssignments.Where(assignment => assignment.ItemInstanceId != instanceId).ToImmutableArray() }
                : value).ToImmutableArray(),
        };
        return Create(candidate, catalog);
    }

    public static ItemSystemResult Transfer(
        ItemSystemState state,
        ItemInstanceId instanceId,
        InventoryContainerId destinationContainerId,
        ItemDefinitionCatalog catalog)
    {
        if (!Validate(state, catalog, out string rejection))
        {
            return ItemSystemResult.Rejected(state, rejection);
        }

        if (!TryFindItem(state, instanceId, out ItemInstance? item))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.ItemMissing);
        }

        if (!TryFindContainer(state, destinationContainerId, out InventoryContainer? destination))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.ContainerMissing);
        }

        if (item!.OwnerContainerId == destinationContainerId)
        {
            return ItemSystemResult.AcceptedState(state);
        }

        if (!catalog.TryGetDefinition(item.DefinitionId, out ItemDefinition? definition))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.DefinitionMissing);
        }

        InventoryContainer destinationContainer = destination!;
        long destinationWeight = GetWeight(state, destinationContainer, catalog);
        if (destinationContainer.ItemInstanceIds.Length >= destinationContainer.MaximumEntries ||
            destinationWeight + definition!.WeightHundredthsOfPound > destinationContainer.MaximumWeightHundredthsOfPound)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.CapacityExceeded);
        }

        InventoryContainerId sourceContainerId = item.OwnerContainerId;
        ItemInstance transferred = item with { OwnerContainerId = destinationContainerId };
        ItemSystemState candidate = state with
        {
            ItemInstances = state.ItemInstances.Select(value => value.InstanceId == instanceId ? transferred : value).ToImmutableArray(),
            InventoryContainers = state.InventoryContainers.Select(value => value.ContainerId switch
            {
                var id when id == sourceContainerId => value with { ItemInstanceIds = value.ItemInstanceIds.Remove(instanceId) },
                var id when id == destinationContainerId => value with { ItemInstanceIds = value.ItemInstanceIds.Add(instanceId) },
                _ => value,
            }).ToImmutableArray(),
            EquipmentLoadouts = state.EquipmentLoadouts.Select(value => value.SlotAssignments.Any(assignment => assignment.ItemInstanceId == instanceId)
                ? value with { SlotAssignments = value.SlotAssignments.Where(assignment => assignment.ItemInstanceId != instanceId).ToImmutableArray() }
                : value).ToImmutableArray(),
        };
        return Create(candidate, catalog);
    }

    private static bool Validate(ItemSystemState state, ItemDefinitionCatalog catalog, out string rejection)
    {
        if (state.ItemInstances.Select(value => value.InstanceId).Distinct().Count() != state.ItemInstances.Length ||
            state.InventoryContainers.Select(value => value.ContainerId).Distinct().Count() != state.InventoryContainers.Length ||
            state.EquipmentLoadouts.Select(value => value.OwnerId).Distinct().Count() != state.EquipmentLoadouts.Length)
        {
            rejection = ItemRejectionCodes.InvalidState;
            return false;
        }

        foreach (InventoryContainer container in state.InventoryContainers)
        {
            if (!container.ContainerId.IsValid || !container.OwnerId.IsValid || container.MaximumWeightHundredthsOfPound < 0 ||
                container.MaximumEntries < 0 || container.ItemInstanceIds.Length > container.MaximumEntries ||
                container.ItemInstanceIds.Distinct().Count() != container.ItemInstanceIds.Length ||
                GetWeight(state, container, catalog) > container.MaximumWeightHundredthsOfPound)
            {
                rejection = ItemRejectionCodes.InvalidState;
                return false;
            }
        }

        foreach (ItemInstance item in state.ItemInstances)
        {
            if (!item.InstanceId.IsValid || !item.DefinitionId.IsValid || !item.OwnerContainerId.IsValid || item.StateVersion < 1 ||
                item.CurrentDurability < 0 || !catalog.TryGetDefinition(item.DefinitionId, out _))
            {
                rejection = ItemRejectionCodes.InvalidState;
                return false;
            }

            if (!TryFindContainer(state, item.OwnerContainerId, out InventoryContainer? owner) ||
                owner!.ItemInstanceIds.Count(value => value == item.InstanceId) != 1)
            {
                rejection = ItemRejectionCodes.OwnershipMismatch;
                return false;
            }
        }

        foreach (EquipmentLoadout loadout in state.EquipmentLoadouts)
        {
            if (!loadout.OwnerId.IsValid || loadout.SlotAssignments.Select(value => value.SlotId).Distinct().Count() != loadout.SlotAssignments.Length)
            {
                rejection = ItemRejectionCodes.InvalidState;
                return false;
            }

            foreach (IGrouping<ItemInstanceId, SlotAssignment> group in loadout.SlotAssignments.GroupBy(value => value.ItemInstanceId))
            {
                if (!TryFindItem(state, group.Key, out ItemInstance? item) ||
                    !TryFindContainer(state, item!.OwnerContainerId, out InventoryContainer? container) || container!.OwnerId != loadout.OwnerId ||
                    !catalog.TryGetDefinition(item.DefinitionId, out ItemDefinition? definition) || definition is not EquipmentDefinition equipment ||
                    group.Select(value => value.SlotId).OrderBy(value => value).SequenceEqual(equipment.OccupiedSlotIds.OrderBy(value => value)) is false)
                {
                    rejection = ItemRejectionCodes.InvalidState;
                    return false;
                }
            }
        }

        rejection = ItemRejectionCodes.None;
        return true;
    }

    private static ItemSystemState Normalize(ItemSystemState state) => state with
    {
        ItemInstances = state.ItemInstances.OrderBy(value => value.InstanceId.Value).ToImmutableArray(),
        InventoryContainers = state.InventoryContainers.OrderBy(value => value.ContainerId.Value).ToImmutableArray(),
        EquipmentLoadouts = state.EquipmentLoadouts.Select(value => value with
        {
            SlotAssignments = value.SlotAssignments.OrderBy(assignment => assignment.SlotId).ToImmutableArray(),
        }).OrderBy(value => value.OwnerId).ToImmutableArray(),
    };

    private static long GetWeight(ItemSystemState state, InventoryContainer container, ItemDefinitionCatalog catalog) =>
        container.ItemInstanceIds.Sum(instanceId => TryFindItem(state, instanceId, out ItemInstance? item) &&
            catalog.TryGetDefinition(item!.DefinitionId, out ItemDefinition? definition)
                ? (long)definition!.WeightHundredthsOfPound
                : long.MaxValue);

    private static bool TryFindItem(ItemSystemState state, ItemInstanceId instanceId, out ItemInstance? item)
    {
        item = state.ItemInstances.SingleOrDefault(value => value.InstanceId == instanceId);
        return item is not null;
    }

    private static bool TryFindContainer(ItemSystemState state, InventoryContainerId containerId, out InventoryContainer? container)
    {
        container = state.InventoryContainers.SingleOrDefault(value => value.ContainerId == containerId);
        return container is not null;
    }
}
