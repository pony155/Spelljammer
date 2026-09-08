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

    private static bool Validate(ItemSystemState state, IItemDefinitionCatalog catalog, out string rejection)
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
                item.CurrentDurability < 0 || !catalog.TryGetItem(item.DefinitionId, out ItemDefinition? definition))
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
        InventoryContainers = state.InventoryContainers.OrderBy(value => value.ContainerId.Value).ToImmutableArray(),
        EquipmentLoadouts = state.EquipmentLoadouts.Select(value => value with
        {
            SlotAssignments = value.SlotAssignments.OrderBy(assignment => assignment.SlotId).ToImmutableArray(),
        }).OrderBy(value => value.OwnerId).ToImmutableArray(),
    };

    private static long GetWeight(ItemSystemState state, InventoryContainer container, IItemDefinitionCatalog catalog) =>
        container.ItemInstanceIds.Sum(instanceId => TryFindItem(state, instanceId, out ItemInstance? item) &&
            catalog.TryGetItem(item!.DefinitionId, out ItemDefinition? definition)
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
