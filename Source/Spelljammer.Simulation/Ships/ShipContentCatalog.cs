using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Ships;

/// <summary>Definitions consumed by ship construction and ship combat.</summary>
public interface IShipContentCatalog : IContentCatalogIdentity
{
    ImmutableArray<ShipFrameDefinition> ShipFrames { get; }
    ImmutableArray<ShipModuleDefinition> ShipModules { get; }
    ImmutableArray<ShipWeaponConfigurationDefinition> ShipWeaponConfigurations { get; }

    bool TryGetShipFrame(ShipFrameId id, out ShipFrameDefinition? definition);
    bool TryGetShipModule(ModuleId id, out ShipModuleDefinition? definition);
    bool TryGetShipWeaponConfiguration(ShipWeaponConfigurationId id, out ShipWeaponConfigurationDefinition? definition);
}
