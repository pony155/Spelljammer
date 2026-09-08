using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

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
