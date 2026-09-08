using System.Collections.Immutable;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Ships;
using Spelljammer.Simulation.World;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Persistence;

public static partial class CampaignSaveCodec
{
    private static CampaignState FromDto(
        SavePreflightDto metadata,
        CampaignContentLock savedLock,
        CampaignPayloadDto payload,
        GameContentSnapshot content,
        bool addCompatibleDefinitions)
    {
        RequireCount(payload.Characters.Length, CampaignSaveLimits.MaximumCharacters);
        RequireCount(payload.World.Ships.Length, CampaignSaveLimits.MaximumShips);
        ImmutableArray<CharacterState> characters = [.. payload.Characters
            .Select(value => FromDto(value, content, addCompatibleDefinitions)).OrderBy(value => value.Id)];
        ImmutableDictionary<ShipId, ShipState> ships = payload.World.Ships.Select(value => FromDto(value, content))
            .ToImmutableDictionary(value => value.Id);
        PersonalEncounterState? encounter = payload.World.PersonalEncounter is null
            ? null
            : FromDto(payload.World.PersonalEncounter, content);
        if (content.WorldTimes.Length != 1)
        {
            throw new InvalidOperationException("Exactly one world-time definition is required to restore a campaign.");
        }

        WorldTimeDefinition timeDefinition = content.WorldTimes[0];
        CalendarId calendarId = new(payload.World.Clock.CalendarId);
        TimeScaleId timeScaleId = new(payload.World.Clock.TimeScaleId);
        if (!content.TryGetCalendar(calendarId, out CalendarDefinition? calendar) ||
            !content.TryGetTimeScale(timeScaleId, out TimeScaleDefinition? timeScale))
        {
            throw new InvalidOperationException("Campaign calendar or time-scale definition is missing.");
        }

        CampaignClockState clock = new(
            payload.World.Clock.ElapsedWorldSeconds,
            payload.World.Clock.FractionRemainder,
            calendarId,
            timeScaleId);

        RequireCount(payload.World.Commands.Length, World.MaximumCommands);
        RequireCount(payload.World.CommandHistory.Length, World.MaximumCommandHistory);
        RequireCount(payload.World.ScheduledActions.Length, World.MaximumSchedules);
        RequireCount(payload.World.ReadyActorIds.Length, World.MaximumReadyActors);
        RequireCount(payload.World.Events.Length, World.MaximumEvents);
        World world = new(
            payload.World.Seed,
            content.Fingerprint,
            timeDefinition,
            calendar!,
            timeScale!,
            clock,
            payload.World.Tick,
            payload.World.RandomSequence,
            new TeamId(payload.World.PlayerTeamId),
            payload.World.ShipPaused,
            payload.World.PersonalPaused,
            ships,
            encounter,
            [.. payload.World.Commands.Select(FromDto)],
            [.. payload.World.CommandHistory.Select(value => new WorldCommandLogEntry(
                value.SubmittedTick, FromDto(value.Command), value.CancelledTick))],
            [.. payload.World.ScheduledActions.Select(value => new ScheduledAction(
                FromDto(value.Command),
                ParseEnum<ScheduledActionPhase>(value.Phase),
                value.CommitTick,
                value.RecoverTick,
                value.ReservedResourceId is null ? null : new ResourceId(value.ReservedResourceId),
                value.ReservedAmount,
                [.. value.History.Select(ParseEnum<ScheduledActionPhase>)]))],
            [.. payload.World.ReadyActorIds.Select(value => new ActorId(value))],
            [.. payload.World.Events.Select(value => new WorldEvent(
                new ContentId(value.Id), value.Tick, new ContentId(value.SourceId), new ContentId(value.TargetId),
                ParseEnum<WorldCommandKind>(value.Kind), value.Succeeded, value.Amount, value.ResultCode))]);

        CampaignContentLock activeLock = savedLock.EffectiveFingerprint == content.Fingerprint &&
            savedLock.SaveSchemaVersion == CampaignSaveVersions.SaveSchema
            ? savedLock
            : CampaignContentLock.Create(content, savedLock.AppliedMigrationIds);
        return new CampaignState(
            metadata.GameBuild,
            activeLock,
            new ContentId(payload.CurrentLocationId),
            world,
            new CharacterId(payload.ProtagonistId),
            characters);
    }

    private static ShipState FromDto(ShipDto value, GameContentSnapshot content)
    {
        RequireCount(value.Modules.Length, ShipLoadoutSystem.MaximumModules);
        RequireCount(value.Resources.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.Contacts.Length, CampaignSaveLimits.MaximumCollectionEntries);
        if (!content.TryGetShipFrame(new ShipFrameId(value.FrameId), out ShipFrameDefinition? frame))
        {
            throw new InvalidOperationException("Ship frame is missing.");
        }

        ImmutableArray<InstalledModuleState> modules = [.. value.Modules.Select(module =>
        {
            if (!content.TryGetShipModule(new ModuleId(module.DefinitionId), out ShipModuleDefinition? definition))
            {
                throw new InvalidOperationException("Ship module is missing.");
            }

            ShipWeaponConfigurationDefinition? weapon = null;
            if (module.WeaponConfigurationId is not null &&
                !content.TryGetShipWeaponConfiguration(new ShipWeaponConfigurationId(module.WeaponConfigurationId), out weapon))
            {
                throw new InvalidOperationException("Ship weapon configuration is missing.");
            }

            return new InstalledModuleState(
                new ContentId(module.InstanceId), definition!, ParseEnum<ModuleCondition>(module.Condition), module.Integrity,
                module.IsOn, module.IsPowered, module.ShieldRaised, module.CurrentShield, weapon,
                ParseEnum<WeaponReadiness>(module.WeaponReadiness), module.ReadyTick);
        }).OrderBy(module => module.InstanceId)];
        ImmutableDictionary<ResourceId, int> resources = ValueDictionary(value.Resources)
            .ToImmutableDictionary(pair => new ResourceId(pair.Key), pair => pair.Value);
        ImmutableDictionary<ShipId, ShipContactState> contacts = value.Contacts.Select(contact => {
            RequireCount(contact.WitnessIds.Length, CampaignSaveLimits.MaximumCharacters);
            ShipContactState state = new(
                new ShipId(contact.ShipId), new ContentId(contact.KnowledgeId), contact.LastObservedTick,
                contact.HasFiringSolution, ParseIds(contact.WitnessIds, CampaignSaveLimits.MaximumCharacters)
                    .Select(id => new ActorId(id)).ToImmutableHashSet());
            return state;
        }).ToImmutableDictionary(contact => contact.ShipId);
        return new ShipState(
            new ShipId(value.Id), new TeamId(value.TeamId), frame!, new ContentId(value.PathId), value.Hull, value.Armor,
            value.Cargo, new FixedVector2(new FixedScalar(value.PositionX), new FixedScalar(value.PositionY)),
            new FixedVector2(new FixedScalar(value.VelocityX), new FixedScalar(value.VelocityY)), value.HeadingMilliDegrees,
            value.CollisionRadius, modules, resources, contacts,
            ParseIds(value.PersistentEvidenceIds, CampaignSaveLimits.MaximumCollectionEntries).ToImmutableHashSet(),
            value.Disengaged, value.Defending);
    }

    private static CharacterState FromDto(
        CharacterDto value,
        GameContentSnapshot content,
        bool addCompatibleDefinitions)
    {
        RequireCount(value.Capabilities.Abilities.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.Capabilities.Skills.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.Capabilities.GrantSources.Length, CharacterCapabilities.MaximumSetEntries);
        RequireCount(value.Statuses.Length, CharacterState.MaximumStatuses);
        ImmutableArray<AbilityValueSnapshot> abilities =
            [.. value.Capabilities.Abilities.Select(item => new AbilityValueSnapshot(new AbilityId(item.Id), item.Value))];
        ImmutableArray<SkillValueSnapshot> skills =
            [.. value.Capabilities.Skills.Select(item => new SkillValueSnapshot(new SkillId(item.Id), item.Value))];
        if (addCompatibleDefinitions)
        {
            abilities = [.. content.Abilities.Select(definition => abilities
                .FirstOrDefault(item => item.Id == definition.AbilityId) ??
                new AbilityValueSnapshot(definition.AbilityId, (short)definition.DefaultValue))];
            skills = [.. content.Skills.Select(definition => skills
                .FirstOrDefault(item => item.Id == definition.SkillId) ??
                new SkillValueSnapshot(definition.SkillId, (byte)definition.Minimum))];
        }

        CharacterCapabilitySnapshot snapshot = new(
            content.Fingerprint,
            abilities,
            skills,
            [.. ParseIds(value.Capabilities.FeatIds, CharacterCapabilities.MaximumSetEntries).Select(id => new FeatId(id))],
            [.. value.Capabilities.GrantSources.Where(grant => grant.CapabilityId.StartsWith("access.", StringComparison.Ordinal))
                .Select(grant => new AccessId(grant.CapabilityId)).Distinct().Order()],
            [.. value.Capabilities.GrantSources.Select(grant => new CapabilityGrant(
                new ContentId(grant.CapabilityId), new ContentId(grant.SourceId), ParseEnum<GrantSourceKind>(grant.SourceKind)))]);
        CharacterCapabilities capabilities = CharacterCapabilities.Restore(snapshot, content);
        ImmutableDictionary<ContentId, int> resources = ValueDictionary(value.Resources);
        ImmutableDictionary<ContentId, int> training = ValueDictionary(value.TrainingProgress);
        ItemSystemState items = FromDto(value.Items);
        CharacterState character = new(
            new CharacterId(value.Id), content.Fingerprint, new ScenarioId(value.ScenarioId), new RaceId(value.RaceId),
            new HeritageId(value.HeritageId), new BackgroundId(value.BackgroundId), new ContentId(value.PositionId), capabilities,
            ParseIds(value.LanguageIds, CampaignSaveLimits.MaximumCollectionEntries),
            ParseIds(value.ScriptIds, CampaignSaveLimits.MaximumCollectionEntries),
            items,
            resources.ToImmutableDictionary(pair => new ResourceId(pair.Key), pair => pair.Value),
            training.ToImmutableDictionary(pair => new TrainingProjectId(pair.Key), pair => pair.Value),
            value.CanAct) {
            CharacterResources = CharacterResourceSet.Restore(value.CharacterResources.Select(FromDto)),
            Statuses = new StatusState([.. value.Statuses.Select(FromDto)]),
            Evidence = [.. value.Evidence.Select(evidence => new ObservableCapabilityEvidence(
                new ContentId(evidence.EvidenceId), new ContentId(evidence.SourceId), new CharacterId(evidence.ActorId),
                new CharacterId(evidence.TargetId), evidence.Tick, evidence.Succeeded))],
        };
        return character;
    }

    private static PersonalEncounterState FromDto(
        PersonalEncounterDto value,
        GameContentSnapshot content)
    {
        if (!content.TryGetEncounter(new EncounterId(value.Id), out EncounterDefinition? encounterDefinition) ||
            !content.TryGetPersonalBoard(new PersonalBoardId(value.BoardId), out PersonalBoardDefinition? boardDefinition) ||
            encounterDefinition!.PersonalBoardId != boardDefinition!.PersonalBoardId)
        {
            throw new InvalidOperationException("Encounter definition is missing or mismatched.");
        }

        BoardValidationResult boardResult = TacticalBoard.Create(
            boardDefinition,
            boardDefinition.CellIds.Select(id => content.TryGetBoardCell(id, out BoardCellDefinition? cell)
                ? cell! : throw new InvalidOperationException("Board cell is missing.")),
            boardDefinition.LinkIds.Select(id => content.TryGetZoneLink(id, out ZoneLinkDefinition? link)
                ? link! : throw new InvalidOperationException("Zone link is missing.")));
        if (!boardResult.Accepted)
        {
            throw new InvalidOperationException(boardResult.RejectionCode);
        }

        RequireCount(value.Actors.Length, boardDefinition.MaximumOccupants);
        TacticalBoard board = boardResult.Board!;
        ImmutableDictionary<ActorId, PersonalActorState>.Builder actors = ImmutableDictionary.CreateBuilder<ActorId, PersonalActorState>();
        foreach (PersonalActorDto actorDto in value.Actors)
        {
            RequireCount(actorDto.CharacterResources.Length, CampaignSaveLimits.MaximumCollectionEntries);
            RequireCount(actorDto.Statuses.Length, PersonalEncounterState.MaximumStatusesPerActor);
            CellId cellId = new(actorDto.CellId);
            ActorId actorId = new(actorDto.Id);
            ItemSystemState items = FromDto(actorDto.Items);

            PersonalActorState actor = new(
                actorId, new TeamId(actorDto.TeamId), actorDto.CharacterId is null ? null : new CharacterId(actorDto.CharacterId),
                cellId, FromDto(actorDto.Turn), CharacterResourceSet.Restore(actorDto.CharacterResources.Select(FromDto)), actorDto.Defending,
                actorDto.Surrendered, actorDto.Prisoner, items,
                [.. actorDto.Injuries.Select(injury => new InjuryState(
                    new ContentId(injury.Id), ParseEnum<InjurySeverity>(injury.Severity), injury.Stabilized))]) {
                ReservedReactionPoints = actorDto.ReservedReactionPoints,
                ReactionExpiresTick = actorDto.ReactionExpiresTick,
                Statuses = new StatusState([.. actorDto.Statuses.Select(FromDto)]),
            };
            board = board.Place(actorId, cellId);
            actors.Add(actorId, actor);
        }

        PersonalEncounterState encounter = new(
            encounterDefinition.EncounterId,
            board,
            actors.ToImmutable(),
            value.Objectives.ToImmutableDictionary(item => new ObjectiveId(item.Id), item => ParseEnum<ObjectiveState>(item.State)),
            ParseIds(value.ExplorationChangeIds, CampaignSaveLimits.MaximumCollectionEntries).ToImmutableHashSet(),
            ParseIds(value.DamagedObjectIds, CampaignSaveLimits.MaximumCollectionEntries).ToImmutableHashSet(),
            value.Retreated,
            value.CleanedUp);
        return encounter;
    }

    private static StatusInstanceDto ToDto(StatusInstance value) => new() {
        InstanceId = value.InstanceId.ToString(),
        DefinitionId = value.DefinitionId.ToString(),
        DefinitionRevision = value.DefinitionRevision,
        SourceId = value.SourceId.ToString(),
        TargetId = value.TargetId.ToString(),
        RemainingDuration = value.RemainingDuration,
        Stacks = value.Stacks,
        Potency = value.Potency,
    };

    private static StatusInstance FromDto(StatusInstanceDto value) => new(
        new StatusInstanceId(ParseGuid(value.InstanceId)),
        new StatusId(value.DefinitionId),
        value.DefinitionRevision,
        new ContentId(value.SourceId),
        new ContentId(value.TargetId),
        value.RemainingDuration,
        value.Stacks,
        value.Potency);

    private static ItemSystemState FromDto(ItemSystemDto value)
    {
        RequireCount(value.ItemInstances.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.InventoryEntries.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.InventoryContainers.Length, CampaignSaveLimits.MaximumCollectionEntries);
        RequireCount(value.EquipmentLoadouts.Length, CampaignSaveLimits.MaximumCollectionEntries);
        return new ItemSystemState(
            [.. value.ItemInstances.Select(item => new ItemInstance(
                new ItemInstanceId(ParseGuid(item.InstanceId)),
                new ContentId(item.DefinitionId),
                new InventoryContainerId(ParseGuid(item.OwnerContainerId)),
                item.CurrentDurability,
                item.QualityId is null ? null : new ContentId(item.QualityId),
                item.CustomName,
                item.StateVersion,
                item.MeleeWeaponState is null ? null : new MeleeWeaponState(
                    new MeleeWeaponId(item.MeleeWeaponState.WeaponId),
                    item.MeleeWeaponState.CurrentDurability,
                    item.MeleeWeaponState.CurrentEnergy),
                item.RangedWeaponState is null ? null : new RangedWeaponState(
                    new RangedWeaponId(item.RangedWeaponState.WeaponId),
                    item.RangedWeaponState.CurrentDurability,
                    item.RangedWeaponState.LoadedAmmunitionId is null ? null : new AmmunitionId(item.RangedWeaponState.LoadedAmmunitionId),
                    item.RangedWeaponState.CurrentAmmunition,
                    item.RangedWeaponState.CurrentEnergy,
                    item.RangedWeaponState.CurrentHeat)))],
            [.. value.InventoryContainers.Select(container =>
            {
                RequireCount(container.ItemInstanceIds.Length, CampaignSaveLimits.MaximumCollectionEntries);
                RequireCount(container.InventoryEntryIds.Length, CampaignSaveLimits.MaximumCollectionEntries);
                return new InventoryContainer(
                    new InventoryContainerId(ParseGuid(container.ContainerId)),
                    new ContentId(container.OwnerId),
                    container.MaximumWeightHundredthsOfPound,
                    container.MaximumEntries,
                    [.. container.ItemInstanceIds.Select(id => new ItemInstanceId(ParseGuid(id)))])
                {
                    InventoryEntryIds = [.. container.InventoryEntryIds.Select(id => new InventoryEntryId(ParseGuid(id)))],
                };
            })],
            [.. value.EquipmentLoadouts.Select(loadout =>
            {
                RequireCount(loadout.SlotAssignments.Length, CampaignSaveLimits.MaximumCollectionEntries);
                return new EquipmentLoadout(new ContentId(loadout.OwnerId),
                    [.. loadout.SlotAssignments.Select(assignment => new SlotAssignment(
                        new ContentId(assignment.SlotId), new ItemInstanceId(ParseGuid(assignment.ItemInstanceId))))]);
            })]) {
            InventoryEntries = [.. value.InventoryEntries.Select(entry => new InventoryEntry(
                new InventoryEntryId(ParseGuid(entry.EntryId)),
                new InventoryContainerId(ParseGuid(entry.OwnerContainerId)),
                new ItemStack(new ContentId(entry.DefinitionId), entry.Quantity)))],
        };
    }

    private static Guid ParseGuid(string value) => Guid.TryParse(value, out Guid result) && result != Guid.Empty
        ? result
        : throw new InvalidOperationException("Item state contains an invalid identifier.");

    private static WorldCommand FromDto(CommandDto value) => new(
        new ContentId(value.Id), ParseEnum<WorldCommandKind>(value.Kind), value.TargetTick, value.Priority,
        new ContentId(value.IssuerId), new ContentId(value.TargetId),
        new FixedVector2(new FixedScalar(value.VectorX), new FixedScalar(value.VectorY)), value.Amount,
        value.OptionId is null ? null : new ContentId(value.OptionId), value.Sequence);

    private static ImmutableDictionary<ContentId, int> ValueDictionary(ValueDto[] values)
    {
        RequireCount(values.Length, CampaignSaveLimits.MaximumCollectionEntries);
        if (values.Any(value => value.Value < 0))
        {
            throw new InvalidOperationException("Negative persistent resource value.");
        }

        return values.ToImmutableDictionary(value => new ContentId(value.Id), value => value.Value);
    }

    private static T ParseEnum<T>(int value)
        where T : struct, Enum
    {
        T parsed = (T)Enum.ToObject(typeof(T), value);
        return Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidOperationException($"Unknown {typeof(T).Name} value.");
    }

    private static void RequireCount(int count, int maximum)
    {
        if (count < 0 || count > maximum)
        {
            throw new InvalidOperationException("Persistent collection exceeds capacity.");
        }
    }
}
