using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

/// <summary>
/// Defines the immutable tactical board and validates bounded occupancy, movement, and paths.
/// </summary>
/// <remarks>
/// Code flow: Board definitions create indexed cells and links, unit placement is validated against capacity, and movement or path queries return replacement occupancy or an explicit rejection.
/// </remarks>
public sealed record BoardValidationResult(TacticalBoard? Board, string RejectionCode)
{
    public bool Accepted => Board is not null;
}

/// <summary>Immutable logical board used for bounded occupancy, movement, and pathfinding.</summary>
public sealed record TacticalBoard(
    PersonalBoardDefinition Definition,
    ImmutableDictionary<CellId, BoardCellDefinition> Cells,
    ImmutableArray<ZoneLinkDefinition> Links,
    ImmutableDictionary<CellId, ImmutableArray<BattleUnitId>> Occupants)
{
    public const int MaximumCells = 256;
    public const int MaximumLinks = 1_024;

    public static BoardValidationResult Create(
        PersonalBoardDefinition definition,
        IEnumerable<BoardCellDefinition> cells,
        IEnumerable<ZoneLinkDefinition> links)
    {
        BoardCellDefinition[] orderedCells = [.. cells.OrderBy(value => value.CellId)];
        ZoneLinkDefinition[] orderedLinks = [.. links.OrderBy(value => value.LinkId)];
        if (orderedCells.Length is 0 or > MaximumCells || orderedLinks.Length > MaximumLinks ||
            definition.CellIds.Length != orderedCells.Length || definition.LinkIds.Length != orderedLinks.Length ||
            definition.MaximumOccupants is < 1 or > 256 || definition.RequiredObjectiveIds.IsEmpty ||
            definition.RetreatCellIds.IsEmpty || orderedCells.Select(value => value.CellId).Distinct().Count() != orderedCells.Length ||
            orderedCells.Select(value => (value.Q, value.R)).Distinct().Count() != orderedCells.Length)
        {
            return new BoardValidationResult(null, "encounter.board-invalid");
        }

        ImmutableDictionary<CellId, BoardCellDefinition> byId = orderedCells.ToImmutableDictionary(value => value.CellId);
        if (!definition.CellIds.All(byId.ContainsKey) || !definition.RetreatCellIds.All(byId.ContainsKey) ||
            orderedCells.Any(value => value.Capacity is < 1 or > 8 || value.Cover is < 0 or > 100 ||
                value.Visibility is < 0 or > 100 || value.HazardTags.Length > 64) ||
            orderedLinks.Any(value => !byId.ContainsKey(value.FromCellId) || !byId.ContainsKey(value.ToCellId) ||
                !value.AccessId.IsValid || value.FromCellId == value.ToCellId || value.OneWay is < 0 or > 1 ||
                value.AllowsRetreat is < 0 or > 1) ||
            definition.RetreatCellIds.Any(retreat => !orderedLinks.Any(link => link.AllowsRetreat == 1 &&
                (link.FromCellId == retreat || link.ToCellId == retreat))))
        {
            return new BoardValidationResult(null, "encounter.board-invalid");
        }

        TacticalBoard candidate = new(
            definition,
            byId,
            [.. orderedLinks],
            ImmutableDictionary<CellId, ImmutableArray<BattleUnitId>>.Empty);
        if (orderedCells.Skip(1).Any(value =>
            candidate.FindPath(orderedCells[0].CellId, value.CellId, MaximumCells).IsEmpty))
        {
            return new BoardValidationResult(null, "encounter.board-disconnected");
        }

        return new BoardValidationResult(candidate, string.Empty);
    }

    public TacticalBoard Place(BattleUnitId unitId, CellId cellId)
    {
        if (!Cells.TryGetValue(cellId, out BoardCellDefinition? cell) ||
            Occupants.Values.SelectMany(value => value).Contains(unitId))
        {
            throw new InvalidOperationException("Encounter placement is invalid.");
        }

        ImmutableArray<BattleUnitId> occupants = Occupants.GetValueOrDefault(cellId, []);
        if (occupants.Length >= cell.Capacity || Occupants.Values.Sum(value => value.Length) >= Definition.MaximumOccupants)
        {
            throw new InvalidOperationException("Encounter placement exceeds capacity.");
        }

        return this with { Occupants = Occupants.SetItem(cellId, [.. occupants.Append(unitId).Order()]) };
    }

    public TacticalBoard Move(BattleUnitId unitId, CellId destination, int maximumVisited)
    {
        CellId origin = Occupants.Single(pair => pair.Value.Contains(unitId)).Key;
        if (FindPath(origin, destination, maximumVisited).IsEmpty)
        {
            throw new InvalidOperationException("No bounded legal path exists.");
        }

        TacticalBoard removed = this with {
            Occupants = Occupants.SetItem(origin, Occupants[origin].Remove(unitId)),
        };
        return removed.Place(unitId, destination);
    }

    public ImmutableArray<CellId> FindPath(CellId start, CellId goal, int maximumVisited)
    {
        if (!Cells.ContainsKey(start) || !Cells.ContainsKey(goal) || maximumVisited is < 1 or > MaximumCells)
        {
            return [];
        }

        Queue<CellId> frontier = new();
        Dictionary<CellId, CellId?> previous = [];
        frontier.Enqueue(start);
        previous[start] = null;
        while (frontier.Count > 0 && previous.Count <= maximumVisited)
        {
            CellId current = frontier.Dequeue();
            if (current == goal)
            {
                List<CellId> result = [];
                for (CellId? cursor = goal; cursor is CellId value; cursor = previous[value])
                {
                    result.Add(value);
                }

                result.Reverse();
                return [.. result];
            }

            foreach (CellId adjacent in Adjacent(current).Where(value => !previous.ContainsKey(value)).Order())
            {
                if (previous.Count >= maximumVisited)
                {
                    break;
                }

                previous[adjacent] = current;
                frontier.Enqueue(adjacent);
            }
        }

        return [];
    }

    private IEnumerable<CellId> Adjacent(CellId cellId)
    {
        foreach (ZoneLinkDefinition link in Links)
        {
            if (link.FromCellId == cellId)
            {
                yield return link.ToCellId;
            }

            if (link.OneWay == 0 && link.ToCellId == cellId)
            {
                yield return link.FromCellId;
            }
        }
    }
}
