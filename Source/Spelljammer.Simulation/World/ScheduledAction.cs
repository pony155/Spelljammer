using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

/// <summary>
/// Defines delayed authoritative actions and their deterministic lifecycle phases.
/// </summary>
/// <remarks>
/// Code flow: Systems schedule an action for a future tick, advancement selects due entries in stable order, and phase transitions eventually commit, recover, complete, or cancel it.
/// </remarks>
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
    WorldCommand Command,
    ScheduledActionPhase Phase,
    long CommitTick,
    long RecoverTick,
    ResourceId? ReservedResourceId,
    int ReservedAmount,
    ImmutableArray<ScheduledActionPhase> History);
