using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

/// <summary>Common base for equipment that provides weapon actions.</summary>
/// <remarks>
/// Code flow: Content compilation supplies shared item and slot data, concrete melee or ranged definitions add action rules, and combat systems resolve those definitions from equipped instances.
/// </remarks>
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
