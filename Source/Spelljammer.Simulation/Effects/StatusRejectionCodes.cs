namespace Spelljammer.Simulation.Effects;

/// <summary>
/// Centralizes stable rejection codes emitted by status operations.
/// </summary>
/// <remarks>
/// Code flow: Status validation chooses one code while preserving the original state, and callers use that code for diagnostics, events, or localized presentation.
/// </remarks>
public static class StatusRejectionCodes
{
    public const string None = "";
    public const string InvalidState = "status.invalid-state";
    public const string DefinitionMissing = "status.definition-missing";
    public const string InstanceInvalid = "status.instance-invalid";
    public const string AlreadyPresent = "status.already-present";
    public const string ExclusiveConflict = "status.exclusive-conflict";
    public const string CapacityExceeded = "status.capacity-exceeded";
    public const string DurationExceeded = "status.duration-exceeded";
}
