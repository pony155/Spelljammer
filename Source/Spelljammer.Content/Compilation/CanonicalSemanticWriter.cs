using System.Security.Cryptography;
using System.Text;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

internal static class CanonicalSemanticWriter
{
    public static (byte[] Bytes, ContentFingerprint Fingerprint) Write(
        IReadOnlyList<ContentPackIdentity> packs,
        IReadOnlyList<ContentDefinition> definitions)
    {
        StringBuilder builder = new();
        builder.Append("{\"definitions\":[");
        bool first = true;
        foreach (ContentDefinition definition in definitions
                     .OrderBy(CanonicalKindOrder)
                     .ThenBy(value => value.Id))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteDefinition(builder, definition);
        }

        builder.Append("],\"format\":\"spelljammer-semantic-v1\",\"packs\":[");
        for (int index = 0; index < packs.Count; index++)
        {
            if (index != 0)
            {
                builder.Append(',');
            }

            ContentPackIdentity pack = packs[index];
            builder.Append("{\"contentRevision\":").Append(pack.ContentRevision).Append(",\"id\":");
            WriteString(builder, pack.Id.ToString());
            builder.Append('}');
        }

        builder.Append("]}\n");
        byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return (bytes, new ContentFingerprint(hash));
    }

    private static void WriteDefinition(StringBuilder builder, ContentDefinition definition)
    {
        SortedDictionary<string, Action<StringBuilder>> properties = new(StringComparer.Ordinal)
        {
            ["id"] = value => WriteString(value, definition.Id.ToString()),
            ["kind"] = value => WriteString(value, definition.GetType().Name.Replace("Definition", string.Empty, StringComparison.Ordinal)),
            ["revision"] = value => value.Append(definition.Revision),
            ["schemaVersion"] = value => value.Append(definition.SchemaVersion),
        };

        switch (definition)
        {
            case AbilityDefinition value:
                properties["defaultValue"] = output => output.Append(value.DefaultValue);
                properties["maximum"] = output => output.Append(value.Maximum);
                properties["minimum"] = output => output.Append(value.Minimum);
                properties["tags"] = output => WriteStrings(output, value.Tags);
                break;
            case SkillDefinition value:
                properties["actionTags"] = output => WriteIds(output, value.ActionTags);
                properties["maximum"] = output => output.Append(value.Maximum);
                properties["minimum"] = output => output.Append(value.Minimum);
                properties["progressionCurveId"] = output => WriteString(output, value.ProgressionCurveId.ToString());
                break;
            case LevelProgressionTableDefinition value:
                properties["levels"] = output => WriteLevelProgressionEntries(output, value.Levels);
                break;
            case AccessDefinition value:
                properties["tags"] = output => WriteStrings(output, value.Tags);
                break;
            case BackgroundDefinition value:
                properties["abilityBonusIds"] = output => WriteIds(output, value.AbilityBonusIds.Select(id => id.Value));
                properties["compatibleRaceIds"] = output => WriteIds(output, value.CompatibleRaceIds.Select(id => id.Value));
                properties["focusSkillIds"] = output => WriteIds(output, value.FocusSkillIds.Select(id => id.Value));
                break;
            case CharacterDefinition value:
                properties["backgroundId"] = output => WriteString(output, value.BackgroundId.ToString());
                properties["equipmentIds"] = output => WriteIds(output, value.EquipmentIds);
                properties["focusSkillIds"] = output => WriteIds(output, value.FocusSkillIds.Select(id => id.Value));
                properties["heritageId"] = output => WriteString(output, value.HeritageId.ToString());
                properties["languageIds"] = output => WriteIds(output, value.LanguageIds);
                properties["positionId"] = output => WriteString(output, value.PositionId.ToString());
                properties["raceId"] = output => WriteString(output, value.RaceId.ToString());
                properties["resourceIds"] = output => WriteIds(output, value.ResourceIds.Select(id => id.Value));
                properties["scenarioIds"] = output => WriteIds(output, value.ScenarioIds.Select(id => id.Value));
                properties["scriptIds"] = output => WriteIds(output, value.ScriptIds);
                break;
            case ScenarioDefinition value:
                properties["maximumRosterSize"] = output => output.Append(value.MaximumRosterSize);
                if (value.LevelProgressionTableId is LevelProgressionTableId progressionTableId)
                {
                    properties["levelProgressionTableId"] = output => WriteString(output, progressionTableId.ToString());
                }

                break;
            case FeatDefinition value:
                properties["activation"] = output => WriteString(output, value.Activation == FeatActivation.Active ? "active" : "passive");
                properties["compatibleRaceIds"] = output => WriteIds(output, value.CompatibleRaceIds.Select(id => id.Value));
                properties["grantedAccessIds"] = output => WriteIds(output, value.GrantedAccessIds.Select(id => id.Value));
                if (!value.RequiredAccessIds.IsEmpty)
                {
                    properties["requiredAccessIds"] = output => WriteIds(output, value.RequiredAccessIds.Select(id => id.Value));
                }

                if (!value.EffectIds.IsEmpty)
                {
                    properties["effectIds"] = output => WriteIds(output, value.EffectIds);
                }

                if (!value.GrantedFeatIds.IsEmpty)
                {
                    properties["grantedFeatIds"] = output => WriteIds(output, value.GrantedFeatIds.Select(id => id.Value));
                }

                if (value.TrainingProjectId is TrainingProjectId trainingProjectId)
                {
                    properties["trainingProjectId"] = output => WriteString(output, trainingProjectId.ToString());
                }

                if (value.SpellRules is SpellFeatRules spell)
                {
                    properties["activeKind"] = output => WriteString(output, "spell");
                    properties["castTimeTicks"] = output => output.Append(spell.CastTimeTicks);
                    properties["cooldownTicks"] = output => output.Append(spell.CooldownTicks);
                    properties["focusCost"] = output => output.Append(spell.FocusCost);
                    properties["focusResourceId"] = output => WriteString(output, spell.FocusResourceId.ToString());
                    properties["rangeId"] = output => WriteString(output, spell.RangeId.ToString());
                    properties["skillId"] = output => WriteString(output, spell.SkillId.ToString());
                    properties["targetTags"] = output => WriteStrings(output, spell.TargetTags);
                }
                else if (value.PsionicRules is PsionicFeatRules psionic)
                {
                    properties["activeKind"] = output => WriteString(output, "psionic");
                    properties["contactModeId"] = output => WriteString(output, psionic.ContactModeId.ToString());
                    properties["disciplineIds"] = output => WriteIds(output, psionic.DisciplineIds);
                    properties["informationScopeId"] = output => WriteString(output, psionic.InformationScopeId.ToString());
                    properties["rangeId"] = output => WriteString(output, psionic.RangeId.ToString());
                    properties["resistanceSkillId"] = output => WriteString(output, psionic.ResistanceSkillId.ToString());
                    properties["skillId"] = output => WriteString(output, psionic.SkillId.ToString());
                    properties["strainCost"] = output => output.Append(psionic.StrainCost);
                    properties["strainResourceId"] = output => WriteString(output, psionic.StrainResourceId.ToString());
                    properties["sustainCostPerTick"] = output => output.Append(psionic.SustainCostPerTick);
                    properties["targetTags"] = output => WriteStrings(output, psionic.TargetTags);
                }
                else if (value.Activation == FeatActivation.Active)
                {
                    properties["activeKind"] = output => WriteString(output, "general");
                }

                break;
            case RaceDefinition value:
                properties["grantedFeatIds"] = output => WriteIds(output, value.GrantedFeatIds.Select(id => id.Value));
                if (!value.RequiredSupportIds.IsEmpty)
                {
                    properties["requiredSupportIds"] = output => WriteIds(output, value.RequiredSupportIds);
                }

                break;
            case HeritageDefinition value:
                properties["grantedFeatIds"] = output => WriteIds(output, value.GrantedFeatIds.Select(id => id.Value));
                properties["raceId"] = output => WriteString(output, value.RaceId.ToString());
                break;
            case TrainingProjectDefinition value:
                properties["facilityId"] = output => WriteString(output, value.FacilityId.ToString());
                properties["grantedFeatIds"] = output => WriteIds(output, value.GrantedFeatIds.Select(id => id.Value));
                properties["progressCap"] = output => output.Append(value.ProgressCap);
                properties["requiredSkillIds"] = output => WriteIds(output, value.RequiredSkillIds.Select(id => id.Value));
                properties["resourceCost"] = output => output.Append(value.ResourceCost);
                properties["resourceId"] = output => WriteString(output, value.ResourceId.ToString());
                properties["safetyId"] = output => WriteString(output, value.SafetyId.ToString());
                properties["workUnits"] = output => output.Append(value.WorkUnits);
                break;
            case EquipmentDefinition value:
                properties["actionIds"] = output => WriteIds(output, value.ActionIds);
                properties["effectIds"] = output => WriteIds(output, value.EffectIds);
                properties["initialStateId"] = output => WriteString(output, value.InitialStateId.ToString());
                properties["resourceCapacity"] = output => output.Append(value.ResourceCapacity);
                properties["resourceId"] = output => WriteString(output, value.ResourceId.ToString());
                properties["slotId"] = output => WriteString(output, value.SlotId.ToString());
                break;
            case BoardCellDefinition value:
                properties["atmosphereId"] = output => WriteString(output, value.AtmosphereId.ToString());
                properties["capacity"] = output => output.Append(value.Capacity);
                properties["cover"] = output => output.Append(value.Cover);
                properties["gravityId"] = output => WriteString(output, value.GravityId.ToString());
                properties["hazardTags"] = output => WriteStrings(output, value.HazardTags);
                properties["q"] = output => output.Append(value.Q);
                properties["r"] = output => output.Append(value.R);
                properties["visibility"] = output => output.Append(value.Visibility);
                properties["zoneId"] = output => WriteString(output, value.ZoneId.ToString());
                break;
            case ZoneLinkDefinition value:
                properties["accessId"] = output => WriteString(output, value.AccessId.ToString());
                properties["allowsRetreat"] = output => output.Append(value.AllowsRetreat);
                properties["fromCellId"] = output => WriteString(output, value.FromCellId.ToString());
                properties["oneWay"] = output => output.Append(value.OneWay);
                properties["toCellId"] = output => WriteString(output, value.ToCellId.ToString());
                break;
            case PersonalBoardDefinition value:
                properties["cellIds"] = output => WriteIds(output, value.CellIds.Select(id => id.Value));
                properties["linkIds"] = output => WriteIds(output, value.LinkIds.Select(id => id.Value));
                properties["maximumOccupants"] = output => output.Append(value.MaximumOccupants);
                properties["requiredObjectiveIds"] = output => WriteIds(output, value.RequiredObjectiveIds.Select(id => id.Value));
                properties["retreatCellIds"] = output => WriteIds(output, value.RetreatCellIds.Select(id => id.Value));
                break;
            case EncounterDefinition value:
                properties["ancientDefenseId"] = output => WriteString(output, value.AncientDefenseId.ToString());
                properties["contextId"] = output => WriteString(output, value.ContextId.ToString());
                properties["extractionObjectiveId"] = output => WriteString(output, value.ExtractionObjectiveId.ToString());
                properties["hostileTeamId"] = output => WriteString(output, value.HostileTeamId.ToString());
                properties["nonCombatObjectiveId"] = output => WriteString(output, value.NonCombatObjectiveId.ToString());
                properties["personalBoardId"] = output => WriteString(output, value.PersonalBoardId.ToString());
                break;
            case ShipFrameDefinition value:
                properties["baseArmor"] = output => output.Append(value.BaseArmor);
                properties["cargoCapacity"] = output => output.Append(value.CargoCapacity);
                properties["maximumHull"] = output => output.Append(value.MaximumHull);
                properties["maximumSlots"] = output => output.Append(value.MaximumSlots);
                properties["mountIds"] = output => WriteIds(output, value.MountIds);
                break;
            case ShipModuleDefinition value:
                properties["armorValue"] = output => output.Append(value.ArmorValue);
                properties["cargoDisplacement"] = output => output.Append(value.CargoDisplacement);
                properties["compatiblePathIds"] = output => WriteIds(output, value.CompatiblePathIds);
                properties["energyConsumption"] = output => output.Append(value.EnergyConsumption);
                properties["energyGeneration"] = output => output.Append(value.EnergyGeneration);
                properties["maximumIntegrity"] = output => output.Append(value.MaximumIntegrity);
                properties["mountId"] = output => WriteString(output, value.MountId.ToString());
                properties["networkId"] = output => WriteString(output, value.NetworkId.ToString());
                properties["primaryEffectId"] = output => WriteString(output, value.PrimaryEffectId.ToString());
                properties["shieldEnergyConsumptionRate"] = output => output.Append(value.ShieldEnergyConsumptionRate);
                properties["shieldRechargeRate"] = output => output.Append(value.ShieldRechargeRate);
                properties["shieldValue"] = output => output.Append(value.ShieldValue);
                properties["slotCost"] = output => output.Append(value.SlotCost);
                break;
            case ShipWeaponConfigurationDefinition value:
                properties["areaId"] = output => WriteString(output, value.AreaId.ToString());
                properties["armorPenetration"] = output => output.Append(value.ArmorPenetration);
                properties["damage"] = output => output.Append(value.Damage);
                properties["damageTypeId"] = output => WriteString(output, value.DamageTypeId.ToString());
                properties["effectiveRange"] = output => output.Append(value.EffectiveRange);
                properties["maximumRange"] = output => output.Append(value.MaximumRange);
                properties["networkId"] = output => WriteString(output, value.NetworkId.ToString());
                properties["rateOfFireTicks"] = output => output.Append(value.RateOfFireTicks);
                properties["reloadTicks"] = output => output.Append(value.ReloadTicks);
                properties["resourceCost"] = output => output.Append(value.ResourceCost);
                properties["resourceId"] = output => WriteString(output, value.ResourceId.ToString());
                break;
        }

        builder.Append('{');
        bool first = true;
        foreach ((string name, Action<StringBuilder> write) in properties)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteString(builder, name);
            builder.Append(':');
            write(builder);
        }

        builder.Append('}');
    }

    private static int CanonicalKindOrder(ContentDefinition definition) => definition switch
    {
        AccessDefinition => 0,
        AbilityDefinition => 1,
        BackgroundDefinition => 2,
        CharacterDefinition => 3,
        ScenarioDefinition => 4,
        FeatDefinition => 5,
        HeritageDefinition => 6,
        RaceDefinition => 7,
        SkillDefinition => 8,
        LevelProgressionTableDefinition => 9,
        TrainingProjectDefinition => 10,
        EquipmentDefinition => 11,
        BoardCellDefinition => 12,
        ZoneLinkDefinition => 13,
        PersonalBoardDefinition => 14,
        EncounterDefinition => 15,
        ShipFrameDefinition => 16,
        ShipModuleDefinition => 17,
        ShipWeaponConfigurationDefinition => 18,
        _ => throw new ArgumentOutOfRangeException(nameof(definition)),
    };

    private static void WriteStrings(StringBuilder builder, IEnumerable<string> values)
    {
        builder.Append('[');
        bool first = true;
        foreach (string value in values.Order(StringComparer.Ordinal))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            WriteString(builder, value);
        }

        builder.Append(']');
    }

    private static void WriteIds(StringBuilder builder, IEnumerable<ContentId> ids) =>
        WriteStrings(builder, ids.Select(id => id.ToString()));

    private static void WriteLevelProgressionEntries(
        StringBuilder builder,
        IEnumerable<LevelProgressionEntry> entries)
    {
        builder.Append('[');
        bool first = true;
        foreach (LevelProgressionEntry entry in entries)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append("{\"abilityPoints\":").Append(entry.AbilityPoints)
                .Append(",\"featPoints\":").Append(entry.FeatPoints)
                .Append(",\"level\":").Append(entry.Level)
                .Append(",\"maximumHealthIncrease\":").Append(entry.MaximumHealthIncrease)
                .Append(",\"maximumManaIncrease\":").Append(entry.MaximumManaIncrease)
                .Append(",\"maximumStaminaIncrease\":").Append(entry.MaximumStaminaIncrease)
                .Append(",\"requiredExperience\":").Append(entry.RequiredExperience)
                .Append(",\"skillPoints\":").Append(entry.SkillPoints)
                .Append('}');
        }

        builder.Append(']');
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (Rune rune in value.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case <= 0x1f:
                    builder.Append("\\u00").Append(rune.Value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                default:
                    builder.Append(rune);
                    break;
            }
        }

        builder.Append('"');
    }
}
