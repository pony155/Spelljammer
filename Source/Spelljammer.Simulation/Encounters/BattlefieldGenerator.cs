using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

/// <summary>Stable result codes returned by procedural battlefield generation.</summary>
public enum BattlefieldGenerationCode : byte
{
    None = 0,
    InvalidRequest = 1,
    InvalidSettings = 2,
    AttemptsExhausted = 3,
    BoardRejected = 4,
}

/// <summary>Bounded inputs that control a generated battlefield's logical layout.</summary>
public sealed record BattlefieldGenerationSettings(
    int Width = 12,
    int Height = 12,
    int RoomCount = 5,
    int MaximumAttempts = 8,
    int CoverPercent = 28,
    int HazardPercent = 8,
    int GeneratorVersion = BattlefieldGenerator.CurrentGeneratorVersion)
{
    public const int MinimumDimension = 8;
    public const int MaximumDimension = 16;
    public const int MinimumRoomCount = 3;
    public const int MaximumRoomCount = 12;
    public const int MaximumGenerationAttempts = 32;
}

/// <summary>Game-owned content references and identity used to generate one battlefield.</summary>
public sealed record BattlefieldGenerationRequest(
    PersonalBoardId BoardTemplateId,
    ContentFingerprint ContentFingerprint,
    ObjectiveId RequiredObjectiveId,
    ContentId AccessId,
    ContentId AtmosphereId,
    ContentId GravityId,
    string NameKey,
    string DescriptionKey,
    string CellNameKey,
    string CellDescriptionKey,
    int MaximumOccupants = 32);

/// <summary>An immutable battlefield and its validated mission-critical cells.</summary>
public sealed record BattlefieldGenerationResult(
    TacticalBoard? Board,
    CellId EntryCellId,
    CellId ObjectiveCellId,
    CellId ExtractionCellId,
    ulong Seed,
    int Attempt,
    BattlefieldGenerationCode Code,
    string RejectionCode)
{
    public bool Succeeded => Board is not null && Code == BattlefieldGenerationCode.None;
}

/// <summary>
/// Produces a deterministic, connected room-and-corridor battlefield using the current tactical-board contract.
/// </summary>
/// <remarks>
/// Topology, cover, and hazards use independent random streams. A candidate is fully validated before publication,
/// and failed candidates retry through a bounded deterministic policy.
/// </remarks>
public static class BattlefieldGenerator
{
    public const int CurrentGeneratorVersion = 1;

    private const int MaximumLocalizationKeyLength = 256;
    private const ulong TopologyStreamTag = 0x746f706f6c6f6779UL;
    private const ulong CoverStreamTag = 0x636f766572000001UL;
    private const ulong HazardStreamTag = 0x68617a6172640001UL;

    public static BattlefieldGenerationResult Generate(
        ulong seed,
        BattlefieldGenerationRequest? request,
        BattlefieldGenerationSettings? settings = null)
    {
        settings ??= new BattlefieldGenerationSettings();
        if (request is null || !IsValid(request))
        {
            return Failure(seed, BattlefieldGenerationCode.InvalidRequest, "encounter.battlefield-request-invalid");
        }

        if (!IsValid(settings))
        {
            return Failure(seed, BattlefieldGenerationCode.InvalidSettings, "encounter.battlefield-settings-invalid");
        }

        for (int attempt = 0; attempt < settings.MaximumAttempts; attempt++)
        {
            DeterministicStream topologyRandom = new(DeriveStreamSeed(seed, TopologyStreamTag, attempt));
            if (!TryCreateLayout(settings, ref topologyRandom, out GeneratedLayout layout))
            {
                continue;
            }

            BattlefieldGenerationResult result = CreateBoard(seed, request, settings, layout, attempt);
            if (result.Succeeded)
            {
                return result;
            }

            if (result.Code == BattlefieldGenerationCode.BoardRejected)
            {
                return result;
            }
        }

        return Failure(
            seed,
            BattlefieldGenerationCode.AttemptsExhausted,
            "encounter.battlefield-generation-exhausted",
            settings.MaximumAttempts);
    }

    private static BattlefieldGenerationResult CreateBoard(
        ulong seed,
        BattlefieldGenerationRequest request,
        BattlefieldGenerationSettings settings,
        GeneratedLayout layout,
        int attempt)
    {
        string identity = BattlefieldIdentity(seed, request, settings, attempt);
        GridCoordinate[] orderedCoordinates = [.. layout.Cells.OrderBy(value => value.X).ThenBy(value => value.Y)];
        Dictionary<GridCoordinate, CellId> cellIds = orderedCoordinates.ToDictionary(
            coordinate => coordinate,
            coordinate => new CellId($"cell.generated.{identity}.x{coordinate.X:00}.y{coordinate.Y:00}"));

        DeterministicStream coverRandom = new(DeriveStreamSeed(seed, CoverStreamTag, attempt));
        DeterministicStream hazardRandom = new(DeriveStreamSeed(seed, HazardStreamTag, attempt));
        BoardCellDefinition[] cells = new BoardCellDefinition[orderedCoordinates.Length];
        for (int index = 0; index < orderedCoordinates.Length; index++)
        {
            GridCoordinate coordinate = orderedCoordinates[index];
            int roomIndex = NearestRoomIndex(coordinate, layout.RoomCenters);
            int coverRoll = coverRandom.Next(0, 100);
            int cover = coverRoll < settings.CoverPercent / 4
                ? 100
                : coverRoll < settings.CoverPercent ? 50 : 0;
            ImmutableArray<string> hazards = hazardRandom.Next(0, 100) < settings.HazardPercent
                ? ["hazard.generated.unstable-ground"]
                : [];
            cells[index] = new BoardCellDefinition(
                cellIds[coordinate],
                1,
                1,
                request.CellNameKey,
                request.CellDescriptionKey,
                new ZoneId($"zone.generated.{identity}.r{roomIndex:00}"),
                coordinate.X,
                coordinate.Y,
                4,
                cover,
                hazards.IsEmpty ? 100 : 80,
                request.AtmosphereId,
                request.GravityId,
                hazards);
        }

        List<(GridCoordinate First, GridCoordinate Second)> adjacency = [];
        foreach (GridCoordinate coordinate in orderedCoordinates)
        {
            AddAdjacent(coordinate, new GridCoordinate(coordinate.X + 1, coordinate.Y));
            AddAdjacent(coordinate, new GridCoordinate(coordinate.X, coordinate.Y + 1));
        }

        GridCoordinate entry = SelectEntry(layout.Cells, settings.Height);
        Dictionary<GridCoordinate, int> entryDistances = DistancesFrom(entry, layout.Cells);
        GridCoordinate extraction = entryDistances
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => pair.Key.X)
            .ThenBy(pair => pair.Key.Y)
            .First().Key;
        Dictionary<GridCoordinate, int> extractionDistances = DistancesFrom(extraction, layout.Cells);
        GridCoordinate objective = orderedCoordinates
            .Where(value => value != entry && value != extraction)
            .OrderByDescending(value => Math.Min(entryDistances[value], extractionDistances[value]))
            .ThenByDescending(value => entryDistances[value])
            .ThenBy(value => value.X)
            .ThenBy(value => value.Y)
            .First();

        ZoneLinkDefinition[] links = new ZoneLinkDefinition[adjacency.Count];
        for (int index = 0; index < adjacency.Count; index++)
        {
            (GridCoordinate first, GridCoordinate second) = adjacency[index];
            bool allowsRetreat = first == entry || first == extraction || second == entry || second == extraction;
            links[index] = new ZoneLinkDefinition(
                new LinkId($"link.generated.{identity}.e{index:000}"),
                1,
                1,
                request.CellNameKey,
                request.CellDescriptionKey,
                cellIds[first],
                cellIds[second],
                request.AccessId,
                0,
                allowsRetreat ? 1 : 0);
        }

        PersonalBoardDefinition definition = new(
            new PersonalBoardId($"board.generated.{identity}"),
            1,
            1,
            request.NameKey,
            request.DescriptionKey,
            request.MaximumOccupants,
            [.. cells.Select(value => value.CellId).Order()],
            [.. links.Select(value => value.LinkId).Order()],
            [request.RequiredObjectiveId],
            [cellIds[entry], cellIds[extraction]]);
        BoardValidationResult validation = TacticalBoard.Create(definition, cells, links);
        return validation.Accepted
            ? new BattlefieldGenerationResult(
                validation.Board,
                cellIds[entry],
                cellIds[objective],
                cellIds[extraction],
                seed,
                attempt + 1,
                BattlefieldGenerationCode.None,
                string.Empty)
            : new BattlefieldGenerationResult(
                null,
                default,
                default,
                default,
                seed,
                attempt + 1,
                BattlefieldGenerationCode.BoardRejected,
                validation.RejectionCode);

        void AddAdjacent(GridCoordinate first, GridCoordinate second)
        {
            if (layout.Cells.Contains(second))
            {
                adjacency.Add((first, second));
            }
        }
    }

    private static bool TryCreateLayout(
        BattlefieldGenerationSettings settings,
        ref DeterministicStream random,
        out GeneratedLayout layout)
    {
        HashSet<GridCoordinate> cells = [];
        List<GridCoordinate> roomCenters = [];
        for (int roomIndex = 0; roomIndex < settings.RoomCount; roomIndex++)
        {
            int roomWidth = random.Next(2, Math.Min(5, settings.Width - 1));
            int roomHeight = random.Next(2, Math.Min(5, settings.Height - 1));
            int left = random.Next(1, settings.Width - roomWidth);
            int top = random.Next(1, settings.Height - roomHeight);
            GridCoordinate center = new(left + roomWidth / 2, top + roomHeight / 2);
            roomCenters.Add(center);
            for (int x = left; x < left + roomWidth; x++)
            {
                for (int y = top; y < top + roomHeight; y++)
                {
                    cells.Add(new GridCoordinate(x, y));
                }
            }

            if (roomIndex > 0)
            {
                GridCoordinate nearest = roomCenters.Take(roomIndex)
                    .OrderBy(value => ManhattanDistance(value, center))
                    .ThenBy(value => value.X)
                    .ThenBy(value => value.Y)
                    .First();
                CarveCorridor(cells, nearest, center, random.Next(0, 2) == 0);
            }
        }

        if (roomCenters.Count >= 3)
        {
            CarveCorridor(cells, roomCenters[0], roomCenters[^1], random.Next(0, 2) == 0);
        }

        int minimumCells = Math.Max(16, settings.Width * settings.Height / 5);
        if (cells.Count < minimumCells || cells.Count > TacticalBoard.MaximumCells)
        {
            layout = null!;
            return false;
        }

        GridCoordinate entry = SelectEntry(cells, settings.Height);
        int maximumDistance = DistancesFrom(entry, cells).Values.Max();
        if (maximumDistance < Math.Max(settings.Width, settings.Height) / 2)
        {
            layout = null!;
            return false;
        }

        layout = new GeneratedLayout(cells, [.. roomCenters]);
        return true;
    }

    private static void CarveCorridor(
        HashSet<GridCoordinate> cells,
        GridCoordinate start,
        GridCoordinate end,
        bool horizontalFirst)
    {
        GridCoordinate cursor = start;
        cells.Add(cursor);
        if (horizontalFirst)
        {
            CarveHorizontal();
            CarveVertical();
        }
        else
        {
            CarveVertical();
            CarveHorizontal();
        }

        void CarveHorizontal()
        {
            while (cursor.X != end.X)
            {
                cursor = cursor with { X = cursor.X + Math.Sign(end.X - cursor.X) };
                cells.Add(cursor);
            }
        }

        void CarveVertical()
        {
            while (cursor.Y != end.Y)
            {
                cursor = cursor with { Y = cursor.Y + Math.Sign(end.Y - cursor.Y) };
                cells.Add(cursor);
            }
        }
    }

    private static Dictionary<GridCoordinate, int> DistancesFrom(
        GridCoordinate start,
        IReadOnlySet<GridCoordinate> cells)
    {
        Queue<GridCoordinate> frontier = new();
        Dictionary<GridCoordinate, int> distances = new() { [start] = 0 };
        frontier.Enqueue(start);
        while (frontier.Count > 0 && distances.Count <= TacticalBoard.MaximumCells)
        {
            GridCoordinate current = frontier.Dequeue();
            foreach (GridCoordinate adjacent in OrthogonalNeighbors(current)
                .Where(cells.Contains)
                .OrderBy(value => value.X)
                .ThenBy(value => value.Y))
            {
                if (distances.TryAdd(adjacent, distances[current] + 1))
                {
                    frontier.Enqueue(adjacent);
                }
            }
        }

        return distances;
    }

    private static IEnumerable<GridCoordinate> OrthogonalNeighbors(GridCoordinate coordinate)
    {
        yield return new GridCoordinate(coordinate.X - 1, coordinate.Y);
        yield return new GridCoordinate(coordinate.X, coordinate.Y - 1);
        yield return new GridCoordinate(coordinate.X, coordinate.Y + 1);
        yield return new GridCoordinate(coordinate.X + 1, coordinate.Y);
    }

    private static GridCoordinate SelectEntry(IReadOnlySet<GridCoordinate> cells, int height) => cells
        .OrderBy(value => value.X)
        .ThenBy(value => Math.Abs(value.Y * 2 - height))
        .ThenBy(value => value.Y)
        .First();

    private static int NearestRoomIndex(GridCoordinate coordinate, ImmutableArray<GridCoordinate> roomCenters) =>
        Enumerable.Range(0, roomCenters.Length)
            .OrderBy(index => ManhattanDistance(coordinate, roomCenters[index]))
            .ThenBy(index => index)
            .First();

    private static int ManhattanDistance(GridCoordinate first, GridCoordinate second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private static bool IsValid(BattlefieldGenerationRequest request) =>
        request.BoardTemplateId.IsValid && request.ContentFingerprint.IsValid &&
        request.RequiredObjectiveId.IsValid && request.AccessId.IsValid &&
        request.AtmosphereId.IsValid && request.GravityId.IsValid && request.MaximumOccupants is >= 1 and <= 256 &&
        IsValidLocalizationKey(request.NameKey) && IsValidLocalizationKey(request.DescriptionKey) &&
        IsValidLocalizationKey(request.CellNameKey) && IsValidLocalizationKey(request.CellDescriptionKey);

    private static bool IsValidLocalizationKey(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumLocalizationKeyLength;

    private static bool IsValid(BattlefieldGenerationSettings settings) =>
        settings.GeneratorVersion == CurrentGeneratorVersion &&
        settings.Width is >= BattlefieldGenerationSettings.MinimumDimension and <= BattlefieldGenerationSettings.MaximumDimension &&
        settings.Height is >= BattlefieldGenerationSettings.MinimumDimension and <= BattlefieldGenerationSettings.MaximumDimension &&
        settings.Width * settings.Height <= TacticalBoard.MaximumCells &&
        settings.RoomCount is >= BattlefieldGenerationSettings.MinimumRoomCount and <= BattlefieldGenerationSettings.MaximumRoomCount &&
        settings.MaximumAttempts is >= 1 and <= BattlefieldGenerationSettings.MaximumGenerationAttempts &&
        settings.CoverPercent is >= 0 and <= 100 && settings.HazardPercent is >= 0 and <= 100;

    private static ulong DeriveStreamSeed(ulong seed, ulong streamTag, int attempt) =>
        seed ^ streamTag ^ ((ulong)(attempt + 1) * 0x9e3779b97f4a7c15UL);

    private static string BattlefieldIdentity(
        ulong seed,
        BattlefieldGenerationRequest request,
        BattlefieldGenerationSettings settings,
        int attempt)
    {
        FormattableString identityInput = $"{request.BoardTemplateId}|{request.ContentFingerprint}|{request.RequiredObjectiveId}|{request.AccessId}|{request.AtmosphereId}|{request.GravityId}|{request.MaximumOccupants}|{seed:x16}|{settings.GeneratorVersion}|{settings.Width}|{settings.Height}|{settings.RoomCount}|{settings.CoverPercent}|{settings.HazardPercent}|{attempt}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant(identityInput)));
        return $"b{Convert.ToHexStringLower(hash.AsSpan(0, 8))}";
    }

    private static BattlefieldGenerationResult Failure(
        ulong seed,
        BattlefieldGenerationCode code,
        string rejectionCode,
        int attempt = 0) => new(
            null,
            default,
            default,
            default,
            seed,
            attempt,
            code,
            rejectionCode);

    private sealed record GeneratedLayout(
        HashSet<GridCoordinate> Cells,
        ImmutableArray<GridCoordinate> RoomCenters);

    private readonly record struct GridCoordinate(int X, int Y);

    private struct DeterministicStream(ulong state)
    {
        private ulong state = state;

        public int Next(int minimum, int maximumExclusive)
        {
            ulong value = NextValue();
            return minimum + (int)(value % (uint)(maximumExclusive - minimum));
        }

        private ulong NextValue()
        {
            state += 0x9e3779b97f4a7c15UL;
            ulong value = state;
            value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
            return value ^ (value >> 31);
        }
    }
}
