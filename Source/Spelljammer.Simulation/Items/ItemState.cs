using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

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

public readonly record struct InventoryEntryId(Guid Value)
{
    public bool IsValid => Value != Guid.Empty;
    public override string ToString() => Value.ToString("N");
}

/// <summary>A quantity of one stackable item definition.</summary>
public sealed record ItemStack(ContentId DefinitionId, int Quantity);

/// <summary>A stable inventory entry which owns one stack in one container.</summary>
public sealed record InventoryEntry(
    InventoryEntryId EntryId,
    InventoryContainerId OwnerContainerId,
    ItemStack Stack);

/// <summary>Persistent state belonging to one non-stackable item.</summary>
public sealed record ItemInstance(
    ItemInstanceId InstanceId,
    ContentId DefinitionId,
    InventoryContainerId OwnerContainerId,
    int? CurrentDurability,
    ContentId? QualityId,
    string? CustomName,
    int StateVersion,
    MeleeWeaponState? MeleeWeaponState = null,
    RangedWeaponState? RangedWeaponState = null);

/// <summary>A bounded container which owns distinct instances and stack entries.</summary>
public sealed record InventoryContainer(
    InventoryContainerId ContainerId,
    ContentId OwnerId,
    int MaximumWeightHundredthsOfPound,
    int MaximumEntries,
    ImmutableArray<ItemInstanceId> ItemInstanceIds)
{
    public ImmutableArray<InventoryEntryId> InventoryEntryIds { get; init; } = [];
}

public sealed record SlotAssignment(ContentId SlotId, ItemInstanceId ItemInstanceId);

/// <summary>The equipment slots currently occupied by one owner.</summary>
public sealed record EquipmentLoadout(ContentId OwnerId, ImmutableArray<SlotAssignment> SlotAssignments);

/// <summary>Whole immutable item state, used as the transaction publication boundary.</summary>
public sealed record ItemSystemState(
    ImmutableArray<ItemInstance> ItemInstances,
    ImmutableArray<InventoryContainer> InventoryContainers,
    ImmutableArray<EquipmentLoadout> EquipmentLoadouts)
{
    public ImmutableArray<InventoryEntry> InventoryEntries { get; init; } = [];
}

public sealed record ItemSystemResult(ItemSystemState State, bool Accepted, string RejectionCode)
{
    public static ItemSystemResult Rejected(ItemSystemState state, string code) => new(state, false, code);
    public static ItemSystemResult AcceptedState(ItemSystemState state) => new(state, true, ItemRejectionCodes.None);
}
