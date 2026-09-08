using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Effects;

public sealed record ActiveStatusRestriction(
    StatusInstanceId InstanceId,
    StatusRestrictionType Type,
    StatusTargetRule TargetRule,
    ContentId? ActionTag,
    ContentId SourceId);

public sealed record ActiveStatusAiRule(
    StatusInstanceId InstanceId,
    StatusAiRuleType Type,
    StatusTargetRule TargetRule,
    ContentId? ActionTag,
    int Amount,
    ContentId SourceId);

public static class StatusQueries
{
    public static bool CanAct(StatusState state, IStatusDefinitionCatalog catalog) =>
        !GetRestrictions(state, catalog).Any(value => value.Type == StatusRestrictionType.CannotAct);

    public static bool CanAttack(
        StatusState state,
        ContentId targetId,
        IStatusDefinitionCatalog catalog) =>
        !GetRestrictions(state, catalog).Any(value =>
            value.Type == StatusRestrictionType.CannotAct ||
            value.Type == StatusRestrictionType.CannotAttack &&
            (value.TargetRule != StatusTargetRule.StatusSource || value.SourceId == targetId));

    public static int GetModifier(
        StatusState state,
        StatusModifierType type,
        IStatusDefinitionCatalog catalog)
    {
        long total = 0;
        foreach (StatusInstance instance in state.Instances)
        {
            if (!catalog.TryGetStatus(instance.DefinitionId, out StatusDefinition? definition))
            {
                continue;
            }

            foreach (StatusModifierDefinition modifier in definition!.Modifiers.Where(value => value.Type == type))
            {
                total += (long)modifier.Amount * instance.Stacks;
            }
        }

        return (int)Math.Clamp(total, int.MinValue, int.MaxValue);
    }

    public static ImmutableArray<ActiveStatusRestriction> GetRestrictions(
        StatusState state,
        IStatusDefinitionCatalog catalog) =>
        [.. state.Instances.OrderBy(value => value.DefinitionId).ThenBy(value => value.InstanceId)
            .SelectMany(instance => catalog.TryGetStatus(instance.DefinitionId, out StatusDefinition? definition)
                ? definition!.Restrictions.Select(restriction => new ActiveStatusRestriction(
                    instance.InstanceId, restriction.Type, restriction.TargetRule, restriction.ActionTag, instance.SourceId))
                : [])];

    public static ImmutableArray<ActiveStatusAiRule> GetAiRules(
        StatusState state,
        IStatusDefinitionCatalog catalog) =>
        [.. state.Instances.OrderBy(value => value.DefinitionId).ThenBy(value => value.InstanceId)
            .SelectMany(instance => catalog.TryGetStatus(instance.DefinitionId, out StatusDefinition? definition)
                ? definition!.AiRules.Select(rule => new ActiveStatusAiRule(
                    instance.InstanceId, rule.Type, rule.TargetRule, rule.ActionTag, rule.Amount,
                    instance.SourceId))
                : [])];
}
