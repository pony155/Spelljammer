using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

/// <summary>Definitions used to construct and validate character encounter boards.</summary>
/// <remarks>
/// Code flow: Compiled content implements this lookup boundary, encounter setup resolves board and cell definitions, and missing links or incompatible identities prevent board publication.
/// </remarks>
public interface IEncounterContentCatalog : IContentCatalogIdentity
{
    ImmutableArray<BoardCellDefinition> BoardCells { get; }
    ImmutableArray<ZoneLinkDefinition> ZoneLinks { get; }
    ImmutableArray<PersonalBoardDefinition> PersonalBoards { get; }
    ImmutableArray<EncounterDefinition> Encounters { get; }

    bool TryGetBoardCell(CellId id, out BoardCellDefinition? definition);
    bool TryGetZoneLink(LinkId id, out ZoneLinkDefinition? definition);
    bool TryGetPersonalBoard(PersonalBoardId id, out PersonalBoardDefinition? definition);
    bool TryGetEncounter(EncounterId id, out EncounterDefinition? definition);
}
