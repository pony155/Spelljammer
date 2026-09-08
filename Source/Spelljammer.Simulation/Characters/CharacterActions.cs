using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Characters;

/// <summary>
/// Localization keys for reasons an action might be rejected during validation.
/// </summary>
public static class ActionRejectionCodes
{
    /// <summary>Action was accepted without issue.</summary>
    public const string None = "";

    /// <summary>The action ID is not recognized or does not exist.</summary>
    public const string ActionUnknown = "command.action-unknown";

    /// <summary>The actor performing the action does not exist in the current context.</summary>
    public const string ActorMissing = "command.actor-missing";

    /// <summary>The actor cannot act (incapacitated, paralyzed, or otherwise prevented from acting).</summary>
    public const string ActorCannotAct = "command.actor-cannot-act";

    /// <summary>The target of the action does not exist or cannot be found.</summary>
    public const string TargetMissing = "command.target-missing";

    /// <summary>The target is not a legal target for this action.</summary>
    public const string TargetIllegal = "command.target-illegal";

    /// <summary>The actor lacks the required access privilege to perform this action.</summary>
    public const string AccessRequired = "command.access-required";

    /// <summary>The active Feat required for this action is not known by the actor.</summary>
    public const string FeatUnknown = "command.feat-unknown";

    /// <summary>The actor's skill level is too low to perform this action.</summary>
    public const string SkillRequired = "command.skill-required";

    /// <summary>The actor's ability value is too low to perform this action.</summary>
    public const string AbilityRequired = "command.ability-required";

    /// <summary>The actor does not have the required equipment to perform this action.</summary>
    public const string EquipmentRequired = "command.equipment-required";

    /// <summary>The equipped weapon is broken and cannot perform an attack.</summary>
    public const string EquipmentBroken = "command.equipment-broken";

    /// <summary>The selected target is beyond the action's effective range.</summary>
    public const string TargetOutOfRange = "command.target-out-of-range";

    /// <summary>The ranged weapon has no compatible ammunition loaded.</summary>
    public const string AmmunitionRequired = "command.ammunition-required";

    /// <summary>The selected ammunition cannot be used by this weapon.</summary>
    public const string AmmunitionIncompatible = "command.ammunition-incompatible";

    /// <summary>The weapon cannot accept more ammunition.</summary>
    public const string MagazineFull = "command.magazine-full";

    /// <summary>The weapon must cool before it can fire again.</summary>
    public const string EquipmentOverheated = "command.equipment-overheated";

    /// <summary>The action requires a specific context that is not present.</summary>
    public const string ContextRequired = "command.context-required";

    /// <summary>The actor does not have enough of a required resource (mana, stamina, etc.) to perform this action.</summary>
    public const string ResourceInsufficient = "command.resource-insufficient";

    /// <summary>The actor's capabilities do not match the action definition's content version.</summary>
    public const string ContentMismatch = "command.content-mismatch";
}

/// <summary>
/// Defines the requirements a character must meet to perform an action.
/// </summary>
/// <remarks>
/// Action requirements include optional access privilege and active-Feat checks, mandatory skill and ability minimums,
/// and optional equipment and context requirements.
/// </remarks>
/// <param name="AccessId">The access privilege required to perform this action, if any.</param>
/// <param name="RequiredFeatId">The active Feat required, if any.</param>
/// <param name="SkillId">The skill that governs success for this action.</param>
/// <param name="MinimumSkill">The minimum skill level required to attempt this action.</param>
/// <param name="AbilityId">The ability that provides the base modifier for this action.</param>
/// <param name="MinimumAbility">The minimum ability value required to attempt this action.</param>
/// <param name="EquipmentId">The equipment that must be equipped to perform this action, if any.</param>
/// <param name="ContextId">A context requirement (e.g., must be in water, must be outdoors), if any.</param>
public sealed record ActionRequirement(
    AccessId? AccessId,
    FeatId? RequiredFeatId,
    SkillId SkillId,
    byte MinimumSkill,
    AbilityId AbilityId,
    short MinimumAbility,
    ContentId? EquipmentId,
    ContentId? ContextId);

/// <summary>
/// Represents a resource cost for performing an action.
/// </summary>
/// <remarks>
/// Actions can consume resources like mana, stamina, ammunition, etc. Each cost specifies a resource type and amount.
/// </remarks>
public sealed record ActionCost
{
    /// <summary>
    /// Initializes an action cost with the specified resource and amount.
    /// </summary>
    /// <param name="resourceId">The type of resource to consume.</param>
    /// <param name="amount">The amount of resource to consume (must be between 1 and 1,000,000).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if amount is outside the valid range.</exception>
    public ActionCost(ResourceId resourceId, int amount)
    {
        if (amount <= 0 || amount > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        ResourceId = resourceId;
        Amount = amount;
    }

    /// <summary>Gets the type of resource consumed by this cost.</summary>
    public ResourceId ResourceId { get; }

    /// <summary>Gets the amount of the resource consumed.</summary>
    public int Amount { get; }
}

/// <summary>
/// Defines an action that a character can perform with its requirements, costs, and effects.
/// </summary>
/// <remarks>
/// Actions are the building blocks of character abilities and combat moves. Each action includes difficulty,
/// success modifiers and potential Feat grants.
/// </remarks>
public sealed record ActionDefinition(
    ActionId Id,
    ContentId FormulaId,
    ActionRequirement Requirement,
    ImmutableArray<ActionCost> Costs,
    int Difficulty,
    int Modifier,
    ImmutableArray<FeatId> GrantedFeatIds);

/// <summary>
/// Describes a potential target for an action with validation status.
/// </summary>
/// <param name="Id">The ID of the target entity.</param>
/// <param name="IsPresent">Whether the target exists and is present in the encounter.</param>
/// <param name="IsLegal">Whether the target is a legal target for the action (e.g., not self-harm when prohibited).</param>
public sealed record ActionTarget(ContentId Id, bool IsPresent, bool IsLegal);

/// <summary>
/// A request to perform an action with all necessary context and randomization parameters.
/// </summary>
/// <remarks>
/// Action requests are validated against character capabilities and action definitions to determine eligibility.
/// They include randomization seeds for outcome determination.
/// </remarks>
/// <param name="ActorId">The character performing the action.</param>
/// <param name="ActionId">The action to perform.</param>
/// <param name="Target">The target of the action, if it requires a target.</param>
/// <param name="ContextIds">The set of active contexts for this action (e.g., outdoors, in water).</param>
/// <param name="RandomSeed">The random seed for outcome determination.</param>
/// <param name="RandomSequence">The sequence number for deterministic randomization.</param>
public sealed record ActionRequest(
    CharacterId ActorId,
    ActionId ActionId,
    ActionTarget? Target,
    ImmutableHashSet<ContentId> ContextIds,
    ulong RandomSeed,
    ulong RandomSequence);

/// <summary>
/// Represents a validated action with reserved resources, ready for execution.
/// </summary>
/// <remarks>
/// This record is produced during action eligibility checking and contains the character state snapshot
/// before the action executes, reserved resources, and resolved ability/skill values.
/// </remarks>
/// <param name="OriginalState">The character state before the action is executed.</param>
/// <param name="Definition">The action definition being performed.</param>
/// <param name="Request">The original action request.</param>
/// <param name="ReservedResources">Resources that will be consumed if the action executes.</param>
/// <param name="AbilityValue">The resolved ability value used as base modifier for this action.</param>
/// <param name="SkillValue">The resolved skill value for this action.</param>
public sealed record ActionReservation(
    CharacterState OriginalState,
    ActionDefinition Definition,
    ActionRequest Request,
    ImmutableDictionary<ResourceId, int> ReservedResources,
    short AbilityValue,
    byte SkillValue);

/// <summary>
/// The result of checking whether a character is eligible to perform an action.
/// </summary>
/// <remarks>
/// If eligible, contains an ActionReservation with reserved resources. If not eligible, contains a rejection code
/// and optionally the ID of the requirement that failed (e.g., the missing access or low skill).
/// </remarks>
/// <param name="Reservation">The action reservation if eligible; null if rejected.</param>
/// <param name="RejectionCode">A localization key indicating why the action was rejected (empty string if accepted).</param>
/// <param name="RelatedId">The ID of the requirement that caused rejection, if relevant (e.g., missing access ID).</param>
public sealed record ActionEligibilityResult(ActionReservation? Reservation, string RejectionCode, ContentId? RelatedId)
{
    public bool Accepted => Reservation is not null;
}

public sealed record ActionResolutionEvent(
    CharacterId ActorId,
    ActionId ActionId,
    ContentId FormulaId,
    ContentId TargetId,
    AbilityId AbilityId,
    short AbilityValue,
    SkillId SkillId,
    byte SkillValue,
    int DefinitionModifier,
    int Roll,
    int Total,
    int Difficulty,
    bool Succeeded,
    string FailureReason,
    ImmutableArray<FeatId> GrantedFeatIds);

public sealed record ActionExecutionResult(
    CharacterState State,
    bool Accepted,
    bool Succeeded,
    string RejectionCode,
    ActionResolutionEvent? Resolution);

public static class CharacterActionSystem
{
    private static readonly ContentId StandardCheckFormula = new("formula.check.standard");

    public static ActionEligibilityResult CheckEligibility(
        CharacterState? actor,
        ActionDefinition? definition,
        ActionRequest request,
        ICombatContentCatalog catalog)
    {
        if (actor is null || actor.Id != request.ActorId)
        {
            return Rejected(ActionRejectionCodes.ActorMissing, request.ActorId.Value);
        }

        if (!actor.CanAct || !StatusQueries.CanAct(actor.Statuses, catalog))
        {
            return Rejected(ActionRejectionCodes.ActorCannotAct, actor.Id.Value);
        }

        if (actor.ContentFingerprint != catalog.Fingerprint)
        {
            return Rejected(ActionRejectionCodes.ContentMismatch);
        }

        if (definition is null || definition.Id != request.ActionId)
        {
            return Rejected(ActionRejectionCodes.ActionUnknown, request.ActionId.Value);
        }

        if (definition.FormulaId != StandardCheckFormula ||
            definition.Costs.Length > 32 || definition.GrantedFeatIds.Length > CharacterCapabilities.MaximumSetEntries ||
            request.ContextIds.Count > CharacterCapabilities.MaximumSetEntries ||
            definition.Costs.Select(value => value.ResourceId).Distinct().Count() != definition.Costs.Length ||
            definition.GrantedFeatIds.Distinct().Count() != definition.GrantedFeatIds.Length ||
            definition.Difficulty is < 0 or > 10_000 || definition.Modifier is < -10_000 or > 10_000)
        {
            return Rejected(ActionRejectionCodes.ActionUnknown, request.ActionId.Value);
        }

        foreach (FeatId featId in definition.GrantedFeatIds)
        {
            if (!catalog.TryGetFeat(featId, out FeatDefinition? feat) || !feat!.CompatibleRaceIds.Contains(actor.RaceId))
            {
                return Rejected(ActionRejectionCodes.ActionUnknown, featId.Value);
            }
        }

        if (request.Target is null || !request.Target.IsPresent)
        {
            return Rejected(ActionRejectionCodes.TargetMissing);
        }

        if (!request.Target.IsLegal)
        {
            return Rejected(ActionRejectionCodes.TargetIllegal);
        }

        ActionRequirement requirement = definition.Requirement;
        if (requirement.AccessId is AccessId accessId &&
            (!actor.Capabilities.Access.Contains(accessId) ||
             !actor.Capabilities.GrantSources.Any(value => value.CapabilityId == accessId.Value)))
        {
            return Rejected(ActionRejectionCodes.AccessRequired, accessId.Value);
        }

        if (requirement.RequiredFeatId is FeatId requiredFeatId &&
            (!catalog.TryGetFeat(requiredFeatId, out FeatDefinition? requiredFeat) ||
             requiredFeat!.Activation != FeatActivation.Active ||
             !actor.Capabilities.Feats.Contains(requiredFeatId) ||
             !actor.Capabilities.GrantSources.Any(value => value.CapabilityId == requiredFeatId.Value)))
        {
            return Rejected(ActionRejectionCodes.FeatUnknown, requiredFeatId.Value);
        }

        if (!actor.Capabilities.TryGetSkill(requirement.SkillId, catalog, out byte skill, out _) ||
            skill < requirement.MinimumSkill)
        {
            return Rejected(ActionRejectionCodes.SkillRequired, requirement.SkillId.Value);
        }

        if (!actor.Capabilities.TryGetAbility(requirement.AbilityId, catalog, out short ability, out _) ||
            ability < requirement.MinimumAbility)
        {
            return Rejected(ActionRejectionCodes.AbilityRequired, requirement.AbilityId.Value);
        }

        if (requirement.EquipmentId is ContentId equipmentId && !actor.HasItemDefinition(equipmentId))
        {
            return Rejected(ActionRejectionCodes.EquipmentRequired, equipmentId);
        }

        if (requirement.ContextId is ContentId contextId && !request.ContextIds.Contains(contextId))
        {
            return Rejected(ActionRejectionCodes.ContextRequired, contextId);
        }

        ImmutableDictionary<ResourceId, int>.Builder reserved = ImmutableDictionary.CreateBuilder<ResourceId, int>();
        foreach (ActionCost cost in definition.Costs.OrderBy(value => value.ResourceId))
        {
            bool available = actor.CharacterResources.TryGet(cost.ResourceId, out _)
                ? actor.CharacterResources.CanSpendResource(cost.ResourceId, cost.Amount)
                : actor.Resources.TryGetValue(cost.ResourceId, out int inventoryAmount) && inventoryAmount >= cost.Amount;
            if (!available)
            {
                return Rejected(ActionRejectionCodes.ResourceInsufficient, cost.ResourceId.Value);
            }

            reserved[cost.ResourceId] = cost.Amount;
        }

        return new ActionEligibilityResult(
            new ActionReservation(actor, definition, request, reserved.ToImmutable(), ability, skill),
            ActionRejectionCodes.None,
            null);
    }

    public static ActionExecutionResult Resolve(ActionReservation reservation, ICombatContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(catalog);
        if (reservation.OriginalState.ContentFingerprint != catalog.Fingerprint)
        {
            return RejectedExecution(reservation.OriginalState, ActionRejectionCodes.ContentMismatch);
        }

        int roll = CombatResolutionUtilities.DeterministicRoll(
            reservation.Request.RandomSeed,
            reservation.Request.RandomSequence);
        int total = checked(reservation.AbilityValue * 10 + reservation.SkillValue + reservation.Definition.Modifier + roll);
        bool succeeded = total >= reservation.Definition.Difficulty;
        ImmutableDictionary<ResourceId, int>.Builder resources = reservation.OriginalState.Resources.ToBuilder();
        CharacterResourceSet characterResources = reservation.OriginalState.CharacterResources;
        foreach ((ResourceId id, int amount) in reservation.ReservedResources)
        {
            if (characterResources.TryGet(id, out _))
            {
                characterResources = characterResources.SpendResource(id, amount);
            }
            else
            {
                resources[id] -= amount;
            }
        }

        CharacterCapabilities capabilities = reservation.OriginalState.Capabilities;
        foreach (FeatId featId in succeeded ? reservation.Definition.GrantedFeatIds : [])
        {
            catalog.TryGetFeat(featId, out FeatDefinition? feat);
            capabilities = capabilities.WithFeatGrant(feat!, reservation.Definition.Id.Value);
        }

        CharacterState committed = reservation.OriginalState with
        {
            Resources = resources.ToImmutable(),
            CharacterResources = characterResources,
            Capabilities = capabilities,
        };
        ActionResolutionEvent resolution = new(
            committed.Id,
            reservation.Definition.Id,
            reservation.Definition.FormulaId,
            reservation.Request.Target!.Id,
            reservation.Definition.Requirement.AbilityId,
            reservation.AbilityValue,
            reservation.Definition.Requirement.SkillId,
            reservation.SkillValue,
            reservation.Definition.Modifier,
            roll,
            total,
            reservation.Definition.Difficulty,
            succeeded,
            succeeded ? ActionRejectionCodes.None : "resolution.check-failed",
            succeeded ? reservation.Definition.GrantedFeatIds : []);
        return new ActionExecutionResult(committed, true, succeeded, ActionRejectionCodes.None, resolution);
    }

    private static ActionEligibilityResult Rejected(string code, ContentId? relatedId = null) => new(null, code, relatedId);

    private static ActionExecutionResult RejectedExecution(CharacterState state, string code) =>
        new(state, false, false, code, null);
}
