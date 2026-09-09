using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Ships;

/// <summary>
/// Defines data-driven ship frames, modules, slots, resources, and related capabilities.
/// </summary>
/// <remarks>
/// Code flow: Content compilation creates immutable definitions, catalogs index them by stable ID, and ship creation and subsystem rules resolve those definitions during simulation.
/// </remarks>
public sealed record ShipFrameDefinition(
    ShipFrameId ShipFrameId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int MaximumHull,
    int BaseArmor,
    int MaximumSlots,
    int CargoCapacity,
    ImmutableArray<ContentId> MountIds)
    : ContentDefinition(ShipFrameId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

public sealed record ShipModuleDefinition(
    ModuleId ModuleId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int SlotCost,
    int CargoDisplacement,
    int MaximumIntegrity,
    NetworkId NetworkId,
    int EnergyGeneration,
    int EnergyConsumption,
    ContentId MountId,
    ContentId PrimaryEffectId,
    int ArmorValue,
    int ShieldValue,
    int ShieldRechargeRate,
    int ShieldEnergyConsumptionRate,
    ImmutableArray<ContentId> CompatiblePathIds,
    PropulsionCostDefinition? Propulsion = null)
    : ContentDefinition(ModuleId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>Defines how an installed propulsion module converts a Starway fuel cost into a ship resource cost.</summary>
public sealed record PropulsionCostDefinition(
    ResourceId ResourceId,
    int CostNumerator,
    int CostDenominator)
{
    public int Calculate(int baseCost)
    {
        if (!ResourceId.IsValid || CostNumerator <= 0 || CostDenominator <= 0 || baseCost <= 0)
        {
            throw new InvalidOperationException("Propulsion cost definition is invalid.");
        }

        return checked((baseCost * CostNumerator + CostDenominator - 1) / CostDenominator);
    }
}

public sealed record ShipWeaponConfigurationDefinition(
    ShipWeaponConfigurationId ShipWeaponConfigurationId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    NetworkId NetworkId,
    ResourceId ResourceId,
    int ResourceCost,
    int Damage,
    int RateOfFireTicks,
    int EffectiveRange,
    int MaximumRange,
    int ReloadTicks,
    ContentId DamageTypeId,
    ContentId AreaId,
    int ArmorPenetration)
    : ContentDefinition(ShipWeaponConfigurationId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);
