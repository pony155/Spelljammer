using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

public enum InjurySeverity : byte
{
    Minor,
    Serious,
    Incapacitating,
}

/// <summary>A persistent character injury shared with encounter battle-unit projections.</summary>
public sealed record InjuryState(ContentId Id, InjurySeverity Severity, bool Stabilized);
