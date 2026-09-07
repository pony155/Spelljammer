using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

public interface ICharacterContentCatalog
{
    ContentFingerprint Fingerprint { get; }
    ImmutableArray<AttributeDefinition> Attributes { get; }
    ImmutableArray<SkillDefinition> Skills { get; }
    ImmutableArray<CharacterDefinition> Characters { get; }
    ImmutableArray<SpellDefinition> Spells { get; }
    ImmutableArray<PsychicTechniqueDefinition> PsychicTechniques { get; }
    ImmutableArray<EquipmentDefinition> Equipment { get; }
    ImmutableArray<BoardCellDefinition> BoardCells { get; }
    ImmutableArray<ZoneLinkDefinition> ZoneLinks { get; }
    ImmutableArray<PersonalBoardDefinition> PersonalBoards { get; }
    ImmutableArray<EncounterDefinition> Encounters { get; }
    ImmutableArray<ShipFrameDefinition> ShipFrames { get; }
    ImmutableArray<ShipModuleDefinition> ShipModules { get; }
    ImmutableArray<ShipWeaponConfigurationDefinition> ShipWeaponConfigurations { get; }

    bool TryGetAttribute(AttributeId id, out AttributeDefinition? definition, out int index);
    bool TryGetSkill(SkillId id, out SkillDefinition? definition, out int index);
    bool TryGetAccess(AccessId id, out AccessDefinition? definition);
    bool TryGetBackground(BackgroundId id, out BackgroundDefinition? definition);
    bool TryGetCharacter(CharacterId id, out CharacterDefinition? definition);
    bool TryGetFeat(FeatId id, out FeatDefinition? definition);
    bool TryGetHeritage(HeritageId id, out HeritageDefinition? definition);
    bool TryGetPerk(PerkId id, out PerkDefinition? definition);
    bool TryGetRace(RaceId id, out RaceDefinition? definition);
    bool TryGetSpell(SpellId id, out SpellDefinition? definition);
    bool TryGetPsychicTechnique(PsychicTechniqueId id, out PsychicTechniqueDefinition? definition);
    bool TryGetEquipment(EquipmentId id, out EquipmentDefinition? definition);
    bool TryGetBoardCell(CellId id, out BoardCellDefinition? definition);
    bool TryGetZoneLink(LinkId id, out ZoneLinkDefinition? definition);
    bool TryGetPersonalBoard(PersonalBoardId id, out PersonalBoardDefinition? definition);
    bool TryGetEncounter(EncounterId id, out EncounterDefinition? definition);
    bool TryGetShipFrame(ShipFrameId id, out ShipFrameDefinition? definition);
    bool TryGetShipModule(ModuleId id, out ShipModuleDefinition? definition);
    bool TryGetShipWeaponConfiguration(ShipWeaponConfigurationId id, out ShipWeaponConfigurationDefinition? definition);
    bool TryGetTechnique(TechniqueId id, out TechniqueDefinition? definition);
    bool TryGetTrainingProject(TrainingProjectId id, out TrainingProjectDefinition? definition);
}

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
    Perk,
    Feat,
    Technique,
    TrainingProject,
    Spell,
    PsychicTechnique,
}

public sealed record CapabilityGrant(ContentId CapabilityId, ContentId SourceId, GrantSourceKind SourceKind);

public sealed record AttributeValueSnapshot(AttributeId Id, short Value);

public sealed record SkillValueSnapshot(SkillId Id, byte Value, ushort Practice);

public sealed record CharacterCapabilitySnapshot(
    ContentFingerprint Fingerprint,
    ImmutableArray<AttributeValueSnapshot> Attributes,
    ImmutableArray<SkillValueSnapshot> Skills,
    ImmutableArray<FeatId> Feats,
    ImmutableArray<PerkId> Perks,
    ImmutableArray<AccessId> Access,
    ImmutableArray<TechniqueId> Techniques,
    ImmutableArray<CapabilityGrant> GrantSources,
    ImmutableArray<ContentId> PracticeKeys);
