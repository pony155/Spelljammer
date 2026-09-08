using System.Collections.Immutable;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Items;

/// <summary>Stackable ammunition item and its ranged-combat modifiers.</summary>
/// <remarks>
/// Code flow: Ammunition definitions enter the unified item catalog, reload resolves a stack entry and compatibility, then atomically consumes quantity while updating the weapon magazine.
/// </remarks>
public sealed record AmmunitionDefinition(
    AmmunitionId AmmunitionId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int WeightHundredthsOfPound,
    int Value,
    int MaximumStackSize,
    ImmutableArray<string> Tags,
    AmmunitionType AmmunitionType,
    RangedWeaponTechnology Technology,
    int DamagePercentage,
    int ArmorDamagePercentage,
    int ArmorPenetrationModifier,
    int RangeModifier)
    : ItemDefinition(AmmunitionId.Value, SchemaVersion, Revision, NameKey, DescriptionKey,
        ItemCategory.Ammunition, WeightHundredthsOfPound, Value, MaximumStackSize, Tags);
