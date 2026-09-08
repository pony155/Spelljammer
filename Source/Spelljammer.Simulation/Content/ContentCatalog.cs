using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Ships;
using Spelljammer.Simulation.World;

namespace Spelljammer.Simulation.Content;

/// <summary>Stable content identity shared by all authoritative catalog views.</summary>
/// <remarks>
/// Code flow: Content compilation produces specialized immutable catalogs with one fingerprint, aggregate consumers receive the combined interface, and simulation lookups resolve stable IDs through focused views.
/// </remarks>
public interface IContentCatalogIdentity
{
    ContentFingerprint Fingerprint { get; }
}

/// <summary>
/// Complete game-content view. Composition roots and persistence may use this aggregate; gameplay systems
/// should depend on a narrower domain catalog.
/// </summary>
public interface IGameContentCatalog :
    ICharacterCreationCatalog,
    ICombatContentCatalog,
    IEncounterContentCatalog,
    IShipContentCatalog,
    IWorldContentCatalog;
