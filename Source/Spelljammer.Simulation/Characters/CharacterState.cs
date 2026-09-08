using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Characters;

/// <summary>
/// Represents all computed abilities, skills, and capabilities of a character.
/// </summary>
/// <remarks>
/// This class encapsulates the character's abilities, skills, and passive or active Feats.
/// All data is immutable and validated against content definitions using fingerprints to ensure consistency.
/// Code flow: Character creation builds capabilities from linked definitions and grants, gameplay systems return
/// replacement character state, and battle projection copies only combat-owned fields across encounter boundaries.
/// </remarks>
public sealed class CharacterCapabilities
{
    /// <summary>
    /// The maximum number of entries allowed in any capability set (Feats or grant sources).
    /// </summary>
    public const int MaximumSetEntries = 256;

    private readonly ImmutableArray<short> abilityValues;
    private readonly ImmutableArray<byte> skillValues;

    /// <summary>
    /// Initializes a new character capabilities object with the given abilities and traits.
    /// </summary>
    /// <remarks>
    /// This constructor is internal; capabilities are created only by character-building systems.
    /// All arrays and sets are validated for completeness and capacity limits.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown if arrays are empty, mismatched in length, or exceed capacity limits.</exception>
    internal CharacterCapabilities(
        ContentFingerprint fingerprint,
        ImmutableArray<short> abilityValues,
        ImmutableArray<byte> skillValues,
        ImmutableHashSet<FeatId> feats,
        ImmutableArray<CapabilityGrant> grantSources)
    {
        if (abilityValues.Length == 0 || skillValues.Length == 0 ||
            feats.Count > MaximumSetEntries || grantSources.Length > MaximumSetEntries)
        {
            throw new ArgumentException("Character capability storage is incomplete or exceeds its bounded capacity.");
        }

        Fingerprint = fingerprint;
        this.abilityValues = abilityValues;
        this.skillValues = skillValues;
        Feats = feats;
        GrantSources = grantSources;
    }

    /// <summary>
    /// Gets the content fingerprint used to validate that this character matches the current content version.
    /// </summary>
    public ContentFingerprint Fingerprint { get; }

    /// <summary>
    /// Gets every Feat granted to this character, regardless of acquisition source.
    /// </summary>
    public ImmutableHashSet<FeatId> Feats { get; }

    /// <summary>
    /// Gets the set of access privileges this character has been granted from Feats.
    /// </summary>
    public ImmutableHashSet<AccessId> Access => GrantSources
        .Where(value => value.CapabilityId.ToString().StartsWith("access.", StringComparison.Ordinal))
        .Select(value => new AccessId(value.CapabilityId))
        .ToImmutableHashSet();

    /// <summary>
    /// Gets the array of sources that granted capabilities to this character (for tracing where abilities came from).
    /// </summary>
    public ImmutableArray<CapabilityGrant> GrantSources { get; }

    /// <summary>
    /// Attempts to retrieve an ability value for this character using the given catalog.
    /// </summary>
    /// <remarks>
    /// The catalog must match this character's fingerprint; if it doesn't, the lookup fails with ContentMismatch.
    /// </remarks>
    /// <param name="id">The ability ID to look up.</param>
    /// <param name="catalog">The content catalog to validate against.</param>
    /// <param name="value">When successful, contains the ability value; otherwise, zero.</param>
    /// <param name="failure">A code indicating why the lookup failed, if it failed.</param>
    /// <returns>True if the ability was found and retrieved; otherwise, false.</returns>
    public bool TryGetAbility(
        AbilityId id,
        ICharacterDefinitionCatalog catalog,
        out short value,
        out CapabilityLookupFailure failure)
    {
        if (catalog.Fingerprint != Fingerprint)
        {
            value = default;
            failure = CapabilityLookupFailure.ContentMismatch;
            return false;
        }

        if (!catalog.TryGetAbility(id, out _, out int index) || (uint)index >= (uint)abilityValues.Length)
        {
            value = default;
            failure = CapabilityLookupFailure.DefinitionMissing;
            return false;
        }

        value = abilityValues[index];
        failure = CapabilityLookupFailure.None;
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a skill value for this character using the given catalog.
    /// </summary>
    /// <remarks>
    /// The catalog must match this character's fingerprint. The skill value represents mastery level (0-255).
    /// </remarks>
    /// <param name="id">The skill ID to look up.</param>
    /// <param name="catalog">The content catalog to validate against.</param>
    /// <param name="value">When successful, contains the skill proficiency level; otherwise, zero.</param>
    /// <param name="failure">A code indicating why the lookup failed, if it failed.</param>
    /// <returns>True if the skill was found and retrieved; otherwise, false.</returns>
    public bool TryGetSkill(
        SkillId id,
        ICharacterDefinitionCatalog catalog,
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

    public CharacterCapabilitySnapshot Snapshot(ICharacterDefinitionCatalog catalog)
    {
        if (catalog.Fingerprint != Fingerprint || catalog.Abilities.Length != abilityValues.Length ||
            catalog.Skills.Length != skillValues.Length)
        {
            throw new InvalidOperationException("The capability state does not belong to this content catalog.");
        }

        ImmutableArray<AbilityValueSnapshot>.Builder abilities = ImmutableArray.CreateBuilder<AbilityValueSnapshot>(abilityValues.Length);
        for (int index = 0; index < abilityValues.Length; index++)
        {
            abilities.Add(new AbilityValueSnapshot(catalog.Abilities[index].AbilityId, abilityValues[index]));
        }

        ImmutableArray<SkillValueSnapshot>.Builder skills = ImmutableArray.CreateBuilder<SkillValueSnapshot>(skillValues.Length);
        for (int index = 0; index < skillValues.Length; index++)
        {
            skills.Add(new SkillValueSnapshot(catalog.Skills[index].SkillId, skillValues[index]));
        }

        return new CharacterCapabilitySnapshot(
            Fingerprint,
            abilities.MoveToImmutable(),
            skills.MoveToImmutable(),
            [.. Feats.Order()],
            [.. Access.Order()],
            [.. GrantSources.OrderBy(value => value.CapabilityId).ThenBy(value => value.SourceId)]);
    }

    public static CharacterCapabilities Restore(CharacterCapabilitySnapshot snapshot, ICharacterDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(catalog);
        if (snapshot.Fingerprint != catalog.Fingerprint || snapshot.Abilities.Length != catalog.Abilities.Length ||
            snapshot.Skills.Length != catalog.Skills.Length || snapshot.Feats.Length > MaximumSetEntries ||
            snapshot.Abilities.Select(value => value.Id).Distinct().Count() != snapshot.Abilities.Length ||
            snapshot.Skills.Select(value => value.Id).Distinct().Count() != snapshot.Skills.Length)
        {
            throw new InvalidOperationException("Character capability snapshot is incompatible or exceeds capacity.");
        }

        ImmutableArray<short>.Builder abilities = ImmutableArray.CreateBuilder<short>(catalog.Abilities.Length);
        foreach (AbilityDefinition definition in catalog.Abilities)
        {
            AbilityValueSnapshot? value = snapshot.Abilities.SingleOrDefault(candidate => candidate.Id == definition.AbilityId);
            if (value is null || value.Value < definition.Minimum || value.Value > definition.Maximum)
            {
                throw new InvalidOperationException("Character Ability state is invalid.");
            }

            abilities.Add(value.Value);
        }

        ImmutableArray<byte>.Builder skills = ImmutableArray.CreateBuilder<byte>(catalog.Skills.Length);
        foreach (SkillDefinition definition in catalog.Skills)
        {
            SkillValueSnapshot? value = snapshot.Skills.SingleOrDefault(candidate => candidate.Id == definition.SkillId);
            if (value is null || value.Value < definition.Minimum || value.Value > definition.Maximum)
            {
                throw new InvalidOperationException("Character Skill state is invalid.");
            }

            skills.Add(value.Value);
        }

        if (snapshot.Feats.Any(id => !catalog.TryGetFeat(id, out _)) ||
            snapshot.GrantSources.Any(value => !value.CapabilityId.IsValid || !value.SourceId.IsValid))
        {
            throw new InvalidOperationException("Character capability references are missing.");
        }

        return new CharacterCapabilities(
            snapshot.Fingerprint,
            abilities.MoveToImmutable(),
            skills.MoveToImmutable(),
            snapshot.Feats.ToImmutableHashSet(),
            snapshot.GrantSources);
    }

    internal CharacterCapabilities WithTrainingGrants(
        TrainingProjectDefinition project,
        ImmutableArray<FeatDefinition> definitions)
    {
        ImmutableHashSet<FeatId> feats = Feats;
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

        if (grants.Count > MaximumSetEntries)
        {
            throw new InvalidOperationException("Training grants exceed the character capability capacity.");
        }

        return new CharacterCapabilities(Fingerprint, abilityValues, skillValues, feats, grants.ToImmutable());
    }

    internal CharacterCapabilities WithFeatGrant(FeatDefinition feat, ContentId sourceId)
    {
        if (Feats.Contains(feat.FeatId))
        {
            return this;
        }

        ImmutableArray<CapabilityGrant>.Builder grants = GrantSources.ToBuilder();
        grants.Add(new CapabilityGrant(feat.FeatId.Value, sourceId, GrantSourceKind.Feat));
        foreach (AccessId accessId in feat.GrantedAccessIds)
        {
            grants.Add(new CapabilityGrant(accessId.Value, feat.FeatId.Value, GrantSourceKind.Feat));
        }

        if (grants.Count > MaximumSetEntries)
        {
            throw new InvalidOperationException("Action grants exceed the character capability capacity.");
        }

        return new CharacterCapabilities(
            Fingerprint,
            abilityValues,
            skillValues,
            Feats.Add(feat.FeatId),
            grants.ToImmutable());
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
            abilityValues,
            skillValues,
            Feats.Where(value => retainedCapabilities.Contains(value.Value)).ToImmutableHashSet(),
            remaining);
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
    ItemSystemState Items,
    ImmutableDictionary<ResourceId, int> Resources,
    ImmutableDictionary<TrainingProjectId, int> TrainingProgress,
    bool CanAct = true)
{
    /// <summary>Maximum number of active Status instances retained for a character.</summary>
    public const int MaximumStatuses = 128;

    /// <summary>
    /// Maximum number of observable evidence entries retained for a character.
    /// </summary>
    public const int MaximumEvidenceEntries = 256;

    public StatusState Statuses { get; init; } = StatusState.Empty;
    public ImmutableArray<ObservableCapabilityEvidence> Evidence { get; init; } = [];
    public CharacterResourceSet CharacterResources { get; init; } = CharacterResourceSet.Empty;
    public ImmutableArray<InjuryState> Injuries { get; init; } = [];

    /// <summary>
    /// Validates the complete character state against the active content catalog.
    /// This is intentionally explicit so callers can validate before publishing
    /// a snapshot or writing a campaign save.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the state has a content mismatch, invalid references, negative
    /// resources or progress, or exceeds a bounded collection.
    /// </exception>
    public void ValidateForContent(ICharacterStateCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (ContentFingerprint != catalog.Fingerprint || Capabilities.Fingerprint != ContentFingerprint)
        {
            throw new InvalidOperationException("Character state does not belong to the active content catalog.");
        }

        if (catalog.TryGetScenario(ScenarioId, out ScenarioDefinition? scenario) &&
            scenario!.CharacterResourceProfileId is CharacterResourceProfileId profileId)
        {
            if (!catalog.TryGetCharacterResourceProfile(profileId, out CharacterResourceProfileDefinition? profile))
            {
                throw new InvalidOperationException("Character resource profile is missing.");
            }

            CharacterResources.Validate(profile!);
        }

        if (LanguageIds.Length > CharacterCapabilities.MaximumSetEntries ||
            ScriptIds.Length > CharacterCapabilities.MaximumSetEntries ||
            Resources.Count > CharacterCapabilities.MaximumSetEntries ||
            TrainingProgress.Count > CharacterCapabilities.MaximumSetEntries ||
            Statuses.Instances.Length > MaximumStatuses ||
            Evidence.Length > MaximumEvidenceEntries ||
            Injuries.Length > CharacterCapabilities.MaximumSetEntries)
        {
            throw new InvalidOperationException("Character state exceeds a bounded capacity.");
        }

        ItemSystemResult itemValidation = ItemSystem.Create(Items, catalog);
        if (!itemValidation.Accepted)
        {
            throw new InvalidOperationException($"Character item state is invalid: {itemValidation.RejectionCode}");
        }

        if (Resources.Any(value => !value.Key.IsValid || value.Value < 0))
        {
            throw new InvalidOperationException("Character resources contain an invalid value.");
        }

        if (Injuries.Any(value => !value.Id.IsValid || !Enum.IsDefined(value.Severity)))
        {
            throw new InvalidOperationException("Character injuries contain an invalid value.");
        }

        foreach ((TrainingProjectId projectId, int progress) in TrainingProgress)
        {
            if (progress < 0 || !catalog.TryGetTrainingProject(projectId, out TrainingProjectDefinition? project) ||
                progress > project!.ProgressCap)
            {
                throw new InvalidOperationException("Character training progress is invalid.");
            }
        }

        StatusResult statusValidation = StatusSystem.Create(
            Statuses,
            catalog,
            new StatusSystemLimits(MaximumStatuses, 1_000_000, MaximumStatuses));
        if (!statusValidation.Accepted || Statuses.Instances.Any(value => value.TargetId.Value != Id.Value))
        {
            throw new InvalidOperationException("Character status state is invalid.");
        }

        foreach (ObservableCapabilityEvidence evidence in Evidence)
        {
            if (!evidence.EvidenceId.IsValid || !evidence.SourceId.IsValid ||
                evidence.ActorId != Id || !evidence.TargetId.IsValid || evidence.Tick < 0)
            {
                throw new InvalidOperationException("Character evidence state is invalid.");
            }
        }
    }

    public bool HasItemDefinition(ContentId definitionId) =>
        Items.ItemInstances.Any(value => value.DefinitionId == definitionId);

    public bool TryGetItem(ItemInstanceId instanceId, out ItemInstance? item)
    {
        item = Items.ItemInstances.SingleOrDefault(value => value.InstanceId == instanceId);
        return item is not null;
    }

    public bool TryGetInventoryEntry(InventoryEntryId entryId, out InventoryEntry? entry)
    {
        entry = Items.InventoryEntries.SingleOrDefault(value => value.EntryId == entryId);
        return entry is not null;
    }

    public bool IsEquipped(ItemInstanceId instanceId) =>
        Items.EquipmentLoadouts.Any(value => value.OwnerId == Id.Value &&
            value.SlotAssignments.Any(assignment => assignment.ItemInstanceId == instanceId));

    public CharacterState ReplaceItem(ItemInstance replacement)
    {
        if (!Items.ItemInstances.Any(value => value.InstanceId == replacement.InstanceId))
        {
            throw new InvalidOperationException("Cannot replace an item which is not owned by the character.");
        }

        return this with
        {
            Items = Items with
            {
                ItemInstances = Items.ItemInstances
                    .Select(value => value.InstanceId == replacement.InstanceId ? replacement : value)
                    .ToImmutableArray(),
            },
        };
    }
}

public sealed record ObservableCapabilityEvidence(
    ContentId EvidenceId,
    ContentId SourceId,
    CharacterId ActorId,
    CharacterId TargetId,
    long Tick,
    bool Succeeded);
