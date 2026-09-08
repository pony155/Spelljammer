using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Combat;

/// <summary>
/// Resolves consent-based psionic mindlinks and their resource-backed lifecycle.
/// </summary>
/// <remarks>
/// Code flow: A link is invited and accepted or rejected, activation reserves resources and applies effects, and revoke or release restores the appropriate immutable character states.
/// </remarks>
public enum MindlinkPhase : byte
{
    Invited,
    Accepted,
    Rejected,
    Reserved,
    Active,
    Revoked,
    Released,
}

public sealed record MindlinkState(
    CharacterState OriginalActor,
    FeatDefinition Definition,
    CharacterId TargetId,
    MindlinkPhase Phase,
    long StartTick,
    long LastSustainTick,
    int ReservedStrain);

public sealed record MindlinkResult(
    CharacterState Actor,
    MindlinkState? Link,
    bool Accepted,
    string RejectionCode,
    ImmutableArray<EffectRequest> Effects);

public static class PsionicActionSystem
{
    public static MindlinkResult Invite(
        CharacterState actor,
        CharacterState target,
        FeatId featId,
        bool isInRange,
        long tick,
        ICombatContentCatalog catalog)
    {
        if (actor.ContentFingerprint != catalog.Fingerprint || target.ContentFingerprint != catalog.Fingerprint)
        {
            return Rejected(actor, ActionRejectionCodes.ContentMismatch);
        }

        if (!actor.CanAct)
        {
            return Rejected(actor, ActionRejectionCodes.ActorCannotAct);
        }

        if (actor.Id == target.Id || !isInRange)
        {
            return Rejected(actor, ActionRejectionCodes.TargetIllegal);
        }

        if (!catalog.TryGetFeat(featId, out FeatDefinition? definition) ||
            definition!.Activation != FeatActivation.Active || definition.PsionicRules is null)
        {
            return Rejected(actor, ActionRejectionCodes.ActionUnknown);
        }

        if (!definition.RequiredAccessIds.All(actor.Capabilities.Access.Contains))
        {
            return Rejected(actor, ActionRejectionCodes.AccessRequired);
        }

        if (!actor.Capabilities.Feats.Contains(featId))
        {
            return Rejected(actor, ActionRejectionCodes.FeatUnknown);
        }

        if (!actor.Capabilities.TryGetSkill(definition.PsionicRules.SkillId, catalog, out _, out _))
        {
            return Rejected(actor, ActionRejectionCodes.SkillRequired);
        }

        MindlinkState link = new(actor, definition, target.Id, MindlinkPhase.Invited, tick, tick, 0);
        return Accepted(actor, link);
    }

    public static MindlinkResult Respond(MindlinkState link, CharacterId responderId, bool consent)
    {
        if (link.Phase != MindlinkPhase.Invited || responderId != link.TargetId)
        {
            return Rejected(link.OriginalActor, "command.action-phase-invalid", link);
        }

        return Accepted(link.OriginalActor, link with { Phase = consent ? MindlinkPhase.Accepted : MindlinkPhase.Rejected });
    }

    public static MindlinkResult Reserve(MindlinkState link)
    {
        if (link.Phase != MindlinkPhase.Accepted)
        {
            return Rejected(link.OriginalActor, "command.consent-required", link);
        }

        PsionicFeatRules rules = link.Definition.PsionicRules!;
        if (!link.OriginalActor.CharacterResources.TryGet(rules.StrainResourceId, out CharacterResourceState? strain) ||
            !strain!.Accumulates)
        {
            return Rejected(link.OriginalActor, ActionRejectionCodes.ResourceInsufficient, link);
        }

        return Accepted(link.OriginalActor, link with
        {
            Phase = MindlinkPhase.Reserved,
            ReservedStrain = rules.StrainCost,
        });
    }

    public static MindlinkResult Commit(MindlinkState link)
    {
        if (link.Phase != MindlinkPhase.Reserved)
        {
            return Rejected(link.OriginalActor, "command.action-phase-invalid", link);
        }

        if (link.Definition.Effects.Length > CharacterCapabilities.MaximumSetEntries ||
            link.OriginalActor.Evidence.Length >= CharacterCapabilities.MaximumSetEntries)
        {
            return Rejected(link.OriginalActor, "command.queue-capacity", link);
        }

        PsionicFeatRules rules = link.Definition.PsionicRules!;
        ImmutableArray<EffectRequest> effects = CombatResolutionUtilities.BuildEffectRequests(
            link.Definition.Effects,
            link.OriginalActor.Id.Value,
            link.TargetId.Value,
            0,
            checked((ulong)link.StartTick));
        ObservableCapabilityEvidence evidence = new(
            new ContentId("evidence.psionics.mindlink"),
            link.Definition.FeatId.Value,
            link.OriginalActor.Id,
            link.TargetId,
            link.StartTick,
            true);
        CharacterState committed = link.OriginalActor with
        {
            CharacterResources = link.OriginalActor.CharacterResources.GenerateStrain(link.ReservedStrain),
            Evidence = [.. link.OriginalActor.Evidence, evidence],
        };
        return Accepted(committed, link with { Phase = MindlinkPhase.Active }, effects);
    }

    public static MindlinkResult Sustain(CharacterState actor, MindlinkState link, long tick)
    {
        if (link.Phase != MindlinkPhase.Active || actor.Id != link.OriginalActor.Id || tick <= link.LastSustainTick)
        {
            return Rejected(actor, "command.action-phase-invalid", link);
        }

        PsionicFeatRules rules = link.Definition.PsionicRules!;
        CharacterState sustained = actor with
        {
            CharacterResources = actor.CharacterResources.GenerateStrain(rules.SustainCostPerTick),
        };
        return Accepted(sustained, link with { LastSustainTick = tick });
    }

    public static MindlinkResult Revoke(CharacterState owner, MindlinkState link, CharacterId requesterId) =>
        requesterId == link.TargetId
            ? Terminate(owner, link, MindlinkPhase.Revoked)
            : Rejected(owner, "command.consent-owner-required", link);

    public static MindlinkResult Release(CharacterState owner, MindlinkState link) =>
        Terminate(owner, link, MindlinkPhase.Released);

    private static MindlinkResult Terminate(CharacterState actor, MindlinkState link, MindlinkPhase phase)
    {
        if (link.Phase != MindlinkPhase.Active || actor.Id != link.OriginalActor.Id)
        {
            return Rejected(actor, "command.action-phase-invalid", link);
        }

        EffectId removeEffectId = new("effect.psionics.remove-shared-channel");
        EffectRequest removal = new(
            EffectInvocationId.Derive(
                0,
                checked((ulong)link.LastSustainTick),
                removeEffectId.Value),
            EffectApplicationDefinition.InstantTarget(removeEffectId),
            link.OriginalActor.Id.Value,
            link.TargetId.Value);
        return Accepted(actor, link with { Phase = phase }, [removal]);
    }

    private static MindlinkResult Accepted(
        CharacterState actor,
        MindlinkState link,
        ImmutableArray<EffectRequest> effects = default) =>
        new(actor, link, true, ActionRejectionCodes.None, effects.IsDefault ? [] : effects);

    private static MindlinkResult Rejected(CharacterState actor, string code, MindlinkState? link = null) =>
        new(actor, link, false, code, []);
}
