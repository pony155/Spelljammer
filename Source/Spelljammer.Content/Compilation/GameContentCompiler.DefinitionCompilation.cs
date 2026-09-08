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
/// Converts normalized source definitions into immutable simulation definition types.
/// </summary>
/// <remarks>
/// Code flow: Validated source fields and linked IDs are mapped by kind, strongly typed records are constructed in canonical order, and snapshot assembly places them in typed registries.
/// </remarks>
public sealed partial class GameContentCompiler
{
    private static WorldTimeDefinition CompileWorldTime(SourceDefinition value) => new(
        new WorldTimeId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["ticksPerSecond"], value.Integers["maximumCatchUpTicks"]);

    private static CalendarDefinition CompileCalendar(SourceDefinition value) => new(
        new CalendarId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["secondsPerMinute"], value.Integers["minutesPerHour"],
        value.Integers["hoursPerDay"], value.Integers["daysPerWeek"], value.Integers["startingYear"],
        value.Integers["startingMonth"], value.Integers["startingDay"],
        value.Integers["startingDayOfWeekIndex"], value.Integers["startingHour"],
        value.Integers["startingMinute"], value.Integers["startingSecond"],
        [.. value.Calendar!.Months.Select(month => new CalendarMonthDefinition(
            month.CalendarMonthId, month.NameKey, month.Days))]);

    private static TimeScaleDefinition CompileTimeScale(SourceDefinition value) => new(
        new TimeScaleId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["worldSecondsNumerator"], value.Integers["simulationTicksDenominator"]);

    private static AbilityDefinition CompileAbility(SourceDefinition value) => new(
        new AbilityId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        (short)value.Ability!.Minimum, (short)value.Ability.Maximum, (short)value.Ability.DefaultValue,
        Sort(value.Ability.Tags));

    private static SkillDefinition CompileSkill(SourceDefinition value) => new(
        new SkillId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        (byte)value.Skill!.Minimum, (byte)value.Skill.Maximum, value.Skill.ProgressionCurveId,
        [.. value.Skill.ActionTags.Order()]);

    private static LevelProgressionTableDefinition CompileLevelProgressionTable(SourceDefinition value) => new(
        new LevelProgressionTableId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.LevelProgressionEntries);

    private static CharacterResourceProfileDefinition CompileCharacterResourceProfile(SourceDefinition value)
    {
        CharacterResourceRule Rule(string id, string fieldPrefix, bool accumulates, string? thresholdsField = null) => new(
            new ResourceId(id),
            value.Integers[fieldPrefix + "Maximum"],
            value.Integers[fieldPrefix + "RecoveryRate"],
            accumulates,
            thresholdsField is null ? [] : value.IntegerArrays[thresholdsField]);

        return new CharacterResourceProfileDefinition(
            new CharacterResourceProfileId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
            [
                Rule("resource.health", "health", false),
                Rule("resource.stamina", "stamina", false),
                Rule("resource.mana", "mana", false),
                Rule("resource.resolve", "resolve", false, "resolveThresholdPercentages"),
                Rule("resource.strain", "strain", true, "strainThresholdPercentages"),
            ],
            new CharacterTurnRules(
                value.Integers["turnMeterThreshold"],
                value.Integers["baseTurnMeterGain"],
                value.Integers["baseActionPoints"],
                value.Integers["normalTurnMeterGainPercentage"],
                [.. value.IntegerArrays["staminaTurnMeterThresholdPercentages"]
                    .Zip(value.IntegerArrays["staminaTurnMeterGainPercentages"])
                    .Select(pair => new StaminaTurnMeterRule(pair.First, pair.Second))],
                value.Arrays["actionPointCostIds"]
                    .Zip(value.IntegerArrays["actionPointCosts"])
                    .ToImmutableDictionary(pair => new ContentId(pair.First), pair => pair.Second)));
    }

    private static AccessDefinition CompileAccess(SourceDefinition value) => new(
        new AccessId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey, Sort(value.Arrays["tags"]));

    private static BackgroundDefinition CompileBackground(SourceDefinition value) => new(
        new BackgroundId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        Sort(value.Arrays["compatibleRaceIds"]).Select(item => new RaceId(item)).ToImmutableArray(),
        Sort(value.Arrays["abilityBonusIds"]).Select(item => new AbilityId(item)).ToImmutableArray(),
        Sort(value.Arrays["focusSkillIds"]).Select(item => new SkillId(item)).ToImmutableArray());

    private static CharacterDefinition CompileCharacter(SourceDefinition value) => new(
        new CharacterId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new RaceId(value.Strings["raceId"]), new HeritageId(value.Strings["heritageId"]),
        new BackgroundId(value.Strings["backgroundId"]),
        Sort(value.Arrays["scenarioIds"]).Select(item => new ScenarioId(item)).ToImmutableArray(),
        new ContentId(value.Strings["positionId"]),
        Sort(value.Arrays["languageIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        Sort(value.Arrays["scriptIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        Sort(value.Arrays["startingItemDefinitionIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        value.Integers["inventoryMaximumWeightHundredthsOfPound"],
        value.Integers["inventoryMaximumEntries"],
        Sort(value.Arrays["focusSkillIds"]).Select(item => new SkillId(item)).ToImmutableArray(),
        Sort(value.Arrays["resourceIds"]).Select(item => new ResourceId(item)).ToImmutableArray());

    private static ScenarioDefinition CompileScenario(SourceDefinition value) => new(
        new ScenarioId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["maximumRosterSize"],
        value.Strings.TryGetValue("levelProgressionTableId", out string? progressionTableId)
            ? new LevelProgressionTableId(progressionTableId)
            : null,
        value.Strings.TryGetValue("characterResourceProfileId", out string? resourceProfileId)
            ? new CharacterResourceProfileId(resourceProfileId)
            : null);

    private static FeatDefinition CompileFeat(SourceDefinition value) => new(
        new FeatId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Strings["activation"] == "active" ? FeatActivation.Active : FeatActivation.Passive,
        value.Strings.TryGetValue("trainingProjectId", out string? trainingProjectId)
            ? new TrainingProjectId(trainingProjectId)
            : null,
        Sort(value.Arrays["compatibleRaceIds"]).Select(item => new RaceId(item)).ToImmutableArray(),
        Sort(value.Arrays["requiredAccessIds"]).Select(item => new AccessId(item)).ToImmutableArray(),
        Sort(value.Arrays["grantedAccessIds"]).Select(item => new AccessId(item)).ToImmutableArray(),
        Sort(value.Arrays["grantedFeatIds"]).Select(item => new FeatId(item)).ToImmutableArray(),
        CompileEffectApplications(value.Arrays["effectIds"]),
        CompileSpellRules(value),
        CompilePsionicRules(value));

    private static SpellFeatRules? CompileSpellRules(SourceDefinition value) =>
        value.Strings.GetValueOrDefault("activeKind") == "spell"
            ? new SpellFeatRules(
                new SkillId(value.Strings["skillId"]), new ResourceId(value.Strings["manaResourceId"]),
                value.Integers["manaCost"], new ContentId(value.Strings["rangeId"]),
                value.Integers["castTimeTicks"], value.Integers["cooldownTicks"], Sort(value.Arrays["targetTags"]))
            : null;

    private static PsionicFeatRules? CompilePsionicRules(SourceDefinition value) =>
        value.Strings.GetValueOrDefault("activeKind") == "psionic"
            ? new PsionicFeatRules(
                new SkillId(value.Strings["skillId"]), new SkillId(value.Strings["resistanceSkillId"]),
                new ResourceId(value.Strings["strainResourceId"]), value.Integers["strainCost"],
                value.Integers["sustainCostPerTick"], new ContentId(value.Strings["contactModeId"]),
                new ContentId(value.Strings["rangeId"]), new ContentId(value.Strings["informationScopeId"]),
                Sort(value.Arrays["disciplineIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
                Sort(value.Arrays["targetTags"]))
            : null;

    private static RaceDefinition CompileRace(SourceDefinition value) => new(
        new RaceId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        Sort(value.Arrays["grantedFeatIds"]).Select(item => new FeatId(item)).ToImmutableArray(),
        Sort(value.Arrays["requiredSupportIds"]).Select(item => new ContentId(item)).ToImmutableArray());

    private static HeritageDefinition CompileHeritage(SourceDefinition value) => new(
        new HeritageId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new RaceId(value.Strings["raceId"]),
        Sort(value.Arrays["grantedFeatIds"]).Select(item => new FeatId(item)).ToImmutableArray());

    private static TrainingProjectDefinition CompileTraining(SourceDefinition value) => new(
        new TrainingProjectId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        Sort(value.Arrays["requiredSkillIds"]).Select(item => new SkillId(item)).ToImmutableArray(),
        value.Integers["workUnits"],
        value.Integers["progressCap"], new ContentId(value.Strings["facilityId"]),
        new ResourceId(value.Strings["resourceId"]), value.Integers["resourceCost"],
        new ContentId(value.Strings["safetyId"]),
        Sort(value.Arrays["grantedFeatIds"]).Select(item => new FeatId(item)).ToImmutableArray());

    private static EquipmentDefinition CompileEquipment(SourceDefinition value)
    {
        ContentId id = value.Id;
        ImmutableArray<string> tags = Sort(value.Arrays["tags"]);
        ImmutableArray<ContentId> slots = Sort(value.Arrays["occupiedSlotIds"])
            .Select(item => new ContentId(item)).ToImmutableArray();
        return value.Strings["kind"] switch
        {
            "armor" => new ArmorDefinition(
                id, 1, value.Revision, value.NameKey, value.DescriptionKey,
                value.Integers["weightHundredthsOfPound"], value.Integers["value"], tags, slots,
                value.Integers["armorValue"], value.Integers["durabilityMaximum"],
                Sort(OptionalArray(value, "resistanceIds")).Select(item => new ContentId(item)).ToImmutableArray(),
                Sort(OptionalArray(value, "coverageTags")), Sort(OptionalArray(value, "traits"))),
            "gear" => new GearDefinition(
                id, 1, value.Revision, value.NameKey, value.DescriptionKey,
                value.Integers["weightHundredthsOfPound"], value.Integers["value"], tags, slots,
                Sort(OptionalArray(value, "actionIds")).Select(item => new ContentId(item)).ToImmutableArray(),
                CompileEffectApplications(OptionalArray(value, "effectIds"))),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
    }

    private static MeleeWeaponDefinition CompileMeleeWeapon(SourceDefinition value) => new(
        new MeleeWeaponId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["weightHundredthsOfPound"], value.Integers["value"], Sort(value.Arrays["traits"]),
        Sort(value.Arrays["occupiedSlotIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        Sort(value.Arrays["actionIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        ParseMeleeWeaponFamily(value.Strings["family"]),
        ParseMeleeWeaponTechnology(value.Strings["technology"]),
        ParseMeleeWeaponHands(value.Strings["hands"]),
        new SkillId(value.Strings["skillId"]), new AbilityId(value.Strings["abilityId"]),
        value.Integers["damageMinimum"], value.Integers["damageMaximum"],
        value.Integers["armorDamagePercentage"], value.Integers["armorPenetrationPercentage"],
        value.Integers["staminaCost"], value.Integers["range"], value.Integers["maximumDurability"],
        value.Integers.GetValueOrDefault("energyCapacity"), value.Integers.GetValueOrDefault("energyPerAttack"),
        value.Integers.GetValueOrDefault("unpoweredDamagePercentage"),
        value.Integers.GetValueOrDefault("unpoweredArmorPenetrationPercentage"),
        value.Integers["strengthDamageScale"]);

    private static MeleeWeaponActionDefinition CompileMeleeWeaponAction(SourceDefinition value) => new(
        new MeleeWeaponActionId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["actionPointCost"], value.Integers["staminaCostModifier"],
        value.Integers["hitModifier"], value.Integers["damagePercentage"],
        value.Integers["armorDamagePercentage"], value.Integers["armorPenetrationModifier"],
        value.Integers["rangeModifier"], value.Integers["energyCostModifier"],
        value.Integers["durabilityCost"],
        CompileEffectApplications(value.Arrays["effectIds"]));

    private static MeleeWeaponFamily ParseMeleeWeaponFamily(string value) => value switch
    {
        "blade" => MeleeWeaponFamily.Blade,
        "dagger" => MeleeWeaponFamily.Dagger,
        "axe" => MeleeWeaponFamily.Axe,
        "blunt" => MeleeWeaponFamily.Blunt,
        "polearm" => MeleeWeaponFamily.Polearm,
        "fist" => MeleeWeaponFamily.Fist,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static MeleeWeaponTechnology ParseMeleeWeaponTechnology(string value) => value switch
    {
        "conventional" => MeleeWeaponTechnology.Conventional,
        "chain" => MeleeWeaponTechnology.Chain,
        "shock" => MeleeWeaponTechnology.Shock,
        "powered" => MeleeWeaponTechnology.Powered,
        "arcane" => MeleeWeaponTechnology.Arcane,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static MeleeWeaponHands ParseMeleeWeaponHands(string value) => value switch
    {
        "one-handed" => MeleeWeaponHands.OneHanded,
        "two-handed" => MeleeWeaponHands.TwoHanded,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static RangedWeaponDefinition CompileRangedWeapon(SourceDefinition value) => new(
        new RangedWeaponId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["weightHundredthsOfPound"], value.Integers["value"], Sort(value.Arrays["traits"]),
        Sort(value.Arrays["occupiedSlotIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        Sort(value.Arrays["actionIds"]).Select(item => new ContentId(item)).ToImmutableArray(),
        ParseRangedWeaponFamily(value.Strings["family"]),
        ParseRangedWeaponTechnology(value.Strings["technology"]),
        ParseRangedWeaponHands(value.Strings["hands"]),
        new SkillId(value.Strings["skillId"]),
        value.Integers["damageMinimum"], value.Integers["damageMaximum"],
        value.Integers["armorDamagePercentage"], value.Integers["armorPenetrationPercentage"],
        value.Integers["staminaCost"], value.Integers["optimalRange"], value.Integers["maximumRange"],
        value.Integers["maximumDurability"],
        value.Strings.TryGetValue("ammunitionType", out string? ammunitionType)
            ? ParseAmmunitionType(ammunitionType)
            : null,
        value.Integers.GetValueOrDefault("magazineCapacity"),
        value.Integers.GetValueOrDefault("energyCapacity"), value.Integers.GetValueOrDefault("energyPerShot"),
        value.Integers.GetValueOrDefault("heatCapacity"), value.Integers.GetValueOrDefault("heatPerShot"));

    private static AmmunitionDefinition CompileAmmunition(SourceDefinition value) => new(
        new AmmunitionId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["weightHundredthsOfPound"], value.Integers["value"], value.Integers["maximumStackSize"],
        Sort(value.Arrays["tags"]),
        ParseAmmunitionType(value.Strings["ammunitionType"]),
        ParseRangedWeaponTechnology(value.Strings["technology"]),
        value.Integers["damagePercentage"], value.Integers["armorDamagePercentage"],
        value.Integers["armorPenetrationModifier"], value.Integers["rangeModifier"]);

    private static RangedWeaponActionDefinition CompileRangedWeaponAction(SourceDefinition value) => new(
        new RangedWeaponActionId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Strings["kind"] == "reload" ? RangedWeaponActionKind.Reload : RangedWeaponActionKind.Attack,
        value.Integers["actionPointCost"], value.Integers["staminaCostModifier"],
        value.Integers["hitModifier"], value.Integers["damagePercentage"],
        value.Integers["ammunitionCost"], value.Integers["shotCount"],
        value.Integers["energyCostModifier"], value.Integers["heatModifier"],
        value.Integers["durabilityCost"], value.Integers["rangePenaltyPerUnit"],
        value.Integers["damageFalloffPerUnitPercentage"], value.Integers["reloadAmount"],
        CompileEffectApplications(value.Arrays["effectIds"]));

    private static ImmutableArray<EffectApplicationDefinition> CompileEffectApplications(
        ImmutableArray<string> effectIds) =>
        [.. Sort(effectIds).Select(value => EffectApplicationDefinition.InstantTarget(new EffectId(value)))];

    private static EffectDefinition CompileEffect(SourceDefinition value)
    {
        EffectType type = ParseEffectType(value.Strings["type"]);
        int amount = value.Integers["amount"];
        int duration = value.Integers.GetValueOrDefault("duration");
        EffectPayload payload = type switch
        {
            EffectType.HealHealth => new ResourceEffectPayload(type, CharacterResourceIds.Health, amount),
            EffectType.RestoreMana => new ResourceEffectPayload(type, CharacterResourceIds.Mana, amount),
            EffectType.RestoreStamina => new ResourceEffectPayload(type, CharacterResourceIds.Stamina, amount),
            EffectType.RestoreResolve => new ResourceEffectPayload(type, CharacterResourceIds.Resolve, amount),
            EffectType.ReduceStrain => new ResourceEffectPayload(type, CharacterResourceIds.Strain, amount),
            EffectType.PhysicalDamage or EffectType.ThermalDamage or EffectType.ShockDamage or EffectType.ArcaneDamage or
                EffectType.ArmorDamage => new DamageEffectPayload(type, amount),
            EffectType.ApplyStatus => new ApplyStatusEffectPayload(
                new StatusId(value.Strings["statusId"]),
                duration == 0 ? null : duration,
                value.Integers["stacks"],
                value.Integers.GetValueOrDefault("potency")),
            EffectType.RemoveStatus => new RemoveStatusEffectPayload(new StatusId(value.Strings["statusId"])),
            EffectType.ModifyDamage or EffectType.ModifyDefense or EffectType.ModifyAccuracy or
                EffectType.ModifyMovement or EffectType.ModifyResistance => new ModifierEffectPayload(type, amount),
            EffectType.GrantShield => new GrantShieldEffectPayload(amount),
            EffectType.EmitEvent => new EmitEventEffectPayload(new ContentId(value.Strings["eventId"])),
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
        return new EffectDefinition(
            new EffectId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey, payload);
    }

    private static StatusDefinition CompileStatus(SourceDefinition value) => new(
        new StatusId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        ParseStatusCategory(value.Strings["category"]), Sort(value.Arrays["tags"]),
        value.Integers["defaultDuration"], ParseStatusDurationType(value.Strings["durationType"]),
        ParseStatusStackPolicy(value.Strings["stackPolicy"]), value.Integers["maximumStacks"],
        value.Strings.TryGetValue("exclusiveGroupId", out string? groupId) ? new ContentId(groupId) : null,
        value.Integers["priority"],
        [.. value.Status!.Modifiers.Select(item => new StatusModifierDefinition(ParseStatusModifierType(item.Type), item.Amount))],
        [.. value.Status.Restrictions.Select(item => new StatusRestrictionDefinition(
            ParseStatusRestrictionType(item.Type), ParseStatusTargetRule(item.TargetRule),
            item.ActionTag is null ? null : new ContentId(item.ActionTag)))],
        [.. value.Status.AiRules.Select(item => new StatusAiRuleDefinition(
            ParseStatusAiRuleType(item.Type), ParseStatusTargetRule(item.TargetRule),
            item.ActionTag is null ? null : new ContentId(item.ActionTag), item.Amount))],
        Sort(value.Arrays["onApplyEffectIds"]).Select(item => new EffectId(item)).ToImmutableArray(),
        Sort(value.Arrays["onTickEffectIds"]).Select(item => new EffectId(item)).ToImmutableArray(),
        Sort(value.Arrays["onExpireEffectIds"]).Select(item => new EffectId(item)).ToImmutableArray());

    private static EffectType ParseEffectType(string value) => value switch
    {
        "heal-health" => EffectType.HealHealth,
        "restore-mana" => EffectType.RestoreMana,
        "restore-stamina" => EffectType.RestoreStamina,
        "restore-resolve" => EffectType.RestoreResolve,
        "reduce-strain" => EffectType.ReduceStrain,
        "physical-damage" => EffectType.PhysicalDamage,
        "thermal-damage" => EffectType.ThermalDamage,
        "shock-damage" => EffectType.ShockDamage,
        "arcane-damage" => EffectType.ArcaneDamage,
        "armor-damage" => EffectType.ArmorDamage,
        "apply-status" => EffectType.ApplyStatus,
        "remove-status" => EffectType.RemoveStatus,
        "modify-damage" => EffectType.ModifyDamage,
        "modify-defense" => EffectType.ModifyDefense,
        "modify-accuracy" => EffectType.ModifyAccuracy,
        "modify-movement" => EffectType.ModifyMovement,
        "modify-resistance" => EffectType.ModifyResistance,
        "grant-shield" => EffectType.GrantShield,
        "emit-event" => EffectType.EmitEvent,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static StatusCategory ParseStatusCategory(string value) => value switch
    {
        "mental" => StatusCategory.Mental,
        "emotion" => StatusCategory.Emotion,
        "control" => StatusCategory.Control,
        "disruption" => StatusCategory.Disruption,
        "damage-over-time" => StatusCategory.DamageOverTime,
        "defensive" => StatusCategory.Defensive,
        "resource" => StatusCategory.Resource,
        "physical" => StatusCategory.Physical,
        "magical" => StatusCategory.Magical,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static StatusDurationType ParseStatusDurationType(string value) => value switch
    {
        "timed" => StatusDurationType.Timed,
        "permanent" => StatusDurationType.Permanent,
        "until-removed" => StatusDurationType.UntilRemoved,
        "conditional" => StatusDurationType.Conditional,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static StatusStackPolicy ParseStatusStackPolicy(string value) => value switch
    {
        "refresh" => StatusStackPolicy.Refresh,
        "extend" => StatusStackPolicy.Extend,
        "intensity-stack" => StatusStackPolicy.IntensityStack,
        "stronger-wins" => StatusStackPolicy.StrongerWins,
        "independent" => StatusStackPolicy.Independent,
        "reject" => StatusStackPolicy.Reject,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static StatusModifierType ParseStatusModifierType(string value) => value switch
    {
        "modify-damage" => StatusModifierType.ModifyDamage,
        "modify-defense" => StatusModifierType.ModifyDefense,
        "modify-accuracy" => StatusModifierType.ModifyAccuracy,
        "modify-movement" => StatusModifierType.ModifyMovement,
        "modify-resistance" => StatusModifierType.ModifyResistance,
        "modify-resolve" => StatusModifierType.ModifyResolve,
        "grant-shield" => StatusModifierType.GrantShield,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static StatusRestrictionType ParseStatusRestrictionType(string value) => value switch
    {
        "cannot-act" => StatusRestrictionType.CannotAct,
        "cannot-attack" => StatusRestrictionType.CannotAttack,
        "cannot-move" => StatusRestrictionType.CannotMove,
        "discourage-action" => StatusRestrictionType.DiscourageAction,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static StatusAiRuleType ParseStatusAiRuleType(string value) => value switch
    {
        "treat-as-ally" => StatusAiRuleType.TreatAsAlly,
        "prefer-target" => StatusAiRuleType.PreferTarget,
        "prefer-action" => StatusAiRuleType.PreferAction,
        "discourage-action" => StatusAiRuleType.DiscourageAction,
        "reduce-decision-reliability" => StatusAiRuleType.ReduceDecisionReliability,
        "unstable-target-selection" => StatusAiRuleType.UnstableTargetSelection,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static StatusTargetRule ParseStatusTargetRule(string? value) => value switch
    {
        null => StatusTargetRule.None,
        "status-source" => StatusTargetRule.StatusSource,
        "nearest-enemy" => StatusTargetRule.NearestEnemy,
        "source-enemies" => StatusTargetRule.SourceEnemies,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static RangedWeaponFamily ParseRangedWeaponFamily(string value) => value switch
    {
        "bow" => RangedWeaponFamily.Bow,
        "crossbow" => RangedWeaponFamily.Crossbow,
        "pistol" => RangedWeaponFamily.Pistol,
        "rifle" => RangedWeaponFamily.Rifle,
        "shotgun" => RangedWeaponFamily.Shotgun,
        "heavy" => RangedWeaponFamily.Heavy,
        "launcher" => RangedWeaponFamily.Launcher,
        "special" => RangedWeaponFamily.Special,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static RangedWeaponTechnology ParseRangedWeaponTechnology(string value) => value switch
    {
        "conventional" => RangedWeaponTechnology.Conventional,
        "ballistic" => RangedWeaponTechnology.Ballistic,
        "laser" => RangedWeaponTechnology.Laser,
        "plasma" => RangedWeaponTechnology.Plasma,
        "arcane" => RangedWeaponTechnology.Arcane,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static RangedWeaponHands ParseRangedWeaponHands(string value) => value switch
    {
        "one-handed" => RangedWeaponHands.OneHanded,
        "two-handed" => RangedWeaponHands.TwoHanded,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static AmmunitionType ParseAmmunitionType(string value) => value switch
    {
        "arrow" => AmmunitionType.Arrow,
        "crossbow-bolt" => AmmunitionType.CrossbowBolt,
        "pistol-round" => AmmunitionType.PistolRound,
        "rifle-round" => AmmunitionType.RifleRound,
        "shotgun-shell" => AmmunitionType.ShotgunShell,
        "grenade" => AmmunitionType.Grenade,
        "rocket" => AmmunitionType.Rocket,
        "mini-nuke" => AmmunitionType.MiniNuke,
        "flamethrower-fuel" => AmmunitionType.FlamethrowerFuel,
        "laser-cell" => AmmunitionType.LaserCell,
        "plasma-cell" => AmmunitionType.PlasmaCell,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static BoardCellDefinition CompileBoardCell(SourceDefinition value) => new(
        new CellId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new ZoneId(value.Strings["zoneId"]), value.Integers["q"], value.Integers["r"],
        value.Integers["capacity"], value.Integers["cover"], value.Integers["visibility"],
        new ContentId(value.Strings["atmosphereId"]), new ContentId(value.Strings["gravityId"]),
        Sort(value.Arrays["hazardTags"]));

    private static ZoneLinkDefinition CompileZoneLink(SourceDefinition value) => new(
        new LinkId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new CellId(value.Strings["fromCellId"]), new CellId(value.Strings["toCellId"]),
        new ContentId(value.Strings["accessId"]),
        value.Integers["oneWay"], value.Integers["allowsRetreat"]);

    private static PersonalBoardDefinition CompilePersonalBoard(SourceDefinition value) => new(
        new PersonalBoardId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["maximumOccupants"],
        Sort(value.Arrays["cellIds"]).Select(item => new CellId(item)).ToImmutableArray(),
        Sort(value.Arrays["linkIds"]).Select(item => new LinkId(item)).ToImmutableArray(),
        Sort(value.Arrays["requiredObjectiveIds"]).Select(item => new ObjectiveId(item)).ToImmutableArray(),
        Sort(value.Arrays["retreatCellIds"]).Select(item => new CellId(item)).ToImmutableArray());

    private static EncounterDefinition CompileEncounter(SourceDefinition value) => new(
        new EncounterId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new PersonalBoardId(value.Strings["personalBoardId"]), new ContentId(value.Strings["contextId"]),
        new TeamId(value.Strings["hostileTeamId"]), new ContentId(value.Strings["ancientDefenseId"]),
        new ObjectiveId(value.Strings["nonCombatObjectiveId"]), new ObjectiveId(value.Strings["extractionObjectiveId"]));

    private static ShipFrameDefinition CompileShipFrame(SourceDefinition value) => new(
        new ShipFrameId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["maximumHull"], value.Integers["baseArmor"], value.Integers["maximumSlots"],
        value.Integers["cargoCapacity"], Sort(value.Arrays["mountIds"]).Select(item => new ContentId(item)).ToImmutableArray());

    private static ShipModuleDefinition CompileShipModule(SourceDefinition value) => new(
        new ModuleId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        value.Integers["slotCost"], value.Integers["cargoDisplacement"], value.Integers["maximumIntegrity"],
        new NetworkId(value.Strings["networkId"]), value.Integers["energyGeneration"], value.Integers["energyConsumption"],
        new ContentId(value.Strings["mountId"]), new ContentId(value.Strings["primaryEffectId"]),
        value.Integers["armorValue"], value.Integers["shieldValue"], value.Integers["shieldRechargeRate"],
        value.Integers["shieldEnergyConsumptionRate"],
        Sort(value.Arrays["compatiblePathIds"]).Select(item => new ContentId(item)).ToImmutableArray());

    private static ShipWeaponConfigurationDefinition CompileShipWeapon(SourceDefinition value) => new(
        new ShipWeaponConfigurationId(value.Id), 1, value.Revision, value.NameKey, value.DescriptionKey,
        new NetworkId(value.Strings["networkId"]), new ResourceId(value.Strings["resourceId"]),
        value.Integers["resourceCost"], value.Integers["damage"], value.Integers["rateOfFireTicks"],
        value.Integers["effectiveRange"], value.Integers["maximumRange"], value.Integers["reloadTicks"],
        new ContentId(value.Strings["damageTypeId"]), new ContentId(value.Strings["areaId"]),
        value.Integers["armorPenetration"]);

    private static ImmutableArray<string> Sort(ImmutableArray<string> values) => [.. values.Order(StringComparer.Ordinal)];

    private static ImmutableArray<string> OptionalArray(SourceDefinition value, string name) =>
        value.Arrays.TryGetValue(name, out ImmutableArray<string> values) ? values : [];
}
