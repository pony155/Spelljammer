using System.Collections.Frozen;
using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

/// <summary>
/// Defines the shared item taxonomy, base definition contract, and immutable item catalog.
/// </summary>
/// <remarks>
/// Code flow: Content definitions are validated and indexed by stable ID, item operations resolve their category and stack limits through the catalog, and incompatible kinds are rejected.
/// </remarks>
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

/// <summary>Read-only definition lookup used by item-state validation.</summary>
public interface IItemDefinitionCatalog
{
    bool TryGetItem(ContentId id, out ItemDefinition? definition);
    bool IsKnownEquipmentSlot(ContentId id);
}

/// <summary>Standalone immutable item catalog used by focused simulation scenarios.</summary>
public sealed class ItemDefinitionCatalog : IItemDefinitionCatalog
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

    public bool TryGetItem(ContentId id, out ItemDefinition? definition) =>
        definitions.TryGetValue(id, out definition);

    public bool IsKnownEquipmentSlot(ContentId id) => equipmentSlots.Contains(id);
}
