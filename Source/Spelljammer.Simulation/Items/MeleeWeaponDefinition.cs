using System.Collections.Immutable;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

/// <summary>
/// Defines melee weapons by combining common item data with close-combat actions.
/// </summary>
/// <remarks>
/// Code flow: Compiled definitions enter the item catalog, equipment selects an instance, and melee combat resolves its family, reach, hands, and authored action data.
/// </remarks>
public sealed record MeleeWeaponDefinition(
    MeleeWeaponId MeleeWeaponId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    ImmutableArray<string> Tags,
    ImmutableArray<ContentId> OccupiedSlotIds,
    ImmutableArray<ContentId> ActionIds,
    MeleeWeaponFamily Family,
    MeleeWeaponTechnology Technology,
    MeleeWeaponHands Hands,
    SkillId SkillId,
    AbilityId AbilityId,
    int DamageMinimum,
    int DamageMaximum,
    int ArmorDamagePercentage,
    int ArmorPenetrationPercentage,
    int StaminaCost,
    int Range,
    int MaximumDurability,
    int EnergyCapacity,
    int EnergyPerAttack,
    int UnpoweredDamagePercentage,
    int UnpoweredArmorPenetrationPercentage,
    int StrengthDamageScale)
    : WeaponDefinition(MeleeWeaponId.Value, SchemaVersion, Revision, NameKey, DescriptionKey,
        WeightHundredthsOfPound, Value, Tags, OccupiedSlotIds, ActionIds);
