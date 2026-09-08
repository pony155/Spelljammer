using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Statuses;

public readonly record struct StatusInstanceId(Guid Value) : IComparable<StatusInstanceId>
{
    public bool IsValid => Value != Guid.Empty;
    public int CompareTo(StatusInstanceId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("N");
}

public sealed record StatusInstance(
    StatusInstanceId InstanceId,
    StatusId DefinitionId,
    int DefinitionRevision,
    ContentId SourceId,
    ContentId TargetId,
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
