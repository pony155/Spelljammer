using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

public sealed partial class SimulationContracts
{
    private static void StatusLifecycleAndConflictsAreAtomic()
    {
        StatusDefinition confused = Status(
            "status.test.confused", StatusStackPolicy.Refresh, 1, 30, "status-group.test.control",
            [new StatusModifierDefinition(StatusModifierType.ModifyAccuracy, -20)], [], []);
        StatusDefinition charmed = Status(
            "status.test.charmed", StatusStackPolicy.Refresh, 1, 50, "status-group.test.control", [],
            [new StatusRestrictionDefinition(StatusRestrictionType.CannotAttack, StatusTargetRule.StatusSource, null)], []);
        EffectDefinition burningDamage = new(
            new EffectId("effect.test.burning-damage"), 1, 1, "effect.test.name", "effect.test.description",
            new DamageEffectPayload(EffectType.ThermalDamage, 5));
        EffectDefinition confusionExpired = new(
            new EffectId("effect.test.confusion-expired"), 1, 1, "effect.test.name", "effect.test.description",
            new ResourceEffectPayload(EffectType.RestoreResolve, CharacterResourceIds.Resolve, 1));
        confused = confused with { OnExpireEffectIds = [confusionExpired.EffectId] };
        StatusDefinition burning = Status(
            "status.test.burning", StatusStackPolicy.IntensityStack, 3, 0, null, [], [],
            [burningDamage.EffectId]);
        StatusCatalog catalog = new([confused, charmed, burning], [burningDamage, confusionExpired]);
        StatusSystemLimits limits = new(8, 20, 16);
        ContentId source = new("character.test.status-source");
        ContentId target = new("character.test.status-target");
        StatusTargetId statusTarget = new(target);

        StatusResult first = StatusSystem.Apply(StatusState.Empty,
            new StatusApplicationRequest(new StatusInstanceId(Guid.Parse("11111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                confused.StatusId, source, statusTarget, null, 1, 1), catalog, limits);
        True(first.Accepted, first.RejectionCode);
        Equal(-20, StatusQueries.GetModifier(first.State, StatusModifierType.ModifyAccuracy, catalog),
            "Status modifier query ignored Confused.");

        StatusResult replacement = StatusSystem.Apply(first.State,
            new StatusApplicationRequest(new StatusInstanceId(Guid.Parse("22222222-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                charmed.StatusId, source, statusTarget, null, 1, 1), catalog, limits);
        True(replacement.Accepted, replacement.RejectionCode);
        Equal(charmed.StatusId, replacement.State.Instances.Single().DefinitionId,
            "Higher-priority exclusive status did not replace its conflict.");
        Equal(confusionExpired.EffectId, replacement.PendingEffects.Single().EffectId,
            "Exclusive status replacement did not emit the replaced status onExpire effect.");
        False(StatusQueries.CanAttack(replacement.State, source, catalog),
            "Charmed allowed an attack against its source.");

        StatusResult lowerPriority = StatusSystem.Apply(replacement.State,
            new StatusApplicationRequest(new StatusInstanceId(Guid.Parse("33333333-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                confused.StatusId, source, statusTarget, null, 1, 1), catalog, limits);
        False(lowerPriority.Accepted, "Lower-priority exclusive status replaced Charmed.");
        True(ReferenceEquals(replacement.State, lowerPriority.State), "Rejected status application replaced state.");

        StatusResult ignited = StatusSystem.Apply(StatusState.Empty,
            new StatusApplicationRequest(new StatusInstanceId(Guid.Parse("44444444-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                burning.StatusId, source, statusTarget, null, 1, 1), catalog, limits);
        StatusResult stacked = StatusSystem.Apply(ignited.State,
            new StatusApplicationRequest(new StatusInstanceId(Guid.Parse("55555555-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                burning.StatusId, source, statusTarget, null, 1, 1), catalog, limits);
        Equal(2, stacked.State.Instances.Single().Stacks, "IntensityStack did not increase stacks.");
        StatusResult advanced = StatusSystem.AdvanceTargetTurn(stacked.State, statusTarget, catalog, limits);
        Equal(2, advanced.State.Instances.Single().RemainingDuration, "Timed status did not consume one target turn.");
        Equal(burningDamage.EffectId, advanced.PendingEffects.Single().EffectId,
            "Status onTick effect was not emitted.");
        EffectResolution damage = EffectSystem.Resolve(
            new EffectTargetState(target, ActorResources(), 0, 0, advanced.State),
            advanced.PendingEffects.Select(value =>
                new EffectRequest(
                    value.InvocationId,
                    EffectApplicationDefinition.InstantTarget(value.EffectId),
                    value.SourceId,
                    value.TargetId,
                    value.StatusInstanceId,
                    value.Scale)),
            catalog,
            limits);
        True(damage.Accepted, damage.RejectionCode);
        Equal(0, damage.State.Resources.GetCurrentValue(CharacterResourceIds.Health),
            "IntensityStack did not scale its lifecycle Effect amount.");
    }

    private static StatusDefinition Status(
        string id,
        StatusStackPolicy stackPolicy,
        int maximumStacks,
        int priority,
        string? exclusiveGroupId,
        ImmutableArray<StatusModifierDefinition> modifiers,
        ImmutableArray<StatusRestrictionDefinition> restrictions,
        ImmutableArray<EffectId> onTick) => new(
            new StatusId(id), 1, 1, $"{id}.name", $"{id}.description", StatusCategory.Mental, [], 3,
            StatusDurationType.Timed, stackPolicy, maximumStacks,
            exclusiveGroupId is null ? null : new ContentId(exclusiveGroupId), priority,
            modifiers, restrictions, [], [], onTick, []);

    private sealed class StatusCatalog(
        IEnumerable<StatusDefinition> statuses,
        IEnumerable<EffectDefinition> effects) : IStatusDefinitionCatalog
    {
        private readonly ImmutableDictionary<StatusId, StatusDefinition> statuses =
            statuses.ToImmutableDictionary(value => value.StatusId);
        private readonly ImmutableDictionary<EffectId, EffectDefinition> effects =
            effects.ToImmutableDictionary(value => value.EffectId);

        public bool TryGetStatus(StatusId id, out StatusDefinition? definition) => statuses.TryGetValue(id, out definition);
        public bool TryGetEffect(EffectId id, out EffectDefinition? definition) => effects.TryGetValue(id, out definition);
    }
}
