using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;
using EquipmentDefinition = Spelljammer.Simulation.Items.EquipmentDefinition;
using ItemDefinition = Spelljammer.Simulation.Items.ItemDefinition;
using Spelljammer.Simulation.Items;

namespace Spelljammer.Simulation.Characters;

public interface ICharacterContentCatalog : IItemDefinitionCatalog
{
    ContentFingerprint Fingerprint { get; }
    ImmutableArray<AbilityDefinition> Abilities { get; }
    ImmutableArray<SkillDefinition> Skills { get; }
    ImmutableArray<CharacterResourceProfileDefinition> CharacterResourceProfiles { get; }
    ImmutableArray<CharacterDefinition> Characters { get; }
    ImmutableArray<ScenarioDefinition> Scenarios { get; }
    ImmutableArray<EquipmentDefinition> Equipment { get; }
    ImmutableArray<ItemDefinition> Items { get; }
    ImmutableArray<MeleeWeaponDefinition> MeleeWeapons { get; }
    ImmutableArray<MeleeWeaponActionDefinition> MeleeWeaponActions { get; }
    ImmutableArray<RangedWeaponDefinition> RangedWeapons { get; }
    ImmutableArray<AmmunitionDefinition> Ammunition { get; }
    ImmutableArray<RangedWeaponActionDefinition> RangedWeaponActions { get; }
    ImmutableArray<BoardCellDefinition> BoardCells { get; }
    ImmutableArray<ZoneLinkDefinition> ZoneLinks { get; }
    ImmutableArray<PersonalBoardDefinition> PersonalBoards { get; }
    ImmutableArray<EncounterDefinition> Encounters { get; }
    ImmutableArray<ShipFrameDefinition> ShipFrames { get; }
    ImmutableArray<ShipModuleDefinition> ShipModules { get; }
    ImmutableArray<ShipWeaponConfigurationDefinition> ShipWeaponConfigurations { get; }

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
    bool TryGetMeleeWeapon(MeleeWeaponId id, out MeleeWeaponDefinition? definition);
    bool TryGetMeleeWeaponAction(MeleeWeaponActionId id, out MeleeWeaponActionDefinition? definition);
    bool TryGetRangedWeapon(RangedWeaponId id, out RangedWeaponDefinition? definition);
    bool TryGetAmmunition(AmmunitionId id, out AmmunitionDefinition? definition);
    bool TryGetRangedWeaponAction(RangedWeaponActionId id, out RangedWeaponActionDefinition? definition);
    bool TryGetBoardCell(CellId id, out BoardCellDefinition? definition);
    bool TryGetZoneLink(LinkId id, out ZoneLinkDefinition? definition);
    bool TryGetPersonalBoard(PersonalBoardId id, out PersonalBoardDefinition? definition);
    bool TryGetEncounter(EncounterId id, out EncounterDefinition? definition);
    bool TryGetShipFrame(ShipFrameId id, out ShipFrameDefinition? definition);
    bool TryGetShipModule(ModuleId id, out ShipModuleDefinition? definition);
    bool TryGetShipWeaponConfiguration(ShipWeaponConfigurationId id, out ShipWeaponConfigurationDefinition? definition);
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
