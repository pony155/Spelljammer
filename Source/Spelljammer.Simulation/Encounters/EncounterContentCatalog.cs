using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

/// <summary>Definitions used to construct and validate character encounter boards.</summary>
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
