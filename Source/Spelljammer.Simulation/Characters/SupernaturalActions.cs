using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

public enum SpellActionPhase : byte
{
    Declared,
    Previewed,
    Reserved,
    Prepared,
    Resolved,
    Committed,
    Recovered,
    Interrupted,
}

public sealed record SupernaturalTarget(
    CharacterId Id,
    bool IsPresent,
    bool IsVisible,
    bool IsInRange,
    ImmutableHashSet<string> Tags);

public sealed record SpellActionState(
    CharacterState OriginalActor,
    FeatDefinition Definition,
    SupernaturalTarget Target,
    SpellActionPhase Phase,
    ulong RandomSeed,
    ulong RandomSequence,
    long Tick,
    int ReservedMana,
    int Roll,
    bool Succeeded);

public sealed record SpellActionResult(
    CharacterState Actor,
    SpellActionState? Action,
    bool Accepted,
    string RejectionCode);

public static class SpellActionSystem
{
    public static SpellActionResult Declare(
        CharacterState actor,
        FeatId featId,
        SupernaturalTarget target,
        ulong randomSeed,
        ulong randomSequence,
        long tick,
        ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(catalog);
        if (actor.ContentFingerprint != catalog.Fingerprint)
        {
            return Rejected(actor, ActionRejectionCodes.ContentMismatch);
        }

        if (!actor.CanAct)
        {
            return Rejected(actor, ActionRejectionCodes.ActorCannotAct);
        }

        if (!catalog.TryGetFeat(featId, out FeatDefinition? definition) ||
            definition!.Activation != FeatActivation.Active || definition.SpellRules is null)
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

        if (!actor.Capabilities.TryGetSkill(definition.SpellRules.SkillId, catalog, out _, out _))
        {
            return Rejected(actor, ActionRejectionCodes.SkillRequired);
        }

        if (!target.IsPresent)
        {
            return Rejected(actor, ActionRejectionCodes.TargetMissing);
        }

        if (!target.IsVisible || !target.IsInRange || !target.Tags.Overlaps(definition.SpellRules.TargetTags))
        {
            return Rejected(actor, ActionRejectionCodes.TargetIllegal);
        }

        SpellActionState action = new(
            actor,
            definition,
            target,
            SpellActionPhase.Declared,
            randomSeed,
            randomSequence,
            tick,
            0,
            0,
            false);
        return Accepted(actor, action);
    }

    public static SpellActionResult Preview(SpellActionState action) =>
        Transition(action, SpellActionPhase.Declared, SpellActionPhase.Previewed);

    public static SpellActionResult Reserve(SpellActionState action)
    {
        if (action.Phase != SpellActionPhase.Previewed)
        {
            return Rejected(action.OriginalActor, "command.action-phase-invalid", action);
        }

        SpellFeatRules rules = action.Definition.SpellRules!;
        if (!action.OriginalActor.CharacterResources.CanSpendResource(rules.ManaResourceId, rules.ManaCost))
        {
            return Rejected(action.OriginalActor, ActionRejectionCodes.ResourceInsufficient, action);
        }

        return Accepted(action.OriginalActor, action with
        {
            Phase = SpellActionPhase.Reserved,
            ReservedMana = rules.ManaCost,
        });
    }

    public static SpellActionResult Prepare(SpellActionState action) =>
        Transition(action, SpellActionPhase.Reserved, SpellActionPhase.Prepared);

    public static SpellActionResult Interrupt(SpellActionState action)
    {
        if (action.Definition.SpellRules!.CastTimeTicks == 0)
        {
            return Rejected(action.OriginalActor, "command.action-not-channeled", action);
        }

        if (action.Phase is not (SpellActionPhase.Reserved or SpellActionPhase.Prepared))
        {
            return Rejected(action.OriginalActor, "command.action-phase-invalid", action);
        }

        return Accepted(action.OriginalActor, action with { Phase = SpellActionPhase.Interrupted, ReservedMana = 0 });
    }

    public static SpellActionResult Resolve(SpellActionState action, ICharacterContentCatalog catalog)
    {
        if (action.Phase != SpellActionPhase.Prepared)
        {
            return Rejected(action.OriginalActor, "command.action-phase-invalid", action);
        }

        if (action.OriginalActor.ContentFingerprint != catalog.Fingerprint ||
            !action.OriginalActor.Capabilities.TryGetSkill(action.Definition.SpellRules!.SkillId, catalog, out byte skill, out _))
        {
            return Rejected(action.OriginalActor, ActionRejectionCodes.ContentMismatch, action);
        }

        int roll = DeterministicRoll(action.RandomSeed, action.RandomSequence);
        bool succeeded = skill + roll >= 20;
        return Accepted(action.OriginalActor, action with
        {
            Phase = SpellActionPhase.Resolved,
            Roll = roll,
            Succeeded = succeeded,
        });
    }

    public static SpellActionResult Commit(SpellActionState action)
    {
        if (action.Phase != SpellActionPhase.Resolved)
        {
            return Rejected(action.OriginalActor, "command.action-phase-invalid", action);
        }

        if (action.Succeeded &&
            action.OriginalActor.ActiveEffects.Length + action.Definition.EffectIds.Length > CharacterCapabilities.MaximumSetEntries ||
            action.OriginalActor.Evidence.Length >= CharacterCapabilities.MaximumSetEntries)
        {
            return Rejected(action.OriginalActor, "command.queue-capacity", action);
        }

        SpellFeatRules rules = action.Definition.SpellRules!;
        if (!action.OriginalActor.CharacterResources.CanSpendResource(rules.ManaResourceId, action.ReservedMana))
        {
            return Rejected(action.OriginalActor, ActionRejectionCodes.ResourceInsufficient, action);
        }

        ImmutableArray<ActiveCapabilityEffect> effects = action.Succeeded
            ? [.. action.OriginalActor.ActiveEffects, .. action.Definition.EffectIds.Select(id => new ActiveCapabilityEffect(
                id,
                action.Definition.FeatId.Value,
                action.OriginalActor.Id,
                action.Target.Id,
                action.Tick,
                action.Tick + 1,
                rules.RangeId))]
            : action.OriginalActor.ActiveEffects;
        ObservableCapabilityEvidence evidence = new(
            new ContentId("evidence.spell.cast"),
            action.Definition.FeatId.Value,
            action.OriginalActor.Id,
            action.Target.Id,
            action.Tick,
            action.Succeeded);
        CharacterState committed = action.OriginalActor with
        {
            CharacterResources = action.OriginalActor.CharacterResources.SpendResource(
                rules.ManaResourceId,
                action.ReservedMana),
            ActiveEffects = effects,
            Evidence = [.. action.OriginalActor.Evidence, evidence],
        };
        return Accepted(committed, action with { Phase = SpellActionPhase.Committed });
    }

    public static SpellActionResult Recover(CharacterState committedActor, SpellActionState action)
    {
        if (action.Phase != SpellActionPhase.Committed)
        {
            return Rejected(committedActor, "command.action-phase-invalid", action);
        }

        return Accepted(committedActor, action with { Phase = SpellActionPhase.Recovered });
    }

    private static SpellActionResult Transition(SpellActionState action, SpellActionPhase from, SpellActionPhase to) =>
        action.Phase == from
            ? Accepted(action.OriginalActor, action with { Phase = to })
            : Rejected(action.OriginalActor, "command.action-phase-invalid", action);

    private static int DeterministicRoll(ulong seed, ulong sequence)
    {
        ulong value = seed + (sequence + 1) * 0x9e3779b97f4a7c15UL;
        value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
        value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
        value ^= value >> 31;
        return 1 + (int)(value % 100);
    }

    private static SpellActionResult Accepted(CharacterState actor, SpellActionState action) =>
        new(actor, action, true, ActionRejectionCodes.None);

    private static SpellActionResult Rejected(CharacterState actor, string code, SpellActionState? action = null) =>
        new(actor, action, false, code);
}

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
    string RejectionCode);

public static class MindlinkSystem
{

    public static MindlinkResult Invite(
        CharacterState actor,
        CharacterState target,
        FeatId featId,
        bool isInRange,
        long tick,
        ICharacterContentCatalog catalog)
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

        if (link.OriginalActor.ActiveEffects.Length + link.Definition.EffectIds.Length > CharacterCapabilities.MaximumSetEntries ||
            link.OriginalActor.Evidence.Length >= CharacterCapabilities.MaximumSetEntries)
        {
            return Rejected(link.OriginalActor, "command.queue-capacity", link);
        }

        PsionicFeatRules rules = link.Definition.PsionicRules!;
        ImmutableArray<ActiveCapabilityEffect> effects =
            [.. link.OriginalActor.ActiveEffects, .. link.Definition.EffectIds.Select(id => new ActiveCapabilityEffect(
                id,
                link.Definition.FeatId.Value,
                link.OriginalActor.Id,
                link.TargetId,
                link.StartTick,
                long.MaxValue,
                rules.InformationScopeId))];
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
            ActiveEffects = effects,
            Evidence = [.. link.OriginalActor.Evidence, evidence],
        };
        return Accepted(committed, link with { Phase = MindlinkPhase.Active });
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

        CharacterState ended = actor with
        {
            ActiveEffects =
            [.. actor.ActiveEffects.Where(value => value.SourceId != link.Definition.FeatId.Value || value.TargetId != link.TargetId)],
        };
        return Accepted(ended, link with { Phase = phase });
    }

    private static MindlinkResult Accepted(CharacterState actor, MindlinkState link) =>
        new(actor, link, true, ActionRejectionCodes.None);

    private static MindlinkResult Rejected(CharacterState actor, string code, MindlinkState? link = null) =>
        new(actor, link, false, code);
}
