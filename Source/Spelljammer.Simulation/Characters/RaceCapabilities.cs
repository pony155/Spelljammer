using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Characters;

/// <summary>
/// Represents evidence of a route observed by a character with a confidence level.
/// </summary>
/// <param name="RouteId">The ID of the route being tracked.</param>
/// <param name="EvidenceId">The type of evidence observed (footprints, scent, magical traces, etc.).</param>
/// <param name="Confidence">The confidence level in this evidence (0-255).</param>
public sealed record ObservedRouteEvidence(ContentId RouteId, ContentId EvidenceId, byte Confidence);

/// <summary>
/// The interpretation of observed evidence as a coherent trail or route.
/// </summary>
/// <param name="RouteId">The ID of the route this trail represents.</param>
/// <param name="EvidenceIds">The collection of evidence types that support this trail interpretation.</param>
/// <param name="Confidence">The overall confidence in this trail interpretation (0-255).</param>
public sealed record TrailInterpretation(ContentId RouteId, ImmutableArray<ContentId> EvidenceIds, byte Confidence);

/// <summary>
/// Utility class for generating race-specific actions and abilities based on character effects and Feats.
/// </summary>
/// <remarks>
/// This class creates dynamic action definitions for racial special abilities like soul anchor recovery
/// and trail sense tracking. Actions are only generated if the character has the appropriate effects enabled.
/// </remarks>
public static class RaceCapabilities
{
    private static readonly EffectId SoulAnchorEffect = new("effect.recovery.soul-anchor");
    private static readonly EffectId TrailSenseEffect = new("effect.tracking.observed-trail");

    /// <summary>
    /// Creates a soul anchor recovery action if the character has the soul anchor effect.
    /// </summary>
    /// <param name="character">The character to check for the effect.</param>
    /// <param name="catalog">The content catalog for validation.</param>
    /// <returns>The soul anchor recovery action if available; null otherwise.</returns>
    public static ActionDefinition? CreateSoulAnchorRecoveryAction(
        CharacterState character,
        ICharacterDefinitionCatalog catalog)
    {
        if (!HasEffect(character, catalog, SoulAnchorEffect))
        {
            return null;
        }

        return new ActionDefinition(
            new ActionId("action.recovery.soul-anchor"),
            new ContentId("formula.check.standard"),
            new ActionRequirement(
                null,
                new FeatId("feat.active.recovery.soul-reconstitution"),
                new SkillId("skill.enchantment"),
                0,
                new AbilityId("ability.willpower"),
                1,
                new ContentId("equipment.soul-anchor.portable"),
                new ContentId("context.recovery.safe-anchor")),
            [new ActionCost(new ResourceId("resource.resonance"), 2)],
            75,
            0,
            []);
    }

    public static ImmutableArray<TrailInterpretation> InterpretObservedTrails(
        CharacterState character,
        ICharacterDefinitionCatalog catalog,
        IReadOnlyList<ObservedRouteEvidence> observedEvidence)
    {
        ArgumentNullException.ThrowIfNull(observedEvidence);
        if (!HasEffect(character, catalog, TrailSenseEffect) || observedEvidence.Count > 256)
        {
            return [];
        }

        return
        [
            .. observedEvidence
                .Where(value => value.Confidence > 0)
                .GroupBy(value => value.RouteId)
                .OrderBy(group => group.Key)
                .Select(group => new TrailInterpretation(
                    group.Key,
                    [.. group.Select(value => value.EvidenceId).Distinct().Order()],
                    group.Max(value => value.Confidence))),
        ];
    }

    private static bool HasEffect(CharacterState character, ICharacterDefinitionCatalog catalog, EffectId effectId)
    {
        if (character.ContentFingerprint != catalog.Fingerprint)
        {
            return false;
        }

        foreach (FeatId featId in character.Capabilities.Feats)
        {
            if (catalog.TryGetFeat(featId, out FeatDefinition? feat) &&
                feat!.EffectIds.Contains(effectId))
            {
                return true;
            }
        }

        return false;
    }
}
