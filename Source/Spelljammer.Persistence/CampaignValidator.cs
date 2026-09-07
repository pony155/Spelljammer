using System.Collections.Immutable;
using System.Text;
using Spelljammer.Content.Compilation;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

namespace Spelljammer.Persistence;

public static class CampaignValidator
{
    public static bool TryValidate(CampaignState campaign, GameContentSnapshot content, out ContentId? missingId)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(content);
        missingId = null;
        VoyageWorld world = campaign.Voyage;
        CampaignContentLock expectedLock = CampaignContentLock.Create(content);
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
            world.ContentFingerprint != content.Fingerprint || world.Tick < 0 ||
            world.Ships.Count is 0 or > CampaignSaveLimits.MaximumShips ||
            campaign.Characters.Length > CampaignSaveLimits.MaximumCharacters ||
            world.Commands.Length > VoyageWorld.MaximumCommands ||
            world.CommandHistory.Length > CampaignSaveLimits.MaximumRetainedCommands ||
            world.ScheduledActions.Length > VoyageWorld.MaximumSchedules ||
            world.Events.Length > CampaignSaveLimits.MaximumRetainedEvents ||
            world.ReadyActors.Length > VoyageWorld.MaximumReadyActors ||
            world.Commands.Select(value => value.Id).Distinct().Count() != world.Commands.Length ||
            world.CommandHistory.Select(value => value.Command.Id).Distinct().Count() != world.CommandHistory.Length ||
            campaign.Characters.Select(value => value.Id).Distinct().Count() != campaign.Characters.Length ||
            !world.Commands.SequenceEqual(world.Commands.OrderBy(value => value.TargetTick).ThenBy(value => value.Priority)
                .ThenBy(value => value.IssuerId).ThenBy(value => value.Sequence).ThenBy(value => value.Id)))
        {
            return false;
        }

        ImmutableHashSet<CharacterId> characterIds = campaign.Characters.Select(value => value.Id).ToImmutableHashSet();
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
                character.ActiveEffects.Length > CampaignSaveLimits.MaximumCollectionEntries ||
                character.Evidence.Length > CampaignSaveLimits.MaximumRetainedEvents ||
                character.ActiveEffects.Any(value => !characterIds.Contains(value.ActorId) ||
                    !characterIds.Contains(value.TargetId) || value.EndTick < value.StartTick) ||
                character.Evidence.Any(value => !characterIds.Contains(value.ActorId) ||
                    !characterIds.Contains(value.TargetId) || value.Tick < 0))
            {
                missingId = content.TryGetCharacter(character.Id, out _) ? null : character.Id.Value;
                return false;
            }

            try
            {
                _ = CharacterCapabilities.Restore(character.Capabilities.Snapshot(content), content);
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
            !ValidateEncounter(encounter, characterIds, content, out missingId))
        {
            return false;
        }

        return world.ReadyActors.Distinct().Count() == world.ReadyActors.Length &&
            world.ReadyActors.All(id => world.PersonalEncounter?.Actors.ContainsKey(id) == true) &&
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

        foreach (CharacterState character in campaign.Characters)
        {
            Add(character.Id.Value);
            Add(character.RaceId.Value);
            Add(character.HeritageId.Value);
            Add(character.BackgroundId.Value);
            CharacterCapabilitySnapshot capabilities = character.Capabilities.Snapshot(content);
            foreach (ContentId id in capabilities.Attributes.Select(value => value.Id.Value)
                         .Concat(capabilities.Skills.Select(value => value.Id.Value))
                         .Concat(capabilities.Feats.Select(value => value.Value))
                         .Concat(capabilities.Perks.Select(value => value.Value))
                         .Concat(capabilities.Techniques.Select(value => value.Value))
                         .Concat(character.TrainingProgress.Keys.Select(value => value.Value))
                         .Concat(character.EquipmentIds))
            {
                Add(id);
            }
        }

        foreach (ShipState ship in campaign.Voyage.Ships.Values)
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

        if (campaign.Voyage.PersonalEncounter is PersonalEncounterState encounter)
        {
            Add(encounter.Id.Value);
            Add(encounter.Board.Definition.PersonalBoardId.Value);
            foreach (ContentId id in encounter.Board.Cells.Keys.Select(value => value.Value)
                         .Concat(encounter.Board.Links.Select(value => value.LinkId.Value))
                         .Concat(encounter.Actors.Values.SelectMany(actor => actor.Loadout.Slots.Values.Select(item => item.Id.Value))))
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
        out ContentId? missingId)
    {
        missingId = null;
        if (!content.TryGetEncounter(encounter.Id, out EncounterDefinition? definition) ||
            definition!.PersonalBoardId != encounter.Board.Definition.PersonalBoardId ||
            encounter.Actors.Count > encounter.Board.Definition.MaximumOccupants ||
            encounter.Objectives.Count > CampaignSaveLimits.MaximumCollectionEntries ||
            encounter.ActiveEffects.Length > PersonalEncounterState.MaximumActiveEffects)
        {
            missingId = encounter.Id.Value;
            return false;
        }

        foreach (PersonalActorState actor in encounter.Actors.Values)
        {
            if (!encounter.Board.Cells.ContainsKey(actor.CellId) ||
                actor.CharacterId is CharacterId characterId && !characterIds.Contains(characterId) ||
                actor.TurnMeter is < 0 or > VoyageTime.TurnMeterThreshold ||
                actor.ActionPoints is < 0 or > VoyageTime.ActionPointsPerActivation || actor.Health < 0 ||
                actor.Loadout.Slots.Count > PersonalLoadout.MaximumSlots || actor.Injuries.Length > CampaignSaveLimits.MaximumCollectionEntries)
            {
                return false;
            }

            foreach (EquipmentState item in actor.Loadout.Slots.Values)
            {
                if (!content.TryGetEquipment(item.Id, out EquipmentDefinition? itemDefinition) ||
                    itemDefinition!.SlotId != item.SlotId || item.ResourceRemaining < 0 ||
                    item.ResourceRemaining > itemDefinition.ResourceCapacity)
                {
                    missingId = item.Id.Value;
                    return false;
                }
            }
        }

        ImmutableArray<ActorId> occupants = [.. encounter.Board.Occupants.Values.SelectMany(value => value).Order()];
        return occupants.SequenceEqual(encounter.Actors.Keys.Order()) &&
            encounter.Actors.Values.All(actor => encounter.Board.Occupants.GetValueOrDefault(actor.CellId, []).Contains(actor.Id)) &&
            encounter.Board.Definition.RequiredObjectiveIds.All(encounter.Objectives.ContainsKey) &&
            encounter.ActiveEffects.All(effect => encounter.Actors.ContainsKey(effect.TargetId) && effect.Stacks is >= 1 and <= 16);
    }
}
