using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

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

    /// <summary>The technique/spell/power required for this action is not known by the actor.</summary>
    public const string TechniqueUnknown = "command.technique-unknown";

    /// <summary>The actor's skill level is too low to perform this action.</summary>
    public const string SkillRequired = "command.skill-required";

    /// <summary>The actor's attribute value is too low to perform this action.</summary>
    public const string AttributeRequired = "command.attribute-required";

    /// <summary>The actor does not have the required equipment to perform this action.</summary>
    public const string EquipmentRequired = "command.equipment-required";

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
/// Action requirements include optional access privilege and technique checks, mandatory skill and attribute minimums,
/// and optional equipment and context requirements.
/// </remarks>
/// <param name="AccessId">The access privilege required to perform this action, if any.</param>
/// <param name="TechniqueId">The technique/spell/power required, if any (for technique-based actions).</param>
/// <param name="SkillId">The skill that governs success for this action.</param>
/// <param name="MinimumSkill">The minimum skill level required to attempt this action.</param>
/// <param name="AttributeId">The attribute that provides the base modifier for this action.</param>
/// <param name="MinimumAttribute">The minimum attribute value required to attempt this action.</param>
/// <param name="EquipmentId">The equipment that must be equipped to perform this action, if any.</param>
/// <param name="ContextId">A context requirement (e.g., must be in water, must be outdoors), if any.</param>
public sealed record ActionRequirement(
    AccessId? AccessId,
    TechniqueId? TechniqueId,
    SkillId SkillId,
    byte MinimumSkill,
    AttributeId AttributeId,
    short MinimumAttribute,
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
/// success modifiers, practice point awards, and potential perk grants for repeated use.
/// </remarks>
public sealed record ActionDefinition(
    ActionId Id,
    ContentId FormulaId,
    ActionRequirement Requirement,
    ImmutableArray<ActionCost> Costs,
    int Difficulty,
    int Modifier,
    ushort PracticeAward,
    ImmutableArray<PerkId> GrantedPerkIds);

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
/// They include randomization seeds for outcome determination and practice key tracking.
/// </remarks>
/// <param name="ActorId">The character performing the action.</param>
/// <param name="ActionId">The action to perform.</param>
/// <param name="Target">The target of the action, if it requires a target.</param>
/// <param name="ContextIds">The set of active contexts for this action (e.g., outdoors, in water).</param>
/// <param name="PracticeKey">A key for tracking practice and determining when perks are granted from repeated actions.</param>
/// <param name="RandomSeed">The random seed for outcome determination.</param>
/// <param name="RandomSequence">The sequence number for deterministic randomization.</param>
public sealed record ActionRequest(
    CharacterId ActorId,
    ActionId ActionId,
    ActionTarget? Target,
    ImmutableHashSet<ContentId> ContextIds,
    ContentId PracticeKey,
    ulong RandomSeed,
    ulong RandomSequence);

/// <summary>
/// Represents a validated action with reserved resources, ready for execution.
/// </summary>
/// <remarks>
/// This record is produced during action eligibility checking and contains the character state snapshot
/// before the action executes, reserved resources, and resolved attribute/skill values.
/// </remarks>
/// <param name="OriginalState">The character state before the action is executed.</param>
/// <param name="Definition">The action definition being performed.</param>
/// <param name="Request">The original action request.</param>
/// <param name="ReservedResources">Resources that will be consumed if the action executes.</param>
/// <param name="AttributeValue">The resolved attribute value used as base modifier for this action.</param>
/// <param name="SkillValue">The resolved skill value for this action.</param>
public sealed record ActionReservation(
    CharacterState OriginalState,
    ActionDefinition Definition,
    ActionRequest Request,
    ImmutableDictionary<ResourceId, int> ReservedResources,
    short AttributeValue,
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
    AttributeId AttributeId,
    short AttributeValue,
    SkillId SkillId,
    byte SkillValue,
    int DefinitionModifier,
    int Roll,
    int Total,
    int Difficulty,
    bool Succeeded,
    string FailureReason,
    ImmutableArray<PerkId> GrantedPerkIds);

public sealed record ActionExecutionResult(
    CharacterState State,
    bool Accepted,
    bool Succeeded,
    string RejectionCode,
    ActionResolutionEvent? Resolution,
    SkillAdvancementEvent? Advancement);

public static class CharacterActionSystem
{
    private static readonly ContentId StandardCheckFormula = new("formula.check.standard");

    public static ActionEligibilityResult CheckEligibility(
        CharacterState? actor,
        ActionDefinition? definition,
        ActionRequest request,
        ICharacterContentCatalog catalog)
    {
        if (actor is null || actor.Id != request.ActorId)
        {
            return Rejected(ActionRejectionCodes.ActorMissing, request.ActorId.Value);
        }

        if (!actor.CanAct)
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
            definition.Costs.Length > 32 || definition.GrantedPerkIds.Length > CharacterCapabilities.MaximumSetEntries ||
            request.ContextIds.Count > CharacterCapabilities.MaximumSetEntries ||
            definition.Costs.Select(value => value.ResourceId).Distinct().Count() != definition.Costs.Length ||
            definition.GrantedPerkIds.Distinct().Count() != definition.GrantedPerkIds.Length ||
            definition.Difficulty is < 0 or > 10_000 || definition.Modifier is < -10_000 or > 10_000)
        {
            return Rejected(ActionRejectionCodes.ActionUnknown, request.ActionId.Value);
        }

        foreach (PerkId perkId in definition.GrantedPerkIds)
        {
            if (!catalog.TryGetPerk(perkId, out PerkDefinition? perk) || !perk!.CompatibleRaceIds.Contains(actor.RaceId))
            {
                return Rejected(ActionRejectionCodes.ActionUnknown, perkId.Value);
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

        if (requirement.TechniqueId is TechniqueId techniqueId &&
            (!catalog.TryGetTechnique(techniqueId, out _) || !actor.Capabilities.Techniques.Contains(techniqueId) ||
             !actor.Capabilities.GrantSources.Any(value => value.CapabilityId == techniqueId.Value)))
        {
            return Rejected(ActionRejectionCodes.TechniqueUnknown, techniqueId.Value);
        }

        if (!actor.Capabilities.TryGetSkill(requirement.SkillId, catalog, out byte skill, out _) ||
            skill < requirement.MinimumSkill)
        {
            return Rejected(ActionRejectionCodes.SkillRequired, requirement.SkillId.Value);
        }

        if (!actor.Capabilities.TryGetAttribute(requirement.AttributeId, catalog, out short attribute, out _) ||
            attribute < requirement.MinimumAttribute)
        {
            return Rejected(ActionRejectionCodes.AttributeRequired, requirement.AttributeId.Value);
        }

        if (requirement.EquipmentId is ContentId equipmentId && !actor.EquipmentIds.Contains(equipmentId))
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
            actor.Resources.TryGetValue(cost.ResourceId, out int available);
            if (available < cost.Amount)
            {
                return Rejected(ActionRejectionCodes.ResourceInsufficient, cost.ResourceId.Value);
            }

            reserved[cost.ResourceId] = cost.Amount;
        }

        return new ActionEligibilityResult(
            new ActionReservation(actor, definition, request, reserved.ToImmutable(), attribute, skill),
            ActionRejectionCodes.None,
            null);
    }

    public static ActionExecutionResult Resolve(ActionReservation reservation, ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(catalog);
        if (reservation.OriginalState.ContentFingerprint != catalog.Fingerprint)
        {
            return RejectedExecution(reservation.OriginalState, ActionRejectionCodes.ContentMismatch);
        }

        int roll = DeterministicRoll(reservation.Request.RandomSeed, reservation.Request.RandomSequence);
        int total = checked(reservation.AttributeValue * 10 + reservation.SkillValue + reservation.Definition.Modifier + roll);
        bool succeeded = total >= reservation.Definition.Difficulty;
        ImmutableDictionary<ResourceId, int>.Builder resources = reservation.OriginalState.Resources.ToBuilder();
        foreach ((ResourceId id, int amount) in reservation.ReservedResources)
        {
            resources[id] -= amount;
        }

        CharacterCapabilities capabilities = reservation.OriginalState.Capabilities;
        foreach (PerkId perkId in succeeded ? reservation.Definition.GrantedPerkIds : [])
        {
            catalog.TryGetPerk(perkId, out PerkDefinition? perk);
            capabilities = capabilities.WithPerkGrant(perk!, reservation.Definition.Id.Value);
        }

        SkillAdvancementEvent? advancement = null;
        if (reservation.Definition.PracticeAward > 0 &&
            reservation.Definition.Difficulty >= reservation.SkillValue)
        {
            capabilities = capabilities.AwardPractice(
                catalog,
                reservation.Definition.Requirement.SkillId,
                reservation.Definition.PracticeAward,
                reservation.Request.PracticeKey,
                out advancement);
        }

        CharacterState committed = reservation.OriginalState with
        {
            Resources = resources.ToImmutable(),
            Capabilities = capabilities,
        };
        ActionResolutionEvent resolution = new(
            committed.Id,
            reservation.Definition.Id,
            reservation.Definition.FormulaId,
            reservation.Request.Target!.Id,
            reservation.Definition.Requirement.AttributeId,
            reservation.AttributeValue,
            reservation.Definition.Requirement.SkillId,
            reservation.SkillValue,
            reservation.Definition.Modifier,
            roll,
            total,
            reservation.Definition.Difficulty,
            succeeded,
            succeeded ? ActionRejectionCodes.None : "resolution.check-failed",
            succeeded ? reservation.Definition.GrantedPerkIds : []);
        return new ActionExecutionResult(committed, true, succeeded, ActionRejectionCodes.None, resolution, advancement);
    }

    private static int DeterministicRoll(ulong seed, ulong sequence)
    {
        ulong value = seed + (sequence + 1) * 0x9e3779b97f4a7c15UL;
        value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
        value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
        value ^= value >> 31;
        return 1 + (int)(value % 100);
    }

    private static ActionEligibilityResult Rejected(string code, ContentId? relatedId = null) => new(null, code, relatedId);

    private static ActionExecutionResult RejectedExecution(CharacterState state, string code) =>
        new(state, false, false, code, null, null);
}
