using System.Collections.Immutable;
using System.Text;
using Spelljammer.Content.Compilation;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Galaxy;
using Spelljammer.Simulation.Ships;
using Spelljammer.Simulation.World;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Persistence;

/// <summary>
/// Validates campaign state for consistency, completeness, and compatibility with content definitions.
/// </summary>
/// <remarks>
/// This validator checks:
/// - Content fingerprints match (base content, packs, manifests)
/// - Version numbers are consistent (schema, world generator, formulas, effects)
/// - All collections stay within capacity limits (characters, ships, commands, events)
/// - No duplicate IDs exist in command history or character roster
/// - Commands are properly sorted by execution order
/// - All character references are valid and unique
/// - All equipment and scenario references exist in content
/// Code flow: A decoded or migrated candidate is traversed against the active content snapshot, the first invariant
/// failure returns a stable diagnostic and optional missing ID, and only a fully valid campaign may be published.
/// </remarks>
public static class CampaignValidator
{
    /// <summary>
    /// Validates a campaign state against a content snapshot, returning any missing definition ID.
    /// </summary>
    /// <param name="campaign">The campaign state to validate.</param>
    /// <param name="content">The content snapshot to validate against.</param>
    /// <param name="missingId">If validation fails due to missing content, contains the missing definition ID; otherwise, null.</param>
    /// <returns>True if the campaign is valid and consistent; false otherwise.</returns>
    public static bool TryValidate(CampaignState campaign, GameContentSnapshot content, out ContentId? missingId)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(content);
        missingId = null;
        World world = campaign.World;
        CampaignContentLock expectedLock = CampaignContentLock.Create(content);
        if (world.TimeDefinition is null ||
            !content.TryGetWorldTime(world.TimeDefinition.WorldTimeId, out WorldTimeDefinition? activeTimeDefinition))
        {
            missingId = world.TimeDefinition?.Id;
            return false;
        }

        if (world.Calendar is null || world.TimeScale is null || world.Clock is null ||
            !content.TryGetCalendar(world.Clock.CalendarId, out CalendarDefinition? activeCalendar) ||
            !content.TryGetTimeScale(world.Clock.TimeScaleId, out TimeScaleDefinition? activeTimeScale))
        {
            missingId = world.Calendar?.Id ?? world.TimeScale?.Id;
            return false;
        }

        if (Encoding.UTF8.GetByteCount(campaign.GameBuild) is 0 or > CampaignState.MaximumGameBuildBytes ||
            campaign.ContentLock.BaseContentRevision != expectedLock.BaseContentRevision ||
            !campaign.ContentLock.Packs.SequenceEqual(expectedLock.Packs) ||
            campaign.ContentLock.ManifestFingerprint != expectedLock.ManifestFingerprint ||
            campaign.ContentLock.EffectiveFingerprint != content.Fingerprint ||
            campaign.ContentLock.SemanticFingerprint != content.Fingerprint ||
            campaign.ContentLock.GeneratorVersion != CampaignSaveVersions.WorldGenerator ||
            campaign.ContentLock.FormulaVersion != CampaignSaveVersions.Formula ||
            campaign.ContentLock.EffectVersion != CampaignSaveVersions.Effect ||
            campaign.ContentLock.SaveSchemaVersion != CampaignSaveVersions.SaveSchema ||
            campaign.ContentLock.AppliedMigrationIds.Length > CampaignSaveLimits.MaximumCollectionEntries ||
            campaign.ContentLock.AppliedMigrationIds.Distinct().Count() != campaign.ContentLock.AppliedMigrationIds.Length ||
            world.TimeDefinition != activeTimeDefinition ||
            world.Calendar != activeCalendar || world.TimeScale != activeTimeScale ||
            world.Clock.CalendarId != world.Calendar.CalendarId ||
            world.Clock.TimeScaleId != world.TimeScale.TimeScaleId ||
            world.Clock.ElapsedWorldSeconds < 0 || world.Clock.FractionRemainder < 0 ||
            world.Clock.FractionRemainder >= world.TimeScale.SimulationTicksDenominator ||
            world.ContentFingerprint != content.Fingerprint || world.Tick < 0 ||
            (world.Galaxy is not null && !GalaxyValidator.Validate(world.Galaxy).Accepted) ||
            world.Ships.Count is 0 or > CampaignSaveLimits.MaximumShips ||
            campaign.Characters.Length is 0 or > CampaignSaveLimits.MaximumCharacters ||
            world.Commands.Length > World.MaximumCommands ||
            world.CommandHistory.Length > CampaignSaveLimits.MaximumRetainedCommands ||
            world.ScheduledActions.Length > World.MaximumSchedules ||
            world.Events.Length > CampaignSaveLimits.MaximumRetainedEvents ||
            world.ReadyUnits.Length > World.MaximumReadyUnits ||
            world.Commands.Select(value => value.Id).Distinct().Count() != world.Commands.Length ||
            world.CommandHistory.Select(value => value.Command.Id).Distinct().Count() != world.CommandHistory.Length ||
            campaign.Characters.Select(value => value.Id).Distinct().Count() != campaign.Characters.Length ||
            !world.Commands.SequenceEqual(world.Commands.OrderBy(value => value.TargetTick).ThenBy(value => value.Priority)
                .ThenBy(value => value.IssuerId).ThenBy(value => value.Sequence).ThenBy(value => value.Id)))
        {
            return false;
        }

        ImmutableHashSet<CharacterId> characterIds = campaign.Characters.Select(value => value.Id).ToImmutableHashSet();
        if (!characterIds.Contains(campaign.ProtagonistId))
        {
            return false;
        }

        CharacterState protagonist = campaign.Characters.Single(value => value.Id == campaign.ProtagonistId);
        if (!content.TryGetScenario(protagonist.ScenarioId, out ScenarioDefinition? scenario))
        {
            missingId = protagonist.ScenarioId.Value;
            return false;
        }

        if (campaign.Characters.Length > scenario!.MaximumRosterSize ||
            campaign.Characters.Any(character => character.ScenarioId != protagonist.ScenarioId))
        {
            return false;
        }

        CharacterResourceProfileDefinition? resourceProfile = null;
        if (scenario.CharacterResourceProfileId is CharacterResourceProfileId resourceProfileId &&
            !content.TryGetCharacterResourceProfile(resourceProfileId, out resourceProfile))
        {
            missingId = resourceProfileId.Value;
            return false;
        }

        foreach (CharacterState character in campaign.Characters)
        {
            if (character.ContentFingerprint != content.Fingerprint ||
                !content.TryGetCharacter(character.Id, out CharacterDefinition? template) ||
                !content.TryGetRace(character.RaceId, out _) || !content.TryGetHeritage(character.HeritageId, out _) ||
                !content.TryGetBackground(character.BackgroundId, out _) || template!.RaceId != character.RaceId ||
                template.HeritageId != character.HeritageId || template.BackgroundId != character.BackgroundId ||
                character.Resources.Count > CampaignSaveLimits.MaximumCollectionEntries ||
                character.Resources.Values.Any(value => value < 0) ||
                character.TrainingProgress.Count > CharacterCapabilities.MaximumSetEntries ||
                character.TrainingProgress.Any(value => value.Value < 0 || !content.TryGetTrainingProject(value.Key, out _)) ||
                character.Evidence.Length > CampaignSaveLimits.MaximumRetainedEvents ||
                character.Evidence.Any(value => !characterIds.Contains(value.ActorId) ||
                    !characterIds.Contains(value.TargetId) || value.Tick < 0))
            {
                missingId = content.TryGetCharacter(character.Id, out _) ? null : character.Id.Value;
                return false;
            }

            try
            {
                _ = CharacterCapabilities.Restore(character.Capabilities.Snapshot(content), content);
                character.ValidateForContent(content);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        foreach (ShipState ship in world.Ships.Values)
        {
            int expectedArmor = ship.Frame.BaseArmor + ship.Modules.Sum(value => value.Definition.ArmorValue);
            if (!content.TryGetShipFrame(ship.Frame.ShipFrameId, out _) || ship.Hull is < 0 || ship.Hull > ship.Frame.MaximumHull ||
                ship.Armor != expectedArmor || ship.Cargo is < 0 || ship.Cargo > ship.Frame.CargoCapacity ||
                ship.CollisionRadius <= 0 || ship.Modules.Length is 0 or > ShipLoadoutSystem.MaximumModules ||
                ship.Resources.Count > CampaignSaveLimits.MaximumCollectionEntries || ship.Resources.Values.Any(value => value < 0) ||
                ship.Modules.Select(value => value.InstanceId).Distinct().Count() != ship.Modules.Length ||
                ship.Modules.Select(value => value.Definition.MountId).Distinct().Count() != ship.Modules.Length ||
                ship.Modules.Sum(value => value.Definition.SlotCost) > ship.Frame.MaximumSlots ||
                ship.Modules.Sum(value => value.Definition.CargoDisplacement) > ship.Frame.CargoCapacity ||
                ship.Modules.Any(value => !value.Definition.CompatiblePathIds.Contains(ship.PathId)))
            {
                missingId = content.TryGetShipFrame(ship.Frame.ShipFrameId, out _) ? null : ship.Frame.ShipFrameId.Value;
                return false;
            }

            if (ship.Contacts.Any(value => value.Key != value.Value.ShipId || !world.Ships.ContainsKey(value.Key) ||
                    value.Value.LastObservedTick < 0 || value.Value.LastObservedTick > world.Tick))
            {
                return false;
            }

            foreach (InstalledModuleState module in ship.Modules)
            {
                if (!content.TryGetShipModule(module.Definition.ModuleId, out _) || module.Integrity < 0 ||
                    module.Integrity > module.Definition.MaximumIntegrity || module.CurrentShield < 0 ||
                    module.CurrentShield > module.Definition.ShieldValue ||
                    module.Weapon is not null && !content.TryGetShipWeaponConfiguration(module.Weapon.ShipWeaponConfigurationId, out _))
                {
                    missingId = module.Definition.ModuleId.Value;
                    return false;
                }

                if (module.Weapon is ShipWeaponConfigurationDefinition weapon &&
                    (weapon.NetworkId != module.Definition.NetworkId || !ship.Resources.ContainsKey(weapon.ResourceId)))
                {
                    return false;
                }
            }
        }

        if (world.PersonalEncounter is PersonalEncounterState encounter &&
            !ValidateEncounter(encounter, characterIds, content, resourceProfile, out missingId))
        {
            return false;
        }

        return world.ReadyUnits.Distinct().Count() == world.ReadyUnits.Length &&
            world.ReadyUnits.All(id => world.PersonalEncounter?.Units.ContainsKey(id) == true) &&
            world.ScheduledActions.All(action => action.CommitTick >= 0 && action.RecoverTick >= action.CommitTick &&
                action.History.Length is > 0 and <= 8 && world.CommandHistory.Any(entry => entry.Command.Id == action.Command.Id));
    }

    public static IEnumerable<ContentId> RequiredDefinitions(CampaignState campaign, GameContentSnapshot content)
    {
        HashSet<ContentId> ids = [];
        void Add(ContentId id)
        {
            if (content.TryGetDefinition(id, out _))
            {
                ids.Add(id);
            }
        }

        Add(campaign.CurrentLocationId);
        Add(campaign.World.TimeDefinition.Id);
        Add(campaign.World.Calendar.Id);
        Add(campaign.World.TimeScale.Id);

        foreach (CharacterState character in campaign.Characters)
        {
            Add(character.Id.Value);
            Add(character.ScenarioId.Value);
            Add(character.RaceId.Value);
            Add(character.HeritageId.Value);
            Add(character.BackgroundId.Value);
            CharacterCapabilitySnapshot capabilities = character.Capabilities.Snapshot(content);
            foreach (ContentId id in capabilities.Abilities.Select(value => value.Id.Value)
                         .Concat(capabilities.Skills.Select(value => value.Id.Value))
                         .Concat(capabilities.Feats.Select(value => value.Value))
                         .Concat(character.TrainingProgress.Keys.Select(value => value.Value))
                         .Concat(character.Items.ItemInstances.Select(value => value.DefinitionId))
                         .Concat(character.Items.InventoryEntries.Select(value => value.Stack.DefinitionId))
                         .Concat(character.Statuses.Instances.Select(value => value.DefinitionId.Value)))
            {
                Add(id);
            }
        }

        foreach (ShipState ship in campaign.World.Ships.Values)
        {
            Add(ship.Frame.ShipFrameId.Value);
            foreach (InstalledModuleState module in ship.Modules)
            {
                Add(module.Definition.ModuleId.Value);
                if (module.Weapon is not null)
                {
                    Add(module.Weapon.ShipWeaponConfigurationId.Value);
                }
            }
        }

        if (campaign.World.PersonalEncounter is PersonalEncounterState encounter)
        {
            Add(encounter.Id.Value);
            Add(encounter.Board.Definition.PersonalBoardId.Value);
            foreach (ContentId id in encounter.Board.Cells.Keys.Select(value => value.Value)
                         .Concat(encounter.Board.Links.Select(value => value.LinkId.Value))
                         .Concat(encounter.Units.Values.SelectMany(unit => unit.Items.ItemInstances.Select(item => item.DefinitionId)))
                         .Concat(encounter.Units.Values.SelectMany(unit =>
                             unit.Items.InventoryEntries.Select(entry => entry.Stack.DefinitionId)))
                         .Concat(encounter.Units.Values.SelectMany(unit =>
                             unit.Statuses.Instances.Select(status => status.DefinitionId.Value))))
            {
                Add(id);
            }
        }

        return ids.Order();
    }

    private static bool ValidateEncounter(
        PersonalEncounterState encounter,
        ImmutableHashSet<CharacterId> characterIds,
        GameContentSnapshot content,
        CharacterResourceProfileDefinition? resourceProfile,
        out ContentId? missingId)
    {
        missingId = null;
        if (!content.TryGetEncounter(encounter.Id, out EncounterDefinition? definition) ||
            definition!.PersonalBoardId != encounter.Board.Definition.PersonalBoardId ||
            encounter.Units.Count > encounter.Board.Definition.MaximumOccupants ||
            encounter.Objectives.Count > CampaignSaveLimits.MaximumCollectionEntries)
        {
            missingId = encounter.Id.Value;
            return false;
        }

        foreach (BattleUnitState unit in encounter.Units.Values)
        {
            if (!encounter.Board.Cells.ContainsKey(unit.CellId) ||
                unit.CharacterId is CharacterId characterId && !characterIds.Contains(characterId) ||
                resourceProfile is null || unit.Health < 0 ||
                unit.Injuries.Length > CampaignSaveLimits.MaximumCollectionEntries)
            {
                return false;
            }


            try
            {
                unit.CharacterResources.Validate(resourceProfile);
                unit.Turn.Validate(resourceProfile.TurnRules);
                if (!ItemSystem.Create(unit.Items, content).Accepted)
                {
                    return false;
                }

                StatusResult statuses = StatusSystem.Create(
                    unit.Statuses,
                    content,
                    new StatusSystemLimits(PersonalEncounterState.MaximumStatusesPerUnit, 1_000_000,
                        PersonalEncounterState.MaximumStatusesPerUnit));
                if (!statuses.Accepted || unit.Statuses.Instances.Any(value => value.TargetId.Value != unit.Id.Value))
                {
                    return false;
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        ImmutableArray<BattleUnitId> occupants = [.. encounter.Board.Occupants.Values.SelectMany(value => value).Order()];
        return occupants.SequenceEqual(encounter.Units.Keys.Order()) &&
            encounter.Units.Values.All(unit => encounter.Board.Occupants.GetValueOrDefault(unit.CellId, []).Contains(unit.Id)) &&
            encounter.Board.Definition.RequiredObjectiveIds.All(encounter.Objectives.ContainsKey);
    }
}
