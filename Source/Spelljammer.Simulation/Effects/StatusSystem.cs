using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Effects;

/// <summary>
/// Applies, stacks, refreshes, expires, and removes bounded status instances.
/// </summary>
/// <remarks>
/// Code flow: A request resolves its status definition, validates target limits and stack policy, produces pending effects, and atomically returns replacement status state or a rejection.
/// </remarks>
public sealed record StatusSystemLimits(
    int MaximumInstancesPerTarget,
    int MaximumDuration,
    int MaximumEffectsPerTransaction)
{
    public void Validate()
    {
        if (MaximumInstancesPerTarget <= 0 || MaximumDuration <= 0 || MaximumEffectsPerTransaction <= 0)
        {
            throw new InvalidOperationException("Status system limits must be positive.");
        }
    }
}

public sealed record StatusApplicationRequest(
    StatusInstanceId InstanceId,
    StatusId DefinitionId,
    ContentId SourceId,
    StatusTargetId TargetId,
    int? Duration,
    int Stacks,
    int Potency);

public sealed record PendingEffect(
    EffectInvocationId InvocationId,
    EffectId EffectId,
    ContentId SourceId,
    ContentId TargetId,
    StatusInstanceId StatusInstanceId,
    int Sequence,
    int Scale);

public sealed record StatusResult(
    StatusState State,
    ImmutableArray<PendingEffect> PendingEffects,
    bool Accepted,
    bool Changed,
    string RejectionCode)
{
    public static StatusResult Rejected(StatusState state, string code) => new(state, [], false, false, code);
}

public static class StatusSystem
{
    public static StatusResult Create(
        StatusState candidate,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        return Validate(candidate, catalog, limits)
            ? Accepted(Normalize(candidate), [], true)
            : StatusResult.Rejected(candidate, StatusRejectionCodes.InvalidState);
    }

    public static StatusResult Apply(
        StatusState state,
        StatusApplicationRequest request,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        if (!Validate(state, catalog, limits))
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.InvalidState);
        }

        StatusInstance? reusedInstance = state.Instances.SingleOrDefault(value => value.InstanceId == request.InstanceId);
        if (!request.InstanceId.IsValid || !request.SourceId.IsValid || !request.TargetId.IsValid ||
            request.Stacks <= 0 || request.Potency < 0 ||
            reusedInstance is not null &&
            (reusedInstance.DefinitionId != request.DefinitionId || reusedInstance.TargetId != request.TargetId))
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.InstanceInvalid);
        }

        if (!catalog.TryGetStatus(request.DefinitionId, out StatusDefinition? definition))
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.DefinitionMissing);
        }

        StatusDefinition resolvedDefinition = definition!;
        int duration = request.Duration ?? resolvedDefinition.DefaultDuration;
        if (!IsDurationValid(resolvedDefinition, duration, limits) || request.Stacks > resolvedDefinition.MaximumStacks)
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.DurationExceeded);
        }

        StatusInstance proposed = new(
            request.InstanceId, resolvedDefinition.StatusId, resolvedDefinition.Revision, request.SourceId, request.TargetId,
            duration, request.Stacks, request.Potency);
        ImmutableArray<StatusInstance> working = state.Instances;
        ImmutableArray<PendingEffect>.Builder effects = ImmutableArray.CreateBuilder<PendingEffect>();

        foreach (StatusInstance conflict in state.Instances.Where(value =>
                     value.TargetId == request.TargetId && value.DefinitionId != request.DefinitionId))
        {
            catalog.TryGetStatus(conflict.DefinitionId, out StatusDefinition? conflictDefinition);
            if (resolvedDefinition.ExclusiveGroupId is not ContentId groupId ||
                conflictDefinition!.ExclusiveGroupId != groupId)
            {
                continue;
            }

            if (ComparePrecedence(proposed, resolvedDefinition, conflict, conflictDefinition) <= 0)
            {
                return StatusResult.Rejected(state, StatusRejectionCodes.ExclusiveConflict);
            }

            working = working.Remove(conflict);
            AppendPending(effects, conflictDefinition.OnExpireEffectIds, conflict, "expire");
        }

        StatusInstance? existing = working
            .Where(value => value.TargetId == request.TargetId && value.DefinitionId == request.DefinitionId)
            .OrderBy(value => value.InstanceId)
            .FirstOrDefault();
        StatusInstance applied = proposed;
        if (existing is not null && resolvedDefinition.StackPolicy != StatusStackPolicy.Independent)
        {
            switch (resolvedDefinition.StackPolicy)
            {
                case StatusStackPolicy.Refresh:
                    applied = existing with { RemainingDuration = duration };
                    break;
                case StatusStackPolicy.Extend:
                    long extended = (long)existing.RemainingDuration + duration;
                    if (extended > limits.MaximumDuration)
                    {
                        return StatusResult.Rejected(state, StatusRejectionCodes.DurationExceeded);
                    }

                    applied = existing with { RemainingDuration = (int)extended };
                    break;
                case StatusStackPolicy.IntensityStack:
                    applied = existing with
                    {
                        RemainingDuration = Math.Max(existing.RemainingDuration, duration),
                        Stacks = (int)Math.Min(resolvedDefinition.MaximumStacks, (long)existing.Stacks + request.Stacks),
                        Potency = Math.Max(existing.Potency, request.Potency),
                    };
                    break;
                case StatusStackPolicy.StrongerWins:
                    if (request.Potency <= existing.Potency)
                    {
                        return Accepted(state, [], false);
                    }

                    applied = proposed;
                    AppendPending(effects, resolvedDefinition.OnExpireEffectIds, existing, "expire");
                    break;
                case StatusStackPolicy.Reject:
                    return StatusResult.Rejected(state, StatusRejectionCodes.AlreadyPresent);
                default:
                    return StatusResult.Rejected(state, StatusRejectionCodes.InvalidState);
            }

            working = working.Remove(existing);
        }

        if (working.Count(value => value.TargetId == request.TargetId) >= limits.MaximumInstancesPerTarget)
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.CapacityExceeded);
        }

        AppendPending(effects, resolvedDefinition.OnApplyEffectIds, applied, "apply");
        if (effects.Count > limits.MaximumEffectsPerTransaction)
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.CapacityExceeded);
        }

        StatusState candidate = Normalize(new StatusState(working.Add(applied)));
        if (!Validate(candidate, catalog, limits))
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.InvalidState);
        }

        return Accepted(candidate, effects.ToImmutable(), true);
    }

    public static StatusResult Remove(
        StatusState state,
        StatusInstanceId instanceId,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        if (!Validate(state, catalog, limits))
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.InvalidState);
        }

        StatusInstance? existing = state.Instances.SingleOrDefault(value => value.InstanceId == instanceId);
        if (existing is null)
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.InstanceInvalid);
        }

        catalog.TryGetStatus(existing.DefinitionId, out StatusDefinition? definition);
        ImmutableArray<PendingEffect>.Builder effects = ImmutableArray.CreateBuilder<PendingEffect>();
        AppendPending(effects, definition!.OnExpireEffectIds, existing, "expire");
        if (effects.Count > limits.MaximumEffectsPerTransaction)
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.CapacityExceeded);
        }

        return Accepted(Normalize(new StatusState(state.Instances.Remove(existing))), effects.ToImmutable(), true);
    }

    public static StatusResult RemoveByDefinition(
        StatusState state,
        StatusId definitionId,
        StatusTargetId targetId,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        if (!targetId.IsValid || !Validate(state, catalog, limits))
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.InvalidState);
        }

        ImmutableArray<StatusInstance> removed =
            [.. state.Instances.Where(value => value.TargetId == targetId && value.DefinitionId == definitionId)];
        ImmutableArray<PendingEffect>.Builder effects = ImmutableArray.CreateBuilder<PendingEffect>();
        foreach (StatusInstance instance in removed.OrderBy(value => value.InstanceId))
        {
            catalog.TryGetStatus(instance.DefinitionId, out StatusDefinition? definition);
            AppendPending(effects, definition!.OnExpireEffectIds, instance, "expire");
        }

        if (effects.Count > limits.MaximumEffectsPerTransaction)
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.CapacityExceeded);
        }

        ImmutableArray<StatusInstance> retained = [.. state.Instances.Except(removed)];
        return Accepted(
            Normalize(new StatusState(retained)),
            effects.ToImmutable(),
            removed.Length != 0);
    }

    public static StatusResult AdvanceTargetTurn(
        StatusState state,
        StatusTargetId targetId,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();
        if (!targetId.IsValid || !Validate(state, catalog, limits))
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.InvalidState);
        }

        ImmutableArray<StatusInstance>.Builder retained = ImmutableArray.CreateBuilder<StatusInstance>();
        ImmutableArray<PendingEffect>.Builder effects = ImmutableArray.CreateBuilder<PendingEffect>();
        foreach (StatusInstance instance in state.Instances.OrderBy(value => value.DefinitionId).ThenBy(value => value.InstanceId))
        {
            if (instance.TargetId != targetId)
            {
                retained.Add(instance);
                continue;
            }

            catalog.TryGetStatus(instance.DefinitionId, out StatusDefinition? definition);
            AppendPending(effects, definition!.OnTickEffectIds, instance, "tick");
            if (definition.DurationType != StatusDurationType.Timed)
            {
                retained.Add(instance);
                continue;
            }

            int remaining = instance.RemainingDuration - 1;
            if (remaining > 0)
            {
                retained.Add(instance with { RemainingDuration = remaining });
            }
            else
            {
                AppendPending(effects, definition.OnExpireEffectIds, instance, "expire");
            }
        }

        if (effects.Count > limits.MaximumEffectsPerTransaction)
        {
            return StatusResult.Rejected(state, StatusRejectionCodes.CapacityExceeded);
        }

        StatusState candidate = Normalize(new StatusState(retained.ToImmutable()));
        return Validate(candidate, catalog, limits)
            ? Accepted(candidate, effects.ToImmutable(), !candidate.Instances.SequenceEqual(state.Instances))
            : StatusResult.Rejected(state, StatusRejectionCodes.InvalidState);
    }

    private static bool Validate(StatusState state, IStatusDefinitionCatalog catalog, StatusSystemLimits limits)
    {
        if (state.Instances.Length > (long)limits.MaximumInstancesPerTarget *
                Math.Max(1, state.Instances.Select(value => value.TargetId).Distinct().Count()) ||
            state.Instances.Select(value => value.InstanceId).Distinct().Count() != state.Instances.Length)
        {
            return false;
        }

        foreach (StatusInstance instance in state.Instances)
        {
            if (!instance.InstanceId.IsValid || !instance.SourceId.IsValid || !instance.TargetId.IsValid || instance.Potency < 0 ||
                !catalog.TryGetStatus(instance.DefinitionId, out StatusDefinition? definition) ||
                instance.DefinitionRevision != definition!.Revision || instance.Stacks is < 1 ||
                instance.Stacks > definition.MaximumStacks || !IsDurationValid(definition, instance.RemainingDuration, limits))
            {
                return false;
            }
        }

        foreach (IGrouping<StatusTargetId, StatusInstance> target in state.Instances.GroupBy(value => value.TargetId))
        {
            if (target.Count() > limits.MaximumInstancesPerTarget)
            {
                return false;
            }

            foreach (IGrouping<StatusId, StatusInstance> same in target.GroupBy(value => value.DefinitionId))
            {
                catalog.TryGetStatus(same.Key, out StatusDefinition? definition);
                if (same.Count() > 1 && definition!.StackPolicy != StatusStackPolicy.Independent)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsDurationValid(StatusDefinition definition, int duration, StatusSystemLimits limits) =>
        definition.DurationType == StatusDurationType.Timed
            ? duration is > 0 && duration <= limits.MaximumDuration
            : duration == 0;

    private static int ComparePrecedence(
        StatusInstance left,
        StatusDefinition leftDefinition,
        StatusInstance right,
        StatusDefinition rightDefinition)
    {
        int comparison = leftDefinition.Priority.CompareTo(rightDefinition.Priority);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Potency.CompareTo(right.Potency);
        return comparison != 0 ? comparison : left.DefinitionId.CompareTo(right.DefinitionId);
    }

    private static void AppendPending(
        ImmutableArray<PendingEffect>.Builder destination,
        ImmutableArray<EffectId> effectIds,
        StatusInstance instance,
        string lifecycle)
    {
        foreach (EffectId effectId in effectIds)
        {
            destination.Add(new PendingEffect(
                EffectInvocationId.Derive(
                    instance.InstanceId,
                    effectId,
                    lifecycle,
                    instance.RemainingDuration,
                    destination.Count),
                effectId,
                instance.SourceId,
                instance.TargetId.Value,
                instance.InstanceId,
                destination.Count,
                instance.Stacks));
        }
    }

    private static StatusState Normalize(StatusState state) => new(
        [.. state.Instances.OrderBy(value => value.TargetId).ThenBy(value => value.DefinitionId).ThenBy(value => value.InstanceId)]);

    private static StatusResult Accepted(
        StatusState state,
        ImmutableArray<PendingEffect> effects,
        bool changed) => new(state, effects, true, changed, StatusRejectionCodes.None);
}
