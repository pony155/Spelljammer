using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

/// <summary>Base definition for an item which may occupy authored equipment slots.</summary>
/// <remarks>
/// Code flow: Derived definitions declare compatible stable slot IDs, item-system equip requests compare those IDs with the target loadout, and accepted assignments reference the item instance.
/// </remarks>
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
