using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Combat;

internal readonly record struct ImmediateEffectAmount(EffectId EffectId, int Amount);

/// <summary>Shared deterministic primitives used by otherwise independent personal-action systems.</summary>
internal static class CombatResolutionUtilities
{
    public static int DeterministicRoll(ulong seed, ulong sequence) =>
        DeterministicRange(seed, sequence, 1, 100);

    public static int DeterministicRange(ulong seed, ulong sequence, int minimum, int maximum)
    {
        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum));
        }

        ulong value = seed + (sequence + 1) * 0x9e3779b97f4a7c15UL;
        value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
        value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
        value ^= value >> 31;
        ulong width = (ulong)((long)maximum - minimum + 1);
        return minimum + (int)(value % width);
    }

    public static ImmutableArray<EffectRequest> BuildEffectRequests(
        ImmutableArray<EffectApplicationDefinition> applications,
        ContentId sourceId,
        ContentId targetId,
        ulong seed,
        ulong sequence) =>
        BuildEffectRequests([], applications, sourceId, targetId, seed, sequence);

    public static ImmutableArray<EffectRequest> BuildEffectRequests(
        IEnumerable<ImmediateEffectAmount> immediateEffects,
        ImmutableArray<EffectApplicationDefinition> applications,
        ContentId sourceId,
        ContentId targetId,
        ulong seed,
        ulong sequence)
    {
        ImmutableArray<EffectRequest>.Builder requests = ImmutableArray.CreateBuilder<EffectRequest>();
        foreach (ImmediateEffectAmount immediate in immediateEffects)
        {
            if (immediate.Amount <= 0)
            {
                continue;
            }

            requests.Add(new EffectRequest(
                EffectInvocationId.Derive(seed, sequence + (ulong)requests.Count, immediate.EffectId.Value),
                EffectApplicationDefinition.InstantTarget(immediate.EffectId),
                sourceId,
                targetId,
                AmountOverride: immediate.Amount));
        }

        foreach (EffectApplicationDefinition application in applications)
        {
            requests.Add(new EffectRequest(
                EffectInvocationId.Derive(seed, sequence + (ulong)requests.Count, application.EffectId.Value),
                application,
                sourceId,
                targetId));
        }

        return requests.ToImmutable();
    }
}
