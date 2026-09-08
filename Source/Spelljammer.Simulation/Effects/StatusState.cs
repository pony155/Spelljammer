using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Effects;

/// <summary>
/// Defines stable status identities, targets, active instances, and immutable status collections.
/// </summary>
/// <remarks>
/// Code flow: Status application derives an instance ID and stores definition-backed duration, stack, potency, source, and target data that queries and tick processing consume.
/// </remarks>
public readonly record struct StatusInstanceId(Guid Value) : IComparable<StatusInstanceId>
{
    public bool IsValid => Value != Guid.Empty;
    public int CompareTo(StatusInstanceId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("N");
}

/// <summary>Stable identity of the character, battle unit, ship, or object that owns a Status.</summary>
public readonly record struct StatusTargetId(ContentId Value) : IComparable<StatusTargetId>
{
    public bool IsValid => Value.IsValid;
    public int CompareTo(StatusTargetId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();
}

public sealed record StatusInstance(
    StatusInstanceId InstanceId,
    StatusId DefinitionId,
    int DefinitionRevision,
    ContentId SourceId,
    StatusTargetId TargetId,
    int RemainingDuration,
    int Stacks,
    int Potency);

public sealed record StatusState(ImmutableArray<StatusInstance> Instances)
{
    public static StatusState Empty { get; } = new([]);
}

public interface IStatusDefinitionCatalog
{
    bool TryGetStatus(StatusId id, out StatusDefinition? definition);
    bool TryGetEffect(EffectId id, out EffectDefinition? definition);
}
