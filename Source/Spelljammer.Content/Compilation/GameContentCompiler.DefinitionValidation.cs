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
/// Implements kind-specific numeric, collection, and semantic validation for definitions.
/// </summary>
/// <remarks>
/// Code flow: The validation pipeline dispatches each normalized definition by kind, specialized checks enforce authored bounds and invariants, and violations become stable diagnostics.
/// </remarks>
public sealed partial class GameContentCompiler
{
    private static void ValidateAbility(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        AbilitySourceDto source = definition.Ability!;
        int minimum = source.Minimum;
        int maximum = source.Maximum;
        int defaultValue = source.DefaultValue;
        bool storageValid = minimum >= short.MinValue && maximum <= short.MaxValue && minimum <= maximum &&
            defaultValue >= minimum && defaultValue <= maximum;
        bool baseRangeValid = definition.PackId != "spelljammer.base" || minimum == 1 && maximum == 10;
        if (!storageValid || !baseRangeValid)
        {
            OutOfRange(definition, "/defaultValue", diagnostics);
        }
    }

    private static void ValidateSkill(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        SkillSourceDto source = definition.Skill!;
        int minimum = source.Minimum;
        int maximum = source.Maximum;
        bool storageValid = minimum >= byte.MinValue && maximum <= byte.MaxValue && minimum <= maximum;
        bool baseRangeValid = definition.PackId != "spelljammer.base" || minimum == 0 && maximum == 100;
        if (!storageValid || !baseRangeValid)
        {
            OutOfRange(definition, "/minimum", diagnostics);
        }
    }

    private static void ValidateLevelProgressionTable(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        ImmutableArray<LevelProgressionEntry> entries = definition.LevelProgressionEntries;
        if (entries.IsEmpty)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                definition.Id.ToString(), "/levels");
            return;
        }

        int previousExperience = -1;
        for (int index = 0; index < entries.Length; index++)
        {
            LevelProgressionEntry entry = entries[index];
            string path = $"/levels/{index}";
            bool invalidLevel = entry.Level != index + 1;
            bool invalidExperience = index == 0
                ? entry.RequiredExperience != 0
                : entry.RequiredExperience <= previousExperience;
            bool invalidReward = entry.MaximumHealthIncrease < 0 || entry.MaximumManaIncrease < 0 || entry.MaximumStaminaIncrease < 0 ||
                entry.MaximumResolveIncrease < 0 || entry.MaximumStrainIncrease < 0 ||
                entry.AbilityPoints < 0 || entry.SkillPoints < 0 || entry.FeatPoints < 0;
            if (invalidLevel || invalidExperience || invalidReward)
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                    definition.Id.ToString(), path);
            }

            previousExperience = entry.RequiredExperience;
        }
    }

    private static void ValidateCharacterResourceProfile(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string[] prefixes = ["health", "stamina", "mana", "resolve", "strain"];
        if (prefixes.Any(prefix => definition.Integers[prefix + "Maximum"] <= 0 ||
                definition.Integers[prefix + "RecoveryRate"] < 0))
        {
            diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, definition.PackId, definition.RelativePath,
                definition.Id.ToString(), "/resources");
        }

        ImmutableArray<int> resolve = definition.IntegerArrays["resolveThresholdPercentages"];
        ImmutableArray<int> strain = definition.IntegerArrays["strainThresholdPercentages"];
        bool invalidResolve = resolve.IsEmpty || resolve.Any(value => value is < 0 or > 100) ||
            resolve.Zip(resolve.Skip(1)).Any(pair => pair.First <= pair.Second);
        bool invalidStrain = strain.IsEmpty || strain.Any(value => value is < 0 or > 100) ||
            strain.Zip(strain.Skip(1)).Any(pair => pair.First >= pair.Second);
        if (invalidResolve || invalidStrain)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                definition.Id.ToString(), invalidResolve ? "/resolveThresholdPercentages" : "/strainThresholdPercentages");
        }

        ImmutableArray<int> staminaThresholds = definition.IntegerArrays["staminaTurnMeterThresholdPercentages"];
        ImmutableArray<int> staminaGains = definition.IntegerArrays["staminaTurnMeterGainPercentages"];
        ImmutableArray<string> actionIds = definition.Arrays["actionPointCostIds"];
        ImmutableArray<int> actionCosts = definition.IntegerArrays["actionPointCosts"];
        string[] requiredActionIds =
        [
            "action.personal.defend",
            "action.personal.engineering",
            "action.personal.interact",
            "action.personal.medicine",
            "action.personal.melee",
            "action.personal.move",
            "action.personal.psionic",
            "action.personal.ranged",
            "action.personal.reserve-reaction",
            "action.personal.retreat",
            "action.personal.spell",
            "action.personal.surrender",
        ];
        bool invalidTurnRules = definition.Integers["turnMeterThreshold"] <= 0 ||
            definition.Integers["baseTurnMeterGain"] <= 0 ||
            definition.Integers["baseActionPoints"] <= 0 ||
            definition.Integers["normalTurnMeterGainPercentage"] <= 0 ||
            staminaThresholds.IsEmpty || staminaThresholds.Length != staminaGains.Length ||
            staminaThresholds.Any(value => value is < 0 or > 100) ||
            staminaThresholds.Zip(staminaThresholds.Skip(1)).Any(pair => pair.First <= pair.Second) ||
            staminaGains.Any(value => value is < 0 or > 100) ||
            actionIds.IsEmpty || actionIds.Length != actionCosts.Length ||
            actionIds.Distinct(StringComparer.Ordinal).Count() != actionIds.Length ||
            !actionIds.Order(StringComparer.Ordinal).SequenceEqual(requiredActionIds) ||
            actionCosts.Any(value => value <= 0 || value > definition.Integers["baseActionPoints"]);
        if (invalidTurnRules)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, definition.PackId, definition.RelativePath,
                definition.Id.ToString(), "/turnRules");
        }
    }

    private static void ValidateFeat(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string activation = definition.Strings["activation"];
        definition.Strings.TryGetValue("activeKind", out string? activeKind);
        if (activation == "passive")
        {
            if (activeKind is not null)
            {
                Invalid("/activeKind");
            }

            return;
        }

        if (activation != "active" || activeKind is not ("general" or "spell" or "psionic"))
        {
            Invalid("/activation");
            return;
        }

        if (activeKind == "spell")
        {
            if (!HasStrings("skillId", "manaResourceId", "rangeId") ||
                !HasIntegers("manaCost", "castTimeTicks", "cooldownTicks") ||
                definition.Integers.GetValueOrDefault("manaCost") is < 1 or > 1_000_000 ||
                definition.Integers.GetValueOrDefault("castTimeTicks") is < 0 or > 10_000 ||
                definition.Integers.GetValueOrDefault("cooldownTicks") is < 0 or > 1_000_000 ||
                definition.Arrays["targetTags"].IsEmpty || definition.Arrays["effectIds"].IsEmpty)
            {
                Invalid("/activeKind");
            }
        }
        else if (activeKind == "psionic")
        {
            if (!HasStrings("skillId", "resistanceSkillId", "strainResourceId", "contactModeId", "rangeId", "informationScopeId") ||
                !HasIntegers("strainCost", "sustainCostPerTick") ||
                definition.Integers.GetValueOrDefault("strainCost") is < 1 or > 100 ||
                definition.Integers.GetValueOrDefault("sustainCostPerTick") is < 0 or > 100 ||
                definition.Arrays["disciplineIds"].IsEmpty || definition.Arrays["targetTags"].IsEmpty ||
                definition.Arrays["effectIds"].IsEmpty)
            {
                Invalid("/activeKind");
            }
        }

        return;

        bool HasStrings(params string[] fields) => fields.All(definition.Strings.ContainsKey);
        bool HasIntegers(params string[] fields) => fields.All(definition.Integers.ContainsKey);
        void Invalid(string property) => diagnostics.Add(
            ContentDiagnosticCodes.SemanticInvalid,
            definition.PackId,
            definition.RelativePath,
            definition.Id.ToString(),
            property);
    }

    private static void ValidateEquipment(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string kind = definition.Strings["kind"];
        bool commonInvalid = kind is not ("armor" or "gear") ||
            definition.Integers["weightHundredthsOfPound"] is < 0 or > 1_000_000 ||
            definition.Integers["value"] is < 0 or > 1_000_000;
        bool armorInvalid = kind == "armor" &&
            (!definition.Integers.TryGetValue("armorValue", out int armorValue) || armorValue is < 0 or > 1_000_000 ||
             !definition.Integers.TryGetValue("durabilityMaximum", out int durability) || durability is < 1 or > 1_000_000);
        if (commonInvalid || armorInvalid)
        {
            OutOfRange(definition, kind is not ("armor" or "gear") ? "/kind" : "/weightHundredthsOfPound", diagnostics);
        }
    }

    private static void ValidateMeleeWeapon(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        bool enumInvalid = definition.Strings["family"] is not ("blade" or "dagger" or "axe" or "blunt" or "polearm" or "fist") ||
            definition.Strings["technology"] is not ("conventional" or "chain" or "shock" or "powered" or "arcane") ||
            definition.Strings["hands"] is not ("one-handed" or "two-handed");
        bool numberInvalid = definition.Integers["damageMinimum"] is < 0 or > 1_000_000 ||
            definition.Integers["damageMaximum"] < definition.Integers["damageMinimum"] ||
            definition.Integers["damageMaximum"] > 1_000_000 ||
            definition.Integers["armorDamagePercentage"] is < 0 or > 1_000 ||
            definition.Integers["armorPenetrationPercentage"] is < 0 or > 100 ||
            definition.Integers["staminaCost"] is < 0 or > 1_000_000 ||
            definition.Integers["range"] is < 1 or > 1_000_000 ||
            definition.Integers["weightHundredthsOfPound"] is < 0 or > 1_000_000 ||
            definition.Integers["maximumDurability"] is < 1 or > 1_000_000 ||
            definition.Integers["value"] is < 0 or > 1_000_000 ||
            definition.Integers.GetValueOrDefault("energyCapacity") is < 0 or > 1_000_000 ||
            definition.Integers.GetValueOrDefault("energyPerAttack") < 0 ||
            definition.Integers.GetValueOrDefault("energyPerAttack") > definition.Integers.GetValueOrDefault("energyCapacity") ||
            definition.Integers.GetValueOrDefault("unpoweredDamagePercentage") is < 0 or > 100 ||
            definition.Integers.GetValueOrDefault("unpoweredArmorPenetrationPercentage") is < 0 or > 100 ||
            definition.Integers["strengthDamageScale"] is < 0 or > 10_000;
        if (enumInvalid || numberInvalid)
        {
            OutOfRange(definition, enumInvalid ? "/family" : "/damageMinimum", diagnostics);
        }
    }

    private static void ValidateMeleeWeaponAction(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        if (definition.Integers["actionPointCost"] is < 1 or > 1_000_000 ||
            definition.Integers["staminaCostModifier"] is < -1_000_000 or > 1_000_000 ||
            definition.Integers["hitModifier"] is < -10_000 or > 10_000 ||
            definition.Integers["damagePercentage"] is < 1 or > 1_000 ||
            definition.Integers["armorDamagePercentage"] is < 0 or > 1_000 ||
            definition.Integers["armorPenetrationModifier"] is < -100 or > 100 ||
            definition.Integers["rangeModifier"] is < -1_000_000 or > 1_000_000 ||
            definition.Integers["energyCostModifier"] is < -1_000_000 or > 1_000_000 ||
            definition.Integers["durabilityCost"] is < 0 or > 1_000_000)
        {
            OutOfRange(definition, "/actionPointCost", diagnostics);
        }
    }

    private static void ValidateRangedWeapon(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string family = definition.Strings["family"];
        string technology = definition.Strings["technology"];
        bool enumInvalid = family is not ("bow" or "crossbow" or "pistol" or "rifle" or "shotgun" or "heavy" or "launcher" or "special") ||
            technology is not ("conventional" or "ballistic" or "laser" or "plasma" or "arcane") ||
            definition.Strings["hands"] is not ("one-handed" or "two-handed") ||
            !IsRangedCombinationAllowed(family, technology);
        int energyCapacity = definition.Integers.GetValueOrDefault("energyCapacity");
        int energyPerShot = definition.Integers.GetValueOrDefault("energyPerShot");
        int heatCapacity = definition.Integers.GetValueOrDefault("heatCapacity");
        int heatPerShot = definition.Integers.GetValueOrDefault("heatPerShot");
        int magazineCapacity = definition.Integers.GetValueOrDefault("magazineCapacity");
        bool hasAmmunition = definition.Strings.ContainsKey("ammunitionType");
        bool numberInvalid = definition.Integers["damageMinimum"] is < 0 or > 1_000_000 ||
            definition.Integers["damageMaximum"] < definition.Integers["damageMinimum"] ||
            definition.Integers["damageMaximum"] > 1_000_000 ||
            definition.Integers["armorDamagePercentage"] is < 0 or > 1_000 ||
            definition.Integers["armorPenetrationPercentage"] is < 0 or > 100 ||
            definition.Integers["staminaCost"] is < 0 or > 1_000_000 ||
            definition.Integers["optimalRange"] is < 0 or > 1_000_000 ||
            definition.Integers["maximumRange"] < definition.Integers["optimalRange"] ||
            definition.Integers["maximumRange"] > 1_000_000 ||
            definition.Integers["weightHundredthsOfPound"] is < 0 or > 1_000_000 ||
            definition.Integers["maximumDurability"] is < 1 or > 1_000_000 ||
            definition.Integers["value"] is < 0 or > 1_000_000 ||
            magazineCapacity is < 0 or > 1_000_000 ||
            energyCapacity is < 0 or > 1_000_000 || energyPerShot < 0 || energyPerShot > energyCapacity ||
            heatCapacity is < 0 or > 1_000_000 || heatPerShot < 0 || heatPerShot > heatCapacity ||
            (!hasAmmunition && magazineCapacity != 0) || (hasAmmunition && energyCapacity != 0);
        if (enumInvalid || numberInvalid)
        {
            OutOfRange(definition, enumInvalid ? "/family" : "/damageMinimum", diagnostics);
        }
    }

    private static void ValidateAmmunition(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        bool enumInvalid = !IsAmmunitionType(definition.Strings["ammunitionType"]) ||
            definition.Strings["technology"] is not ("conventional" or "ballistic" or "laser" or "plasma" or "arcane");
        bool numberInvalid = definition.Integers["damagePercentage"] is < 1 or > 1_000 ||
            definition.Integers["armorDamagePercentage"] is < 0 or > 1_000 ||
            definition.Integers["armorPenetrationModifier"] is < -100 or > 100 ||
            definition.Integers["rangeModifier"] is < -1_000_000 or > 1_000_000 ||
            definition.Integers["maximumStackSize"] is < 2 or > 1_000_000 ||
            definition.Integers["weightHundredthsOfPound"] is < 0 or > 1_000_000 ||
            definition.Integers["value"] is < 0 or > 1_000_000;
        if (enumInvalid || numberInvalid)
        {
            OutOfRange(definition, enumInvalid ? "/ammunitionType" : "/damagePercentage", diagnostics);
        }
    }

    private static void ValidateRangedWeaponAction(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string kind = definition.Strings["kind"];
        bool commonInvalid = kind is not ("attack" or "reload") ||
            definition.Integers["actionPointCost"] is < 1 or > 1_000_000 ||
            definition.Integers["staminaCostModifier"] is < -1_000_000 or > 1_000_000 ||
            definition.Integers["hitModifier"] is < -10_000 or > 10_000 ||
            definition.Integers["ammunitionCost"] is < 0 or > 1_000_000 ||
            definition.Integers["energyCostModifier"] is < -1_000_000 or > 1_000_000 ||
            definition.Integers["heatModifier"] is < -1_000_000 or > 1_000_000 ||
            definition.Integers["durabilityCost"] is < 0 or > 1_000_000 ||
            definition.Integers["rangePenaltyPerUnit"] is < 0 or > 10_000 ||
            definition.Integers["damageFalloffPerUnitPercentage"] is < 0 or > 100 ||
            definition.Integers["reloadAmount"] is < 0 or > 1_000_000;
        bool kindInvalid = kind == "attack"
            ? definition.Integers["damagePercentage"] is < 1 or > 1_000 ||
              definition.Integers["shotCount"] is < 1 or > 1_000 || definition.Integers["reloadAmount"] != 0
            : definition.Integers["damagePercentage"] != 0 || definition.Integers["shotCount"] != 0 ||
              definition.Integers["ammunitionCost"] != 0 || definition.Integers["reloadAmount"] < 1;
        if (commonInvalid || kindInvalid)
        {
            OutOfRange(definition, "/kind", diagnostics);
        }
    }

    private static void ValidateEffect(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string type = definition.Strings["type"];
        bool knownType = type is "heal-health" or "restore-mana" or "restore-stamina" or "restore-resolve" or
            "reduce-strain" or "physical-damage" or "thermal-damage" or "shock-damage" or "arcane-damage" or
            "armor-damage" or "apply-status" or "remove-status" or "modify-damage" or "modify-defense" or
            "modify-accuracy" or "modify-movement" or "modify-resistance" or "grant-shield" or "emit-event";
        int amount = definition.Integers["amount"];
        int duration = definition.Integers.GetValueOrDefault("duration");
        int stacks = definition.Integers.GetValueOrDefault("stacks");
        int potency = definition.Integers.GetValueOrDefault("potency");
        bool statusOperation = type is "apply-status" or "remove-status";
        bool applyStatus = type == "apply-status";
        bool emitEvent = type == "emit-event";
        bool invalid = !knownType || amount is < 0 or > 1_000_000 || duration is < 0 or > 1_000_000 ||
            stacks is < 0 or > 1_000_000 || potency is < 0 or > 1_000_000 ||
            statusOperation != definition.Strings.ContainsKey("statusId") ||
            emitEvent != definition.Strings.ContainsKey("eventId") ||
            (statusOperation || emitEvent) && amount != 0 ||
            applyStatus && stacks < 1 || !applyStatus && (duration != 0 || stacks != 0 || potency != 0);
        if (invalid)
        {
            OutOfRange(definition, "/type", diagnostics);
        }
    }

    private static void ValidateStatus(SourceDefinition definition, DiagnosticSink diagnostics)
    {
        string category = definition.Strings["category"];
        string durationType = definition.Strings["durationType"];
        string stackPolicy = definition.Strings["stackPolicy"];
        int defaultDuration = definition.Integers["defaultDuration"];
        bool invalid = category is not ("mental" or "emotion" or "control" or "disruption" or
                "damage-over-time" or "defensive" or "resource" or "physical" or "magical") ||
              durationType is not ("timed" or "permanent" or "until-removed" or "conditional") ||
              stackPolicy is not ("refresh" or "extend" or "intensity-stack" or "stronger-wins" or "independent" or "reject") ||
              (durationType == "timed" ? defaultDuration is < 1 or > 1_000_000 : defaultDuration != 0) ||
              definition.Integers["maximumStacks"] is < 1 or > 1_000_000 ||
              stackPolicy == "extend" && durationType != "timed" ||
              stackPolicy != "intensity-stack" && definition.Integers["maximumStacks"] != 1 ||
              definition.Integers["priority"] is < -1_000_000 or > 1_000_000 ||
            definition.Status is null ||
            definition.Status.Modifiers.Any(value =>
                value.Type is not ("modify-damage" or "modify-defense" or "modify-accuracy" or "modify-movement" or
                    "modify-resistance" or "modify-resolve" or "grant-shield") ||
                value.Amount is < -1_000_000 or > 1_000_000) ||
            definition.Status.Restrictions.Any(value =>
                value.Type is not ("cannot-act" or "cannot-attack" or "cannot-move" or "discourage-action") ||
                value.TargetRule is not (null or "status-source" or "nearest-enemy" or "source-enemies")) ||
            definition.Status.AiRules.Any(value =>
                value.Type is not ("treat-as-ally" or "prefer-target" or "prefer-action" or "discourage-action" or
                    "reduce-decision-reliability" or "unstable-target-selection") ||
                value.TargetRule is not (null or "status-source" or "nearest-enemy" or "source-enemies") ||
                value.Amount is < -1_000_000 or > 1_000_000);
        if (invalid)
        {
            OutOfRange(definition, "/category", diagnostics);
        }
    }

    private static bool IsRangedCombinationAllowed(string family, string technology) => family switch
    {
        "bow" or "crossbow" => technology is "conventional" or "arcane",
        "pistol" or "rifle" or "heavy" => technology is "ballistic" or "laser" or "plasma",
        "shotgun" or "launcher" or "special" => technology == "ballistic",
        _ => false,
    };

    private static bool IsAmmunitionType(string value) => value is
        "arrow" or "crossbow-bolt" or "pistol-round" or "rifle-round" or "shotgun-shell" or
        "grenade" or "rocket" or "mini-nuke" or "flamethrower-fuel" or "laser-cell" or "plasma-cell";

    private static void ValidateRace(SourceDefinition race, IReadOnlyDictionary<string, SourceDefinition> byId, DiagnosticSink diagnostics)
    {
        foreach (string featId in race.Arrays["grantedFeatIds"])
        {
            SourceDefinition feat = byId[featId];
            if (!feat.Arrays["compatibleRaceIds"].Contains(race.Id.ToString(), StringComparer.Ordinal))
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, race.PackId, race.RelativePath, race.Id.ToString(), "/grantedFeatIds");
            }
        }
    }

    private static void ValidateHeritage(SourceDefinition heritage, IReadOnlyDictionary<string, SourceDefinition> byId, DiagnosticSink diagnostics)
    {
        string raceId = heritage.Strings["raceId"];
        foreach (string featId in heritage.Arrays["grantedFeatIds"])
        {
            SourceDefinition feat = byId[featId];
            if (!feat.Arrays["compatibleRaceIds"].Contains(raceId, StringComparer.Ordinal))
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, heritage.PackId, heritage.RelativePath,
                    heritage.Id.ToString(), "/grantedFeatIds");
            }
        }
    }

    private static void ValidateCharacter(SourceDefinition character, IReadOnlyDictionary<string, SourceDefinition> byId, DiagnosticSink diagnostics)
    {
        string raceId = character.Strings["raceId"];
        SourceDefinition race = byId[raceId];
        SourceDefinition heritage = byId[character.Strings["heritageId"]];
        SourceDefinition background = byId[character.Strings["backgroundId"]];
        if (heritage.Strings["raceId"] != raceId)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, character.PackId, character.RelativePath,
                character.Id.ToString(), "/heritageId");
        }

        if (!background.Arrays["compatibleRaceIds"].Contains(raceId, StringComparer.Ordinal))
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, character.PackId, character.RelativePath,
                character.Id.ToString(), "/backgroundId");
        }

        if (race.Arrays["requiredSupportIds"].IsEmpty)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, character.PackId, character.RelativePath,
                character.Id.ToString(), "/raceId");
        }

        if (character.Integers["inventoryMaximumWeightHundredthsOfPound"] <= 0 ||
            character.Integers["inventoryMaximumEntries"] <= 0)
        {
            diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, character.PackId, character.RelativePath,
                character.Id.ToString(), "/inventory");
        }
    }

    private static void CheckItemReferences(
        SourceDefinition definition,
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, SourceDefinition> byId,
        string property,
        DiagnosticSink diagnostics)
    {
        foreach (string id in ids)
        {
            if (!byId.TryGetValue(id, out SourceDefinition? target) ||
                target.Kind is not (DefinitionKind.Equipment or DefinitionKind.MeleeWeapon or DefinitionKind.RangedWeapon))
            {
                Unknown(definition, id, property, diagnostics);
            }
        }
    }

    private void ValidateBoard(
        SourceDefinition board,
        IReadOnlyDictionary<string, SourceDefinition> byId,
        DiagnosticSink diagnostics)
    {
        HashSet<string> cells = board.Arrays["cellIds"].ToHashSet(StringComparer.Ordinal);
        if (board.Arrays["retreatCellIds"].Any(value => !cells.Contains(value)))
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, board.PackId, board.RelativePath,
                board.Id.ToString(), "/retreatCellIds");
            return;
        }

        Dictionary<string, List<string>> adjacent = cells.ToDictionary(value => value, _ => new List<string>(), StringComparer.Ordinal);
        foreach (string linkId in board.Arrays["linkIds"])
        {
            if (!byId.TryGetValue(linkId, out SourceDefinition? link))
            {
                continue;
            }

            string from = link.Strings["fromCellId"];
            string to = link.Strings["toCellId"];
            if (!cells.Contains(from) || !cells.Contains(to))
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, board.PackId, board.RelativePath,
                    board.Id.ToString(), "/linkIds");
                return;
            }

            adjacent[from].Add(to);
            if (link.Integers["oneWay"] == 0)
            {
                adjacent[to].Add(from);
            }
        }

        foreach (string retreatCell in board.Arrays["retreatCellIds"])
        {
            bool hasRetreatLink = board.Arrays["linkIds"].Any(linkId =>
                byId.TryGetValue(linkId, out SourceDefinition? link) &&
                link.Integers["allowsRetreat"] == 1 &&
                (link.Strings["fromCellId"] == retreatCell || link.Strings["toCellId"] == retreatCell));
            if (!hasRetreatLink)
            {
                diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, board.PackId, board.RelativePath,
                    board.Id.ToString(), "/retreatCellIds");
                return;
            }
        }

        if (cells.Count == 0)
        {
            return;
        }

        HashSet<string> reached = [];
        Queue<string> pending = new();
        pending.Enqueue(cells.Order(StringComparer.Ordinal).First());
        while (pending.Count > 0 && reached.Count <= limits.GraphNodes)
        {
            string current = pending.Dequeue();
            if (!reached.Add(current))
            {
                continue;
            }

            foreach (string next in adjacent[current].Order(StringComparer.Ordinal))
            {
                pending.Enqueue(next);
            }
        }

        if (reached.Count != cells.Count)
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, board.PackId, board.RelativePath,
                board.Id.ToString(), "/linkIds");
        }
    }

    private void ValidateGrantCycles(IReadOnlyList<SourceDefinition> definitions, DiagnosticSink diagnostics)
    {
        Dictionary<string, SourceDefinition> grantNodes = definitions
            .Where(value => value.Kind == DefinitionKind.Feat)
            .ToDictionary(value => value.Id.ToString(), StringComparer.Ordinal);
        Dictionary<string, byte> marks = new(StringComparer.Ordinal);
        foreach (SourceDefinition node in grantNodes.Values.OrderBy(value => value.Id))
        {
            Visit(node, 0);
        }

        void Visit(SourceDefinition node, int depth)
        {
            if (depth >= limits.ValidationTraversalDepth)
            {
                diagnostics.Limit("grant-depth", node.PackId, node.RelativePath);
                return;
            }

            string id = node.Id.ToString();
            if (marks.TryGetValue(id, out byte mark))
            {
                if (mark == 1)
                {
                    diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, node.PackId, node.RelativePath, id, "/grants");
                }

                return;
            }

            marks[id] = 1;
            IEnumerable<string> successors = node.Arrays["grantedFeatIds"];
            foreach (string successor in successors.Order(StringComparer.Ordinal))
            {
                if (grantNodes.TryGetValue(successor, out SourceDefinition? target))
                {
                    Visit(target, depth + 1);
                }
            }

            marks[id] = 2;
        }
    }
}
