using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Items;

/// <summary>
/// Defines non-weapon, non-armor equipment and the passive effects it grants.
/// </summary>
/// <remarks>
/// Code flow: Content compilation links equipment slots and effect applications, the item catalog publishes the definition, and equipped gear contributes those effects to its owner.
/// </remarks>
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
    ImmutableArray<EffectApplicationDefinition> Effects)
    : EquipmentDefinition(Id, SchemaVersion, Revision, NameKey, DescriptionKey,
        WeightHundredthsOfPound, Value, Tags, OccupiedSlotIds)
{
    public ImmutableArray<EffectId> EffectIds => [.. Effects.Select(value => value.EffectId)];
}
