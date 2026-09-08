using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Ships;

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
    ImmutableArray<ContentId> CompatiblePathIds)
    : ContentDefinition(ModuleId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

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
