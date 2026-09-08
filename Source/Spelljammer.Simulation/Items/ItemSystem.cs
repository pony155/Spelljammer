using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

/// <summary>Atomic item, inventory, and equipment-loadout mutations.</summary>
public static class ItemSystem
{
    public static ItemSystemResult Create(ItemSystemState candidate, IItemDefinitionCatalog catalog)
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
        IItemDefinitionCatalog catalog)
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

        if (!catalog.TryGetItem(item.DefinitionId, out ItemDefinition? definition))
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
        IItemDefinitionCatalog catalog)
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
        IItemDefinitionCatalog catalog)
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

        if (!catalog.TryGetItem(item.DefinitionId, out ItemDefinition? definition))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.DefinitionMissing);
        }

        InventoryContainer destinationContainer = destination!;
        long destinationWeight = GetWeight(state, destinationContainer, catalog);
        if (GetEntryCount(destinationContainer) >= destinationContainer.MaximumEntries ||
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

    public static ItemSystemResult AddStack(
        ItemSystemState state,
        InventoryContainerId containerId,
        InventoryEntryId entryId,
        ContentId definitionId,
        int quantity,
        IItemDefinitionCatalog catalog)
    {
        if (!Validate(state, catalog, out string rejection))
        {
            return ItemSystemResult.Rejected(state, rejection);
        }

        if (!entryId.IsValid || state.InventoryEntries.Any(value => value.EntryId == entryId))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.EntryAlreadyExists);
        }

        if (!TryFindContainer(state, containerId, out InventoryContainer? container))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.ContainerMissing);
        }

        if (!catalog.TryGetItem(definitionId, out ItemDefinition? definition))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.DefinitionMissing);
        }

        if (definition!.MaximumStackSize <= 1)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.StackRequired);
        }

        if (quantity <= 0)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.QuantityInvalid);
        }

        if (quantity > definition.MaximumStackSize)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.StackLimitExceeded);
        }

        InventoryContainer targetContainer = container!;
        long addedWeight = (long)definition.WeightHundredthsOfPound * quantity;
        if (GetEntryCount(targetContainer) >= targetContainer.MaximumEntries ||
            GetWeight(state, targetContainer, catalog) + addedWeight > targetContainer.MaximumWeightHundredthsOfPound)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.CapacityExceeded);
        }

        InventoryEntry entry = new(entryId, containerId, new ItemStack(definitionId, quantity));
        ItemSystemState candidate = state with
        {
            InventoryEntries = state.InventoryEntries.Add(entry),
            InventoryContainers = state.InventoryContainers.Select(value => value.ContainerId == containerId
                ? value with { InventoryEntryIds = value.InventoryEntryIds.Add(entryId) }
                : value).ToImmutableArray(),
        };
        return Create(candidate, catalog);
    }

    public static ItemSystemResult SplitStack(
        ItemSystemState state,
        InventoryEntryId sourceEntryId,
        InventoryEntryId newEntryId,
        int quantity,
        IItemDefinitionCatalog catalog)
    {
        if (!Validate(state, catalog, out string rejection))
        {
            return ItemSystemResult.Rejected(state, rejection);
        }

        if (!TryFindEntry(state, sourceEntryId, out InventoryEntry? source))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.EntryMissing);
        }

        if (!newEntryId.IsValid || state.InventoryEntries.Any(value => value.EntryId == newEntryId))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.EntryAlreadyExists);
        }

        if (quantity <= 0 || quantity >= source!.Stack.Quantity)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.QuantityInvalid);
        }

        InventoryContainer container = state.InventoryContainers.Single(value => value.ContainerId == source.OwnerContainerId);
        if (GetEntryCount(container) >= container.MaximumEntries)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.CapacityExceeded);
        }

        InventoryEntry retained = source with { Stack = source.Stack with { Quantity = source.Stack.Quantity - quantity } };
        InventoryEntry split = new(newEntryId, source.OwnerContainerId, source.Stack with { Quantity = quantity });
        ItemSystemState candidate = state with
        {
            InventoryEntries = state.InventoryEntries
                .Select(value => value.EntryId == sourceEntryId ? retained : value).Append(split).ToImmutableArray(),
            InventoryContainers = state.InventoryContainers.Select(value => value.ContainerId == container.ContainerId
                ? value with { InventoryEntryIds = value.InventoryEntryIds.Add(newEntryId) }
                : value).ToImmutableArray(),
        };
        return Create(candidate, catalog);
    }

    public static ItemSystemResult MergeStack(
        ItemSystemState state,
        InventoryEntryId sourceEntryId,
        InventoryEntryId destinationEntryId,
        IItemDefinitionCatalog catalog)
    {
        if (!Validate(state, catalog, out string rejection))
        {
            return ItemSystemResult.Rejected(state, rejection);
        }

        if (sourceEntryId == destinationEntryId ||
            !TryFindEntry(state, sourceEntryId, out InventoryEntry? source) ||
            !TryFindEntry(state, destinationEntryId, out InventoryEntry? destination))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.EntryMissing);
        }

        if (source!.OwnerContainerId != destination!.OwnerContainerId ||
            source.Stack.DefinitionId != destination.Stack.DefinitionId)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.DefinitionMismatch);
        }

        catalog.TryGetItem(source.Stack.DefinitionId, out ItemDefinition? definition);
        long combined = (long)source.Stack.Quantity + destination.Stack.Quantity;
        if (combined > definition!.MaximumStackSize)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.StackLimitExceeded);
        }

        InventoryEntry merged = destination with { Stack = destination.Stack with { Quantity = (int)combined } };
        ItemSystemState candidate = state with
        {
            InventoryEntries = state.InventoryEntries.Where(value => value.EntryId != sourceEntryId)
                .Select(value => value.EntryId == destinationEntryId ? merged : value).ToImmutableArray(),
            InventoryContainers = state.InventoryContainers.Select(value => value.ContainerId == source.OwnerContainerId
                ? value with { InventoryEntryIds = value.InventoryEntryIds.Remove(sourceEntryId) }
                : value).ToImmutableArray(),
        };
        return Create(candidate, catalog);
    }

    public static ItemSystemResult Consume(
        ItemSystemState state,
        InventoryEntryId entryId,
        int quantity,
        IItemDefinitionCatalog catalog)
    {
        if (!Validate(state, catalog, out string rejection))
        {
            return ItemSystemResult.Rejected(state, rejection);
        }

        if (!TryFindEntry(state, entryId, out InventoryEntry? entry))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.EntryMissing);
        }

        if (quantity <= 0 || quantity > entry!.Stack.Quantity)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.QuantityInvalid);
        }

        bool remove = quantity == entry.Stack.Quantity;
        ItemSystemState candidate = state with
        {
            InventoryEntries = remove
                ? state.InventoryEntries.Remove(entry)
                : state.InventoryEntries.Select(value => value.EntryId == entryId
                    ? value with { Stack = value.Stack with { Quantity = value.Stack.Quantity - quantity } }
                    : value).ToImmutableArray(),
            InventoryContainers = remove
                ? state.InventoryContainers.Select(value => value.ContainerId == entry.OwnerContainerId
                    ? value with { InventoryEntryIds = value.InventoryEntryIds.Remove(entryId) }
                    : value).ToImmutableArray()
                : state.InventoryContainers,
        };
        return Create(candidate, catalog);
    }

    public static ItemSystemResult TransferStack(
        ItemSystemState state,
        InventoryEntryId entryId,
        InventoryContainerId destinationContainerId,
        IItemDefinitionCatalog catalog) =>
        TransferStackCore(state, entryId, destinationContainerId, null, null, catalog);

    public static ItemSystemResult TransferStack(
        ItemSystemState state,
        InventoryEntryId sourceEntryId,
        InventoryContainerId destinationContainerId,
        int quantity,
        InventoryEntryId destinationEntryId,
        IItemDefinitionCatalog catalog) =>
        TransferStackCore(state, sourceEntryId, destinationContainerId, quantity, destinationEntryId, catalog);

    private static ItemSystemResult TransferStackCore(
        ItemSystemState state,
        InventoryEntryId sourceEntryId,
        InventoryContainerId destinationContainerId,
        int? requestedQuantity,
        InventoryEntryId? destinationEntryId,
        IItemDefinitionCatalog catalog)
    {
        if (!Validate(state, catalog, out string rejection))
        {
            return ItemSystemResult.Rejected(state, rejection);
        }

        if (!TryFindEntry(state, sourceEntryId, out InventoryEntry? source))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.EntryMissing);
        }

        if (!TryFindContainer(state, destinationContainerId, out InventoryContainer? destination))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.ContainerMissing);
        }

        int quantity = requestedQuantity ?? source!.Stack.Quantity;
        bool wholeStack = quantity == source!.Stack.Quantity;
        if (quantity <= 0 || quantity > source.Stack.Quantity ||
            (!wholeStack && (destinationEntryId is not InventoryEntryId newId || !newId.IsValid)))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.QuantityInvalid);
        }

        if (!wholeStack && state.InventoryEntries.Any(value => value.EntryId == destinationEntryId!.Value))
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.EntryAlreadyExists);
        }

        if (source.OwnerContainerId == destinationContainerId)
        {
            return wholeStack
                ? ItemSystemResult.AcceptedState(state)
                : SplitStack(state, sourceEntryId, destinationEntryId!.Value, quantity, catalog);
        }

        InventoryContainer targetContainer = destination!;
        catalog.TryGetItem(source.Stack.DefinitionId, out ItemDefinition? definition);
        long addedWeight = (long)definition!.WeightHundredthsOfPound * quantity;
        if (GetEntryCount(targetContainer) >= targetContainer.MaximumEntries ||
            GetWeight(state, targetContainer, catalog) + addedWeight > targetContainer.MaximumWeightHundredthsOfPound)
        {
            return ItemSystemResult.Rejected(state, ItemRejectionCodes.CapacityExceeded);
        }

        InventoryEntryId movedId = wholeStack ? sourceEntryId : destinationEntryId!.Value;
        InventoryEntry moved = new(movedId, destinationContainerId, source.Stack with { Quantity = quantity });
        ItemSystemState candidate = state with
        {
            InventoryEntries = wholeStack
                ? state.InventoryEntries.Select(value => value.EntryId == sourceEntryId ? moved : value).ToImmutableArray()
                : state.InventoryEntries.Select(value => value.EntryId == sourceEntryId
                    ? value with { Stack = value.Stack with { Quantity = value.Stack.Quantity - quantity } }
                    : value).Append(moved).ToImmutableArray(),
            InventoryContainers = state.InventoryContainers.Select(value => value.ContainerId switch
            {
                var id when id == source.OwnerContainerId => value with
                {
                    InventoryEntryIds = wholeStack ? value.InventoryEntryIds.Remove(sourceEntryId) : value.InventoryEntryIds,
                },
                var id when id == destinationContainerId => value with
                {
                    InventoryEntryIds = value.InventoryEntryIds.Add(movedId),
                },
                _ => value,
            }).ToImmutableArray(),
        };
        return Create(candidate, catalog);
    }

    private static bool Validate(ItemSystemState state, IItemDefinitionCatalog catalog, out string rejection)
    {
        if (state.ItemInstances.Select(value => value.InstanceId).Distinct().Count() != state.ItemInstances.Length ||
            state.InventoryEntries.Select(value => value.EntryId).Distinct().Count() != state.InventoryEntries.Length ||
            state.InventoryContainers.Select(value => value.ContainerId).Distinct().Count() != state.InventoryContainers.Length ||
            state.EquipmentLoadouts.Select(value => value.OwnerId).Distinct().Count() != state.EquipmentLoadouts.Length)
        {
            rejection = ItemRejectionCodes.InvalidState;
            return false;
        }

        foreach (InventoryContainer container in state.InventoryContainers)
        {
            if (!container.ContainerId.IsValid || !container.OwnerId.IsValid || container.MaximumWeightHundredthsOfPound < 0 ||
                container.MaximumEntries < 0 || GetEntryCount(container) > container.MaximumEntries ||
                container.ItemInstanceIds.Distinct().Count() != container.ItemInstanceIds.Length ||
                container.InventoryEntryIds.Distinct().Count() != container.InventoryEntryIds.Length ||
                container.ItemInstanceIds.Any(id => !TryFindItem(state, id, out ItemInstance? item) ||
                    item!.OwnerContainerId != container.ContainerId) ||
                container.InventoryEntryIds.Any(id => !TryFindEntry(state, id, out InventoryEntry? entry) ||
                    entry!.OwnerContainerId != container.ContainerId) ||
                GetWeight(state, container, catalog) > container.MaximumWeightHundredthsOfPound)
            {
                rejection = ItemRejectionCodes.InvalidState;
                return false;
            }
        }

        foreach (ItemInstance item in state.ItemInstances)
        {
            if (!item.InstanceId.IsValid || !item.DefinitionId.IsValid || !item.OwnerContainerId.IsValid || item.StateVersion < 1 ||
                item.CurrentDurability < 0 || !catalog.TryGetItem(item.DefinitionId, out ItemDefinition? definition) ||
                definition!.MaximumStackSize != 1)
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

            try
            {
                switch (definition)
                {
                    case MeleeWeaponDefinition melee when item.MeleeWeaponState is not null && item.RangedWeaponState is null:
                        item.MeleeWeaponState.Validate(melee);
                        if (item.CurrentDurability != item.MeleeWeaponState.CurrentDurability)
                        {
                            throw new InvalidOperationException();
                        }
                        break;
                    case RangedWeaponDefinition ranged when item.RangedWeaponState is not null && item.MeleeWeaponState is null &&
                        catalog is ICharacterContentCatalog characterCatalog:
                        item.RangedWeaponState.Validate(ranged, characterCatalog);
                        if (item.CurrentDurability != item.RangedWeaponState.CurrentDurability)
                        {
                            throw new InvalidOperationException();
                        }
                        break;
                    case MeleeWeaponDefinition or RangedWeaponDefinition:
                        throw new InvalidOperationException();
                    default:
                        if (item.MeleeWeaponState is not null || item.RangedWeaponState is not null)
                        {
                            throw new InvalidOperationException();
                        }
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                rejection = ItemRejectionCodes.InvalidState;
                return false;
            }
        }

        foreach (InventoryEntry entry in state.InventoryEntries)
        {
            if (!entry.EntryId.IsValid || !entry.OwnerContainerId.IsValid || !entry.Stack.DefinitionId.IsValid ||
                !catalog.TryGetItem(entry.Stack.DefinitionId, out ItemDefinition? definition) ||
                definition!.MaximumStackSize <= 1 || entry.Stack.Quantity <= 0 ||
                entry.Stack.Quantity > definition.MaximumStackSize ||
                !TryFindContainer(state, entry.OwnerContainerId, out InventoryContainer? owner) ||
                owner!.InventoryEntryIds.Count(value => value == entry.EntryId) != 1)
            {
                rejection = ItemRejectionCodes.InvalidState;
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
                    !catalog.TryGetItem(item.DefinitionId, out ItemDefinition? definition) || definition is not EquipmentDefinition equipment ||
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
        InventoryEntries = state.InventoryEntries.OrderBy(value => value.EntryId.Value).ToImmutableArray(),
        InventoryContainers = state.InventoryContainers.Select(value => value with
        {
            ItemInstanceIds = value.ItemInstanceIds.OrderBy(id => id.Value).ToImmutableArray(),
            InventoryEntryIds = value.InventoryEntryIds.OrderBy(id => id.Value).ToImmutableArray(),
        }).OrderBy(value => value.ContainerId.Value).ToImmutableArray(),
        EquipmentLoadouts = state.EquipmentLoadouts.Select(value => value with
        {
            SlotAssignments = value.SlotAssignments.OrderBy(assignment => assignment.SlotId).ToImmutableArray(),
        }).OrderBy(value => value.OwnerId).ToImmutableArray(),
    };

    private static long GetWeight(ItemSystemState state, InventoryContainer container, IItemDefinitionCatalog catalog)
    {
        long total = 0;
        foreach (ItemInstanceId instanceId in container.ItemInstanceIds)
        {
            if (!TryFindItem(state, instanceId, out ItemInstance? item) ||
                !catalog.TryGetItem(item!.DefinitionId, out ItemDefinition? definition))
            {
                return long.MaxValue;
            }

            total = AddWeight(total, definition!.WeightHundredthsOfPound);
        }

        foreach (InventoryEntryId entryId in container.InventoryEntryIds)
        {
            if (!TryFindEntry(state, entryId, out InventoryEntry? entry) ||
                !catalog.TryGetItem(entry!.Stack.DefinitionId, out ItemDefinition? definition))
            {
                return long.MaxValue;
            }

            total = AddWeight(total, (long)definition!.WeightHundredthsOfPound * entry.Stack.Quantity);
        }

        return total;
    }

    private static long AddWeight(long total, long addition) =>
        addition < 0 || total > long.MaxValue - addition ? long.MaxValue : total + addition;

    private static long GetEntryCount(InventoryContainer container) =>
        (long)container.ItemInstanceIds.Length + container.InventoryEntryIds.Length;

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

    private static bool TryFindEntry(ItemSystemState state, InventoryEntryId entryId, out InventoryEntry? entry)
    {
        entry = state.InventoryEntries.SingleOrDefault(value => value.EntryId == entryId);
        return entry is not null;
    }
}
