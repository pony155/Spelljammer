using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.World;

namespace Spelljammer.Simulation.Combat;

/// <summary>
/// Immutable input supplied to the personal-combat authority when a scheduled combat command commits.
/// </summary>
/// <remarks>
/// Code flow: World command commitment builds a context from encounter-owned units, the selected action system returns one atomic resolution, and the world validates and installs the replacement units and statuses.
/// </remarks>
public sealed record PersonalCombatContext(
    WorldCommand Command,
    PersonalEncounterState Encounter,
    BattleUnitState Actor,
    BattleUnitState Target,
    long Tick,
    ulong WorldSeed,
    ulong RandomSequence);

/// <summary>
/// Atomic actor/target state produced by a melee, ranged, spell, or psionic action system.
/// </summary>
public sealed record PersonalCombatResolution(
    bool Accepted,
    string RejectionCode,
    BattleUnitState Actor,
    BattleUnitState Target,
    int EventAmount,
    ContentId? DamagedObjectId = null)
{
    public static PersonalCombatResolution Rejected(
        BattleUnitState actor,
        BattleUnitState target,
        string rejectionCode) =>
        new(false, rejectionCode, actor, target, 0);
}

/// <summary>
/// Resolves personal combat commands through the typed weapon, spell, psionic, status, and effect systems.
/// <see cref="CombatSystem"/> is the standard coordinator; World schedules and commits the returned
/// transaction but does not calculate combat damage.
/// </summary>
public interface IPersonalCombatResolver
{
    PersonalCombatResolution Resolve(PersonalCombatContext context);
}
