using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

public enum WorldCommandKind : byte
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

public sealed record WorldCommand(
    ContentId Id,
    WorldCommandKind Kind,
    long TargetTick,
    int Priority,
    ContentId IssuerId,
    ContentId TargetId,
    FixedVector2 Vector,
    int Amount,
    ContentId? OptionId,
    ulong Sequence);

public sealed record WorldCommandLogEntry(long SubmittedTick, WorldCommand Command, long? CancelledTick);

public sealed record WorldCommandResult(World World, bool Accepted, string RejectionCode);
