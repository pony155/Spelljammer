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
            [DefinitionKind.Ability] = new(["minimum", "maximum", "defaultValue", "tags"], []),
            [DefinitionKind.Skill] = new(["minimum", "maximum", "progressionCurveId", "actionTags"], []),
            [DefinitionKind.LevelProgressionTable] = new(["levels"], []),
            [DefinitionKind.CharacterResourceProfile] = new(
                ["healthMaximum", "healthRecoveryRate", "staminaMaximum", "staminaRecoveryRate",
                 "manaMaximum", "manaRecoveryRate", "resolveMaximum", "resolveRecoveryRate",
                 "strainMaximum", "strainRecoveryRate", "resolveThresholdPercentages", "strainThresholdPercentages",
                 "turnMeterThreshold", "baseTurnMeterGain", "baseActionPoints", "normalTurnMeterGainPercentage",
                 "staminaTurnMeterThresholdPercentages", "staminaTurnMeterGainPercentages",
                 "actionPointCostIds", "actionPointCosts"], []),
            [DefinitionKind.Access] = new(["tags"], []),
            [DefinitionKind.Background] = new(["compatibleRaceIds", "abilityBonusIds", "focusSkillIds"], []),
            [DefinitionKind.Character] = new(
                ["raceId", "heritageId", "backgroundId", "scenarioIds", "positionId", "languageIds", "scriptIds", "equipmentIds", "focusSkillIds", "resourceIds"], []),
            [DefinitionKind.Scenario] = new(["maximumRosterSize"], ["levelProgressionTableId", "characterResourceProfileId"]),
            [DefinitionKind.Feat] = new(
                ["activation", "grantedAccessIds"],
                ["activeKind", "trainingProjectId", "compatibleRaceIds", "requiredAccessIds", "grantedFeatIds", "effectIds",
                 "skillId", "manaResourceId", "manaCost", "rangeId", "castTimeTicks", "cooldownTicks", "targetTags",
                 "resistanceSkillId", "strainResourceId", "strainCost", "sustainCostPerTick", "contactModeId",
                 "informationScopeId", "disciplineIds"]),
            [DefinitionKind.Heritage] = new(["raceId", "grantedFeatIds"], []),
            [DefinitionKind.Race] = new(["grantedFeatIds"], ["requiredSupportIds"]),
            [DefinitionKind.TrainingProject] = new(
                ["requiredSkillIds", "workUnits", "progressCap", "facilityId", "resourceId", "resourceCost", "safetyId", "grantedFeatIds"], []),
            [DefinitionKind.Equipment] = new(
                ["slotId", "initialStateId", "resourceId", "resourceCapacity", "actionIds", "effectIds"],
                ["meleeWeaponId", "rangedWeaponId"]),
            [DefinitionKind.MeleeWeapon] = new(
                ["family", "technology", "hands", "skillId", "abilityId", "damageMinimum", "damageMaximum", "armorDamagePercentage",
                 "armorPenetrationPercentage", "staminaCost", "range", "weightGrams", "maximumDurability", "value",
                 "strengthDamageScale", "traits", "actionIds"],
                ["energyCapacity", "energyPerAttack", "unpoweredDamagePercentage",
                 "unpoweredArmorPenetrationPercentage"]),
            [DefinitionKind.MeleeWeaponAction] = new(
                ["actionPointCost", "staminaCostModifier", "hitModifier", "damagePercentage",
                 "armorDamagePercentage", "armorPenetrationModifier", "rangeModifier", "energyCostModifier",
                 "durabilityCost", "effectIds"], []),
            [DefinitionKind.RangedWeapon] = new(
                ["family", "technology", "hands", "skillId", "damageMinimum", "damageMaximum",
                 "armorDamagePercentage", "armorPenetrationPercentage", "staminaCost", "optimalRange", "maximumRange",
                 "weightGrams", "maximumDurability", "value", "traits", "actionIds"],
                ["ammunitionType", "magazineCapacity", "energyCapacity", "energyPerShot", "heatCapacity", "heatPerShot"]),
            [DefinitionKind.Ammunition] = new(
                ["ammunitionType", "technology", "damagePercentage", "armorDamagePercentage",
                 "armorPenetrationModifier", "rangeModifier", "stackSize", "weightGrams", "value", "traits"], []),
            [DefinitionKind.RangedWeaponAction] = new(
                ["kind", "actionPointCost", "staminaCostModifier", "hitModifier", "damagePercentage",
                 "ammunitionCost", "shotCount", "energyCostModifier", "heatModifier", "durabilityCost",
                 "rangePenaltyPerUnit", "damageFalloffPerUnitPercentage", "reloadAmount", "effectIds"], []),
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
            ["Abilities"] = DefinitionKind.Ability,
            ["Skills"] = DefinitionKind.Skill,
            ["LevelProgressionTables"] = DefinitionKind.LevelProgressionTable,
            ["CharacterResourceProfiles"] = DefinitionKind.CharacterResourceProfile,
            ["Access"] = DefinitionKind.Access,
            ["Backgrounds"] = DefinitionKind.Background,
            ["Characters"] = DefinitionKind.Character,
            ["Scenarios"] = DefinitionKind.Scenario,
            ["Feats"] = DefinitionKind.Feat,
            ["Heritages"] = DefinitionKind.Heritage,
            ["Races"] = DefinitionKind.Race,
            ["TrainingProjects"] = DefinitionKind.TrainingProject,
            ["Equipment"] = DefinitionKind.Equipment,
            ["MeleeWeapons"] = DefinitionKind.MeleeWeapon,
            ["MeleeWeaponActions"] = DefinitionKind.MeleeWeaponAction,
            ["RangedWeapons"] = DefinitionKind.RangedWeapon,
            ["Ammunition"] = DefinitionKind.Ammunition,
            ["RangedWeaponActions"] = DefinitionKind.RangedWeaponAction,
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
        Dictionary<string, ImmutableArray<int>> integerArrays = new(StringComparer.Ordinal);
        ImmutableArray<LevelProgressionEntry> levelProgressionEntries = [];
        foreach (string field in kindFields)
        {
            if (!root.TryGetProperty(field, out JsonElement value))
            {
                arrays.Add(field, []);
                continue;
            }

            if (kind == DefinitionKind.LevelProgressionTable && field == "levels")
            {
                if (!TryParseLevelProgressionEntries(value, packId, relativePath, idText, diagnostics, out levelProgressionEntries))
                {
                    return null;
                }
            }
            else if (value.ValueKind == JsonValueKind.Number)
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
                JsonElement[] items = [.. value.EnumerateArray()];
                if (items.Length == 0)
                {
                    if (field.EndsWith("ThresholdPercentages", StringComparison.Ordinal) ||
                        field is "staminaTurnMeterGainPercentages" or "actionPointCosts")
                    {
                        integerArrays.Add(field, []);
                    }
                    else
                    {
                        arrays.Add(field, []);
                    }
                }
                else if (items.All(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out _)))
                {
                    integerArrays.Add(field, [.. items.Select(item => item.GetInt32())]);
                }
                else if (items.All(item => item.ValueKind == JsonValueKind.String))
                {
                    arrays.Add(field, [.. items.Select(item => item.GetString()!)]);
                }
                else
                {
                    diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath, idText, "/" + field);
                    return null;
                }
            }
            else
            {
                diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath, idText, "/" + field);
                return null;
            }
        }

        foreach ((string field, string value) in strings)
        {
            bool valid = field is "activation" or "activeKind" or "family" or "technology" or "hands" or
                "ammunitionType" or "kind"
                ? SourceValidation.IsIdSegment(value)
                : ContentId.IsCanonical(value);
            if (!valid)
            {
                diagnostics.Add(ContentDiagnosticCodes.IdInvalid, packId, relativePath, idText, "/" + field);
                return null;
            }
        }

        foreach ((string field, ImmutableArray<string> values) in arrays)
        {
            if (field is "tags" or "targetTags" or "hazardTags" or "traits" or "effectIds")
            {
                bool invalid = field is "tags" or "targetTags" or "hazardTags" or "traits"
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

        AbilitySourceDto? ability = kind == DefinitionKind.Ability
            ? new AbilitySourceDto(
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
            integers, strings, arrays, integerArrays, ability, skill, levelProgressionEntries);
    }

    private static bool TryParseLevelProgressionEntries(
        JsonElement value,
        string packId,
        string relativePath,
        string definitionId,
        DiagnosticSink diagnostics,
        out ImmutableArray<LevelProgressionEntry> entries)
    {
        entries = [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath, definitionId, "/levels");
            return false;
        }

        string[] fields =
        [
            "level", "requiredExperience", "maximumHealthIncrease", "maximumManaIncrease", "maximumStaminaIncrease",
            "maximumResolveIncrease", "maximumStrainIncrease",
            "abilityPoints", "skillPoints", "featPoints"
        ];
        HashSet<string> allowed = new(fields, StringComparer.Ordinal);
        ImmutableArray<LevelProgressionEntry>.Builder builder = ImmutableArray.CreateBuilder<LevelProgressionEntry>();
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string path = $"/levels/{index}";
            if (item.ValueKind != JsonValueKind.Object ||
                !SourceValidation.ValidateProperties(item, allowed, fields, diagnostics, packId, relativePath))
            {
                diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath, definitionId, path);
                return false;
            }

            int[] numbers = new int[fields.Length];
            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                JsonElement property = item.GetProperty(fields[fieldIndex]);
                if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out numbers[fieldIndex]))
                {
                    diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, packId, relativePath, definitionId,
                        $"{path}/{fields[fieldIndex]}");
                    return false;
                }
            }

            builder.Add(new LevelProgressionEntry(
                numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5], numbers[6], numbers[7], numbers[8], numbers[9]));
            index++;
        }

        entries = builder.ToImmutable();
        return true;
    }
}
