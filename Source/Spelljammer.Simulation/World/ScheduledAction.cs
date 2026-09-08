using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

public enum ScheduledActionPhase : byte
{
    Declared,
    Validated,
    Reserved,
    Preparing,
    Committed,
    Recovering,
    Completed,
    Interrupted,
}

public sealed record ScheduledAction(
    Command Command,
    ScheduledActionPhase Phase,
    long CommitTick,
    long RecoverTick,
    ResourceId? ReservedResourceId,
    int ReservedAmount,
    ImmutableArray<ScheduledActionPhase> History);
