using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Statuses;

public sealed record EffectRequest(
    EffectId EffectId,
    ContentId SourceId,
    ContentId TargetId,
    StatusInstanceId? StatusInstanceId = null,
    int Scale = 1);

public sealed record EffectTargetState(
    ContentId TargetId,
    CharacterResourceSet Resources,
    int Armor,
    int Shield,
    StatusState Statuses);

public sealed record ResolvedEffect(
    EffectId EffectId,
    EffectType Type,
    ContentId SourceId,
    ContentId TargetId,
    int Amount);

public sealed record EffectResolution(
    EffectTargetState State,
    ImmutableArray<ResolvedEffect> Effects,
    bool Accepted,
    string RejectionCode)
{
    public static EffectResolution Rejected(EffectTargetState state, string code) => new(state, [], false, code);
}

public static class EffectSystem
{
    public static EffectResolution Resolve(
        EffectTargetState state,
        IEnumerable<EffectRequest> requests,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        StatusResult statusValidation = StatusSystem.Create(state.Statuses, catalog, limits);
        if (!state.TargetId.IsValid || state.Armor < 0 || state.Shield < 0 || !statusValidation.Accepted ||
            state.Statuses.Instances.Any(value => value.TargetId != state.TargetId))
        {
            return EffectResolution.Rejected(state, StatusRejectionCodes.InvalidState);
        }

        Queue<EffectRequest> pending = [];
        EffectTargetState candidate = state;
        ImmutableArray<ResolvedEffect>.Builder resolved = ImmutableArray.CreateBuilder<ResolvedEffect>();
        int sequence = 0;
        try
        {
            foreach (EffectRequest request in requests)
            {
                if (pending.Count >= limits.MaximumEffectsPerTransaction)
                {
                    return EffectResolution.Rejected(state, StatusRejectionCodes.CapacityExceeded);
                }

                pending.Enqueue(request);
            }

            while (pending.TryDequeue(out EffectRequest? request))
            {
                if (resolved.Count >= limits.MaximumEffectsPerTransaction || request.TargetId != state.TargetId || request.Scale <= 0 ||
                    !request.SourceId.IsValid || !catalog.TryGetEffect(request.EffectId, out EffectDefinition? definition))
                {
                    return EffectResolution.Rejected(state, StatusRejectionCodes.InvalidState);
                }

                int amount = (int)Math.Clamp((long)definition!.Amount * request.Scale, 0, int.MaxValue);
                candidate = Apply(candidate, request, definition, amount, catalog, limits, pending, sequence++);
                resolved.Add(new ResolvedEffect(
                    definition.EffectId, definition.Type, request.SourceId, request.TargetId, amount));
            }
        }
        catch (EffectRejectedException exception)
        {
            return EffectResolution.Rejected(state, exception.RejectionCode);
        }
        catch (InvalidOperationException)
        {
            return EffectResolution.Rejected(state, StatusRejectionCodes.InvalidState);
        }

        return new EffectResolution(candidate, resolved.ToImmutable(), true, StatusRejectionCodes.None);
    }

    private static EffectTargetState Apply(
        EffectTargetState state,
        EffectRequest request,
        EffectDefinition definition,
        int amount,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits,
        Queue<EffectRequest> pending,
        int sequence)
    {
        CharacterResourceSet resources = state.Resources;
        switch (definition.Type)
        {
            case EffectType.HealHealth:
                resources = resources.RestoreResource(CharacterResourceIds.Health, amount);
                break;
            case EffectType.RestoreMana:
                resources = resources.RestoreResource(CharacterResourceIds.Mana, amount);
                break;
            case EffectType.RestoreStamina:
                resources = resources.RestoreResource(CharacterResourceIds.Stamina, amount);
                break;
            case EffectType.RestoreResolve:
                resources = resources.RestoreResource(CharacterResourceIds.Resolve, amount);
                break;
            case EffectType.ReduceStrain:
                resources = resources.ReduceStrain(amount);
                break;
            case EffectType.PhysicalDamage:
            case EffectType.ThermalDamage:
            case EffectType.ShockDamage:
            case EffectType.ArcaneDamage:
                resources = resources.ApplyResourceDamage(CharacterResourceIds.Health, amount);
                break;
            case EffectType.ArmorDamage:
                return state with { Armor = Math.Max(0, state.Armor - amount) };
            case EffectType.GrantShield:
                return state with { Shield = AddClamped(state.Shield, amount) };
            case EffectType.ApplyStatus:
            {
                StatusId statusId = definition.StatusId ?? throw new InvalidOperationException();
                StatusInstanceId instanceId = request.StatusInstanceId ?? DeriveInstanceId(request, sequence);
                StatusResult applied = StatusSystem.Apply(
                    state.Statuses,
                    new StatusApplicationRequest(
                        instanceId, statusId, request.SourceId, request.TargetId,
                        definition.Duration == 0 ? null : definition.Duration,
                        definition.Stacks, definition.Potency),
                    catalog,
                    limits);
                if (!applied.Accepted)
                {
                    throw new EffectRejectedException(applied.RejectionCode);
                }

                Enqueue(applied.PendingEffects, pending, limits);
                return state with { Statuses = applied.State };
            }
            case EffectType.RemoveStatus:
            {
                StatusId statusId = definition.StatusId ?? throw new InvalidOperationException();
                StatusResult removed = StatusSystem.RemoveByDefinition(state.Statuses, statusId, request.TargetId, catalog, limits);
                if (!removed.Accepted)
                {
                    throw new EffectRejectedException(removed.RejectionCode);
                }

                Enqueue(removed.PendingEffects, pending, limits);
                return state with { Statuses = removed.State };
            }
            case EffectType.ModifyDamage:
            case EffectType.ModifyDefense:
            case EffectType.ModifyAccuracy:
            case EffectType.ModifyMovement:
            case EffectType.ModifyResistance:
                break;
        }

        return state with { Resources = resources };
    }

    private static void Enqueue(
        ImmutableArray<PendingEffect> effects,
        Queue<EffectRequest> pending,
        StatusSystemLimits limits)
    {
        if (pending.Count + effects.Length > limits.MaximumEffectsPerTransaction)
        {
            throw new EffectRejectedException(StatusRejectionCodes.CapacityExceeded);
        }

        foreach (PendingEffect effect in effects.OrderBy(value => value.Sequence))
        {
            pending.Enqueue(new EffectRequest(effect.EffectId, effect.SourceId, effect.TargetId, null, effect.Scale));
        }
    }

    private static StatusInstanceId DeriveInstanceId(EffectRequest request, int sequence)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{request.EffectId}|{request.SourceId}|{request.TargetId}|{sequence}"));
        return new StatusInstanceId(new Guid(hash.AsSpan(0, 16)));
    }

    private static int AddClamped(int value, int addition) =>
        (int)Math.Clamp((long)value + addition, 0, int.MaxValue);

    private sealed class EffectRejectedException(string rejectionCode) : Exception
    {
        public string RejectionCode { get; } = rejectionCode;
    }
}
