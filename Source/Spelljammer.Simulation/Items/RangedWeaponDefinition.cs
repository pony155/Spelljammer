using System.Collections.Immutable;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

/// <summary>
/// Defines ranged weapons by combining common item data with firing and ammunition rules.
/// </summary>
/// <remarks>
/// Code flow: Compiled definitions enter the item catalog, equipment selects an instance, and ranged combat resolves its actions, range, magazine, and compatible ammunition data.
/// </remarks>
public sealed record RangedWeaponDefinition(
    RangedWeaponId RangedWeaponId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    ImmutableArray<string> Tags,
    ImmutableArray<ContentId> OccupiedSlotIds,
    ImmutableArray<ContentId> ActionIds,
    RangedWeaponFamily Family,
    RangedWeaponTechnology Technology,
    RangedWeaponHands Hands,
    SkillId SkillId,
    int DamageMinimum,
    int DamageMaximum,
    int ArmorDamagePercentage,
    int ArmorPenetrationPercentage,
    int StaminaCost,
    int OptimalRange,
    int MaximumRange,
    int MaximumDurability,
    AmmunitionType? AmmunitionType,
    int MagazineCapacity,
    int EnergyCapacity,
    int EnergyPerShot,
    int HeatCapacity,
    int HeatPerShot)
    : WeaponDefinition(RangedWeaponId.Value, SchemaVersion, Revision, NameKey, DescriptionKey,
        WeightHundredthsOfPound, Value, Tags, OccupiedSlotIds, ActionIds);
