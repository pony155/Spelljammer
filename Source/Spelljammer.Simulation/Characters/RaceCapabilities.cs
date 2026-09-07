using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

public sealed record ObservedRouteEvidence(ContentId RouteId, ContentId EvidenceId, byte Confidence);

public sealed record TrailInterpretation(ContentId RouteId, ImmutableArray<ContentId> EvidenceIds, byte Confidence);

public static class RaceCapabilities
{
    private static readonly ContentId SoulAnchorEffect = new("effect.recovery.soul-anchor");
    private static readonly ContentId TrailSenseEffect = new("effect.tracking.observed-trail");

    public static ActionDefinition? CreateSoulAnchorRecoveryAction(
        CharacterState character,
        ICharacterContentCatalog catalog)
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
                new TechniqueId("technique.recovery.soul-reconstitution"),
                new SkillId("skill.enchantment"),
                0,
                new AttributeId("attribute.willpower"),
                1,
                new ContentId("equipment.soul-anchor.portable"),
                new ContentId("context.recovery.safe-anchor")),
            [new ActionCost(new ResourceId("resource.resonance"), 2)],
            75,
            0,
            4,
            []);
    }

    public static ImmutableArray<TrailInterpretation> InterpretObservedTrails(
        CharacterState character,
        ICharacterContentCatalog catalog,
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

    private static bool HasEffect(CharacterState character, ICharacterContentCatalog catalog, ContentId effectId)
    {
        if (character.ContentFingerprint != catalog.Fingerprint)
        {
            return false;
        }

        foreach (PerkId perkId in character.Capabilities.Perks)
        {
            if (catalog.TryGetPerk(perkId, out PerkDefinition? perk) && perk!.EffectIds.Contains(effectId))
            {
                return true;
            }
        }

        return false;
    }
}
