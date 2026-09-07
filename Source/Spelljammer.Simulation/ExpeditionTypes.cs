namespace Spelljammer.Simulation;

public enum TravelDirection
{
    North,
    East,
    South,
    West
}

public enum SectorKind
{
    Anchorage,
    OpenVoid,
    DebrisField,
    CrystalShoals,
    AncientRuin,
    AetherStorm
}

public enum ExpeditionStatus
{
    Active,
    Returned,
    Lost
}

public enum ExpeditionCommandKind
{
    Travel,
    Salvage,
    Repair,
    ReturnHome
}

public enum CommandRejection
{
    None,
    ExpeditionEnded,
    SectorBoundary,
    InsufficientFuel,
    NothingToSalvage,
    AlreadySalvaged,
    HullAlreadySound,
    InsufficientCargo,
    NotAtAnchorage,
    InsufficientPrize
}

public readonly record struct SectorPosition
{
    public const int Width = 4;
    public const int Height = 4;

    public SectorPosition(int x, int y)
    {
        if (x is < 0 or >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (y is < 0 or >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    public int Index => Y * Width + X;
}

public readonly record struct SectorSnapshot(
    int StableId,
    SectorPosition Position,
    SectorKind Kind,
    int Danger,
    int SalvageYield,
    int FuelCache);

public readonly record struct ExpeditionCommand(
    ExpeditionCommandKind Kind,
    TravelDirection Direction = TravelDirection.North)
{
    public static ExpeditionCommand Travel(TravelDirection direction) =>
        new(ExpeditionCommandKind.Travel, direction);

    public static ExpeditionCommand Salvage => new(ExpeditionCommandKind.Salvage);

    public static ExpeditionCommand Repair => new(ExpeditionCommandKind.Repair);

    public static ExpeditionCommand ReturnHome => new(ExpeditionCommandKind.ReturnHome);
}

public sealed class ExpeditionState
{
    internal ExpeditionState(
        ulong seed,
        int turn,
        SectorPosition position,
        int fuel,
        int hull,
        int supplies,
        int cargo,
        ushort visitedSectors,
        ushort salvagedSectors,
        ExpeditionStatus status)
    {
        Seed = seed;
        Turn = turn;
        Position = position;
        Fuel = fuel;
        Hull = hull;
        Supplies = supplies;
        Cargo = cargo;
        VisitedSectors = visitedSectors;
        SalvagedSectors = salvagedSectors;
        Status = status;
    }

    public ulong Seed { get; }

    public int Turn { get; }

    public SectorPosition Position { get; }

    public int Fuel { get; }

    public int Hull { get; }

    public int Supplies { get; }

    public int Cargo { get; }

    public ushort VisitedSectors { get; }

    public ushort SalvagedSectors { get; }

    public ExpeditionStatus Status { get; }

    public bool HasVisited(SectorPosition position) =>
        (VisitedSectors & 1 << position.Index) != 0;

    public bool HasSalvaged(SectorPosition position) =>
        (SalvagedSectors & 1 << position.Index) != 0;
}

public readonly record struct CommandResult(
    ExpeditionState State,
    bool Accepted,
    CommandRejection Rejection,
    int HullDamage = 0,
    int CargoRecovered = 0,
    int FuelRecovered = 0);
