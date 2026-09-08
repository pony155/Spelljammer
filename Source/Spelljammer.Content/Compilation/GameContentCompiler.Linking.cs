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

/// <summary>
/// Resolves cross-definition references and validates compatible content kinds.
/// </summary>
/// <remarks>
/// Code flow: Definitions are indexed by stable ID, each reference is resolved in deterministic order, missing or mismatched targets add diagnostics, and only fully linked sources proceed.
/// </remarks>
public sealed partial class GameContentCompiler
{
    private bool Link(IReadOnlyList<SourceDefinition> definitions, DiagnosticSink diagnostics)
    {
        Dictionary<string, SourceDefinition> byId = definitions.ToDictionary(value => value.Id.ToString(), StringComparer.Ordinal);
        foreach (SourceDefinition definition in definitions.OrderBy(value => value.Id))
        {
            switch (definition.Kind)
            {
                case DefinitionKind.Skill:
                    CheckPrimitive(definition, definition.Strings["progressionCurveId"], "/progressionCurveId", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["actionTags"], "/actionTags", diagnostics);
                    break;
                case DefinitionKind.Scenario:
                    if (definition.Strings.TryGetValue("levelProgressionTableId", out string? levelProgressionTableId))
                    {
                        CheckReference(definition, levelProgressionTableId, DefinitionKind.LevelProgressionTable, byId,
                            "/levelProgressionTableId", diagnostics);
                    }

                    if (definition.Strings.TryGetValue("characterResourceProfileId", out string? characterResourceProfileId))
                    {
                        CheckReference(definition, characterResourceProfileId, DefinitionKind.CharacterResourceProfile, byId,
                            "/characterResourceProfileId", diagnostics);
                    }

                    break;
                case DefinitionKind.Feat:
                    if (definition.Strings.TryGetValue("trainingProjectId", out string? trainingProjectId))
                    {
                        CheckReference(definition, trainingProjectId, DefinitionKind.TrainingProject, byId, "/trainingProjectId", diagnostics);
                    }

                    CheckReferences(definition, definition.Arrays["compatibleRaceIds"], DefinitionKind.Race, byId, "/compatibleRaceIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["requiredAccessIds"], DefinitionKind.Access, byId, "/requiredAccessIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["grantedAccessIds"], DefinitionKind.Access, byId, "/grantedAccessIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["grantedFeatIds"], DefinitionKind.Feat, byId, "/grantedFeatIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["effectIds"], DefinitionKind.Effect, byId,
                        "/effectIds", diagnostics);
                    if (definition.Strings.TryGetValue("skillId", out string? featSkillId))
                    {
                        CheckReference(definition, featSkillId, DefinitionKind.Skill, byId, "/skillId", diagnostics);
                    }

                    if (definition.Strings.TryGetValue("resistanceSkillId", out string? resistanceSkillId))
                    {
                        CheckReference(definition, resistanceSkillId, DefinitionKind.Skill, byId, "/resistanceSkillId", diagnostics);
                    }

                    foreach (string field in new[] { "manaResourceId", "strainResourceId", "contactModeId", "rangeId", "informationScopeId" })
                    {
                        if (definition.Strings.TryGetValue(field, out string? primitive))
                        {
                            CheckPrimitive(definition, primitive, "/" + field, diagnostics);
                        }
                    }

                    CheckPrimitives(definition, definition.Arrays["disciplineIds"], "/disciplineIds", diagnostics);

                    break;
                case DefinitionKind.Background:
                    CheckReferences(definition, definition.Arrays["compatibleRaceIds"], DefinitionKind.Race, byId, "/compatibleRaceIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["abilityBonusIds"], DefinitionKind.Ability, byId, "/abilityBonusIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["focusSkillIds"], DefinitionKind.Skill, byId, "/focusSkillIds", diagnostics);
                    break;
                case DefinitionKind.Character:
                    CheckReference(definition, definition.Strings["raceId"], DefinitionKind.Race, byId, "/raceId", diagnostics);
                    CheckReference(definition, definition.Strings["heritageId"], DefinitionKind.Heritage, byId, "/heritageId", diagnostics);
                    CheckReference(definition, definition.Strings["backgroundId"], DefinitionKind.Background, byId, "/backgroundId", diagnostics);
                    CheckReferences(definition, definition.Arrays["scenarioIds"], DefinitionKind.Scenario, byId, "/scenarioIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["focusSkillIds"], DefinitionKind.Skill, byId, "/focusSkillIds", diagnostics);
                    CheckItemReferences(definition, definition.Arrays["startingItemDefinitionIds"], byId,
                        "/startingItemDefinitionIds", diagnostics);
                    break;
                case DefinitionKind.Heritage:
                    CheckReference(definition, definition.Strings["raceId"], DefinitionKind.Race, byId, "/raceId", diagnostics);
                    CheckReferences(definition, definition.Arrays["grantedFeatIds"], DefinitionKind.Feat, byId, "/grantedFeatIds", diagnostics);
                    break;
                case DefinitionKind.Race:
                    CheckReferences(definition, definition.Arrays["grantedFeatIds"], DefinitionKind.Feat, byId, "/grantedFeatIds", diagnostics);
                    break;
                case DefinitionKind.TrainingProject:
                    CheckReferences(definition, definition.Arrays["requiredSkillIds"], DefinitionKind.Skill, byId, "/requiredSkillIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["grantedFeatIds"], DefinitionKind.Feat, byId, "/grantedFeatIds", diagnostics);
                    CheckPrimitive(definition, definition.Strings["facilityId"], "/facilityId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["resourceId"], "/resourceId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["safetyId"], "/safetyId", diagnostics);
                    break;
                case DefinitionKind.Equipment:
                    CheckPrimitives(definition, definition.Arrays["occupiedSlotIds"], "/occupiedSlotIds", diagnostics);
                    CheckPrimitives(definition, OptionalArray(definition, "actionIds"), "/actionIds", diagnostics);
                    CheckReferences(definition, OptionalArray(definition, "effectIds"), DefinitionKind.Effect, byId,
                        "/effectIds", diagnostics);
                    CheckPrimitives(definition, OptionalArray(definition, "resistanceIds"), "/resistanceIds", diagnostics);
                    break;
                case DefinitionKind.MeleeWeapon:
                    CheckReference(definition, definition.Strings["skillId"], DefinitionKind.Skill, byId, "/skillId", diagnostics);
                    CheckReference(definition, definition.Strings["abilityId"], DefinitionKind.Ability, byId, "/abilityId", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["occupiedSlotIds"], "/occupiedSlotIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["actionIds"], DefinitionKind.MeleeWeaponAction, byId, "/actionIds", diagnostics);
                    break;
                case DefinitionKind.MeleeWeaponAction:
                    CheckReferences(definition, definition.Arrays["effectIds"], DefinitionKind.Effect, byId,
                        "/effectIds", diagnostics);
                    break;
                case DefinitionKind.RangedWeapon:
                    CheckReference(definition, definition.Strings["skillId"], DefinitionKind.Skill, byId, "/skillId", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["occupiedSlotIds"], "/occupiedSlotIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["actionIds"], DefinitionKind.RangedWeaponAction, byId, "/actionIds", diagnostics);
                    break;
                case DefinitionKind.RangedWeaponAction:
                    CheckReferences(definition, definition.Arrays["effectIds"], DefinitionKind.Effect, byId,
                        "/effectIds", diagnostics);
                    break;
                case DefinitionKind.Effect:
                    if (definition.Strings.TryGetValue("statusId", out string? effectStatusId))
                    {
                        CheckReference(definition, effectStatusId, DefinitionKind.Status, byId, "/statusId", diagnostics);
                    }

                    break;
                case DefinitionKind.Status:
                    CheckReferences(definition, definition.Arrays["onApplyEffectIds"], DefinitionKind.Effect, byId,
                        "/onApplyEffectIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["onTickEffectIds"], DefinitionKind.Effect, byId,
                        "/onTickEffectIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["onExpireEffectIds"], DefinitionKind.Effect, byId,
                        "/onExpireEffectIds", diagnostics);
                    break;
                case DefinitionKind.BoardCell:
                    CheckPrimitive(definition, definition.Strings["zoneId"], "/zoneId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["atmosphereId"], "/atmosphereId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["gravityId"], "/gravityId", diagnostics);
                    break;
                case DefinitionKind.ZoneLink:
                    CheckReference(definition, definition.Strings["fromCellId"], DefinitionKind.BoardCell, byId, "/fromCellId", diagnostics);
                    CheckReference(definition, definition.Strings["toCellId"], DefinitionKind.BoardCell, byId, "/toCellId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["accessId"], "/accessId", diagnostics);
                    break;
                case DefinitionKind.PersonalBoard:
                    CheckReferences(definition, definition.Arrays["cellIds"], DefinitionKind.BoardCell, byId, "/cellIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["linkIds"], DefinitionKind.ZoneLink, byId, "/linkIds", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["requiredObjectiveIds"], "/requiredObjectiveIds", diagnostics);
                    CheckReferences(definition, definition.Arrays["retreatCellIds"], DefinitionKind.BoardCell, byId, "/retreatCellIds", diagnostics);
                    break;
                case DefinitionKind.Encounter:
                    CheckReference(definition, definition.Strings["personalBoardId"], DefinitionKind.PersonalBoard, byId, "/personalBoardId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["contextId"], "/contextId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["hostileTeamId"], "/hostileTeamId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["ancientDefenseId"], "/ancientDefenseId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["nonCombatObjectiveId"], "/nonCombatObjectiveId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["extractionObjectiveId"], "/extractionObjectiveId", diagnostics);
                    break;
                case DefinitionKind.ShipFrame:
                    CheckPrimitives(definition, definition.Arrays["mountIds"], "/mountIds", diagnostics);
                    break;
                case DefinitionKind.ShipModule:
                    CheckPrimitive(definition, definition.Strings["networkId"], "/networkId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["mountId"], "/mountId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["primaryEffectId"], "/primaryEffectId", diagnostics);
                    CheckPrimitives(definition, definition.Arrays["compatiblePathIds"], "/compatiblePathIds", diagnostics);
                    break;
                case DefinitionKind.ShipWeaponConfiguration:
                    CheckPrimitive(definition, definition.Strings["networkId"], "/networkId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["resourceId"], "/resourceId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["damageTypeId"], "/damageTypeId", diagnostics);
                    CheckPrimitive(definition, definition.Strings["areaId"], "/areaId", diagnostics);
                    break;
            }
        }


        ValidateGrantCycles(definitions, diagnostics);

        return !diagnostics.HasErrors;
    }
}
