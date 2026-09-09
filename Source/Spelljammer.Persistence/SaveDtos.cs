namespace Spelljammer.Persistence;

/// <summary>
/// Defines the bounded JSON transfer objects used inside campaign save envelopes.
/// </summary>
/// <remarks>
/// Code flow: Write mappings project authoritative domain state into DTOs, JSON serialization stores them in the save envelope, and read mappings reconstruct validated domain values.
/// </remarks>
internal sealed class SavePreflightDto
{
    public string Discriminator { get; set; } = string.Empty;
    public string GameBuild { get; set; } = string.Empty;
    public ContentLockDto ContentLock { get; set; } = new();
    public string[] RequiredDefinitionIds { get; set; } = [];
}

internal sealed class ContentLockDto
{
    public int BaseContentRevision { get; set; }
    public PackLockDto[] Packs { get; set; } = [];
    public string ManifestFingerprint { get; set; } = string.Empty;
    public string SemanticFingerprint { get; set; } = string.Empty;
    public string EffectiveFingerprint { get; set; } = string.Empty;
    public int GeneratorVersion { get; set; }
    public int FormulaVersion { get; set; }
    public int EffectVersion { get; set; }
    public ushort SaveSchemaVersion { get; set; }
    public string[] AppliedMigrationIds { get; set; } = [];
}

internal sealed class PackLockDto
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int ContentRevision { get; set; }
}

internal sealed class CampaignPayloadDto
{
    public string CurrentLocationId { get; set; } = string.Empty;
    public string ProtagonistId { get; set; } = string.Empty;
    public WorldDto World { get; set; } = new();
    public CharacterDto[] Characters { get; set; } = [];
}

internal sealed class WorldDto
{
    public ulong Seed { get; set; }
    public GalaxyDto? Galaxy { get; set; }
    public VoyageNavigationDto? VoyageNavigation { get; set; }
    public CampaignClockDto Clock { get; set; } = new();
    public long Tick { get; set; }
    public ulong RandomSequence { get; set; }
    public string PlayerTeamId { get; set; } = string.Empty;
    public bool ShipPaused { get; set; }
    public bool PersonalPaused { get; set; }
    public ShipDto[] Ships { get; set; } = [];
    public PersonalEncounterDto? PersonalEncounter { get; set; }
    public CommandDto[] Commands { get; set; } = [];
    public CommandLogDto[] CommandHistory { get; set; } = [];
    public ScheduledActionDto[] ScheduledActions { get; set; } = [];
    public string[] ReadyUnitIds { get; set; } = [];
    public EventDto[] Events { get; set; } = [];
}

internal sealed class GalaxyDto
{
    public GalaxyTopologyDto Topology { get; set; } = new();
    public GalaxyKnowledgeDto[] Knowledge { get; set; } = [];
    public GalaxyDynamicDto Dynamic { get; set; } = new();
}

internal sealed class GalaxyTopologyDto
{
    public int GeneratorVersion { get; set; }
    public ulong Seed { get; set; }
    public GalaxySystemDto[] Systems { get; set; } = [];
    public StarwayDto[] Starways { get; set; } = [];
}

internal sealed class GalaxyDynamicDto
{
    public StarwayDynamicDto[] Starways { get; set; } = [];
    public string[] ChangedSiteIds { get; set; } = [];
}

internal sealed class StarwayDynamicDto
{
    public string StarwayId { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public int DangerModifier { get; set; }
    public string? ControllingFactionId { get; set; }
}

internal sealed class VoyageNavigationDto
{
    public string CurrentSystemId { get; set; } = string.Empty;
    public string? ActiveStarwayId { get; set; }
    public string[] PlannedRouteIds { get; set; } = [];
    public int RouteProgress { get; set; }
    public long DepartureTick { get; set; }
    public long ArrivalTick { get; set; }
}

internal sealed class GalaxySystemDto
{
    public string Id { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public int Region { get; set; }
    public int DisplayX { get; set; }
    public int DisplayY { get; set; }
    public string ArchetypeId { get; set; } = string.Empty;
}

internal sealed class StarwayDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstSystemId { get; set; } = string.Empty;
    public string SecondSystemId { get; set; } = string.Empty;
    public int TravelTime { get; set; }
    public int FuelCost { get; set; }
    public int Danger { get; set; }
}

internal sealed class GalaxyKnowledgeDto
{
    public string SystemId { get; set; } = string.Empty;
    public int Level { get; set; }
}

internal sealed class CampaignClockDto
{
    public long ElapsedWorldSeconds { get; set; }
    public int FractionRemainder { get; set; }
    public string CalendarId { get; set; } = string.Empty;
    public string TimeScaleId { get; set; } = string.Empty;
}

internal sealed class ShipDto
{
    public string Id { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string FrameId { get; set; } = string.Empty;
    public string PathId { get; set; } = string.Empty;
    public int Hull { get; set; }
    public int Armor { get; set; }
    public int Cargo { get; set; }
    public long PositionX { get; set; }
    public long PositionY { get; set; }
    public long VelocityX { get; set; }
    public long VelocityY { get; set; }
    public int HeadingMilliDegrees { get; set; }
    public int CollisionRadius { get; set; }
    public ModuleDto[] Modules { get; set; } = [];
    public ValueDto[] Resources { get; set; } = [];
    public ContactDto[] Contacts { get; set; } = [];
    public string[] PersistentEvidenceIds { get; set; } = [];
    public bool Disengaged { get; set; }
    public bool Defending { get; set; }
}

internal sealed class ModuleDto
{
    public string InstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public int Condition { get; set; }
    public int Integrity { get; set; }
    public bool IsOn { get; set; }
    public bool IsPowered { get; set; }
    public bool ShieldRaised { get; set; }
    public int CurrentShield { get; set; }
    public string? WeaponConfigurationId { get; set; }
    public int WeaponReadiness { get; set; }
    public long ReadyTick { get; set; }
}

internal sealed class ContactDto
{
    public string ShipId { get; set; } = string.Empty;
    public string KnowledgeId { get; set; } = string.Empty;
    public long LastObservedTick { get; set; }
    public bool HasFiringSolution { get; set; }
    public string[] WitnessIds { get; set; } = [];
}

internal sealed class CharacterDto
{
    public string Id { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public string RaceId { get; set; } = string.Empty;
    public string HeritageId { get; set; } = string.Empty;
    public string BackgroundId { get; set; } = string.Empty;
    public string PositionId { get; set; } = string.Empty;
    public CapabilityDto Capabilities { get; set; } = new();
    public string[] LanguageIds { get; set; } = [];
    public string[] ScriptIds { get; set; } = [];
    public ItemSystemDto Items { get; set; } = new();
    public ValueDto[] Resources { get; set; } = [];
    public CharacterResourceDto[] CharacterResources { get; set; } = [];
    public ValueDto[] TrainingProgress { get; set; } = [];
    public bool CanAct { get; set; }
    public StatusInstanceDto[] Statuses { get; set; } = [];
    public CapabilityEvidenceDto[] Evidence { get; set; } = [];
    public InjuryDto[] Injuries { get; set; } = [];
}

internal sealed class CharacterResourceDto
{
    public string Id { get; set; } = string.Empty;
    public int CurrentValue { get; set; }
    public int BaseMaximum { get; set; }
    public int BaseRecoveryRate { get; set; }
    public bool Accumulates { get; set; }
    public CharacterResourceModifierDto[] PermanentModifiers { get; set; } = [];
    public CharacterResourceModifierDto[] TemporaryModifiers { get; set; } = [];
}

internal sealed class CharacterResourceModifierDto
{
    public string SourceId { get; set; } = string.Empty;
    public int MaximumDelta { get; set; }
    public int RecoveryRateDelta { get; set; }
}

internal sealed class CharacterTurnDto
{
    public int CurrentTurnMeter { get; set; }
    public int TurnMeterThreshold { get; set; }
    public int BaseTurnMeterGain { get; set; }
    public int CurrentActionPoints { get; set; }
    public int BaseMaximumActionPoints { get; set; }
    public int NormalTurnMeterGainPercentage { get; set; }
    public StaminaTurnMeterRuleDto[] StaminaTurnMeterRules { get; set; } = [];
    public ValueDto[] ActionPointCosts { get; set; } = [];
    public CharacterTurnModifierDto[] PermanentModifiers { get; set; } = [];
    public CharacterTurnModifierDto[] TemporaryModifiers { get; set; } = [];
}

internal sealed class StaminaTurnMeterRuleDto
{
    public int MaximumStaminaPercentage { get; set; }
    public int TurnMeterGainPercentage { get; set; }
}

internal sealed class CharacterTurnModifierDto
{
    public string SourceId { get; set; } = string.Empty;
    public int TurnMeterGainPercentageDelta { get; set; }
    public int MaximumActionPointsDelta { get; set; }
}

internal sealed class CapabilityDto
{
    public AbilityValueDto[] Abilities { get; set; } = [];
    public SkillValueDto[] Skills { get; set; } = [];
    public string[] FeatIds { get; set; } = [];
    public GrantDto[] GrantSources { get; set; } = [];
}

internal sealed class AbilityValueDto
{
    public string Id { get; set; } = string.Empty;
    public short Value { get; set; }
}

internal sealed class SkillValueDto
{
    public string Id { get; set; } = string.Empty;
    public byte Value { get; set; }
}

internal sealed class GrantDto
{
    public string CapabilityId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public int SourceKind { get; set; }
}

internal sealed class CapabilityEvidenceDto
{
    public string EvidenceId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public long Tick { get; set; }
    public bool Succeeded { get; set; }
}

internal sealed class PersonalEncounterDto
{
    public string Id { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public BattleUnitDto[] Units { get; set; } = [];
    public ObjectiveDto[] Objectives { get; set; } = [];
    public string[] ExplorationChangeIds { get; set; } = [];
    public string[] DamagedObjectIds { get; set; } = [];
    public bool Retreated { get; set; }
    public bool CleanedUp { get; set; }
}

internal sealed class BattleUnitDto
{
    public string Id { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string? CharacterId { get; set; }
    public string CellId { get; set; } = string.Empty;
    public CharacterTurnDto Turn { get; set; } = new();
    public CharacterResourceDto[] CharacterResources { get; set; } = [];
    public bool Defending { get; set; }
    public bool Surrendered { get; set; }
    public bool Prisoner { get; set; }
    public int ReservedReactionPoints { get; set; }
    public long ReactionExpiresTick { get; set; }
    public ItemSystemDto Items { get; set; } = new();
    public InjuryDto[] Injuries { get; set; } = [];
    public StatusInstanceDto[] Statuses { get; set; } = [];
}

internal sealed class StatusInstanceDto
{
    public string InstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public int DefinitionRevision { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public int RemainingDuration { get; set; }
    public int Stacks { get; set; }
    public int Potency { get; set; }
}

internal sealed class ItemSystemDto
{
    public ItemInstanceDto[] ItemInstances { get; set; } = [];
    public InventoryEntryDto[] InventoryEntries { get; set; } = [];
    public InventoryContainerDto[] InventoryContainers { get; set; } = [];
    public EquipmentLoadoutDto[] EquipmentLoadouts { get; set; } = [];
}

internal sealed class InventoryEntryDto
{
    public string EntryId { get; set; } = string.Empty;
    public string OwnerContainerId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

internal sealed class ItemInstanceDto
{
    public string InstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string OwnerContainerId { get; set; } = string.Empty;
    public int? CurrentDurability { get; set; }
    public string? QualityId { get; set; }
    public string? CustomName { get; set; }
    public int StateVersion { get; set; }
    public MeleeWeaponStateDto? MeleeWeaponState { get; set; }
    public RangedWeaponStateDto? RangedWeaponState { get; set; }
}

internal sealed class InventoryContainerDto
{
    public string ContainerId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public int MaximumWeightHundredthsOfPound { get; set; }
    public int MaximumEntries { get; set; }
    public string[] ItemInstanceIds { get; set; } = [];
    public string[] InventoryEntryIds { get; set; } = [];
}

internal sealed class EquipmentLoadoutDto
{
    public string OwnerId { get; set; } = string.Empty;
    public SlotAssignmentDto[] SlotAssignments { get; set; } = [];
}

internal sealed class SlotAssignmentDto
{
    public string SlotId { get; set; } = string.Empty;
    public string ItemInstanceId { get; set; } = string.Empty;
}

internal sealed class MeleeWeaponStateDto
{
    public string WeaponId { get; set; } = string.Empty;
    public int CurrentDurability { get; set; }
    public int CurrentEnergy { get; set; }
}

internal sealed class RangedWeaponStateDto
{
    public string WeaponId { get; set; } = string.Empty;
    public int CurrentDurability { get; set; }
    public string? LoadedAmmunitionId { get; set; }
    public int CurrentAmmunition { get; set; }
    public int CurrentEnergy { get; set; }
    public int CurrentHeat { get; set; }
}

internal sealed class InjuryDto
{
    public string Id { get; set; } = string.Empty;
    public int Severity { get; set; }
    public bool Stabilized { get; set; }
}

internal sealed class ObjectiveDto
{
    public string Id { get; set; } = string.Empty;
    public int State { get; set; }
}

internal sealed class CommandDto
{
    public string Id { get; set; } = string.Empty;
    public int Kind { get; set; }
    public long TargetTick { get; set; }
    public int Priority { get; set; }
    public string IssuerId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public long VectorX { get; set; }
    public long VectorY { get; set; }
    public int Amount { get; set; }
    public string? OptionId { get; set; }
    public ulong Sequence { get; set; }
}

internal sealed class CommandLogDto
{
    public long SubmittedTick { get; set; }
    public CommandDto Command { get; set; } = new();
    public long? CancelledTick { get; set; }
}

internal sealed class ScheduledActionDto
{
    public CommandDto Command { get; set; } = new();
    public int Phase { get; set; }
    public long CommitTick { get; set; }
    public long RecoverTick { get; set; }
    public string? ReservedResourceId { get; set; }
    public int ReservedAmount { get; set; }
    public int[] History { get; set; } = [];
}

internal sealed class EventDto
{
    public string Id { get; set; } = string.Empty;
    public long Tick { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public int Kind { get; set; }
    public bool Succeeded { get; set; }
    public int Amount { get; set; }
    public string ResultCode { get; set; } = string.Empty;
}

internal sealed class ValueDto
{
    public string Id { get; set; } = string.Empty;
    public int Value { get; set; }
}
