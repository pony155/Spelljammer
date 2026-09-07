namespace Spelljammer.Persistence;

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
    public WorldDto World { get; set; } = new();
    public CharacterDto[] Characters { get; set; } = [];
}

internal sealed class WorldDto
{
    public ulong Seed { get; set; }
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
    public string[] ReadyActorIds { get; set; } = [];
    public VoyageEventDto[] Events { get; set; } = [];
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
    public string[] EquipmentIds { get; set; } = [];
    public ValueDto[] Resources { get; set; } = [];
    public ValueDto[] TrainingProgress { get; set; } = [];
    public bool CanAct { get; set; }
    public CapabilityEffectDto[] ActiveEffects { get; set; } = [];
    public CapabilityEvidenceDto[] Evidence { get; set; } = [];
}

internal sealed class CapabilityDto
{
    public AttributeValueDto[] Attributes { get; set; } = [];
    public SkillValueDto[] Skills { get; set; } = [];
    public string[] FeatIds { get; set; } = [];
    public string[] PerkIds { get; set; } = [];
    public string[] TechniqueIds { get; set; } = [];
    public GrantDto[] GrantSources { get; set; } = [];
    public string[] PracticeKeys { get; set; } = [];
}

internal sealed class AttributeValueDto
{
    public string Id { get; set; } = string.Empty;
    public short Value { get; set; }
}

internal sealed class SkillValueDto
{
    public string Id { get; set; } = string.Empty;
    public byte Value { get; set; }
    public ushort Practice { get; set; }
}

internal sealed class GrantDto
{
    public string CapabilityId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public int SourceKind { get; set; }
}

internal sealed class CapabilityEffectDto
{
    public string EffectId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public long StartTick { get; set; }
    public long EndTick { get; set; }
    public string ScopeId { get; set; } = string.Empty;
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
    public PersonalActorDto[] Actors { get; set; } = [];
    public ObjectiveDto[] Objectives { get; set; } = [];
    public string[] ExplorationChangeIds { get; set; } = [];
    public string[] DamagedObjectIds { get; set; } = [];
    public bool Retreated { get; set; }
    public bool CleanedUp { get; set; }
    public EncounterEffectDto[] ActiveEffects { get; set; } = [];
}

internal sealed class PersonalActorDto
{
    public string Id { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string? CharacterId { get; set; }
    public string CellId { get; set; } = string.Empty;
    public int TurnMeter { get; set; }
    public int TurnRate { get; set; }
    public int ActionPoints { get; set; }
    public int Health { get; set; }
    public bool Defending { get; set; }
    public bool Surrendered { get; set; }
    public bool Prisoner { get; set; }
    public int ReservedReactionPoints { get; set; }
    public long ReactionExpiresTick { get; set; }
    public EquipmentStateDto[] Equipment { get; set; } = [];
    public InjuryDto[] Injuries { get; set; } = [];
}

internal sealed class EquipmentStateDto
{
    public string SlotId { get; set; } = string.Empty;
    public string EquipmentId { get; set; } = string.Empty;
    public int Condition { get; set; }
    public int ResourceRemaining { get; set; }
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

internal sealed class EncounterEffectDto
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public long ExpiresTick { get; set; }
    public int Stacks { get; set; }
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

internal sealed class VoyageEventDto
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
