using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Ships;

/// <summary>Definitions consumed by ship construction and ship combat.</summary>
/// <remarks>
/// Code flow: Compiled content implements this lookup boundary, ship systems resolve stable definition IDs through it, and missing or incompatible definitions reject the operation without mutation.
/// </remarks>
public interface IShipContentCatalog : IContentCatalogIdentity
{
    ImmutableArray<ShipFrameDefinition> ShipFrames { get; }
    ImmutableArray<ShipModuleDefinition> ShipModules { get; }
    ImmutableArray<ShipWeaponConfigurationDefinition> ShipWeaponConfigurations { get; }

    bool TryGetShipFrame(ShipFrameId id, out ShipFrameDefinition? definition);
    bool TryGetShipModule(ModuleId id, out ShipModuleDefinition? definition);
    bool TryGetShipWeaponConfiguration(ShipWeaponConfigurationId id, out ShipWeaponConfigurationDefinition? definition);
}
