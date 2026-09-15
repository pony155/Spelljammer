using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

public sealed partial class SimulationContracts
{
    private static void BattlefieldGenerationIsDeterministicAndBounded()
    {
        BattlefieldGenerationRequest request = Request();
        BattlefieldGenerationResult first = BattlefieldGenerator.Generate(0xbaff1eUL, request);
        BattlefieldGenerationResult second = BattlefieldGenerator.Generate(0xbaff1eUL, request);
        True(first.Succeeded, $"First battlefield generation failed: {first.Code} / {first.RejectionCode}.");
        True(second.Succeeded, $"Second battlefield generation failed: {second.Code} / {second.RejectionCode}.");

        TacticalBoard firstBoard = first.Board!;
        TacticalBoard secondBoard = second.Board!;
        True(CellSignatures(firstBoard).SequenceEqual(CellSignatures(secondBoard)),
            "The same seed and request produced different battlefield cells.");
        True(firstBoard.Links.SequenceEqual(secondBoard.Links),
            "The same seed and request produced different battlefield links.");
        True(firstBoard.Cells.Count is > 15 and <= TacticalBoard.MaximumCells,
            "The generated battlefield exceeded its bounded cell contract.");
        True(firstBoard.Links.Length <= TacticalBoard.MaximumLinks,
            "The generated battlefield exceeded its bounded link contract.");
        True(!firstBoard.FindPath(first.EntryCellId, first.ObjectiveCellId, TacticalBoard.MaximumCells).IsEmpty,
            "The generated objective was unreachable from deployment.");
        True(!firstBoard.FindPath(first.ObjectiveCellId, first.ExtractionCellId, TacticalBoard.MaximumCells).IsEmpty,
            "The generated extraction was unreachable from the objective.");

        BattlefieldGenerationResult undecorated = BattlefieldGenerator.Generate(
            0xbaff1eUL,
            request,
            new BattlefieldGenerationSettings(CoverPercent: 0, HazardPercent: 0));
        BattlefieldGenerationResult decorated = BattlefieldGenerator.Generate(
            0xbaff1eUL,
            request,
            new BattlefieldGenerationSettings(CoverPercent: 100, HazardPercent: 100));
        True(undecorated.Succeeded && decorated.Succeeded,
            "A valid decoration setting rejected the logical battlefield.");
        True(undecorated.Board!.Cells.Values.Select(value => (value.Q, value.R))
                .Order()
                .SequenceEqual(decorated.Board!.Cells.Values.Select(value => (value.Q, value.R)).Order()),
            "Decoration settings changed battlefield topology.");
        True(LogicalEdges(undecorated.Board).SequenceEqual(LogicalEdges(decorated.Board)),
            "Decoration settings changed battlefield connectivity.");

        BattlefieldGenerationResult otherSeed = BattlefieldGenerator.Generate(0xbaff1fUL, request);
        True(otherSeed.Succeeded, $"Alternate-seed generation failed: {otherSeed.Code}.");
        False(firstBoard.Cells.Values.Select(value => (value.Q, value.R)).Order().SequenceEqual(
                otherSeed.Board!.Cells.Values.Select(value => (value.Q, value.R)).Order()),
            "Different seeds produced the same logical battlefield layout.");

        BattlefieldGenerationResult invalid = BattlefieldGenerator.Generate(
            1,
            request,
            new BattlefieldGenerationSettings(Width: BattlefieldGenerationSettings.MaximumDimension + 1));
        Equal(BattlefieldGenerationCode.InvalidSettings, invalid.Code,
            "An oversized battlefield request did not fail before publication.");
        True(invalid.Board is null, "An invalid battlefield request published a partial board.");
    }

    private static BattlefieldGenerationRequest Request() => new(
        new PersonalBoardId("board.generated.contract"),
        new ContentFingerprint(new string('a', 64)),
        new ObjectiveId("objective.generated.recover-artifact"),
        new ContentId("traversal.open"),
        new ContentId("atmosphere.breathable"),
        new ContentId("gravity.standard"),
        "board.generated.name",
        "board.generated.description",
        "cell.generated.name",
        "cell.generated.description");

    private static IEnumerable<(int FromX, int FromY, int ToX, int ToY)> LogicalEdges(TacticalBoard board) =>
        board.Links.Select(link => (
            board.Cells[link.FromCellId].Q,
            board.Cells[link.FromCellId].R,
            board.Cells[link.ToCellId].Q,
            board.Cells[link.ToCellId].R)).Order();

    private static IEnumerable<string> CellSignatures(TacticalBoard board) => board.Cells.Values
        .OrderBy(value => value.CellId)
        .Select(value => FormattableString.Invariant(
            $"{value.CellId}|{value.SchemaVersion}|{value.Revision}|{value.NameKey}|{value.DescriptionKey}|{value.ZoneId}|{value.Q}|{value.R}|{value.Capacity}|{value.Cover}|{value.Visibility}|{value.AtmosphereId}|{value.GravityId}|{string.Join(',', value.HazardTags)}"));
}
