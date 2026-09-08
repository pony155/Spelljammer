namespace Spelljammer.Simulation.Items;

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
