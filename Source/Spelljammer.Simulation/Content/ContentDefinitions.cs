using System.Collections.Immutable;

namespace Spelljammer.Simulation.Content;

public abstract record ContentDefinition(ContentId Id, int SchemaVersion, int Revision, string NameKey, string DescriptionKey);

public sealed record AttributeDefinition(
    AttributeId AttributeId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    short Minimum,
    short Maximum,
    short DefaultValue,
    ImmutableArray<string> Tags)
    : ContentDefinition(AttributeId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record SkillDefinition(
    SkillId SkillId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    byte Minimum,
    byte Maximum,
    ContentId ProgressionCurveId,
    ImmutableArray<ContentId> ActionTags)
    : ContentDefinition(SkillId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record AccessDefinition(
    AccessId AccessId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<string> Tags)
    : ContentDefinition(AccessId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record FeatDefinition(
    FeatId FeatId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    TrainingProjectId TrainingProjectId,
    ImmutableArray<AccessId> GrantedAccessIds)
    : ContentDefinition(FeatId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record PerkDefinition(
    PerkId PerkId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<RaceId> CompatibleRaceIds,
    ImmutableArray<AccessId> GrantedAccessIds,
    ImmutableArray<TechniqueId> GrantedTechniqueIds,
    ImmutableArray<PerkId> GrantedPerkIds,
    ImmutableArray<ContentId> EffectIds)
    : ContentDefinition(PerkId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record RaceDefinition(
    RaceId RaceId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<PerkId> GrantedPerkIds,
    ImmutableArray<ContentId> RequiredSupportIds)
    : ContentDefinition(RaceId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record TrainingProjectDefinition(
    TrainingProjectId TrainingProjectId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<SkillId> RequiredSkillIds,
    int WorkUnits,
    int ProgressCap,
    ContentId FacilityId,
    ResourceId ResourceId,
    int ResourceCost,
    ContentId SafetyId,
    ImmutableArray<FeatId> GrantedFeatIds,
    ImmutableArray<TechniqueId> GrantedTechniqueIds)
    : ContentDefinition(TrainingProjectId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record HeritageDefinition(
    HeritageId HeritageId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    RaceId RaceId,
    ImmutableArray<PerkId> GrantedPerkIds)
    : ContentDefinition(HeritageId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record BackgroundDefinition(
    BackgroundId BackgroundId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<RaceId> CompatibleRaceIds,
    ImmutableArray<AttributeId> AttributeBonusIds,
    ImmutableArray<SkillId> FocusSkillIds)
    : ContentDefinition(BackgroundId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record TechniqueDefinition(
    TechniqueId TechniqueId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<AccessId> RequiredAccessIds,
    ImmutableArray<PerkId> GrantedPerkIds)
    : ContentDefinition(TechniqueId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record SpellDefinition(
    SpellId SpellId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    AccessId RequiredAccessId,
    SkillId SkillId,
    ResourceId FocusResourceId,
    int FocusCost,
    ContentId RangeId,
    int CastTimeTicks,
    int CooldownTicks,
    ImmutableArray<string> TargetTags,
    ImmutableArray<ContentId> EffectIds)
    : ContentDefinition(SpellId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record PsychicTechniqueDefinition(
    PsychicTechniqueId PsychicTechniqueId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    AccessId RequiredAccessId,
    SkillId SkillId,
    SkillId ResistanceSkillId,
    ResourceId StrainResourceId,
    int StrainCost,
    int SustainCostPerTick,
    ContentId ContactModeId,
    ContentId RangeId,
    ContentId InformationScopeId,
    ImmutableArray<ContentId> DisciplineIds,
    ImmutableArray<string> TargetTags,
    ImmutableArray<ContentId> EffectIds)
    : ContentDefinition(PsychicTechniqueId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record CharacterDefinition(
    CharacterId CharacterId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    RaceId RaceId,
    HeritageId HeritageId,
    BackgroundId BackgroundId,
    ImmutableArray<ScenarioId> ScenarioIds,
    ContentId PositionId,
    ImmutableArray<ContentId> LanguageIds,
    ImmutableArray<ContentId> ScriptIds,
    ImmutableArray<ContentId> EquipmentIds,
    ImmutableArray<SkillId> FocusSkillIds,
    ImmutableArray<ResourceId> ResourceIds)
    : ContentDefinition(CharacterId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record EquipmentDefinition(
    EquipmentId EquipmentId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ContentId SlotId,
    ContentId InitialStateId,
    ResourceId ResourceId,
    int ResourceCapacity,
    ImmutableArray<ContentId> ActionIds,
    ImmutableArray<ContentId> EffectIds)
    : ContentDefinition(EquipmentId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record BoardCellDefinition(
    CellId CellId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ZoneId ZoneId,
    int Q,
    int R,
    int Capacity,
    int Cover,
    int Visibility,
    ContentId AtmosphereId,
    ContentId GravityId,
    ImmutableArray<string> HazardTags)
    : ContentDefinition(CellId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record ZoneLinkDefinition(
    LinkId LinkId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    CellId FromCellId,
    CellId ToCellId,
    ContentId AccessId,
    int OneWay,
    int AllowsRetreat)
    : ContentDefinition(LinkId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record PersonalBoardDefinition(
    PersonalBoardId PersonalBoardId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int MaximumOccupants,
    ImmutableArray<CellId> CellIds,
    ImmutableArray<LinkId> LinkIds,
    ImmutableArray<ObjectiveId> RequiredObjectiveIds,
    ImmutableArray<CellId> RetreatCellIds)
    : ContentDefinition(PersonalBoardId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record EncounterDefinition(
    EncounterId EncounterId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    PersonalBoardId PersonalBoardId,
    ContentId ContextId,
    TeamId HostileTeamId,
    ContentId AncientDefenseId,
    ObjectiveId NonCombatObjectiveId,
    ObjectiveId ExtractionObjectiveId)
    : ContentDefinition(EncounterId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record ShipFrameDefinition(
    ShipFrameId ShipFrameId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int MaximumHull,
    int BaseArmor,
    int MaximumSlots,
    int CargoCapacity,
    ImmutableArray<ContentId> MountIds)
    : ContentDefinition(ShipFrameId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record ShipModuleDefinition(
    ModuleId ModuleId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int SlotCost,
    int CargoDisplacement,
    int MaximumIntegrity,
    NetworkId NetworkId,
    int EnergyGeneration,
    int EnergyConsumption,
    ContentId MountId,
    ContentId PrimaryEffectId,
    int ArmorValue,
    int ShieldValue,
    int ShieldRechargeRate,
    int ShieldEnergyConsumptionRate,
    ImmutableArray<ContentId> CompatiblePathIds)
    : ContentDefinition(ModuleId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record ShipWeaponConfigurationDefinition(
    ShipWeaponConfigurationId ShipWeaponConfigurationId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    NetworkId NetworkId,
    ResourceId ResourceId,
    int ResourceCost,
    int Damage,
    int RateOfFireTicks,
    int EffectiveRange,
    int MaximumRange,
    int ReloadTicks,
    ContentId DamageTypeId,
    ContentId AreaId,
    int ArmorPenetration)
    : ContentDefinition(ShipWeaponConfigurationId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);
