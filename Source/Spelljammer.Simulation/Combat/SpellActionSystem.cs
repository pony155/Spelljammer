using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Combat;

/// <summary>
/// Resolves multi-phase spell actions, resource reservation, interruption, and authored effects.
/// </summary>
/// <remarks>
/// Code flow: A spell is declared and previewed, costs are reserved, preparation may be interrupted, and successful resolution applies ordered effects before committing replacement actor and target state.
/// </remarks>
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
    string RejectionCode,
    ImmutableArray<EffectRequest> Effects);

public static class SpellActionSystem
{
    public static SpellActionResult Declare(
        CharacterState actor,
        FeatId featId,
        SupernaturalTarget target,
        ulong randomSeed,
        ulong randomSequence,
        long tick,
        ICombatContentCatalog catalog)
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

    public static SpellActionResult Resolve(SpellActionState action, ICombatContentCatalog catalog)
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

        int roll = CombatResolutionUtilities.DeterministicRoll(action.RandomSeed, action.RandomSequence);
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

        if (action.Succeeded && action.Definition.Effects.Length > CharacterCapabilities.MaximumSetEntries ||
            action.OriginalActor.Evidence.Length >= CharacterCapabilities.MaximumSetEntries)
        {
            return Rejected(action.OriginalActor, "command.queue-capacity", action);
        }

        SpellFeatRules rules = action.Definition.SpellRules!;
        if (!action.OriginalActor.CharacterResources.CanSpendResource(rules.ManaResourceId, action.ReservedMana))
        {
            return Rejected(action.OriginalActor, ActionRejectionCodes.ResourceInsufficient, action);
        }

        ImmutableArray<EffectRequest> effects = action.Succeeded
            ? CombatResolutionUtilities.BuildEffectRequests(
                action.Definition.Effects,
                action.OriginalActor.Id.Value,
                action.Target.Id.Value,
                action.RandomSeed,
                action.RandomSequence)
            : [];
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
            Evidence = [.. action.OriginalActor.Evidence, evidence],
        };
        return Accepted(committed, action with { Phase = SpellActionPhase.Committed }, effects);
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

    private static SpellActionResult Accepted(
        CharacterState actor,
        SpellActionState action,
        ImmutableArray<EffectRequest> effects = default) =>
        new(actor, action, true, ActionRejectionCodes.None, effects.IsDefault ? [] : effects);

    private static SpellActionResult Rejected(CharacterState actor, string code, SpellActionState? action = null) =>
        new(actor, action, false, code, []);

}
