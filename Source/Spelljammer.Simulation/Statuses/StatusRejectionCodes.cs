namespace Spelljammer.Simulation.Statuses;

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
