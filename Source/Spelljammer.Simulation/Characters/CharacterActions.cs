using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

public static class ActionRejectionCodes
{
    public const string None = "";
    public const string ActionUnknown = "command.action-unknown";
    public const string ActorMissing = "command.actor-missing";
    public const string ActorCannotAct = "command.actor-cannot-act";
    public const string TargetMissing = "command.target-missing";
    public const string TargetIllegal = "command.target-illegal";
    public const string AccessRequired = "command.access-required";
    public const string TechniqueUnknown = "command.technique-unknown";
    public const string SkillRequired = "command.skill-required";
    public const string AttributeRequired = "command.attribute-required";
    public const string EquipmentRequired = "command.equipment-required";
    public const string ContextRequired = "command.context-required";
    public const string ResourceInsufficient = "command.resource-insufficient";
    public const string ContentMismatch = "command.content-mismatch";
}

public sealed record ActionRequirement(
    AccessId? AccessId,
    TechniqueId? TechniqueId,
    SkillId SkillId,
    byte MinimumSkill,
    AttributeId AttributeId,
    short MinimumAttribute,
    ContentId? EquipmentId,
    ContentId? ContextId);

public sealed record ActionCost
{
    public ActionCost(ResourceId resourceId, int amount)
    {
        if (amount <= 0 || amount > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        ResourceId = resourceId;
        Amount = amount;
    }

    public ResourceId ResourceId { get; }
    public int Amount { get; }
}

public sealed record ActionDefinition(
    ActionId Id,
    ContentId FormulaId,
    ActionRequirement Requirement,
    ImmutableArray<ActionCost> Costs,
    int Difficulty,
    int Modifier,
    ushort PracticeAward,
    ImmutableArray<PerkId> GrantedPerkIds);

public sealed record ActionTarget(ContentId Id, bool IsPresent, bool IsLegal);

public sealed record ActionRequest(
    CharacterId ActorId,
    ActionId ActionId,
    ActionTarget? Target,
    ImmutableHashSet<ContentId> ContextIds,
    ContentId PracticeKey,
    ulong RandomSeed,
    ulong RandomSequence);

public sealed record ActionReservation(
    CharacterState OriginalState,
    ActionDefinition Definition,
    ActionRequest Request,
    ImmutableDictionary<ResourceId, int> ReservedResources,
    short AttributeValue,
    byte SkillValue);

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
