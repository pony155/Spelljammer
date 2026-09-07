using System.Collections.Immutable;
using System.Text;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

public sealed record CharacterCreationRequest(
    ContentFingerprint ContentFingerprint,
    ScenarioId ScenarioId,
    CharacterId CharacterId,
    RaceId RaceId,
    HeritageId HeritageId,
    BackgroundId BackgroundId,
    ulong Seed);

public enum CharacterCreationFailure : byte
{
    None,
    ContentMismatch,
    DefinitionMissing,
    DefinitionMismatch,
    IncompatibleHeritage,
    IncompatibleBackground,
    MissingGrant,
    CapacityExceeded,
    SupportUnavailable,
    EquipmentUnavailable,
}

public sealed record CharacterCreationResult(CharacterState? Character, CharacterCreationFailure Failure, ContentId? RelatedId)
{
    public bool Succeeded => Character is not null;
}

public sealed record CrewSupportProfile(
    ImmutableHashSet<ContentId> SupportedRequirementIds,
    ImmutableHashSet<ContentId> AvailableEquipmentIds);

public sealed record RosterSnapshot(
    ContentFingerprint ContentFingerprint,
    ScenarioId ScenarioId,
    ImmutableArray<CharacterState> Characters,
    ImmutableArray<AttributeDefinition> AttributeColumns,
    ImmutableArray<SkillDefinition> SkillColumns);

public sealed record RosterCreationResult(RosterSnapshot? Roster, CharacterCreationFailure Failure, ContentId? RelatedId)
{
    public bool Succeeded => Roster is not null;
}

public static class CharacterCreator
{
    private const int MaximumRosterSize = 64;

    public static CharacterCreationResult Create(
        CharacterCreationRequest request,
        ICharacterContentCatalog catalog,
        CrewSupportProfile support)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(support);
        if (request.ContentFingerprint != catalog.Fingerprint)
        {
            return Failure(CharacterCreationFailure.ContentMismatch);
        }

        if (!catalog.TryGetCharacter(request.CharacterId, out CharacterDefinition? template))
        {
            return Failure(CharacterCreationFailure.DefinitionMissing, request.CharacterId.Value);
        }

        if (!catalog.TryGetRace(request.RaceId, out RaceDefinition? race))
        {
            return Failure(CharacterCreationFailure.DefinitionMissing, request.RaceId.Value);
        }

        if (!catalog.TryGetHeritage(request.HeritageId, out HeritageDefinition? heritage))
        {
            return Failure(CharacterCreationFailure.DefinitionMissing, request.HeritageId.Value);
        }

        if (!catalog.TryGetBackground(request.BackgroundId, out BackgroundDefinition? background))
        {
            return Failure(CharacterCreationFailure.DefinitionMissing, request.BackgroundId.Value);
        }

        if (template!.RaceId != request.RaceId || template.HeritageId != request.HeritageId ||
            template.BackgroundId != request.BackgroundId || !template.ScenarioIds.Contains(request.ScenarioId))
        {
            return Failure(CharacterCreationFailure.DefinitionMismatch, template.CharacterId.Value);
        }

        if (heritage!.RaceId != request.RaceId)
        {
            return Failure(CharacterCreationFailure.IncompatibleHeritage, heritage.HeritageId.Value);
        }

        if (!background!.CompatibleRaceIds.Contains(request.RaceId))
        {
            return Failure(CharacterCreationFailure.IncompatibleBackground, background.BackgroundId.Value);
        }

        foreach (ContentId requiredSupport in race!.RequiredSupportIds)
        {
            if (!support.SupportedRequirementIds.Contains(requiredSupport))
            {
                return Failure(CharacterCreationFailure.SupportUnavailable, requiredSupport);
            }
        }

        foreach (ContentId equipmentId in template.EquipmentIds)
        {
            if (!support.AvailableEquipmentIds.Contains(equipmentId))
            {
                return Failure(CharacterCreationFailure.EquipmentUnavailable, equipmentId);
            }
        }

        OwnedRandom random = new(DeriveSeed(request));
        ImmutableArray<short>.Builder attributes = ImmutableArray.CreateBuilder<short>(catalog.Attributes.Length);
        foreach (AttributeDefinition definition in catalog.Attributes)
        {
            int value = definition.DefaultValue + random.NextInclusive(-1, 1);
            if (background.AttributeBonusIds.Contains(definition.AttributeId))
            {
                value++;
            }

            attributes.Add((short)Math.Clamp(value, definition.Minimum, definition.Maximum));
        }

        ImmutableHashSet<SkillId> focusSkills = background.FocusSkillIds.Union(template.FocusSkillIds).ToImmutableHashSet();
        ImmutableArray<byte>.Builder skills = ImmutableArray.CreateBuilder<byte>(catalog.Skills.Length);
        foreach (SkillDefinition definition in catalog.Skills)
        {
            int value = focusSkills.Contains(definition.SkillId)
                ? random.NextInclusive(20, 35)
                : random.NextInclusive(0, 4);
            skills.Add((byte)Math.Clamp(value, definition.Minimum, definition.Maximum));
        }

        GrantCollector grants = new(catalog, request.RaceId);
        foreach (PerkId perkId in race.GrantedPerkIds)
        {
            if (!grants.AddPerk(perkId, race.RaceId.Value, GrantSourceKind.Race, out ContentId? missing))
            {
                return Failure(grants.CapacityExceeded ? CharacterCreationFailure.CapacityExceeded : CharacterCreationFailure.MissingGrant, missing);
            }
        }

        foreach (PerkId perkId in heritage.GrantedPerkIds)
        {
            if (!grants.AddPerk(perkId, heritage.HeritageId.Value, GrantSourceKind.Heritage, out ContentId? missing))
            {
                return Failure(grants.CapacityExceeded ? CharacterCreationFailure.CapacityExceeded : CharacterCreationFailure.MissingGrant, missing);
            }
        }

        CharacterCapabilities capabilities = new(
            catalog.Fingerprint,
            attributes.MoveToImmutable(),
            skills.MoveToImmutable(),
            ImmutableArray.CreateRange(Enumerable.Repeat((ushort)0, catalog.Skills.Length)),
            ImmutableHashSet<FeatId>.Empty,
            grants.Perks,
            grants.Techniques,
            grants.Sources);
        ImmutableDictionary<ResourceId, int> resources = template.ResourceIds
            .ToImmutableDictionary(id => id, id => id == new ResourceId("resource.psychic-strain") ? 0 : 10);

        CharacterState published = new(
            template.CharacterId,
            catalog.Fingerprint,
            request.ScenarioId,
            race.RaceId,
            heritage.HeritageId,
            background.BackgroundId,
            template.PositionId,
            capabilities,
            template.LanguageIds,
            template.ScriptIds,
            template.EquipmentIds.ToImmutableHashSet(),
            resources,
            ImmutableDictionary<TrainingProjectId, int>.Empty);
        return new CharacterCreationResult(published, CharacterCreationFailure.None, null);
    }

    public static RosterCreationResult CreateRoster(
        ContentFingerprint fingerprint,
        ScenarioId scenarioId,
        ulong seed,
        ICharacterContentCatalog catalog,
        CrewSupportProfile support)
    {
        if (fingerprint != catalog.Fingerprint)
        {
            return new RosterCreationResult(null, CharacterCreationFailure.ContentMismatch, null);
        }

        CharacterDefinition[] templates = catalog.Characters
            .Where(value => value.ScenarioIds.Contains(scenarioId))
            .OrderBy(value => value.CharacterId)
            .ToArray();
        if (templates.Length is 0 or > MaximumRosterSize)
        {
            return new RosterCreationResult(null, CharacterCreationFailure.CapacityExceeded, null);
        }

        ImmutableArray<CharacterState>.Builder characters = ImmutableArray.CreateBuilder<CharacterState>(templates.Length);
        for (int index = 0; index < templates.Length; index++)
        {
            CharacterDefinition template = templates[index];
            CharacterCreationRequest request = new(
                fingerprint,
                scenarioId,
                template.CharacterId,
                template.RaceId,
                template.HeritageId,
                template.BackgroundId,
                seed + (ulong)index);
            CharacterCreationResult result = Create(request, catalog, support);
            if (!result.Succeeded)
            {
                return new RosterCreationResult(null, result.Failure, result.RelatedId);
            }

            characters.Add(result.Character!);
        }

        return new RosterCreationResult(
            new RosterSnapshot(fingerprint, scenarioId, characters.MoveToImmutable(), catalog.Attributes, catalog.Skills),
            CharacterCreationFailure.None,
            null);
    }

    private static CharacterCreationResult Failure(CharacterCreationFailure failure, ContentId? id = null) =>
        new(null, failure, id);

    private static ulong DeriveSeed(CharacterCreationRequest request)
    {
        ulong hash = 14695981039346656037UL ^ request.Seed;
        foreach (byte value in Encoding.UTF8.GetBytes(request.ContentFingerprint + ":" + request.CharacterId))
        {
            hash = (hash ^ value) * 1099511628211UL;
        }

        return hash;
    }

    private struct OwnedRandom
    {
        private ulong state;

        public OwnedRandom(ulong seed) => state = seed == 0 ? 0x9e3779b97f4a7c15UL : seed;

        public int NextInclusive(int minimum, int maximum)
        {
            state += 0x9e3779b97f4a7c15UL;
            ulong value = state;
            value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
            value ^= value >> 31;
            return minimum + (int)(value % (uint)(maximum - minimum + 1));
        }
    }

    private sealed class GrantCollector(ICharacterContentCatalog catalog, RaceId raceId)
    {
        private readonly HashSet<PerkId> perks = [];
        private readonly HashSet<TechniqueId> techniques = [];
        private readonly List<CapabilityGrant> sources = [];

        public ImmutableHashSet<PerkId> Perks => perks.ToImmutableHashSet();
        public ImmutableHashSet<TechniqueId> Techniques => techniques.ToImmutableHashSet();
        public ImmutableArray<CapabilityGrant> Sources => [.. sources];
        public bool CapacityExceeded { get; private set; }

        public bool AddPerk(PerkId perkId, ContentId sourceId, GrantSourceKind sourceKind, out ContentId? missing)
        {
            return AddPerk(perkId, sourceId, sourceKind, 0, out missing);
        }

        private bool AddPerk(PerkId perkId, ContentId sourceId, GrantSourceKind sourceKind, int depth, out ContentId? missing)
        {
            missing = null;
            if (depth >= 64)
            {
                CapacityExceeded = true;
                return false;
            }

            if (perks.Contains(perkId))
            {
                return true;
            }

            if (!catalog.TryGetPerk(perkId, out PerkDefinition? perk) || !perk!.CompatibleRaceIds.Contains(raceId))
            {
                missing = perkId.Value;
                return false;
            }

            if (perks.Count >= CharacterCapabilities.MaximumSetEntries)
            {
                CapacityExceeded = true;
                return false;
            }

            perks.Add(perkId);
            sources.Add(new CapabilityGrant(perkId.Value, sourceId, sourceKind));
            foreach (AccessId accessId in perk.GrantedAccessIds)
            {
                sources.Add(new CapabilityGrant(accessId.Value, perkId.Value, GrantSourceKind.Perk));
            }

            foreach (TechniqueId techniqueId in perk.GrantedTechniqueIds)
            {
                if (!TryGetTechnique(techniqueId, out ImmutableArray<PerkId> nestedPerks))
                {
                    missing = techniqueId.Value;
                    return false;
                }

                techniques.Add(techniqueId);
                sources.Add(new CapabilityGrant(techniqueId.Value, perkId.Value, GrantSourceKind.Perk));
                foreach (PerkId nested in nestedPerks)
                {
                    if (!AddPerk(nested, techniqueId.Value, GrantSourceKind.Technique, depth + 1, out missing))
                    {
                        return false;
                    }
                }
            }

            foreach (PerkId nested in perk.GrantedPerkIds)
            {
                if (!AddPerk(nested, perkId.Value, GrantSourceKind.Perk, depth + 1, out missing))
                {
                    return false;
                }
            }

            CapacityExceeded = sources.Count > CharacterCapabilities.MaximumSetEntries;
            return !CapacityExceeded;
        }

        private bool TryGetTechnique(TechniqueId id, out ImmutableArray<PerkId> grantedPerks)
        {
            if (id.Value.ToString().StartsWith("spell.", StringComparison.Ordinal) &&
                catalog.TryGetSpell(new SpellId(id.Value), out _))
            {
                grantedPerks = [];
                return true;
            }

            if (id.Value.ToString().StartsWith("psychic.", StringComparison.Ordinal) &&
                catalog.TryGetPsychicTechnique(new PsychicTechniqueId(id.Value), out _))
            {
                grantedPerks = [];
                return true;
            }

            if (catalog.TryGetTechnique(id, out TechniqueDefinition? technique))
            {
                grantedPerks = technique!.GrantedPerkIds;
                return true;
            }

            grantedPerks = [];
            return false;
        }
    }
}
