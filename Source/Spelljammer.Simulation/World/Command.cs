using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

public enum VoyageCommandKind : byte
{
    Scan,
    Course,
    Thrust,
    Turn,
    Brake,
    Intercept,
    Fire,
    Ram,
    RaiseShield,
    LowerShield,
    Defend,
    DamageControl,
    Signal,
    Retreat,
    PersonalMove,
    PersonalDefend,
    PersonalReserveReaction,
    PersonalMelee,
    PersonalRanged,
    PersonalSpell,
    PersonalPsionic,
    PersonalEngineering,
    PersonalMedicine,
    PersonalInteract,
    PersonalSurrender,
    PersonalRetreat,
    PersonalEndActivation,
}

public sealed record VoyageCommand(
    ContentId Id,
    VoyageCommandKind Kind,
    long TargetTick,
    int Priority,
    ContentId IssuerId,
    ContentId TargetId,
    FixedVector2 Vector,
    int Amount,
    ContentId? OptionId,
    ulong Sequence);

public sealed record VoyageCommandLogEntry(long SubmittedTick, VoyageCommand Command, long? CancelledTick);

public sealed record VoyageCommandResult(VoyageWorld World, bool Accepted, string RejectionCode);
