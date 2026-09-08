using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Ships;

namespace Spelljammer.Simulation.Content;

/// <summary>Stable content identity shared by all authoritative catalog views.</summary>
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
    IShipContentCatalog;
