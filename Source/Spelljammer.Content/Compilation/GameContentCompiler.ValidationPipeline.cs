using System.Collections.Immutable;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Parsing;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;
using EquipmentDefinition = Spelljammer.Simulation.Items.EquipmentDefinition;
using ArmorDefinition = Spelljammer.Simulation.Items.ArmorDefinition;
using GearDefinition = Spelljammer.Simulation.Items.GearDefinition;
using AmmunitionDefinition = Spelljammer.Simulation.Items.AmmunitionDefinition;

namespace Spelljammer.Content.Compilation;

public sealed partial class GameContentCompiler
{
    private bool Validate(IReadOnlyList<SourceDefinition> definitions, DiagnosticSink diagnostics)
    {
        IOrderedEnumerable<SourceDefinition> ordered = definitions.OrderBy(value => value.Id);
        Dictionary<string, SourceDefinition> byId = definitions.ToDictionary(value => value.Id.ToString(), StringComparer.Ordinal);
        foreach (SourceDefinition definition in ordered)
        {
            if (definition.Revision < 1)
            {
                OutOfRange(definition, "/revision", diagnostics);
            }

            switch (definition.Kind)
            {
                case DefinitionKind.WorldTime when
                    definition.Integers["ticksPerSecond"] is < 1 or > 1_000 ||
                    definition.Integers["maximumCatchUpTicks"] is < 1 or > 100_000:
                    OutOfRange(definition, "/ticksPerSecond", diagnostics);
                    break;
                case DefinitionKind.Calendar:
                    ValidateCalendar(definition, diagnostics);
                    break;
                case DefinitionKind.TimeScale when
                    definition.Integers["worldSecondsNumerator"] is < 1 or > 1_000_000_000 ||
                    definition.Integers["simulationTicksDenominator"] is < 1 or > 1_000_000:
                    OutOfRange(definition, "/worldSecondsNumerator", diagnostics);
                    break;
                case DefinitionKind.Ability:
                    ValidateAbility(definition, diagnostics);
                    break;
                case DefinitionKind.Skill:
                    ValidateSkill(definition, diagnostics);
                    break;
                case DefinitionKind.LevelProgressionTable:
                    ValidateLevelProgressionTable(definition, diagnostics);
                    break;
                case DefinitionKind.CharacterResourceProfile:
                    ValidateCharacterResourceProfile(definition, diagnostics);
                    break;
                case DefinitionKind.Feat:
                    ValidateFeat(definition, diagnostics);
                    break;
                case DefinitionKind.Scenario when definition.Integers["maximumRosterSize"] is < 1 or > CharacterCapabilities.MaximumSetEntries:
                    OutOfRange(definition, "/maximumRosterSize", diagnostics);
                    break;
                case DefinitionKind.TrainingProject when definition.Integers["workUnits"] is < 1 or > 1_000_000:
                    OutOfRange(definition, "/workUnits", diagnostics);
                    break;
                case DefinitionKind.TrainingProject when definition.Integers["progressCap"] < definition.Integers["workUnits"] ||
                    definition.Integers["progressCap"] > 1_000_000:
                    OutOfRange(definition, "/progressCap", diagnostics);
                    break;
                case DefinitionKind.TrainingProject when definition.Integers["resourceCost"] is < 0 or > 1_000_000:
                    OutOfRange(definition, "/resourceCost", diagnostics);
                    break;
                case DefinitionKind.Equipment:
                    ValidateEquipment(definition, diagnostics);
                    break;
                case DefinitionKind.MeleeWeapon:
                    ValidateMeleeWeapon(definition, diagnostics);
                    break;
                case DefinitionKind.MeleeWeaponAction:
                    ValidateMeleeWeaponAction(definition, diagnostics);
                    break;
                case DefinitionKind.RangedWeapon:
                    ValidateRangedWeapon(definition, diagnostics);
                    break;
                case DefinitionKind.Ammunition:
                    ValidateAmmunition(definition, diagnostics);
                    break;
                case DefinitionKind.RangedWeaponAction:
                    ValidateRangedWeaponAction(definition, diagnostics);
                    break;
                case DefinitionKind.Effect:
                    ValidateEffect(definition, diagnostics);
                    break;
                case DefinitionKind.Status:
                    ValidateStatus(definition, diagnostics);
                    break;
                case DefinitionKind.BoardCell when definition.Integers["q"] is < -1_024 or > 1_024 ||
                    definition.Integers["r"] is < -1_024 or > 1_024 || definition.Integers["capacity"] is < 1 or > 8 ||
                    definition.Integers["cover"] is < 0 or > 100 || definition.Integers["visibility"] is < 0 or > 100:
                    OutOfRange(definition, "/capacity", diagnostics);
                    break;
                case DefinitionKind.ZoneLink when definition.Integers["oneWay"] is < 0 or > 1 ||
                    definition.Integers["allowsRetreat"] is < 0 or > 1:
                    OutOfRange(definition, "/oneWay", diagnostics);
                    break;
                case DefinitionKind.PersonalBoard when definition.Integers["maximumOccupants"] is < 1 or > 256:
                    OutOfRange(definition, "/maximumOccupants", diagnostics);
                    break;
                case DefinitionKind.ShipFrame when definition.Integers["maximumHull"] is < 1 or > 1_000_000 ||
                    definition.Integers["baseArmor"] is < 0 or > 100_000 || definition.Integers["maximumSlots"] is < 1 or > 256 ||
                    definition.Integers["cargoCapacity"] is < 0 or > 1_000_000:
                    OutOfRange(definition, "/maximumHull", diagnostics);
                    break;
                case DefinitionKind.ShipModule when definition.Integers["slotCost"] is < 1 or > 256 ||
                    definition.Integers["cargoDisplacement"] is < 0 or > 1_000_000 ||
                    definition.Integers["maximumIntegrity"] is < 1 or > 1_000_000 ||
                    definition.Integers["energyGeneration"] is < 0 or > 1_000_000 ||
                    definition.Integers["energyConsumption"] is < 0 or > 1_000_000 ||
                    definition.Integers["armorValue"] is < 0 or > 100_000 ||
                    definition.Integers["shieldValue"] is < 0 or > 1_000_000 ||
                    definition.Integers["shieldRechargeRate"] is < 0 or > 1_000_000 ||
                    definition.Integers["shieldEnergyConsumptionRate"] is < 0 or > 1_000_000:
                    OutOfRange(definition, "/slotCost", diagnostics);
                    break;
                case DefinitionKind.ShipWeaponConfiguration when definition.Integers["resourceCost"] is < 1 or > 1_000_000 ||
                    definition.Integers["damage"] is < 1 or > 1_000_000 ||
                    definition.Integers["rateOfFireTicks"] is < 1 or > 1_000_000 ||
                    definition.Integers["effectiveRange"] is < 1 or > 1_000_000_000 ||
                    definition.Integers["maximumRange"] < definition.Integers["effectiveRange"] ||
                    definition.Integers["maximumRange"] > 1_000_000_000 ||
                    definition.Integers["reloadTicks"] is < 1 or > 1_000_000 ||
                    definition.Integers["armorPenetration"] is < 0 or > 100_000:
                    OutOfRange(definition, "/damage", diagnostics);
                    break;
            }
        }

        int totalReferences = 0;
        foreach (SourceDefinition definition in ordered)
        {
            int definitionReferences = 0;
            foreach ((string field, ImmutableArray<string> values) in definition.Arrays)
            {
                if (values.Length != values.Distinct(StringComparer.Ordinal).Count())
                {
                    diagnostics.Add(ContentDiagnosticCodes.CollectionDuplicate, definition.PackId, definition.RelativePath,
                        definition.Id.ToString(), "/" + field);
                }

                if (field is not ("tags" or "targetTags" or "hazardTags" or "coverageTags" or "traits"))
                {
                    totalReferences = checked(totalReferences + values.Length);
                    definitionReferences = checked(definitionReferences + values.Length);
                }
            }

            totalReferences = checked(totalReferences + definition.Strings.Count);
            definitionReferences = checked(definitionReferences + definition.Strings.Count);

            switch (definition.Kind)
            {
                case DefinitionKind.Background:
                    RequireNonempty(definition, "compatibleRaceIds", diagnostics);
                    RequireNonempty(definition, "focusSkillIds", diagnostics);
                    break;
                case DefinitionKind.Character:
                    ValidateCharacter(definition, byId, diagnostics);
                    RequireNonempty(definition, "scenarioIds", diagnostics);
                    RequireNonempty(definition, "languageIds", diagnostics);
                    RequireNonempty(definition, "scriptIds", diagnostics);
                    RequireNonempty(definition, "startingItemDefinitionIds", diagnostics);
                    RequireNonempty(definition, "focusSkillIds", diagnostics);
                    RequireNonempty(definition, "resourceIds", diagnostics);
                    break;
                case DefinitionKind.Heritage:
                    ValidateHeritage(definition, byId, diagnostics);
                    RequireNonempty(definition, "grantedFeatIds", diagnostics);
                    break;
                case DefinitionKind.Race:
                    ValidateRace(definition, byId, diagnostics);
                    RequireNonempty(definition, "grantedFeatIds", diagnostics);
                    break;
                case DefinitionKind.TrainingProject:
                    RequireNonempty(definition, "requiredSkillIds", diagnostics);
                    if (definition.Arrays["grantedFeatIds"].IsEmpty)
                    {
                        diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                            definition.Id.ToString(), "/grantedFeatIds");
                    }
                    break;
                case DefinitionKind.Equipment:
                    RequireNonempty(definition, "occupiedSlotIds", diagnostics);
                    break;
                case DefinitionKind.MeleeWeapon:
                    RequireNonempty(definition, "occupiedSlotIds", diagnostics);
                    RequireNonempty(definition, "actionIds", diagnostics);
                    break;
                case DefinitionKind.RangedWeapon:
                    RequireNonempty(definition, "occupiedSlotIds", diagnostics);
                    RequireNonempty(definition, "actionIds", diagnostics);
                    break;
                case DefinitionKind.ZoneLink:
                    if (definition.Strings["fromCellId"] == definition.Strings["toCellId"])
                    {
                        diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                            definition.Id.ToString(), "/toCellId");
                    }
                    break;
                case DefinitionKind.PersonalBoard:
                    RequireNonempty(definition, "cellIds", diagnostics);
                    RequireNonempty(definition, "linkIds", diagnostics);
                    RequireNonempty(definition, "requiredObjectiveIds", diagnostics);
                    RequireNonempty(definition, "retreatCellIds", diagnostics);
                    ValidateBoard(definition, byId, diagnostics);
                    break;
                case DefinitionKind.ShipFrame:
                    RequireNonempty(definition, "mountIds", diagnostics);
                    break;
                case DefinitionKind.ShipModule:
                    RequireNonempty(definition, "compatiblePathIds", diagnostics);
                    break;
            }
        }

        foreach (SourceDefinition definition in ordered)
        {
            foreach ((string field, ImmutableArray<string> values) in definition.Arrays)
            {
                bool isTagField = field is "tags" or "targetTags" or "hazardTags" or "coverageTags" or "traits";
                int maximum = isTagField ? limits.TagsPerDefinition : limits.ReferencesPerDefinition;
                if (values.Length > maximum)
                {
                    diagnostics.Limit(isTagField ? "tags-per-definition" : "references-per-definition",
                        definition.PackId, definition.RelativePath);
                }
            }

            int definitionReferences = definition.Arrays
                .Where(pair => pair.Key is not ("tags" or "targetTags" or "hazardTags" or "coverageTags" or "traits"))
                .Sum(pair => pair.Value.Length);
            definitionReferences += definition.Strings.Count;

            if (definitionReferences > limits.ReferencesPerDefinition)
            {
                diagnostics.Limit("references-per-definition", definition.PackId, definition.RelativePath);
            }
        }

        if (totalReferences > limits.ReferencesPerSet)
        {
            diagnostics.Limit("references-per-content-set");
        }

        if (totalReferences > limits.GraphEdges)
        {
            diagnostics.Limit("graph-edges");
        }

        SourceDefinition[] worldTimes = [.. ordered.Where(value => value.Kind == DefinitionKind.WorldTime)];
        if (worldTimes.Length > 1)
        {
            SourceDefinition duplicate = worldTimes[1];
            diagnostics.Add(
                ContentDiagnosticCodes.SemanticInvalid,
                duplicate.PackId,
                duplicate.RelativePath,
                duplicate.Id.ToString(),
                "/id");
        }

        return !diagnostics.HasErrors;
    }

    private static void ValidateCalendar(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        if (definition.Integers["secondsPerMinute"] is < 1 or > 3_600 ||
            definition.Integers["minutesPerHour"] is < 1 or > 1_000 ||
            definition.Integers["hoursPerDay"] is < 1 or > 1_000 ||
            definition.Integers["daysPerWeek"] is < 1 or > 1_000 ||
            definition.Integers["startingYear"] is < -1_000_000 or > 1_000_000 ||
            definition.Calendar is not CalendarSourceDto { Months.Length: > 0 and <= 100 } calendar ||
            calendar.Months.Any(month => month.Days is < 1 or > 1_000))
        {
            OutOfRange(definition, "/months", diagnostics);
            return;
        }

        int startingMonth = definition.Integers["startingMonth"];
        if (startingMonth < 1 || startingMonth > calendar.Months.Length ||
            definition.Integers["startingDay"] < 1 ||
            definition.Integers["startingDay"] > calendar.Months[startingMonth - 1].Days ||
            definition.Integers["startingDayOfWeekIndex"] < 0 ||
            definition.Integers["startingDayOfWeekIndex"] >= definition.Integers["daysPerWeek"] ||
            definition.Integers["startingHour"] < 0 ||
            definition.Integers["startingHour"] >= definition.Integers["hoursPerDay"] ||
            definition.Integers["startingMinute"] < 0 ||
            definition.Integers["startingMinute"] >= definition.Integers["minutesPerHour"] ||
            definition.Integers["startingSecond"] < 0 ||
            definition.Integers["startingSecond"] >= definition.Integers["secondsPerMinute"])
        {
            OutOfRange(definition, "/startingMonth", diagnostics);
            return;
        }

        if (calendar.Months.Select(month => month.CalendarMonthId).Distinct().Count() != calendar.Months.Length)
        {
            diagnostics.Add(
                ContentDiagnosticCodes.CollectionDuplicate,
                definition.PackId,
                definition.RelativePath,
                definition.Id.ToString(),
                "/months");
        }
    }
}
