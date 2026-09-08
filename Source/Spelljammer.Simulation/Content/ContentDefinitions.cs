using System.Collections.Immutable;

namespace Spelljammer.Simulation.Content;

/// <summary>
/// Base class for all content entity definitions, providing common metadata for game content.
/// </summary>
/// <remarks>
/// This abstract record serves as the parent for all specific content types (skills, feats, spells, etc.).
/// It provides common fields for versioning, localization keys, and identification.
/// All content definitions are immutable records for thread-safety and functional programming patterns.
/// </remarks>
/// <param name="Id">The unique content ID identifying this entity.</param>
/// <param name="SchemaVersion">The schema version of this content definition, used for compatibility checking.</param>
/// <param name="Revision">The revision number of this content, incremented when the content is updated.</param>
/// <param name="NameKey">A localization key for the entity's display name (e.g., "content.skill.athletics").</param>
/// <param name="DescriptionKey">A localization key for the entity's description text.</param>
public abstract record ContentDefinition(ContentId Id, int SchemaVersion, int Revision, string NameKey, string DescriptionKey);

/// <summary>
/// Defines a character ability (Strength, Dexterity, etc.) with its range and default value.
/// </summary>
public sealed record AbilityDefinition(
    AbilityId AbilityId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    short Minimum,
    short Maximum,
    short DefaultValue,
    ImmutableArray<string> Tags)
    : ContentDefinition(AbilityId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>
/// Defines a character skill with proficiency range and progression rules.
/// </summary>
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

/// <summary>Defines the threshold and rewards granted when a character reaches one level.</summary>
public sealed record LevelProgressionEntry(
    int Level,
    int RequiredExperience,
    int MaximumHealthIncrease,
    int MaximumManaIncrease,
    int MaximumStaminaIncrease,
    int MaximumResolveIncrease,
    int MaximumStrainIncrease,
    int AbilityPoints,
    int SkillPoints,
    int FeatPoints);

/// <summary>Defines every level threshold and level-up reward for one character progression model.</summary>
public sealed record LevelProgressionTableDefinition(
    LevelProgressionTableId LevelProgressionTableId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<LevelProgressionEntry> Levels)
    : ContentDefinition(LevelProgressionTableId.Value, SchemaVersion, Revision, NameKey, DescriptionKey)
{
    /// <summary>The highest level authored by this table.</summary>
    public int MaximumLevel => Levels.Length;
}

public sealed record CharacterResourceRule(
    ResourceId ResourceId,
    int BaseMaximum,
    int BaseRecoveryRate,
    bool Accumulates,
    ImmutableArray<int> ThresholdPercentages);

public sealed record StaminaTurnMeterRule(
    int MaximumStaminaPercentage,
    int TurnMeterGainPercentage);

public sealed record CharacterTurnRules(
    int TurnMeterThreshold,
    int BaseTurnMeterGain,
    int BaseActionPoints,
    int NormalTurnMeterGainPercentage,
    ImmutableArray<StaminaTurnMeterRule> StaminaTurnMeterRules,
    ImmutableDictionary<ContentId, int> ActionPointCosts);

public sealed record CharacterResourceProfileDefinition(
    CharacterResourceProfileId CharacterResourceProfileId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<CharacterResourceRule> Resources,
    CharacterTurnRules TurnRules)
    : ContentDefinition(CharacterResourceProfileId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>
/// Defines an access privilege or ability category that characters can be granted.
/// </summary>
public sealed record AccessDefinition(
    AccessId AccessId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<string> Tags)
    : ContentDefinition(AccessId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>
/// Identifies whether a Feat changes rules continuously or must be invoked.
/// </summary>
public enum FeatActivation : byte
{
    Passive,
    Active,
}

/// <summary>Execution rules for an active spell Feat.</summary>
public sealed record SpellFeatRules(
    SkillId SkillId,
    ResourceId ManaResourceId,
    int ManaCost,
    ContentId RangeId,
    int CastTimeTicks,
    int CooldownTicks,
    ImmutableArray<string> TargetTags);

/// <summary>Execution rules for an active psionic Feat.</summary>
public sealed record PsionicFeatRules(
    SkillId SkillId,
    SkillId ResistanceSkillId,
    ResourceId StrainResourceId,
    int StrainCost,
    int SustainCostPerTick,
    ContentId ContactModeId,
    ContentId RangeId,
    ContentId InformationScopeId,
    ImmutableArray<ContentId> DisciplineIds,
    ImmutableArray<string> TargetTags);

/// <summary>Defines a passive or active character Feat and its acquisition constraints and rules.</summary>
public sealed record FeatDefinition(
    FeatId FeatId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    FeatActivation Activation,
    TrainingProjectId? TrainingProjectId,
    ImmutableArray<RaceId> CompatibleRaceIds,
    ImmutableArray<AccessId> RequiredAccessIds,
    ImmutableArray<AccessId> GrantedAccessIds,
    ImmutableArray<FeatId> GrantedFeatIds,
    ImmutableArray<ContentId> EffectIds,
    SpellFeatRules? SpellRules,
    PsionicFeatRules? PsionicRules)
    : ContentDefinition(FeatId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>
/// Defines a playable character race with inherent Feats and compatibility requirements.
/// </summary>
public sealed record RaceDefinition(
    RaceId RaceId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<FeatId> GrantedFeatIds,
    ImmutableArray<ContentId> RequiredSupportIds)
    : ContentDefinition(RaceId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>
/// Defines a training project that characters can undertake to learn Feats.
/// </summary>
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
    ImmutableArray<FeatId> GrantedFeatIds)
    : ContentDefinition(TrainingProjectId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>
/// Defines a heritage or ethnic variant of a race with additional Feats.
/// </summary>
public sealed record HeritageDefinition(
    HeritageId HeritageId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    RaceId RaceId,
    ImmutableArray<FeatId> GrantedFeatIds)
    : ContentDefinition(HeritageId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>
/// Defines a character background (origin story) with skill bonuses and ability increases.
/// </summary>
public sealed record BackgroundDefinition(
    BackgroundId BackgroundId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<RaceId> CompatibleRaceIds,
    ImmutableArray<AbilityId> AbilityBonusIds,
    ImmutableArray<SkillId> FocusSkillIds)
    : ContentDefinition(BackgroundId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

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

/// <summary>
/// Defines scenario-level crew rules shared by character creation and recruitment.
/// </summary>
public sealed record ScenarioDefinition(
    ScenarioId ScenarioId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int MaximumRosterSize,
    LevelProgressionTableId? LevelProgressionTableId,
    CharacterResourceProfileId? CharacterResourceProfileId)
    : ContentDefinition(ScenarioId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

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
    ImmutableArray<ContentId> EffectIds,
    MeleeWeaponId? MeleeWeaponId)
    : ContentDefinition(EquipmentId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public enum MeleeWeaponFamily : byte
{
    Blade,
    Dagger,
    Axe,
    Blunt,
    Polearm,
    Fist,
}

public enum MeleeWeaponTechnology : byte
{
    Conventional,
    Chain,
    Shock,
    Powered,
    Arcane,
}

public enum MeleeWeaponHands : byte
{
    OneHanded,
    TwoHanded,
}

/// <summary>Data-driven properties shared by every action performed with one melee weapon.</summary>
public sealed record MeleeWeaponDefinition(
    MeleeWeaponId MeleeWeaponId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    MeleeWeaponFamily Family,
    MeleeWeaponTechnology Technology,
    MeleeWeaponHands Hands,
    SkillId SkillId,
    AbilityId AbilityId,
    int DamageMinimum,
    int DamageMaximum,
    int ArmorDamagePercentage,
    int ArmorPenetrationPercentage,
    int StaminaCost,
    int Range,
    int WeightGrams,
    int MaximumDurability,
    int Value,
    int EnergyCapacity,
    int EnergyPerAttack,
    int UnpoweredDamagePercentage,
    int UnpoweredArmorPenetrationPercentage,
    int StrengthDamageScale,
    ImmutableArray<string> Traits,
    ImmutableArray<MeleeWeaponActionId> ActionIds)
    : ContentDefinition(MeleeWeaponId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>Action-owned AP, accuracy, cost, and effect modifiers for a melee attack.</summary>
public sealed record MeleeWeaponActionDefinition(
    MeleeWeaponActionId MeleeWeaponActionId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int ActionPointCost,
    int StaminaCostModifier,
    int HitModifier,
    int DamagePercentage,
    int ArmorDamagePercentage,
    int ArmorPenetrationModifier,
    int RangeModifier,
    int EnergyCostModifier,
    int DurabilityCost,
    ImmutableArray<ContentId> EffectIds)
    : ContentDefinition(MeleeWeaponActionId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

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
