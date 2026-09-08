namespace Spelljammer.Simulation.Items;

/// <summary>
/// Centralizes stable rejection codes returned by inventory and equipment operations.
/// </summary>
/// <remarks>
/// Code flow: Item-system validation selects one code without mutating state, callers inspect the result, and persistence or UI layers may record or localize the stable code.
/// </remarks>
public static class ItemRejectionCodes
{
    public const string None = "none";
    public const string InvalidState = "item.invalid-state";
    public const string ContainerMissing = "item.container-missing";
    public const string ItemMissing = "item.missing";
    public const string EntryMissing = "item.entry-missing";
    public const string EntryAlreadyExists = "item.entry-already-exists";
    public const string DefinitionMissing = "item.definition-missing";
    public const string StackRequired = "item.stack-required";
    public const string QuantityInvalid = "item.quantity-invalid";
    public const string StackLimitExceeded = "item.stack-limit-exceeded";
    public const string DefinitionMismatch = "item.definition-mismatch";
    public const string OwnershipMismatch = "item.ownership-mismatch";
    public const string CapacityExceeded = "item.capacity-exceeded";
    public const string EquipmentRequired = "item.equipment-required";
    public const string SlotUnknown = "item.slot-unknown";
    public const string SlotConflict = "item.slot-conflict";
    public const string LoadoutMissing = "item.loadout-missing";
}
