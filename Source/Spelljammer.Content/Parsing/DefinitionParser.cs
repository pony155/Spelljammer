using System.Collections.Immutable;
using System.Text.Json;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Parsing;

internal static class DefinitionParser
{
    private static readonly string[] CommonRequired = ["schemaVersion", "revision", "id", "nameKey", "descriptionKey"];

    private sealed record KindSchema(string[] Required, string[] Optional);

    private static readonly IReadOnlyDictionary<DefinitionKind, KindSchema> KindSchemas =
        new Dictionary<DefinitionKind, KindSchema>
        {
            [DefinitionKind.Attribute] = new(["minimum", "maximum", "defaultValue", "tags"], []),
            [DefinitionKind.Skill] = new(["minimum", "maximum", "progressionCurveId", "actionTags"], []),
            [DefinitionKind.Access] = new(["tags"], []),
            [DefinitionKind.Background] = new(["compatibleRaceIds", "attributeBonusIds", "focusSkillIds"], []),
            [DefinitionKind.Character] = new(
                ["raceId", "heritageId", "backgroundId", "scenarioIds", "positionId", "languageIds", "scriptIds", "equipmentIds", "focusSkillIds", "resourceIds"], []),
            [DefinitionKind.Feat] = new(["trainingProjectId", "grantedAccessIds"], []),
            [DefinitionKind.Heritage] = new(["raceId", "grantedPerkIds"], []),
            [DefinitionKind.Perk] = new(["compatibleRaceIds", "grantedAccessIds", "grantedTechniqueIds"], ["grantedPerkIds", "effectIds"]),
            [DefinitionKind.Race] = new(["grantedPerkIds"], ["requiredSupportIds"]),
            [DefinitionKind.Spell] = new(
                ["requiredAccessId", "skillId", "focusResourceId", "focusCost", "rangeId", "castTimeTicks", "cooldownTicks", "targetTags", "effectIds"], []),
            [DefinitionKind.PsychicTechnique] = new(
                ["requiredAccessId", "skillId", "resistanceSkillId", "strainResourceId", "strainCost", "sustainCostPerTick", "contactModeId", "rangeId", "informationScopeId", "disciplineIds", "targetTags", "effectIds"], []),
            [DefinitionKind.Technique] = new(["requiredAccessIds", "grantedPerkIds"], []),
            [DefinitionKind.TrainingProject] = new(
                ["requiredSkillIds", "workUnits", "progressCap", "facilityId", "resourceId", "resourceCost", "safetyId", "grantedFeatIds", "grantedTechniqueIds"], []),
            [DefinitionKind.Equipment] = new(
                ["slotId", "initialStateId", "resourceId", "resourceCapacity", "actionIds", "effectIds"], []),
            [DefinitionKind.BoardCell] = new(
                ["zoneId", "q", "r", "capacity", "cover", "visibility", "atmosphereId", "gravityId", "hazardTags"], []),
            [DefinitionKind.ZoneLink] = new(["fromCellId", "toCellId", "accessId", "oneWay", "allowsRetreat"], []),
            [DefinitionKind.PersonalBoard] = new(
                ["maximumOccupants", "cellIds", "linkIds", "requiredObjectiveIds", "retreatCellIds"], []),
            [DefinitionKind.Encounter] = new(
                ["personalBoardId", "contextId", "hostileTeamId", "ancientDefenseId", "nonCombatObjectiveId", "extractionObjectiveId"], []),
            [DefinitionKind.ShipFrame] = new(["maximumHull", "baseArmor", "maximumSlots", "cargoCapacity", "mountIds"], []),
            [DefinitionKind.ShipModule] = new(
                ["slotCost", "cargoDisplacement", "maximumIntegrity", "networkId", "energyGeneration", "energyConsumption", "mountId", "primaryEffectId", "armorValue", "shieldValue", "shieldRechargeRate", "shieldEnergyConsumptionRate", "compatiblePathIds"], []),
            [DefinitionKind.ShipWeaponConfiguration] = new(
                ["networkId", "resourceId", "resourceCost", "damage", "rateOfFireTicks", "effectiveRange", "maximumRange", "reloadTicks", "damageTypeId", "areaId", "armorPenetration"], []),
        };

    private static readonly IReadOnlyDictionary<string, DefinitionKind> Directories =
        new Dictionary<string, DefinitionKind>(StringComparer.Ordinal)
        {
            ["Attributes"] = DefinitionKind.Attribute,
            ["Skills"] = DefinitionKind.Skill,
            ["Access"] = DefinitionKind.Access,
            ["Backgrounds"] = DefinitionKind.Background,
            ["Characters"] = DefinitionKind.Character,
            ["Feats"] = DefinitionKind.Feat,
            ["Heritages"] = DefinitionKind.Heritage,
            ["Perks"] = DefinitionKind.Perk,
            ["Races"] = DefinitionKind.Race,
            ["Spells"] = DefinitionKind.Spell,
            ["PsychicTechniques"] = DefinitionKind.PsychicTechnique,
            ["Techniques"] = DefinitionKind.Technique,
            ["TrainingProjects"] = DefinitionKind.TrainingProject,
            ["Equipment"] = DefinitionKind.Equipment,
            ["BoardCells"] = DefinitionKind.BoardCell,
            ["ZoneLinks"] = DefinitionKind.ZoneLink,
            ["PersonalBoards"] = DefinitionKind.PersonalBoard,
            ["Encounters"] = DefinitionKind.Encounter,
            ["ShipFrames"] = DefinitionKind.ShipFrame,
            ["ShipModules"] = DefinitionKind.ShipModule,
            ["ShipWeaponConfigurations"] = DefinitionKind.ShipWeaponConfiguration,
        };

    public static bool TryGetKind(string pathUnderRoot, out DefinitionKind kind)
    {
        int separator = pathUnderRoot.IndexOf('/');
        if (separator <= 0 || separator == pathUnderRoot.Length - 1)
        {
            kind = default;
            return false;
        }

        return Directories.TryGetValue(pathUnderRoot[..separator], out kind);
    }

    public static SourceDefinition? Parse(
        byte[] bytes,
        DefinitionKind kind,
        string packId,
        string relativePath,
        ContentLimits limits,
        DiagnosticSink diagnostics)
    {
        using JsonDocument? document = StrictJson.Parse(bytes, packId, relativePath, limits, diagnostics);
        if (document is null)
        {
            return null;
        }

        KindSchema schema = KindSchemas[kind];
        string[] kindFields = [.. schema.Required, .. schema.Optional];
        string[] required = [.. CommonRequired, .. schema.Required];
        HashSet<string> allowed = new([.. required, .. schema.Optional], StringComparer.Ordinal);
        JsonElement root = document.RootElement;
        if (!SourceValidation.ValidateProperties(root, allowed, required, diagnostics, packId, relativePath) ||
            !SourceValidation.TrySchemaVersion(root, diagnostics, packId, relativePath))
        {
            return null;
        }

        if (!SourceValidation.TryString(root, "id", out string idText) || !ContentId.TryParse(idText, out ContentId id) ||
            !SourceValidation.TryString(root, "nameKey", out string nameKey) || !SourceValidation.IsLocalizationKey(nameKey, limits) ||
            !SourceValidation.TryString(root, "descriptionKey", out string descriptionKey) || !SourceValidation.IsLocalizationKey(descriptionKey, limits))
        {
            diagnostics.Add(ContentDiagnosticCodes.IdInvalid, packId, relativePath);
            return null;
        }

        JsonElement revisionElement = root.GetProperty("revision");
        int revision = revisionElement.ValueKind == JsonValueKind.Number && revisionElement.TryGetInt32(out int parsedRevision)
            ? parsedRevision
            : 0;

        Dictionary<string, int> integers = new(StringComparer.Ordinal);
        Dictionary<string, string> strings = new(StringComparer.Ordinal);
        Dictionary<string, ImmutableArray<string>> arrays = new(StringComparer.Ordinal);
        foreach (string field in kindFields)
        {
            if (!root.TryGetProperty(field, out JsonElement value))
            {
                arrays.Add(field, []);
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                if (!value.TryGetInt32(out int number))
                {
                    diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, packId, relativePath, idText, "/" + field);
                    return null;
                }

                integers.Add(field, number);
            }
            else if (value.ValueKind == JsonValueKind.String)
            {
                strings.Add(field, value.GetString()!);
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                ImmutableArray<string>.Builder values = ImmutableArray.CreateBuilder<string>();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath, idText, "/" + field);
                        return null;
                    }

                    values.Add(item.GetString()!);
                }

                arrays.Add(field, values.ToImmutable());
            }
            else
            {
                diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath, idText, "/" + field);
                return null;
            }
        }

        foreach ((string field, string value) in strings)
        {
            if (!ContentId.IsCanonical(value))
            {
                diagnostics.Add(ContentDiagnosticCodes.IdInvalid, packId, relativePath, idText, "/" + field);
                return null;
            }
        }

        foreach ((string field, ImmutableArray<string> values) in arrays)
        {
            if (field is "tags" or "targetTags" or "hazardTags" or "effectIds")
            {
                bool invalid = field is "tags" or "targetTags" or "hazardTags"
                    ? values.Any(value => !SourceValidation.IsIdSegment(value))
                    : values.Any(value => !ContentId.IsCanonical(value));
                if (invalid)
                {
                    diagnostics.Add(ContentDiagnosticCodes.IdInvalid, packId, relativePath, idText, "/" + field);
                    return null;
                }

                continue;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (!ContentId.IsCanonical(values[index]))
                {
                    diagnostics.Add(ContentDiagnosticCodes.IdInvalid, packId, relativePath, idText, $"/{field}/{index}");
                    return null;
                }
            }
        }

        AttributeSourceDto? attribute = kind == DefinitionKind.Attribute
            ? new AttributeSourceDto(
                integers["minimum"],
                integers["maximum"],
                integers["defaultValue"],
                arrays["tags"])
            : null;
        SkillSourceDto? skill = kind == DefinitionKind.Skill
            ? new SkillSourceDto(
                integers["minimum"],
                integers["maximum"],
                new ContentId(strings["progressionCurveId"]),
                [.. arrays["actionTags"].Select(value => new ContentId(value))])
            : null;
        return new SourceDefinition(kind, id, 1, revision, nameKey, descriptionKey, packId, relativePath,
            integers, strings, arrays, attribute, skill);
    }
}
