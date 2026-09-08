using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Effects;

/// <summary>
/// Resolves ordered effect applications into deterministic resource, damage, status, and event outcomes.
/// </summary>
/// <remarks>
/// Code flow: A seeded request derives stable invocation IDs, validates and orders applications, applies payloads to a working target state, and publishes all results as one resolution.
/// </remarks>
public readonly record struct EffectInvocationId(Guid Value) : IComparable<EffectInvocationId>
{
    public bool IsValid => Value != Guid.Empty;
    public int CompareTo(EffectInvocationId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString("N");

    public static EffectInvocationId Derive(ulong seed, ulong sequence, ContentId discriminator) =>
        FromText(FormattableString.Invariant($"{seed}|{sequence}|{discriminator}"));

    public static EffectInvocationId Derive(
        StatusInstanceId statusInstanceId,
        EffectId effectId,
        string lifecycle,
        int occurrence,
        int sequence) =>
        FromText(FormattableString.Invariant(
            $"{statusInstanceId}|{effectId}|{lifecycle}|{occurrence}|{sequence}"));

    private static EffectInvocationId FromText(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new EffectInvocationId(new Guid(hash.AsSpan(0, 16)));
    }
}

public sealed record EffectRequest(
    EffectInvocationId InvocationId,
    EffectApplicationDefinition Application,
    ContentId SourceId,
    ContentId DeclaredTargetId,
    StatusInstanceId? StatusInstanceId = null,
    int Scale = 1,
    int? AmountOverride = null)
{
    public EffectId EffectId => Application.EffectId;
    public ContentId TargetId => Application.TargetSelector == EffectTargetSelector.Source ? SourceId : DeclaredTargetId;
}

public sealed record EffectTargetState(
    ContentId TargetId,
    CharacterResourceSet Resources,
    int Armor,
    int Shield,
    StatusState Statuses);

public enum EffectOutcome : byte
{
    Applied,
    NoChange,
    SkippedProbability,
}

public sealed record ResolvedEffect(
    EffectInvocationId InvocationId,
    EffectId EffectId,
    EffectType Type,
    ContentId SourceId,
    ContentId TargetId,
    int Amount,
    EffectOutcome Outcome,
    ContentId? EventId);

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
            state.Statuses.Instances.Any(value => value.TargetId.Value != state.TargetId))
        {
            return EffectResolution.Rejected(state, StatusRejectionCodes.InvalidState);
        }

        Queue<EffectRequest> pending = [];
        EffectTargetState candidate = state;
        ImmutableArray<ResolvedEffect>.Builder resolved = ImmutableArray.CreateBuilder<ResolvedEffect>();
        HashSet<EffectInvocationId> invocationIds = [];
        try
        {
            foreach (EffectRequest? request in requests)
            {
                if (request is null)
                {
                    return EffectResolution.Rejected(state, StatusRejectionCodes.InvalidState);
                }

                if (pending.Count >= limits.MaximumEffectsPerTransaction)
                {
                    return EffectResolution.Rejected(state, StatusRejectionCodes.CapacityExceeded);
                }

                pending.Enqueue(request);
            }

            while (pending.TryDequeue(out EffectRequest? request))
            {
                if (request.Application is null || resolved.Count >= limits.MaximumEffectsPerTransaction ||
                    !request.InvocationId.IsValid || !invocationIds.Add(request.InvocationId) ||
                    request.TargetId != state.TargetId || request.Scale <= 0 ||
                    request.AmountOverride is < 0 ||
                    request.Application.Timing != EffectTiming.Instant || !IsApplicationValid(request.Application) ||
                    !request.SourceId.IsValid || !catalog.TryGetEffect(request.EffectId, out EffectDefinition? definition) ||
                    !IsPayloadValid(definition!.Payload))
                {
                    return EffectResolution.Rejected(state, StatusRejectionCodes.InvalidState);
                }

                if (!PassesProbability(request))
                {
                    resolved.Add(new ResolvedEffect(
                        request.InvocationId, request.EffectId, definition!.Type, request.SourceId, request.TargetId,
                        0, EffectOutcome.SkippedProbability, null));
                    continue;
                }

                AppliedEffect applied = Apply(candidate, request, definition!, catalog, limits, pending);
                candidate = applied.State;
                resolved.Add(new ResolvedEffect(
                    request.InvocationId, definition!.EffectId, definition.Type, request.SourceId, request.TargetId,
                    applied.Amount, applied.Changed ? EffectOutcome.Applied : EffectOutcome.NoChange, applied.EventId));
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

    private static AppliedEffect Apply(
        EffectTargetState state,
        EffectRequest request,
        EffectDefinition definition,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits,
        Queue<EffectRequest> pending) =>
        definition.Payload switch
        {
            ResourceEffectPayload payload => ApplyResource(state, request, payload),
            DamageEffectPayload payload => ApplyDamage(state, request, payload),
            ApplyStatusEffectPayload payload => ApplyStatus(state, request, payload, catalog, limits, pending),
            RemoveStatusEffectPayload payload => RemoveStatus(state, request, payload, catalog, limits, pending),
            ModifierEffectPayload payload => new AppliedEffect(state, ResolveAmount(request, payload.Amount), false, null),
            GrantShieldEffectPayload payload => ApplyShield(state, request, payload),
            EmitEventEffectPayload payload => new AppliedEffect(state, 0, true, payload.EventId),
            _ => throw new InvalidOperationException(),
        };

    private static AppliedEffect ApplyResource(
        EffectTargetState state,
        EffectRequest request,
        ResourceEffectPayload payload)
    {
        int amount = ResolveAmount(request, payload.Amount);
        int before = state.Resources.GetCurrentValue(payload.ResourceId);
        CharacterResourceSet resources = payload.Type switch
        {
            EffectType.HealHealth or EffectType.RestoreMana or EffectType.RestoreStamina or EffectType.RestoreResolve =>
                state.Resources.RestoreResource(payload.ResourceId, amount),
            EffectType.ReduceStrain => state.Resources.ReduceStrain(amount),
            _ => throw new InvalidOperationException(),
        };
        int actual = Math.Abs(resources.GetCurrentValue(payload.ResourceId) - before);
        return new AppliedEffect(state with { Resources = resources }, actual, actual != 0, null);
    }

    private static AppliedEffect ApplyDamage(
        EffectTargetState state,
        EffectRequest request,
        DamageEffectPayload payload)
    {
        int amount = ResolveAmount(request, payload.Amount);
        if (payload.Type == EffectType.ArmorDamage)
        {
            int armor = Math.Max(0, state.Armor - amount);
            return new AppliedEffect(state with { Armor = armor }, state.Armor - armor, armor != state.Armor, null);
        }

        int before = state.Resources.GetCurrentValue(CharacterResourceIds.Health);
        CharacterResourceSet resources = state.Resources.ApplyResourceDamage(CharacterResourceIds.Health, amount);
        int actual = before - resources.GetCurrentValue(CharacterResourceIds.Health);
        return new AppliedEffect(state with { Resources = resources }, actual, actual != 0, null);
    }

    private static AppliedEffect ApplyStatus(
        EffectTargetState state,
        EffectRequest request,
        ApplyStatusEffectPayload payload,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits,
        Queue<EffectRequest> pending)
    {
        StatusInstanceId instanceId = request.StatusInstanceId ?? new StatusInstanceId(request.InvocationId.Value);
        StatusResult applied = StatusSystem.Apply(
            state.Statuses,
            new StatusApplicationRequest(
                instanceId, payload.StatusId, request.SourceId, new StatusTargetId(request.TargetId),
                payload.Duration, payload.Stacks, payload.Potency),
            catalog,
            limits);
        if (!applied.Accepted)
        {
            throw new EffectRejectedException(applied.RejectionCode);
        }

        Enqueue(applied.PendingEffects, pending, limits);
        return new AppliedEffect(state with { Statuses = applied.State }, 0, applied.Changed, null);
    }

    private static AppliedEffect RemoveStatus(
        EffectTargetState state,
        EffectRequest request,
        RemoveStatusEffectPayload payload,
        IStatusDefinitionCatalog catalog,
        StatusSystemLimits limits,
        Queue<EffectRequest> pending)
    {
        StatusResult removed = StatusSystem.RemoveByDefinition(
            state.Statuses, payload.StatusId, new StatusTargetId(request.TargetId), catalog, limits);
        if (!removed.Accepted)
        {
            throw new EffectRejectedException(removed.RejectionCode);
        }

        Enqueue(removed.PendingEffects, pending, limits);
        return new AppliedEffect(state with { Statuses = removed.State }, 0, removed.Changed, null);
    }

    private static AppliedEffect ApplyShield(
        EffectTargetState state,
        EffectRequest request,
        GrantShieldEffectPayload payload)
    {
        int amount = ResolveAmount(request, payload.Amount);
        int shield = (int)Math.Clamp((long)state.Shield + amount, 0, int.MaxValue);
        return new AppliedEffect(state with { Shield = shield }, shield - state.Shield, shield != state.Shield, null);
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
            pending.Enqueue(new EffectRequest(
                effect.InvocationId, EffectApplicationDefinition.InstantTarget(effect.EffectId),
                effect.SourceId, effect.TargetId, null, effect.Scale));
        }
    }

    private static bool IsApplicationValid(EffectApplicationDefinition application) =>
        application.EffectId.IsValid && application.Delay >= 0 && application.Duration >= 0 && application.Interval >= 0 &&
        application.Probability is >= 0 and <= 100 && application.Power >= 0 &&
        !application.Tags.IsDefault && application.Tags.Length <= 128 &&
        application.Tags.All(value => !string.IsNullOrWhiteSpace(value)) &&
        (application.Timing != EffectTiming.Instant ||
            application.Delay == 0 && application.Duration == 0 && application.Interval == 0);

    private static bool IsPayloadValid(EffectPayload? payload) => payload switch
    {
        ResourceEffectPayload value => value.Amount >= 0 && value.Type switch
        {
            EffectType.HealHealth => value.ResourceId == CharacterResourceIds.Health,
            EffectType.RestoreMana => value.ResourceId == CharacterResourceIds.Mana,
            EffectType.RestoreStamina => value.ResourceId == CharacterResourceIds.Stamina,
            EffectType.RestoreResolve => value.ResourceId == CharacterResourceIds.Resolve,
            EffectType.ReduceStrain => value.ResourceId == CharacterResourceIds.Strain,
            _ => false,
        },
        DamageEffectPayload value => value.Amount >= 0 && value.Type is
            EffectType.PhysicalDamage or EffectType.ThermalDamage or EffectType.ShockDamage or
            EffectType.ArcaneDamage or EffectType.ArmorDamage,
        ApplyStatusEffectPayload value => value.StatusId.IsValid && value.Duration is null or >= 0 &&
            value.Stacks > 0 && value.Potency >= 0,
        RemoveStatusEffectPayload value => value.StatusId.IsValid,
        ModifierEffectPayload value => value.Amount >= 0 && value.Type is
            EffectType.ModifyDamage or EffectType.ModifyDefense or EffectType.ModifyAccuracy or
            EffectType.ModifyMovement or EffectType.ModifyResistance,
        GrantShieldEffectPayload value => value.Amount >= 0,
        EmitEventEffectPayload value => value.EventId.IsValid,
        _ => false,
    };

    private static bool PassesProbability(EffectRequest request)
    {
        if (request.Application.Probability == 100)
        {
            return true;
        }

        Span<byte> bytes = stackalloc byte[16];
        request.InvocationId.Value.TryWriteBytes(bytes);
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        return value % 100 < request.Application.Probability;
    }

    private static int ResolveAmount(EffectRequest request, int authoredAmount) =>
        (int)Math.Clamp((long)(request.AmountOverride ?? authoredAmount) * request.Scale, 0, int.MaxValue);

    private sealed record AppliedEffect(EffectTargetState State, int Amount, bool Changed, ContentId? EventId);

    private sealed class EffectRejectedException(string rejectionCode) : Exception
    {
        public string RejectionCode { get; } = rejectionCode;
    }
}
