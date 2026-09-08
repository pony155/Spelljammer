using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Persistence;

public static partial class CampaignSaveCodec
{
    private static CampaignPayloadDto ToDto(CampaignState campaign, GameContentSnapshot content) => new()
    {
        CurrentLocationId = campaign.CurrentLocationId.ToString(),
        ProtagonistId = campaign.ProtagonistId.ToString(),
        World = ToDto(campaign.Voyage),
        Characters = [.. campaign.Characters.OrderBy(value => value.Id).Select(value => ToDto(value, content))],
    };

    private static WorldDto ToDto(VoyageWorld world) => new()
    {
        Seed = world.Seed,
        Tick = world.Tick,
        RandomSequence = world.RandomSequence,
        PlayerTeamId = world.PlayerTeamId.ToString(),
        ShipPaused = world.ShipPaused,
        PersonalPaused = world.PersonalPaused,
        Ships = [.. world.Ships.Values.OrderBy(value => value.Id).Select(ToDto)],
        PersonalEncounter = world.PersonalEncounter is null ? null : ToDto(world.PersonalEncounter),
        Commands = [.. world.Commands.Select(ToDto)],
        CommandHistory = [.. world.CommandHistory.Select(value => new CommandLogDto
        {
            SubmittedTick = value.SubmittedTick,
            Command = ToDto(value.Command),
            CancelledTick = value.CancelledTick,
        })],
        ScheduledActions = [.. world.ScheduledActions.Select(value => new ScheduledActionDto
        {
            Command = ToDto(value.Command),
            Phase = (int)value.Phase,
            CommitTick = value.CommitTick,
            RecoverTick = value.RecoverTick,
            ReservedResourceId = value.ReservedResourceId?.ToString(),
            ReservedAmount = value.ReservedAmount,
            History = [.. value.History.Select(phase => (int)phase)],
        })],
        ReadyActorIds = [.. world.ReadyActors.Select(value => value.ToString())],
        Events = [.. world.Events.Select(value => new VoyageEventDto
        {
            Id = value.Id.ToString(),
            Tick = value.Tick,
            SourceId = value.SourceId.ToString(),
            TargetId = value.TargetId.ToString(),
            Kind = (int)value.Kind,
            Succeeded = value.Succeeded,
            Amount = value.Amount,
            ResultCode = value.ResultCode,
        })],
    };

    private static ShipDto ToDto(ShipState ship) => new()
    {
        Id = ship.Id.ToString(),
        TeamId = ship.TeamId.ToString(),
        FrameId = ship.Frame.ShipFrameId.ToString(),
        PathId = ship.PathId.ToString(),
        Hull = ship.Hull,
        Armor = ship.Armor,
        Cargo = ship.Cargo,
        PositionX = ship.Position.X.Raw,
        PositionY = ship.Position.Y.Raw,
        VelocityX = ship.Velocity.X.Raw,
        VelocityY = ship.Velocity.Y.Raw,
        HeadingMilliDegrees = ship.HeadingMilliDegrees,
        CollisionRadius = ship.CollisionRadius,
        Modules = [.. ship.Modules.OrderBy(value => value.InstanceId).Select(value => new ModuleDto
        {
            InstanceId = value.InstanceId.ToString(),
            DefinitionId = value.Definition.ModuleId.ToString(),
            Condition = (int)value.Condition,
            Integrity = value.Integrity,
            IsOn = value.IsOn,
            IsPowered = value.IsPowered,
            ShieldRaised = value.ShieldRaised,
            CurrentShield = value.CurrentShield,
            WeaponConfigurationId = value.Weapon?.ShipWeaponConfigurationId.ToString(),
            WeaponReadiness = (int)value.WeaponReadiness,
            ReadyTick = value.ReadyTick,
        })],
        Resources = Values(ship.Resources.Select(value => (value.Key.Value, value.Value))),
        Contacts = [.. ship.Contacts.Values.OrderBy(value => value.ShipId).Select(value => new ContactDto
        {
            ShipId = value.ShipId.ToString(),
            KnowledgeId = value.KnowledgeId.ToString(),
            LastObservedTick = value.LastObservedTick,
            HasFiringSolution = value.HasFiringSolution,
            WitnessIds = [.. value.Witnesses.Order().Select(id => id.ToString())],
        })],
        PersistentEvidenceIds = [.. ship.PersistentEvidence.Order().Select(value => value.ToString())],
        Disengaged = ship.Disengaged,
        Defending = ship.Defending,
    };

    private static CharacterDto ToDto(CharacterState character, GameContentSnapshot content)
    {
        CharacterCapabilitySnapshot snapshot = character.Capabilities.Snapshot(content);
        return new CharacterDto
        {
            Id = character.Id.ToString(),
            ScenarioId = character.ScenarioId.ToString(),
            RaceId = character.RaceId.ToString(),
            HeritageId = character.HeritageId.ToString(),
            BackgroundId = character.BackgroundId.ToString(),
            PositionId = character.PositionId.ToString(),
            Capabilities = new CapabilityDto
            {
                Abilities = [.. snapshot.Abilities.Select(value => new AbilityValueDto { Id = value.Id.ToString(), Value = value.Value })],
                Skills = [.. snapshot.Skills.Select(value => new SkillValueDto { Id = value.Id.ToString(), Value = value.Value })],
                FeatIds = [.. snapshot.Feats.Select(value => value.ToString())],
                GrantSources = [.. snapshot.GrantSources.Select(value => new GrantDto
                {
                    CapabilityId = value.CapabilityId.ToString(),
                    SourceId = value.SourceId.ToString(),
                    SourceKind = (int)value.SourceKind,
                })],
            },
            LanguageIds = [.. character.LanguageIds.Order().Select(value => value.ToString())],
            ScriptIds = [.. character.ScriptIds.Order().Select(value => value.ToString())],
            Items = ToDto(character.Items),
            Resources = Values(character.Resources.Select(value => (value.Key.Value, value.Value))),
            CharacterResources = [.. character.CharacterResources.Values.OrderBy(value => value.ResourceId).Select(ToDto)],
            TrainingProgress = Values(character.TrainingProgress.Select(value => (value.Key.Value, value.Value))),
            CanAct = character.CanAct,
            Statuses = [.. character.Statuses.Instances.OrderBy(value => value.InstanceId).Select(ToDto)],
            Evidence = [.. character.Evidence.Select(value => new CapabilityEvidenceDto
            {
                EvidenceId = value.EvidenceId.ToString(),
                SourceId = value.SourceId.ToString(),
                ActorId = value.ActorId.ToString(),
                TargetId = value.TargetId.ToString(),
                Tick = value.Tick,
                Succeeded = value.Succeeded,
            })],
        };
    }

    private static ItemSystemDto ToDto(ItemSystemState state) => new()
    {
        ItemInstances = [.. state.ItemInstances.OrderBy(value => value.InstanceId.Value).Select(value => new ItemInstanceDto
        {
            InstanceId = value.InstanceId.ToString(),
            DefinitionId = value.DefinitionId.ToString(),
            OwnerContainerId = value.OwnerContainerId.ToString(),
            CurrentDurability = value.CurrentDurability,
            QualityId = value.QualityId?.ToString(),
            CustomName = value.CustomName,
            StateVersion = value.StateVersion,
            MeleeWeaponState = value.MeleeWeaponState is null ? null : new MeleeWeaponStateDto
            {
                WeaponId = value.MeleeWeaponState.WeaponId.ToString(),
                CurrentDurability = value.MeleeWeaponState.CurrentDurability,
                CurrentEnergy = value.MeleeWeaponState.CurrentEnergy,
            },
            RangedWeaponState = value.RangedWeaponState is null ? null : new RangedWeaponStateDto
            {
                WeaponId = value.RangedWeaponState.WeaponId.ToString(),
                CurrentDurability = value.RangedWeaponState.CurrentDurability,
                LoadedAmmunitionId = value.RangedWeaponState.LoadedAmmunitionId?.ToString(),
                CurrentAmmunition = value.RangedWeaponState.CurrentAmmunition,
                CurrentEnergy = value.RangedWeaponState.CurrentEnergy,
                CurrentHeat = value.RangedWeaponState.CurrentHeat,
            },
        })],
        InventoryEntries = [.. state.InventoryEntries.OrderBy(value => value.EntryId.Value).Select(value => new InventoryEntryDto
        {
            EntryId = value.EntryId.ToString(),
            OwnerContainerId = value.OwnerContainerId.ToString(),
            DefinitionId = value.Stack.DefinitionId.ToString(),
            Quantity = value.Stack.Quantity,
        })],
        InventoryContainers = [.. state.InventoryContainers.OrderBy(value => value.ContainerId.Value).Select(value => new InventoryContainerDto
        {
            ContainerId = value.ContainerId.ToString(),
            OwnerId = value.OwnerId.ToString(),
            MaximumWeightHundredthsOfPound = value.MaximumWeightHundredthsOfPound,
            MaximumEntries = value.MaximumEntries,
            ItemInstanceIds = [.. value.ItemInstanceIds.OrderBy(id => id.Value).Select(id => id.ToString())],
            InventoryEntryIds = [.. value.InventoryEntryIds.OrderBy(id => id.Value).Select(id => id.ToString())],
        })],
        EquipmentLoadouts = [.. state.EquipmentLoadouts.OrderBy(value => value.OwnerId).Select(value => new EquipmentLoadoutDto
        {
            OwnerId = value.OwnerId.ToString(),
            SlotAssignments = [.. value.SlotAssignments.OrderBy(item => item.SlotId).Select(item => new SlotAssignmentDto
            {
                SlotId = item.SlotId.ToString(),
                ItemInstanceId = item.ItemInstanceId.ToString(),
            })],
        })],
    };

    private static CharacterResourceModifierDto ToDto(CharacterResourceModifier modifier) => new()
    {
        SourceId = modifier.SourceId.ToString(),
        MaximumDelta = modifier.MaximumDelta,
        RecoveryRateDelta = modifier.RecoveryRateDelta,
    };

    private static CharacterResourceModifier FromDto(CharacterResourceModifierDto modifier) => new(
        new ContentId(modifier.SourceId), modifier.MaximumDelta, modifier.RecoveryRateDelta);

    private static CharacterResourceDto ToDto(CharacterResourceState value) => new()
    {
        Id = value.ResourceId.ToString(),
        CurrentValue = value.CurrentValue,
        BaseMaximum = value.BaseMaximum,
        BaseRecoveryRate = value.BaseRecoveryRate,
        Accumulates = value.Accumulates,
        PermanentModifiers = [.. value.PermanentModifiers.Select(ToDto)],
        TemporaryModifiers = [.. value.TemporaryModifiers.Select(ToDto)],
    };

    private static CharacterResourceState FromDto(CharacterResourceDto value)
    {
        RequireCount(value.PermanentModifiers.Length, CharacterResourceSet.MaximumModifiersPerResource);
        RequireCount(value.TemporaryModifiers.Length, CharacterResourceSet.MaximumModifiersPerResource);
        return new CharacterResourceState(
            new ResourceId(value.Id), value.CurrentValue, value.BaseMaximum, value.BaseRecoveryRate, value.Accumulates,
            [.. value.PermanentModifiers.Select(FromDto)], [.. value.TemporaryModifiers.Select(FromDto)]);
    }

    private static CharacterTurnModifierDto ToDto(CharacterTurnModifier modifier) => new()
    {
        SourceId = modifier.SourceId.ToString(),
        TurnMeterGainPercentageDelta = modifier.TurnMeterGainPercentageDelta,
        MaximumActionPointsDelta = modifier.MaximumActionPointsDelta,
    };

    private static CharacterTurnModifier FromDto(CharacterTurnModifierDto modifier) => new(
        new ContentId(modifier.SourceId), modifier.TurnMeterGainPercentageDelta, modifier.MaximumActionPointsDelta);

    private static CharacterTurnDto ToDto(CharacterTurnState value) => new()
    {
        CurrentTurnMeter = value.CurrentTurnMeter,
        TurnMeterThreshold = value.TurnMeterThreshold,
        BaseTurnMeterGain = value.BaseTurnMeterGain,
        CurrentActionPoints = value.CurrentActionPoints,
        BaseMaximumActionPoints = value.BaseMaximumActionPoints,
        NormalTurnMeterGainPercentage = value.NormalTurnMeterGainPercentage,
        StaminaTurnMeterRules = [.. value.StaminaTurnMeterRules.Select(rule => new StaminaTurnMeterRuleDto
        {
            MaximumStaminaPercentage = rule.MaximumStaminaPercentage,
            TurnMeterGainPercentage = rule.TurnMeterGainPercentage,
        })],
        ActionPointCosts = Values(value.ActionPointCosts.Select(pair => (pair.Key, pair.Value))),
        PermanentModifiers = [.. value.PermanentModifiers.Select(ToDto)],
        TemporaryModifiers = [.. value.TemporaryModifiers.Select(ToDto)],
    };

    private static CharacterTurnState FromDto(CharacterTurnDto value)
    {
        RequireCount(value.StaminaTurnMeterRules.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.ActionPointCosts.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.PermanentModifiers.Length, CharacterTurnState.MaximumModifiers);
        RequireCount(value.TemporaryModifiers.Length, CharacterTurnState.MaximumModifiers);
        return new CharacterTurnState(
            value.CurrentTurnMeter,
            value.TurnMeterThreshold,
            value.BaseTurnMeterGain,
            value.CurrentActionPoints,
            value.BaseMaximumActionPoints,
            value.NormalTurnMeterGainPercentage,
            [.. value.StaminaTurnMeterRules.Select(rule => new StaminaTurnMeterRule(
                rule.MaximumStaminaPercentage, rule.TurnMeterGainPercentage))],
            ValueDictionary(value.ActionPointCosts).ToImmutableDictionary(),
            [.. value.PermanentModifiers.Select(FromDto)],
            [.. value.TemporaryModifiers.Select(FromDto)]).Clamp();
    }

    private static PersonalEncounterDto ToDto(PersonalEncounterState encounter) => new()
    {
        Id = encounter.Id.ToString(),
        BoardId = encounter.Board.Definition.PersonalBoardId.ToString(),
        Actors = [.. encounter.Actors.Values.OrderBy(value => value.Id).Select(value => new PersonalActorDto
        {
            Id = value.Id.ToString(),
            TeamId = value.TeamId.ToString(),
            CharacterId = value.CharacterId?.ToString(),
            CellId = value.CellId.ToString(),
            Turn = ToDto(value.Turn),
            CharacterResources = [.. value.CharacterResources.Values.OrderBy(resource => resource.ResourceId).Select(ToDto)],
            Defending = value.Defending,
            Surrendered = value.Surrendered,
            Prisoner = value.Prisoner,
            ReservedReactionPoints = value.ReservedReactionPoints,
            ReactionExpiresTick = value.ReactionExpiresTick,
            Items = ToDto(value.Items),
            Injuries = [.. value.Injuries.Select(injury => new InjuryDto
            {
                Id = injury.Id.ToString(),
                Severity = (int)injury.Severity,
                Stabilized = injury.Stabilized,
            })],
            Statuses = [.. value.Statuses.Instances.OrderBy(status => status.InstanceId).Select(ToDto)],
        })],
        Objectives = [.. encounter.Objectives.OrderBy(value => value.Key).Select(value =>
            new ObjectiveDto { Id = value.Key.ToString(), State = (int)value.Value })],
        ExplorationChangeIds = [.. encounter.ExplorationChanges.Order().Select(value => value.ToString())],
        DamagedObjectIds = [.. encounter.DamagedObjects.Order().Select(value => value.ToString())],
        Retreated = encounter.Retreated,
        CleanedUp = encounter.CleanedUp,
    };

    private static CommandDto ToDto(VoyageCommand value) => new()
    {
        Id = value.Id.ToString(),
        Kind = (int)value.Kind,
        TargetTick = value.TargetTick,
        Priority = value.Priority,
        IssuerId = value.IssuerId.ToString(),
        TargetId = value.TargetId.ToString(),
        VectorX = value.Vector.X.Raw,
        VectorY = value.Vector.Y.Raw,
        Amount = value.Amount,
        OptionId = value.OptionId?.ToString(),
        Sequence = value.Sequence,
    };

    private static ValueDto[] Values(IEnumerable<(ContentId Id, int Value)> values) =>
        [.. values.OrderBy(value => value.Id).Select(value => new ValueDto { Id = value.Id.ToString(), Value = value.Value })];
}
