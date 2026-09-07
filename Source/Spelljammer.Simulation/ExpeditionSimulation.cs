namespace Spelljammer.Simulation;

public sealed class ExpeditionSimulation
{
    public const int MaximumHull = 12;
    public const int RepairCargoCost = 2;
    public const int PrizeCargoRequired = 8;

    private static readonly SectorPosition Anchorage = new(1, 1);

    public ExpeditionState Create(ulong seed)
    {
        ushort anchorageMask = ToMask(Anchorage);
        return new ExpeditionState(
            seed,
            turn: 0,
            Anchorage,
            fuel: 10,
            hull: MaximumHull,
            supplies: 7,
            cargo: 0,
            visitedSectors: anchorageMask,
            salvagedSectors: anchorageMask,
            ExpeditionStatus.Active);
    }

    public SectorSnapshot Inspect(ExpeditionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return GenerateSector(state.Seed, state.Position);
    }

    public CommandResult Apply(ExpeditionState state, ExpeditionCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Status != ExpeditionStatus.Active)
        {
            return Reject(state, CommandRejection.ExpeditionEnded);
        }

        return command.Kind switch
        {
            ExpeditionCommandKind.Travel => Travel(state, command.Direction),
            ExpeditionCommandKind.Salvage => Salvage(state),
            ExpeditionCommandKind.Repair => Repair(state),
            ExpeditionCommandKind.ReturnHome => ReturnHome(state),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
    }

    private static CommandResult Travel(ExpeditionState state, TravelDirection direction)
    {
        if (state.Fuel == 0)
        {
            return Reject(state, CommandRejection.InsufficientFuel);
        }

        (int deltaX, int deltaY) = direction switch
        {
            TravelDirection.North => (0, -1),
            TravelDirection.East => (1, 0),
            TravelDirection.South => (0, 1),
            TravelDirection.West => (-1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        int destinationX = state.Position.X + deltaX;
        int destinationY = state.Position.Y + deltaY;
        if (destinationX is < 0 or >= SectorPosition.Width ||
            destinationY is < 0 or >= SectorPosition.Height)
        {
            return Reject(state, CommandRejection.SectorBoundary);
        }

        SectorPosition destination = new(destinationX, destinationY);
        SectorSnapshot sector = GenerateSector(state.Seed, destination);
        int turn = state.Turn + 1;
        int supplies = ConsumeSupplies(state.Supplies, turn);
        int hull = Math.Max(0, state.Hull - sector.Danger);
        ExpeditionStatus status = ResolveStatus(hull, supplies);
        ExpeditionState next = new(
            state.Seed,
            turn,
            destination,
            state.Fuel - 1,
            hull,
            supplies,
            state.Cargo,
            (ushort)(state.VisitedSectors | ToMask(destination)),
            state.SalvagedSectors,
            status);
        return new CommandResult(next, true, CommandRejection.None, HullDamage: sector.Danger);
    }

    private static CommandResult Salvage(ExpeditionState state)
    {
        SectorSnapshot sector = GenerateSector(state.Seed, state.Position);
        if (sector.SalvageYield == 0)
        {
            return Reject(state, CommandRejection.NothingToSalvage);
        }

        if (state.HasSalvaged(state.Position))
        {
            return Reject(state, CommandRejection.AlreadySalvaged);
        }

        int turn = state.Turn + 1;
        int supplies = ConsumeSupplies(state.Supplies, turn);
        ExpeditionState next = new(
            state.Seed,
            turn,
            state.Position,
            Math.Min(12, state.Fuel + sector.FuelCache),
            state.Hull,
            supplies,
            state.Cargo + sector.SalvageYield,
            state.VisitedSectors,
            (ushort)(state.SalvagedSectors | ToMask(state.Position)),
            ResolveStatus(state.Hull, supplies));
        return new CommandResult(
            next,
            true,
            CommandRejection.None,
            CargoRecovered: sector.SalvageYield,
            FuelRecovered: sector.FuelCache);
    }

    private static CommandResult Repair(ExpeditionState state)
    {
        if (state.Hull == MaximumHull)
        {
            return Reject(state, CommandRejection.HullAlreadySound);
        }

        if (state.Cargo < RepairCargoCost)
        {
            return Reject(state, CommandRejection.InsufficientCargo);
        }

        int turn = state.Turn + 1;
        int supplies = ConsumeSupplies(state.Supplies, turn);
        ExpeditionState next = new(
            state.Seed,
            turn,
            state.Position,
            state.Fuel,
            Math.Min(MaximumHull, state.Hull + 3),
            supplies,
            state.Cargo - RepairCargoCost,
            state.VisitedSectors,
            state.SalvagedSectors,
            ResolveStatus(state.Hull, supplies));
        return new CommandResult(next, true, CommandRejection.None);
    }

    private static CommandResult ReturnHome(ExpeditionState state)
    {
        if (state.Position != Anchorage)
        {
            return Reject(state, CommandRejection.NotAtAnchorage);
        }

        if (state.Cargo < PrizeCargoRequired)
        {
            return Reject(state, CommandRejection.InsufficientPrize);
        }

        ExpeditionState next = new(
            state.Seed,
            state.Turn,
            state.Position,
            state.Fuel,
            state.Hull,
            state.Supplies,
            state.Cargo,
            state.VisitedSectors,
            state.SalvagedSectors,
            ExpeditionStatus.Returned);
        return new CommandResult(next, true, CommandRejection.None);
    }

    private static SectorSnapshot GenerateSector(ulong seed, SectorPosition position)
    {
        if (position == Anchorage)
        {
            return new SectorSnapshot(position.Index, position, SectorKind.Anchorage, 0, 0, 0);
        }

        ulong value = Mix(seed + (ulong)position.Index * 0x9e3779b97f4a7c15UL);
        SectorKind kind = (SectorKind)(1 + value % 5);
        int danger = kind switch
        {
            SectorKind.OpenVoid => 0,
            SectorKind.DebrisField => 1,
            SectorKind.CrystalShoals => 1,
            SectorKind.AncientRuin => 2,
            SectorKind.AetherStorm => 3,
            _ => 0,
        };
        int salvageYield = kind == SectorKind.OpenVoid ? 1 : 2 + (int)(value >> 8 & 3);
        int fuelCache = (value >> 16 & 3) == 0 ? 2 : 0;
        return new SectorSnapshot(position.Index, position, kind, danger, salvageYield, fuelCache);
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xbf58476d1ce4e5b9UL;
        value ^= value >> 27;
        value *= 0x94d049bb133111ebUL;
        return value ^ value >> 31;
    }

    private static ushort ToMask(SectorPosition position) => (ushort)(1 << position.Index);

    private static int ConsumeSupplies(int supplies, int turn) =>
        turn % 3 == 0 ? Math.Max(0, supplies - 1) : supplies;

    private static ExpeditionStatus ResolveStatus(int hull, int supplies) =>
        hull == 0 || supplies == 0 ? ExpeditionStatus.Lost : ExpeditionStatus.Active;

    private static CommandResult Reject(ExpeditionState state, CommandRejection rejection) =>
        new(state, false, rejection);
}
