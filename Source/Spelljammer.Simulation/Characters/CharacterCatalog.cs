using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;
using Spelljammer.Simulation.Items;

namespace Spelljammer.Simulation.Characters;

/// <summary>Definitions needed by character creation, capability lookup, progression, and recruitment.</summary>
/// <remarks>
/// Code flow: Compiled content implements the catalog, character systems resolve stable IDs and typed definitions through it, and a shared fingerprint prevents cross-content state mutation.
/// </remarks>
public interface ICharacterDefinitionCatalog : IContentCatalogIdentity
{
    ImmutableArray<AbilityDefinition> Abilities { get; }
    ImmutableArray<SkillDefinition> Skills { get; }
    ImmutableArray<CharacterResourceProfileDefinition> CharacterResourceProfiles { get; }
    ImmutableArray<CharacterDefinition> Characters { get; }
    ImmutableArray<ScenarioDefinition> Scenarios { get; }

    bool TryGetAbility(AbilityId id, out AbilityDefinition? definition, out int index);
    bool TryGetSkill(SkillId id, out SkillDefinition? definition, out int index);
    bool TryGetCharacterResourceProfile(CharacterResourceProfileId id, out CharacterResourceProfileDefinition? definition);
    bool TryGetAccess(AccessId id, out AccessDefinition? definition);
    bool TryGetBackground(BackgroundId id, out BackgroundDefinition? definition);
    bool TryGetCharacter(CharacterId id, out CharacterDefinition? definition);
    bool TryGetScenario(ScenarioId id, out ScenarioDefinition? definition);
    bool TryGetFeat(FeatId id, out FeatDefinition? definition);
    bool TryGetHeritage(HeritageId id, out HeritageDefinition? definition);
    bool TryGetRace(RaceId id, out RaceDefinition? definition);
    bool TryGetTrainingProject(TrainingProjectId id, out TrainingProjectDefinition? definition);
}

/// <summary>Definitions needed to validate a complete persisted character state.</summary>
public interface ICharacterStateCatalog :
    ICharacterDefinitionCatalog,
    IItemDefinitionCatalog,
    IStatusDefinitionCatalog;

/// <summary>Character definitions plus inventory definitions needed while creating a character.</summary>
public interface ICharacterCreationCatalog : ICharacterDefinitionCatalog, IItemDefinitionCatalog;

public enum CapabilityLookupFailure : byte
{
    None,
    ContentMismatch,
    DefinitionMissing,
}

public enum GrantSourceKind : byte
{
    Race,
    Heritage,
    Feat,
    TrainingProject,
}

public sealed record CapabilityGrant(ContentId CapabilityId, ContentId SourceId, GrantSourceKind SourceKind);

public sealed record AbilityValueSnapshot(AbilityId Id, short Value);

public sealed record SkillValueSnapshot(SkillId Id, byte Value);

public sealed record CharacterCapabilitySnapshot(
    ContentFingerprint Fingerprint,
    ImmutableArray<AbilityValueSnapshot> Abilities,
    ImmutableArray<SkillValueSnapshot> Skills,
    ImmutableArray<FeatId> Feats,
    ImmutableArray<AccessId> Access,
    ImmutableArray<CapabilityGrant> GrantSources);
