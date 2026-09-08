using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Characters;

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

public sealed record LevelProgressionTableDefinition(
    LevelProgressionTableId LevelProgressionTableId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<LevelProgressionEntry> Levels)
    : ContentDefinition(LevelProgressionTableId.Value, SchemaVersion, Revision, NameKey, DescriptionKey)
{
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

public sealed record AccessDefinition(
    AccessId AccessId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<string> Tags)
    : ContentDefinition(AccessId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public enum FeatActivation : byte
{
    Passive,
    Active,
}

public sealed record SpellFeatRules(
    SkillId SkillId,
    ResourceId ManaResourceId,
    int ManaCost,
    ContentId RangeId,
    int CastTimeTicks,
    int CooldownTicks,
    ImmutableArray<string> TargetTags);

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
    ImmutableArray<EffectApplicationDefinition> Effects,
    SpellFeatRules? SpellRules,
    PsionicFeatRules? PsionicRules)
    : ContentDefinition(FeatId.Value, SchemaVersion, Revision, NameKey, DescriptionKey)
{
    public ImmutableArray<EffectId> EffectIds => [.. Effects.Select(value => value.EffectId)];
}

public sealed record RaceDefinition(
    RaceId RaceId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    ImmutableArray<FeatId> GrantedFeatIds,
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
    ImmutableArray<FeatId> GrantedFeatIds)
    : ContentDefinition(TrainingProjectId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record HeritageDefinition(
    HeritageId HeritageId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    RaceId RaceId,
    ImmutableArray<FeatId> GrantedFeatIds)
    : ContentDefinition(HeritageId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

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
    ImmutableArray<ContentId> StartingItemDefinitionIds,
    int InventoryMaximumWeightHundredthsOfPound,
    int InventoryMaximumEntries,
    ImmutableArray<SkillId> FocusSkillIds,
    ImmutableArray<ResourceId> ResourceIds)
    : ContentDefinition(CharacterId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

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
