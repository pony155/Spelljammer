using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

/// <summary>
/// Defines persistent character injuries and their severity and stabilization state.
/// </summary>
/// <remarks>
/// Code flow: Combat or effects add injury records to battle units, stabilization changes their state, and battle-unit projection commits surviving injuries back to the persistent character.
/// </remarks>
public enum InjurySeverity : byte
{
    Minor,
    Serious,
    Incapacitating,
}

/// <summary>A persistent character injury shared with encounter battle-unit projections.</summary>
public sealed record InjuryState(ContentId Id, InjurySeverity Severity, bool Stabilized);
