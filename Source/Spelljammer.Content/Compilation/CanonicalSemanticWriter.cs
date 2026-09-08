using System.Security.Cryptography;
using System.Text;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;
using ArmorDefinition = Spelljammer.Simulation.Items.ArmorDefinition;
using GearDefinition = Spelljammer.Simulation.Items.GearDefinition;
using AmmunitionDefinition = Spelljammer.Simulation.Items.AmmunitionDefinition;

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
            case CharacterResourceProfileDefinition value:
                foreach (CharacterResourceRule rule in value.Resources)
                {
                    string prefix = rule.ResourceId.ToString()["resource.".Length..];
                    properties[prefix + "Maximum"] = output => output.Append(rule.BaseMaximum);
                    properties[prefix + "RecoveryRate"] = output => output.Append(rule.BaseRecoveryRate);
                    if (!rule.ThresholdPercentages.IsEmpty)
                    {
                        properties[prefix + "ThresholdPercentages"] = output => WriteIntegers(output, rule.ThresholdPercentages);
                    }
                }

                properties["actionPointCostIds"] = output => WriteIds(output,
                    value.TurnRules.ActionPointCosts.OrderBy(pair => pair.Key).Select(pair => pair.Key));
                properties["actionPointCosts"] = output => WriteIntegers(output,
                    value.TurnRules.ActionPointCosts.OrderBy(pair => pair.Key).Select(pair => pair.Value));
                properties["baseActionPoints"] = output => output.Append(value.TurnRules.BaseActionPoints);
                properties["baseTurnMeterGain"] = output => output.Append(value.TurnRules.BaseTurnMeterGain);
                properties["normalTurnMeterGainPercentage"] = output => output.Append(value.TurnRules.NormalTurnMeterGainPercentage);
                properties["staminaTurnMeterGainPercentages"] = output => WriteIntegers(output,
                    value.TurnRules.StaminaTurnMeterRules.Select(rule => rule.TurnMeterGainPercentage));
                properties["staminaTurnMeterThresholdPercentages"] = output => WriteIntegers(output,
                    value.TurnRules.StaminaTurnMeterRules.Select(rule => rule.MaximumStaminaPercentage));
                properties["turnMeterThreshold"] = output => output.Append(value.TurnRules.TurnMeterThreshold);

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
                properties["startingItemDefinitionIds"] = output => WriteIds(output, value.StartingItemDefinitionIds);
                properties["inventoryMaximumWeightHundredthsOfPound"] = output => output.Append(value.InventoryMaximumWeightHundredthsOfPound);
                properties["inventoryMaximumEntries"] = output => output.Append(value.InventoryMaximumEntries);
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

                if (value.CharacterResourceProfileId is CharacterResourceProfileId resourceProfileId)
                {
                    properties["characterResourceProfileId"] = output => WriteString(output, resourceProfileId.ToString());
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
                    properties["manaCost"] = output => output.Append(spell.ManaCost);
                    properties["manaResourceId"] = output => WriteString(output, spell.ManaResourceId.ToString());
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
            case ArmorDefinition value:
                properties["armorValue"] = output => output.Append(value.ArmorValue);
                properties["coverageTags"] = output => WriteStrings(output, value.CoverageTags);
                properties["durabilityMaximum"] = output => output.Append(value.DurabilityMaximum);
                properties["kind"] = output => WriteString(output, "armor");
                properties["occupiedSlotIds"] = output => WriteIds(output, value.OccupiedSlotIds);
                properties["resistanceIds"] = output => WriteIds(output, value.ResistanceIds);
                properties["tags"] = output => WriteStrings(output, value.Tags);
                properties["traits"] = output => WriteStrings(output, value.Traits);
                properties["value"] = output => output.Append(value.Value);
                properties["weightHundredthsOfPound"] = output => output.Append(value.WeightHundredthsOfPound);
                break;
            case GearDefinition value:
                properties["actionIds"] = output => WriteIds(output, value.ActionIds);
                properties["effectIds"] = output => WriteIds(output, value.EffectIds);
                properties["kind"] = output => WriteString(output, "gear");
                properties["occupiedSlotIds"] = output => WriteIds(output, value.OccupiedSlotIds);
                properties["tags"] = output => WriteStrings(output, value.Tags);
                properties["value"] = output => output.Append(value.Value);
                properties["weightHundredthsOfPound"] = output => output.Append(value.WeightHundredthsOfPound);
                break;
            case MeleeWeaponDefinition value:
                properties["abilityId"] = output => WriteString(output, value.AbilityId.ToString());
                properties["actionIds"] = output => WriteIds(output, value.ActionIds);
                properties["armorDamagePercentage"] = output => output.Append(value.ArmorDamagePercentage);
                properties["armorPenetrationPercentage"] = output => output.Append(value.ArmorPenetrationPercentage);
                properties["damageMaximum"] = output => output.Append(value.DamageMaximum);
                properties["damageMinimum"] = output => output.Append(value.DamageMinimum);
                properties["energyCapacity"] = output => output.Append(value.EnergyCapacity);
                properties["energyPerAttack"] = output => output.Append(value.EnergyPerAttack);
                properties["family"] = output => WriteString(output, WriteMeleeFamily(value.Family));
                properties["hands"] = output => WriteString(output, value.Hands == MeleeWeaponHands.OneHanded ? "one-handed" : "two-handed");
                properties["maximumDurability"] = output => output.Append(value.MaximumDurability);
                properties["range"] = output => output.Append(value.Range);
                properties["skillId"] = output => WriteString(output, value.SkillId.ToString());
                properties["staminaCost"] = output => output.Append(value.StaminaCost);
                properties["strengthDamageScale"] = output => output.Append(value.StrengthDamageScale);
                properties["technology"] = output => WriteString(output, WriteMeleeTechnology(value.Technology));
                properties["occupiedSlotIds"] = output => WriteIds(output, value.OccupiedSlotIds);
                properties["traits"] = output => WriteStrings(output, value.Tags);
                properties["unpoweredArmorPenetrationPercentage"] = output => output.Append(value.UnpoweredArmorPenetrationPercentage);
                properties["unpoweredDamagePercentage"] = output => output.Append(value.UnpoweredDamagePercentage);
                properties["value"] = output => output.Append(value.Value);
                properties["weightHundredthsOfPound"] = output => output.Append(value.WeightHundredthsOfPound);
                break;
            case MeleeWeaponActionDefinition value:
                properties["actionPointCost"] = output => output.Append(value.ActionPointCost);
                properties["armorDamagePercentage"] = output => output.Append(value.ArmorDamagePercentage);
                properties["armorPenetrationModifier"] = output => output.Append(value.ArmorPenetrationModifier);
                properties["damagePercentage"] = output => output.Append(value.DamagePercentage);
                properties["durabilityCost"] = output => output.Append(value.DurabilityCost);
                properties["effectIds"] = output => WriteIds(output, value.EffectIds);
                properties["energyCostModifier"] = output => output.Append(value.EnergyCostModifier);
                properties["hitModifier"] = output => output.Append(value.HitModifier);
                properties["rangeModifier"] = output => output.Append(value.RangeModifier);
                properties["staminaCostModifier"] = output => output.Append(value.StaminaCostModifier);
                break;
            case RangedWeaponDefinition value:
                properties["actionIds"] = output => WriteIds(output, value.ActionIds);
                if (value.AmmunitionType is AmmunitionType ammunitionType)
                {
                    properties["ammunitionType"] = output => WriteString(output, WriteAmmunitionType(ammunitionType));
                }
                properties["armorDamagePercentage"] = output => output.Append(value.ArmorDamagePercentage);
                properties["armorPenetrationPercentage"] = output => output.Append(value.ArmorPenetrationPercentage);
                properties["damageMaximum"] = output => output.Append(value.DamageMaximum);
                properties["damageMinimum"] = output => output.Append(value.DamageMinimum);
                properties["energyCapacity"] = output => output.Append(value.EnergyCapacity);
                properties["energyPerShot"] = output => output.Append(value.EnergyPerShot);
                properties["family"] = output => WriteString(output, WriteRangedFamily(value.Family));
                properties["hands"] = output => WriteString(output, value.Hands == RangedWeaponHands.OneHanded ? "one-handed" : "two-handed");
                properties["heatCapacity"] = output => output.Append(value.HeatCapacity);
                properties["heatPerShot"] = output => output.Append(value.HeatPerShot);
                properties["magazineCapacity"] = output => output.Append(value.MagazineCapacity);
                properties["maximumDurability"] = output => output.Append(value.MaximumDurability);
                properties["maximumRange"] = output => output.Append(value.MaximumRange);
                properties["optimalRange"] = output => output.Append(value.OptimalRange);
                properties["skillId"] = output => WriteString(output, value.SkillId.ToString());
                properties["staminaCost"] = output => output.Append(value.StaminaCost);
                properties["technology"] = output => WriteString(output, WriteRangedTechnology(value.Technology));
                properties["occupiedSlotIds"] = output => WriteIds(output, value.OccupiedSlotIds);
                properties["traits"] = output => WriteStrings(output, value.Tags);
                properties["value"] = output => output.Append(value.Value);
                properties["weightHundredthsOfPound"] = output => output.Append(value.WeightHundredthsOfPound);
                break;
            case AmmunitionDefinition value:
                properties["ammunitionType"] = output => WriteString(output, WriteAmmunitionType(value.AmmunitionType));
                properties["armorDamagePercentage"] = output => output.Append(value.ArmorDamagePercentage);
                properties["armorPenetrationModifier"] = output => output.Append(value.ArmorPenetrationModifier);
                properties["damagePercentage"] = output => output.Append(value.DamagePercentage);
                properties["rangeModifier"] = output => output.Append(value.RangeModifier);
                properties["maximumStackSize"] = output => output.Append(value.MaximumStackSize);
                properties["technology"] = output => WriteString(output, WriteRangedTechnology(value.Technology));
                properties["tags"] = output => WriteStrings(output, value.Tags);
                properties["value"] = output => output.Append(value.Value);
                properties["weightHundredthsOfPound"] = output => output.Append(value.WeightHundredthsOfPound);
                break;
            case RangedWeaponActionDefinition value:
                properties["actionPointCost"] = output => output.Append(value.ActionPointCost);
                properties["ammunitionCost"] = output => output.Append(value.AmmunitionCost);
                properties["damageFalloffPerUnitPercentage"] = output => output.Append(value.DamageFalloffPerUnitPercentage);
                properties["damagePercentage"] = output => output.Append(value.DamagePercentage);
                properties["durabilityCost"] = output => output.Append(value.DurabilityCost);
                properties["effectIds"] = output => WriteIds(output, value.EffectIds);
                properties["energyCostModifier"] = output => output.Append(value.EnergyCostModifier);
                properties["heatModifier"] = output => output.Append(value.HeatModifier);
                properties["hitModifier"] = output => output.Append(value.HitModifier);
                properties["kind"] = output => WriteString(output, value.Kind == RangedWeaponActionKind.Attack ? "attack" : "reload");
                properties["rangePenaltyPerUnit"] = output => output.Append(value.RangePenaltyPerUnit);
                properties["reloadAmount"] = output => output.Append(value.ReloadAmount);
                properties["shotCount"] = output => output.Append(value.ShotCount);
                properties["staminaCostModifier"] = output => output.Append(value.StaminaCostModifier);
                break;
            case EffectDefinition value:
                properties["amount"] = output => output.Append(value.Amount);
                properties["duration"] = output => output.Append(value.Duration);
                properties["potency"] = output => output.Append(value.Potency);
                properties["stacks"] = output => output.Append(value.Stacks);
                if (value.StatusId is StatusId statusId)
                {
                    properties["statusId"] = output => WriteString(output, statusId.ToString());
                }

                properties["type"] = output => WriteString(output, WriteEffectType(value.Type));
                break;
            case StatusDefinition value:
                properties["aiRules"] = output => WriteStatusAiRules(output, value.AiRules);
                properties["category"] = output => WriteString(output, WriteStatusCategory(value.Category));
                properties["defaultDuration"] = output => output.Append(value.DefaultDuration);
                properties["durationType"] = output => WriteString(output, WriteStatusDurationType(value.DurationType));
                if (value.ExclusiveGroupId is ContentId groupId)
                {
                    properties["exclusiveGroupId"] = output => WriteString(output, groupId.ToString());
                }

                properties["maximumStacks"] = output => output.Append(value.MaximumStacks);
                properties["modifiers"] = output => WriteStatusModifiers(output, value.Modifiers);
                properties["onApplyEffectIds"] = output => WriteStrings(output, value.OnApplyEffectIds.Select(id => id.ToString()));
                properties["onExpireEffectIds"] = output => WriteStrings(output, value.OnExpireEffectIds.Select(id => id.ToString()));
                properties["onTickEffectIds"] = output => WriteStrings(output, value.OnTickEffectIds.Select(id => id.ToString()));
                properties["priority"] = output => output.Append(value.Priority);
                properties["restrictions"] = output => WriteStatusRestrictions(output, value.Restrictions);
                properties["stackPolicy"] = output => WriteString(output, WriteStatusStackPolicy(value.StackPolicy));
                properties["tags"] = output => WriteStrings(output, value.Tags);
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
        CharacterResourceProfileDefinition => 10,
        TrainingProjectDefinition => 11,
        ArmorDefinition => 12,
        GearDefinition => 12,
        MeleeWeaponDefinition => 13,
        MeleeWeaponActionDefinition => 14,
        RangedWeaponDefinition => 15,
        AmmunitionDefinition => 16,
        RangedWeaponActionDefinition => 17,
        EffectDefinition => 18,
        StatusDefinition => 19,
        BoardCellDefinition => 20,
        ZoneLinkDefinition => 21,
        PersonalBoardDefinition => 22,
        EncounterDefinition => 23,
        ShipFrameDefinition => 24,
        ShipModuleDefinition => 25,
        ShipWeaponConfigurationDefinition => 26,
        _ => throw new ArgumentOutOfRangeException(nameof(definition)),
    };

    private static string WriteMeleeFamily(MeleeWeaponFamily family) => family switch
    {
        MeleeWeaponFamily.Blade => "blade",
        MeleeWeaponFamily.Dagger => "dagger",
        MeleeWeaponFamily.Axe => "axe",
        MeleeWeaponFamily.Blunt => "blunt",
        MeleeWeaponFamily.Polearm => "polearm",
        MeleeWeaponFamily.Fist => "fist",
        _ => throw new ArgumentOutOfRangeException(nameof(family)),
    };

    private static string WriteMeleeTechnology(MeleeWeaponTechnology technology) => technology switch
    {
        MeleeWeaponTechnology.Conventional => "conventional",
        MeleeWeaponTechnology.Chain => "chain",
        MeleeWeaponTechnology.Shock => "shock",
        MeleeWeaponTechnology.Powered => "powered",
        MeleeWeaponTechnology.Arcane => "arcane",
        _ => throw new ArgumentOutOfRangeException(nameof(technology)),
    };

    private static string WriteRangedFamily(RangedWeaponFamily family) => family switch
    {
        RangedWeaponFamily.Bow => "bow",
        RangedWeaponFamily.Crossbow => "crossbow",
        RangedWeaponFamily.Pistol => "pistol",
        RangedWeaponFamily.Rifle => "rifle",
        RangedWeaponFamily.Shotgun => "shotgun",
        RangedWeaponFamily.Heavy => "heavy",
        RangedWeaponFamily.Launcher => "launcher",
        RangedWeaponFamily.Special => "special",
        _ => throw new ArgumentOutOfRangeException(nameof(family)),
    };

    private static string WriteRangedTechnology(RangedWeaponTechnology technology) => technology switch
    {
        RangedWeaponTechnology.Conventional => "conventional",
        RangedWeaponTechnology.Ballistic => "ballistic",
        RangedWeaponTechnology.Laser => "laser",
        RangedWeaponTechnology.Plasma => "plasma",
        RangedWeaponTechnology.Arcane => "arcane",
        _ => throw new ArgumentOutOfRangeException(nameof(technology)),
    };

    private static string WriteAmmunitionType(AmmunitionType ammunitionType) => ammunitionType switch
    {
        AmmunitionType.Arrow => "arrow",
        AmmunitionType.CrossbowBolt => "crossbow-bolt",
        AmmunitionType.PistolRound => "pistol-round",
        AmmunitionType.RifleRound => "rifle-round",
        AmmunitionType.ShotgunShell => "shotgun-shell",
        AmmunitionType.Grenade => "grenade",
        AmmunitionType.Rocket => "rocket",
        AmmunitionType.MiniNuke => "mini-nuke",
        AmmunitionType.FlamethrowerFuel => "flamethrower-fuel",
        AmmunitionType.LaserCell => "laser-cell",
        AmmunitionType.PlasmaCell => "plasma-cell",
        _ => throw new ArgumentOutOfRangeException(nameof(ammunitionType)),
    };

    private static string WriteEffectType(EffectType value) => ToKebabCase(value.ToString());
    private static string WriteStatusCategory(StatusCategory value) => ToKebabCase(value.ToString());
    private static string WriteStatusDurationType(StatusDurationType value) => ToKebabCase(value.ToString());
    private static string WriteStatusStackPolicy(StatusStackPolicy value) => ToKebabCase(value.ToString());

    private static void WriteStatusModifiers(StringBuilder builder, IEnumerable<StatusModifierDefinition> values)
    {
        builder.Append('[');
        bool first = true;
        foreach (StatusModifierDefinition value in values.OrderBy(item => item.Type).ThenBy(item => item.Amount))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append("{\"amount\":").Append(value.Amount).Append(",\"type\":");
            WriteString(builder, ToKebabCase(value.Type.ToString()));
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void WriteStatusRestrictions(StringBuilder builder, IEnumerable<StatusRestrictionDefinition> values)
    {
        builder.Append('[');
        bool first = true;
        foreach (StatusRestrictionDefinition value in values.OrderBy(item => item.Type).ThenBy(item => item.TargetRule)
                     .ThenBy(item => item.ActionTag))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append('{');
            bool hasProperty = false;
            if (value.ActionTag is ContentId actionTag)
            {
                builder.Append("\"actionTag\":");
                WriteString(builder, actionTag.ToString());
                hasProperty = true;
            }

            if (value.TargetRule != StatusTargetRule.None)
            {
                if (hasProperty) builder.Append(',');
                builder.Append("\"targetRule\":");
                WriteString(builder, ToKebabCase(value.TargetRule.ToString()));
                hasProperty = true;
            }

            if (hasProperty) builder.Append(',');
            builder.Append("\"type\":");
            WriteString(builder, ToKebabCase(value.Type.ToString()));
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static void WriteStatusAiRules(StringBuilder builder, IEnumerable<StatusAiRuleDefinition> values)
    {
        builder.Append('[');
        bool first = true;
        foreach (StatusAiRuleDefinition value in values.OrderBy(item => item.Type).ThenBy(item => item.TargetRule)
                     .ThenBy(item => item.ActionTag).ThenBy(item => item.Amount))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder.Append('{');
            bool hasProperty = false;
            if (value.ActionTag is ContentId actionTag)
            {
                builder.Append("\"actionTag\":");
                WriteString(builder, actionTag.ToString());
                hasProperty = true;
            }

            if (value.Amount != 0)
            {
                if (hasProperty) builder.Append(',');
                builder.Append("\"amount\":").Append(value.Amount);
                hasProperty = true;
            }

            if (value.TargetRule != StatusTargetRule.None)
            {
                if (hasProperty) builder.Append(',');
                builder.Append("\"targetRule\":");
                WriteString(builder, ToKebabCase(value.TargetRule.ToString()));
                hasProperty = true;
            }

            if (hasProperty) builder.Append(',');
            builder.Append("\"type\":");
            WriteString(builder, ToKebabCase(value.Type.ToString()));
            builder.Append('}');
        }

        builder.Append(']');
    }

    private static string ToKebabCase(string value)
    {
        StringBuilder builder = new();
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (index > 0 && char.IsUpper(character))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

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

    private static void WriteIntegers(StringBuilder builder, IEnumerable<int> values)
    {
        builder.Append('[').AppendJoin(',', values).Append(']');
    }

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
                .Append(",\"maximumResolveIncrease\":").Append(entry.MaximumResolveIncrease)
                .Append(",\"maximumStaminaIncrease\":").Append(entry.MaximumStaminaIncrease)
                .Append(",\"maximumStrainIncrease\":").Append(entry.MaximumStrainIncrease)
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
