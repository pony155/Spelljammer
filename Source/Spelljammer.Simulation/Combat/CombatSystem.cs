using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.World;

namespace Spelljammer.Simulation.Combat;

/// <summary>Stable rejection codes owned by character-combat orchestration.</summary>
/// <remarks>
/// Code flow: The coordinator resolves an action definition and specialized action system, delegates deterministic resolution without owning unit state, validates returned identities, and publishes one encounter-level result.
/// </remarks>
public static class CombatRejectionCodes
{
    public const string None = ActionRejectionCodes.None;
    public const string ActionUnknown = ActionRejectionCodes.ActionUnknown;
    public const string ActorCannotAct = ActionRejectionCodes.ActorCannotAct;
    public const string TargetIllegal = ActionRejectionCodes.TargetIllegal;
    public const string ActionUnavailable = "command.personal-combat-action-unavailable";
    public const string ResolutionFailed = "command.personal-combat-resolution-failed";
    public const string ResultInvalid = "command.personal-combat-result-invalid";
}

/// <summary>
/// Resolves one character-combat command kind. Implementations adapt melee, ranged,
/// spell, or psionic domain rules without moving those rules into <see cref="CombatSystem"/>.
/// </summary>
public interface ICharacterCombatActionSystem
{
    WorldCommandKind Kind { get; }

    PersonalCombatResolution Resolve(PersonalCombatContext context);
}

/// <summary>
/// Deterministic character-combat coordinator used by <see cref="World"/>.
/// It routes commands to focused action systems and validates their replacement
/// actor states before the encounter publishes the transaction.
/// </summary>
public sealed class CombatSystem : IPersonalCombatResolver
{
    private static readonly ImmutableHashSet<WorldCommandKind> SupportedWorldCommandKinds =
    [
        WorldCommandKind.PersonalMelee,
        WorldCommandKind.PersonalRanged,
        WorldCommandKind.PersonalSpell,
        WorldCommandKind.PersonalPsionic,
    ];

    private readonly ImmutableDictionary<WorldCommandKind, ICharacterCombatActionSystem> actionSystems;

    public CombatSystem(IEnumerable<ICharacterCombatActionSystem> actionSystems)
    {
        ArgumentNullException.ThrowIfNull(actionSystems);
        ICharacterCombatActionSystem[] registrations = [.. actionSystems];
        if (registrations.Length > SupportedWorldCommandKinds.Count ||
            registrations.Any(value => value is null || !SupportedWorldCommandKinds.Contains(value.Kind)) ||
            registrations.Select(value => value.Kind).Distinct().Count() != registrations.Length)
        {
            throw new ArgumentException("Character combat action-system registrations are invalid.", nameof(actionSystems));
        }

        this.actionSystems = registrations.ToImmutableDictionary(value => value.Kind);
    }

    public PersonalCombatResolution Resolve(PersonalCombatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!SupportedWorldCommandKinds.Contains(context.Command.Kind))
        {
            return Reject(context, CombatRejectionCodes.ActionUnknown);
        }

        if (!HasStableIdentity(context.Actor, context.Command.IssuerId) ||
            !context.Encounter.Units.TryGetValue(context.Actor.Id, out BattleUnitState? encounterUnit) ||
            !HasStableIdentity(encounterUnit, context.Actor) ||
            context.Encounter.CleanedUp || context.Actor.IsIncapacitated || context.Actor.Surrendered || context.Actor.Prisoner)
        {
            return Reject(context, CombatRejectionCodes.ActorCannotAct);
        }

        if (context.Actor.Id == context.Target.Id ||
            !HasStableIdentity(context.Target, context.Command.TargetId) ||
            !context.Encounter.Units.TryGetValue(context.Target.Id, out BattleUnitState? encounterTarget) ||
            !HasStableIdentity(encounterTarget, context.Target) || context.Target.Prisoner)
        {
            return Reject(context, CombatRejectionCodes.TargetIllegal);
        }

        if (!actionSystems.TryGetValue(context.Command.Kind, out ICharacterCombatActionSystem? actionSystem))
        {
            return Reject(context, CombatRejectionCodes.ActionUnavailable);
        }

        PersonalCombatResolution resolution;
        try
        {
            resolution = actionSystem.Resolve(context);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Reject(context, CombatRejectionCodes.ResolutionFailed);
        }

        if (resolution is null)
        {
            return Reject(context, CombatRejectionCodes.ResultInvalid);
        }

        if (!resolution.Accepted)
        {
            return Reject(
                context,
                string.IsNullOrWhiteSpace(resolution.RejectionCode)
                    ? CombatRejectionCodes.ResultInvalid
                    : resolution.RejectionCode);
        }

        return IsValid(context, resolution)
            ? resolution
            : Reject(context, CombatRejectionCodes.ResultInvalid);
    }

    private static bool IsValid(PersonalCombatContext context, PersonalCombatResolution resolution) =>
        string.IsNullOrEmpty(resolution.RejectionCode) &&
        HasStableIdentity(context.Actor, resolution.Actor) &&
        HasStableIdentity(context.Target, resolution.Target) &&
        resolution.Actor.ActionPoints >= 0 && resolution.Actor.ActionPoints <= context.Actor.ActionPoints &&
        resolution.Target.ActionPoints >= 0 && resolution.Target.ActionPoints <= context.Target.ActionPoints &&
        resolution.Actor.Statuses.Instances.Length <= PersonalEncounterState.MaximumStatusesPerUnit &&
        resolution.Target.Statuses.Instances.Length <= PersonalEncounterState.MaximumStatusesPerUnit &&
        !resolution.Actor.Items.ItemInstances.IsDefault && !resolution.Actor.Items.InventoryEntries.IsDefault &&
        !resolution.Target.Items.ItemInstances.IsDefault && !resolution.Target.Items.InventoryEntries.IsDefault &&
        resolution.EventAmount >= 0 &&
        (resolution.DamagedObjectId is not ContentId damagedObjectId || damagedObjectId.IsValid);

    private static bool HasStableIdentity(BattleUnitState unit, ContentId expectedId) =>
        unit.Id.Value == expectedId && unit.Id.IsValid && unit.TeamId.IsValid && unit.CellId.IsValid;

    private static bool HasStableIdentity(BattleUnitState original, BattleUnitState replacement) =>
        replacement.Id == original.Id && replacement.TeamId == original.TeamId &&
        replacement.CharacterId == original.CharacterId && replacement.CellId == original.CellId;

    private static PersonalCombatResolution Reject(PersonalCombatContext context, string rejectionCode) =>
        PersonalCombatResolution.Rejected(context.Actor, context.Target, rejectionCode);
}
