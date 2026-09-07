using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

public sealed class CharacterCapabilities
{
    public const int MaximumSetEntries = 256;
    public const int MaximumPracticeKeys = 512;

    private readonly ImmutableArray<short> attributeValues;
    private readonly ImmutableArray<byte> skillValues;
    private readonly ImmutableArray<ushort> skillPractice;
    private readonly ImmutableHashSet<ContentId> practiceKeys;

    internal CharacterCapabilities(
        ContentFingerprint fingerprint,
        ImmutableArray<short> attributeValues,
        ImmutableArray<byte> skillValues,
        ImmutableArray<ushort> skillPractice,
        ImmutableHashSet<FeatId> feats,
        ImmutableHashSet<PerkId> perks,
        ImmutableHashSet<TechniqueId> techniques,
        ImmutableArray<CapabilityGrant> grantSources,
        ImmutableHashSet<ContentId>? practiceKeys = null)
    {
        if (attributeValues.Length == 0 || skillValues.Length == 0 || skillValues.Length != skillPractice.Length ||
            feats.Count > MaximumSetEntries || perks.Count > MaximumSetEntries ||
            techniques.Count > MaximumSetEntries || grantSources.Length > MaximumSetEntries)
        {
            throw new ArgumentException("Character capability storage is incomplete or exceeds its bounded capacity.");
        }

        Fingerprint = fingerprint;
        this.attributeValues = attributeValues;
        this.skillValues = skillValues;
        this.skillPractice = skillPractice;
        Feats = feats;
        Perks = perks;
        Techniques = techniques;
        GrantSources = grantSources;
        this.practiceKeys = practiceKeys ?? ImmutableHashSet<ContentId>.Empty;
    }

    public ContentFingerprint Fingerprint { get; }
    public ImmutableHashSet<FeatId> Feats { get; }
    public ImmutableHashSet<PerkId> Perks { get; }
    public ImmutableHashSet<AccessId> Access => GrantSources
        .Where(value => value.CapabilityId.ToString().StartsWith("access.", StringComparison.Ordinal))
        .Select(value => new AccessId(value.CapabilityId))
        .ToImmutableHashSet();
    public ImmutableHashSet<TechniqueId> Techniques { get; }
    public ImmutableHashSet<SpellId> KnownSpellIds => Techniques
        .Where(value => value.Value.ToString().StartsWith("spell.", StringComparison.Ordinal))
        .Select(value => new SpellId(value.Value))
        .ToImmutableHashSet();
    public ImmutableHashSet<PsychicTechniqueId> KnownPsychicTechniqueIds => Techniques
        .Where(value => value.Value.ToString().StartsWith("psychic.", StringComparison.Ordinal))
        .Select(value => new PsychicTechniqueId(value.Value))
        .ToImmutableHashSet();
    public ImmutableArray<CapabilityGrant> GrantSources { get; }

    public bool TryGetAttribute(
        AttributeId id,
        ICharacterContentCatalog catalog,
        out short value,
        out CapabilityLookupFailure failure)
    {
        if (catalog.Fingerprint != Fingerprint)
        {
            value = default;
            failure = CapabilityLookupFailure.ContentMismatch;
            return false;
        }

        if (!catalog.TryGetAttribute(id, out _, out int index) || (uint)index >= (uint)attributeValues.Length)
        {
            value = default;
            failure = CapabilityLookupFailure.DefinitionMissing;
            return false;
        }

        value = attributeValues[index];
        failure = CapabilityLookupFailure.None;
        return true;
    }

    public bool TryGetSkill(
        SkillId id,
        ICharacterContentCatalog catalog,
        out byte value,
        out CapabilityLookupFailure failure)
    {
        if (catalog.Fingerprint != Fingerprint)
        {
            value = default;
            failure = CapabilityLookupFailure.ContentMismatch;
            return false;
        }

        if (!catalog.TryGetSkill(id, out _, out int index) || (uint)index >= (uint)skillValues.Length)
        {
            value = default;
            failure = CapabilityLookupFailure.DefinitionMissing;
            return false;
        }

        value = skillValues[index];
        failure = CapabilityLookupFailure.None;
        return true;
    }

    public CharacterCapabilitySnapshot Snapshot(ICharacterContentCatalog catalog)
    {
        if (catalog.Fingerprint != Fingerprint || catalog.Attributes.Length != attributeValues.Length ||
            catalog.Skills.Length != skillValues.Length)
        {
            throw new InvalidOperationException("The capability state does not belong to this content catalog.");
        }

        ImmutableArray<AttributeValueSnapshot>.Builder attributes = ImmutableArray.CreateBuilder<AttributeValueSnapshot>(attributeValues.Length);
        for (int index = 0; index < attributeValues.Length; index++)
        {
            attributes.Add(new AttributeValueSnapshot(catalog.Attributes[index].AttributeId, attributeValues[index]));
        }

        ImmutableArray<SkillValueSnapshot>.Builder skills = ImmutableArray.CreateBuilder<SkillValueSnapshot>(skillValues.Length);
        for (int index = 0; index < skillValues.Length; index++)
        {
            skills.Add(new SkillValueSnapshot(catalog.Skills[index].SkillId, skillValues[index], skillPractice[index]));
        }

        return new CharacterCapabilitySnapshot(
            Fingerprint,
            attributes.MoveToImmutable(),
            skills.MoveToImmutable(),
            [.. Feats.Order()],
            [.. Perks.Order()],
            [.. Access.Order()],
            [.. Techniques.Order()],
            [.. GrantSources.OrderBy(value => value.CapabilityId).ThenBy(value => value.SourceId)],
            [.. practiceKeys.Order()]);
    }

    public static CharacterCapabilities Restore(CharacterCapabilitySnapshot snapshot, ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);
        if (snapshot.Fingerprint != catalog.Fingerprint || snapshot.Attributes.Length != catalog.Attributes.Length ||
            snapshot.Skills.Length != catalog.Skills.Length || snapshot.Feats.Length > MaximumSetEntries ||
            snapshot.Perks.Length > MaximumSetEntries || snapshot.Techniques.Length > MaximumSetEntries ||
            snapshot.GrantSources.Length > MaximumSetEntries || snapshot.PracticeKeys.Length > MaximumPracticeKeys ||
            snapshot.Attributes.Select(value => value.Id).Distinct().Count() != snapshot.Attributes.Length ||
            snapshot.Skills.Select(value => value.Id).Distinct().Count() != snapshot.Skills.Length ||
            snapshot.PracticeKeys.Distinct().Count() != snapshot.PracticeKeys.Length)
        {
            throw new InvalidOperationException("Character capability snapshot is incompatible or exceeds capacity.");
        }

        ImmutableArray<short>.Builder attributes = ImmutableArray.CreateBuilder<short>(catalog.Attributes.Length);
        foreach (AttributeDefinition definition in catalog.Attributes)
        {
            AttributeValueSnapshot? value = snapshot.Attributes.SingleOrDefault(candidate => candidate.Id == definition.AttributeId);
            if (value is null || value.Value < definition.Minimum || value.Value > definition.Maximum)
            {
                throw new InvalidOperationException("Character Attribute state is invalid.");
            }

            attributes.Add(value.Value);
        }

        ImmutableArray<byte>.Builder skills = ImmutableArray.CreateBuilder<byte>(catalog.Skills.Length);
        ImmutableArray<ushort>.Builder practice = ImmutableArray.CreateBuilder<ushort>(catalog.Skills.Length);
        foreach (SkillDefinition definition in catalog.Skills)
        {
            SkillValueSnapshot? value = snapshot.Skills.SingleOrDefault(candidate => candidate.Id == definition.SkillId);
            if (value is null || value.Value < definition.Minimum || value.Value > definition.Maximum)
            {
                throw new InvalidOperationException("Character Skill state is invalid.");
            }

            skills.Add(value.Value);
            practice.Add(value.Practice);
        }

        if (snapshot.Feats.Any(id => !catalog.TryGetFeat(id, out _)) ||
            snapshot.Perks.Any(id => !catalog.TryGetPerk(id, out _)) ||
            snapshot.Techniques.Any(id => !TechniqueExists(id, catalog)) ||
            snapshot.GrantSources.Any(value => !value.CapabilityId.IsValid || !value.SourceId.IsValid))
        {
            throw new InvalidOperationException("Character capability references are missing.");
        }

        return new CharacterCapabilities(
            snapshot.Fingerprint,
            attributes.MoveToImmutable(),
            skills.MoveToImmutable(),
            practice.MoveToImmutable(),
            snapshot.Feats.ToImmutableHashSet(),
            snapshot.Perks.ToImmutableHashSet(),
            snapshot.Techniques.ToImmutableHashSet(),
            snapshot.GrantSources,
            snapshot.PracticeKeys.ToImmutableHashSet());
    }

    private static bool TechniqueExists(TechniqueId id, ICharacterContentCatalog catalog) =>
        id.Value.ToString().StartsWith("spell.", StringComparison.Ordinal)
            ? catalog.TryGetSpell(new SpellId(id.Value), out _)
            : id.Value.ToString().StartsWith("psychic.", StringComparison.Ordinal)
                ? catalog.TryGetPsychicTechnique(new PsychicTechniqueId(id.Value), out _)
                : catalog.TryGetTechnique(id, out _);

    internal CharacterCapabilities AwardPractice(
        ICharacterContentCatalog catalog,
        SkillId skillId,
        ushort amount,
        ContentId practiceKey,
        out SkillAdvancementEvent? advancement)
    {
        advancement = null;
        if (amount == 0 || practiceKeys.Contains(practiceKey) || practiceKeys.Count >= MaximumPracticeKeys ||
            catalog.Fingerprint != Fingerprint || !catalog.TryGetSkill(skillId, out SkillDefinition? definition, out int index))
        {
            return this;
        }

        int practice = Math.Min(ushort.MaxValue, skillPractice[index] + amount);
        int value = skillValues[index];
        int threshold = checked((value + 1) * 10);
        if (practice >= threshold && value < definition!.Maximum)
        {
            practice -= threshold;
            value++;
            advancement = new SkillAdvancementEvent(skillId, skillValues[index], (byte)value, amount, practiceKey);
        }

        return new CharacterCapabilities(
            Fingerprint,
            attributeValues,
            skillValues.SetItem(index, (byte)value),
            skillPractice.SetItem(index, (ushort)practice),
            Feats,
            Perks,
            Techniques,
            GrantSources,
            practiceKeys.Add(practiceKey));
    }

    internal CharacterCapabilities WithTrainingGrants(
        TrainingProjectDefinition project,
        ImmutableArray<FeatDefinition> definitions)
    {
        ImmutableHashSet<FeatId> feats = Feats;
        ImmutableHashSet<TechniqueId> techniques = Techniques;
        ImmutableArray<CapabilityGrant>.Builder grants = GrantSources.ToBuilder();
        foreach (FeatDefinition feat in definitions)
        {
            if (feats.Contains(feat.FeatId))
            {
                continue;
            }

            feats = feats.Add(feat.FeatId);
            grants.Add(new CapabilityGrant(feat.FeatId.Value, project.TrainingProjectId.Value, GrantSourceKind.TrainingProject));
            foreach (AccessId accessId in feat.GrantedAccessIds)
            {
                grants.Add(new CapabilityGrant(accessId.Value, feat.FeatId.Value, GrantSourceKind.Feat));
            }
        }

        foreach (TechniqueId techniqueId in project.GrantedTechniqueIds)
        {
            if (techniques.Contains(techniqueId))
            {
                continue;
            }

            techniques = techniques.Add(techniqueId);
            grants.Add(new CapabilityGrant(techniqueId.Value, project.TrainingProjectId.Value, GrantSourceKind.TrainingProject));
        }

        if (grants.Count > MaximumSetEntries)
        {
            throw new InvalidOperationException("Training grants exceed the character capability capacity.");
        }

        return new CharacterCapabilities(Fingerprint, attributeValues, skillValues, skillPractice, feats, Perks,
            techniques, grants.MoveToImmutable(), practiceKeys);
    }

    internal CharacterCapabilities WithPerkGrant(PerkDefinition perk, ContentId sourceId)
    {
        if (Perks.Contains(perk.PerkId))
        {
            return this;
        }

        ImmutableArray<CapabilityGrant>.Builder grants = GrantSources.ToBuilder();
        grants.Add(new CapabilityGrant(perk.PerkId.Value, sourceId, GrantSourceKind.Perk));
        foreach (AccessId accessId in perk.GrantedAccessIds)
        {
            grants.Add(new CapabilityGrant(accessId.Value, perk.PerkId.Value, GrantSourceKind.Perk));
        }

        foreach (TechniqueId techniqueId in perk.GrantedTechniqueIds)
        {
            grants.Add(new CapabilityGrant(techniqueId.Value, perk.PerkId.Value, GrantSourceKind.Perk));
        }

        if (grants.Count > MaximumSetEntries)
        {
            throw new InvalidOperationException("Action grants exceed the character capability capacity.");
        }

        return new CharacterCapabilities(
            Fingerprint,
            attributeValues,
            skillValues,
            skillPractice,
            Feats,
            Perks.Add(perk.PerkId),
            Techniques.Union(perk.GrantedTechniqueIds),
            grants.MoveToImmutable(),
            practiceKeys);
    }

    public CharacterCapabilities WithoutGrantSource(ContentId sourceId)
    {
        HashSet<ContentId> removedSources = [sourceId];
        bool changed;
        do
        {
            changed = false;
            foreach (CapabilityGrant grant in GrantSources)
            {
                if (removedSources.Contains(grant.SourceId) && removedSources.Add(grant.CapabilityId))
                {
                    changed = true;
                }
            }
        }
        while (changed && removedSources.Count <= MaximumSetEntries + 1);

        ImmutableArray<CapabilityGrant> remaining =
            [.. GrantSources.Where(value => !removedSources.Contains(value.SourceId))];
        ImmutableHashSet<ContentId> retainedCapabilities = remaining.Select(value => value.CapabilityId).ToImmutableHashSet();
        return new CharacterCapabilities(
            Fingerprint,
            attributeValues,
            skillValues,
            skillPractice,
            Feats.Where(value => retainedCapabilities.Contains(value.Value)).ToImmutableHashSet(),
            Perks.Where(value => retainedCapabilities.Contains(value.Value)).ToImmutableHashSet(),
            Techniques.Where(value => retainedCapabilities.Contains(value.Value)).ToImmutableHashSet(),
            remaining,
            practiceKeys);
    }
}

public sealed record CharacterState(
    CharacterId Id,
    ContentFingerprint ContentFingerprint,
    ScenarioId ScenarioId,
    RaceId RaceId,
    HeritageId HeritageId,
    BackgroundId BackgroundId,
    ContentId PositionId,
    CharacterCapabilities Capabilities,
    ImmutableArray<ContentId> LanguageIds,
    ImmutableArray<ContentId> ScriptIds,
    ImmutableHashSet<ContentId> EquipmentIds,
    ImmutableDictionary<ResourceId, int> Resources,
    ImmutableDictionary<TrainingProjectId, int> TrainingProgress,
    bool CanAct = true)
{
    public ImmutableArray<ActiveCapabilityEffect> ActiveEffects { get; init; } = [];
    public ImmutableArray<ObservableCapabilityEvidence> Evidence { get; init; } = [];
}

public sealed record ActiveCapabilityEffect(
    ContentId EffectId,
    ContentId SourceId,
    CharacterId ActorId,
    CharacterId TargetId,
    long StartTick,
    long EndTick,
    ContentId ScopeId);

public sealed record ObservableCapabilityEvidence(
    ContentId EvidenceId,
    ContentId SourceId,
    CharacterId ActorId,
    CharacterId TargetId,
    long Tick,
    bool Succeeded);

public sealed record SkillAdvancementEvent(
    SkillId SkillId,
    byte PreviousValue,
    byte NewValue,
    ushort PracticeAward,
    ContentId PracticeKey);
