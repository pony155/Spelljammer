namespace Spelljammer.Simulation;

/// <summary>
/// Represents the cardinal direction for ship travel in the exploration grid.
/// </summary>
public enum TravelDirection
{
    /// <summary>
    /// Move toward the top of the map (decreasing Y coordinate).
    /// </summary>
    North,

    /// <summary>
    /// Move toward the right of the map (increasing X coordinate).
    /// </summary>
    East,

    /// <summary>
    /// Move toward the bottom of the map (increasing Y coordinate).
    /// </summary>
    South,

    /// <summary>
    /// Move toward the left of the map (decreasing X coordinate).
    /// </summary>
    West
}

/// <summary>
/// Categorizes the type of space sector the player's ship can encounter.
/// </summary>
/// <remarks>
/// Each sector kind presents different challenges, opportunities for salvage, and resource availability.
/// Some sectors may be more dangerous, while others offer rich salvage potential.
/// </remarks>
public enum SectorKind
{
    /// <summary>
    /// A safe, controlled position suitable for resting, refueling, and resupply.
    /// </summary>
    Anchorage,

    /// <summary>
    /// Empty space with minimal hazards but no resources or salvage opportunities.
    /// </summary>
    OpenVoid,

    /// <summary>
    /// A field of space debris from destroyed ships or objects; dangerous but potentially rich in salvage.
    /// </summary>
    DebrisField,

    /// <summary>
    /// Crystalline formations hazard the ship but may contain valuable salvage resources.
    /// </summary>
    CrystalShoals,

    /// <summary>
    /// Remnants of a long-dead civilization; dangerous exploration site with valuable artifacts.
    /// </summary>
    AncientRuin,

    /// <summary>
    /// Dangerous energy storm; high risk but potential for power resources.
    /// </summary>
    AetherStorm
}

/// <summary>
/// Indicates the overall status of an expedition.
/// </summary>
public enum ExpeditionStatus
{
    /// <summary>
    /// The expedition is ongoing and new commands can be issued.
    /// </summary>
    Active,

    /// <summary>
    /// The expedition has completed successfully and the ship has returned home.
    /// </summary>
    Returned,

    /// <summary>
    /// The expedition has ended in failure; the ship was destroyed or is otherwise lost.
    /// </summary>
    Lost
}

/// <summary>
/// Represents the types of actions the player can command their ship to perform during an expedition.
/// </summary>
public enum ExpeditionCommandKind
{
    /// <summary>
    /// Move the ship to an adjacent sector in a given direction.
    /// </summary>
    Travel,

    /// <summary>
    /// Attempt to salvage resources from the current sector.
    /// </summary>
    Salvage,

    /// <summary>
    /// Repair damage to the ship's hull using available supplies.
    /// </summary>
    Repair,

    /// <summary>
    /// Terminate the expedition and return home.
    /// </summary>
    ReturnHome
}

/// <summary>
/// Enumeration of reasons why a command may be rejected by the simulation.
/// </summary>
public enum CommandRejection
{
    /// <summary>
    /// The command was accepted without issues.
    /// </summary>
    None,

    /// <summary>
    /// The expedition has already ended and no new commands can be issued.
    /// </summary>
    ExpeditionEnded,

    /// <summary>
    /// The movement command would move the ship outside the bounds of the exploration grid.
    /// </summary>
    SectorBoundary,

    /// <summary>
    /// The ship does not have enough fuel to perform the travel command.
    /// </summary>
    InsufficientFuel,

    /// <summary>
    /// The current sector has no salvageable resources.
    /// </summary>
    NothingToSalvage,

    /// <summary>
    /// The current sector has already been salvaged and cannot be salvaged again.
    /// </summary>
    AlreadySalvaged,

    /// <summary>
    /// The ship's hull is already fully repaired and does not need repair.
    /// </summary>
    HullAlreadySound,

    /// <summary>
    /// The ship does not have enough cargo capacity to hold recovered resources.
    /// </summary>
    InsufficientCargo,

    /// <summary>
    /// The return home command requires the ship to be in an anchorage sector.
    /// </summary>
    NotAtAnchorage,

    /// <summary>
    /// The salvage yield is too small to be worth the effort.
    /// </summary>
    InsufficientPrize
}

/// <summary>
/// Represents a position on the exploration grid where a ship can travel.
/// </summary>
/// <remarks>
/// The exploration grid is 4x4, with coordinates ranging from (0,0) to (3,3). Coordinates are validated
/// at construction to ensure they remain within bounds. The Index property provides a flattened array index
/// for bit-packing visited/salvaged sector information.
/// </remarks>
public readonly record struct SectorPosition
{
    /// <summary>
    /// The width of the exploration grid in sectors.
    /// </summary>
    public const int Width = 4;

    /// <summary>
    /// The height of the exploration grid in sectors.
    /// </summary>
    public const int Height = 4;

    /// <summary>
    /// Initializes a new sector position with the given X and Y coordinates.
    /// </summary>
    /// <param name="x">The X coordinate (0-3). Must be within the grid bounds.</param>
    /// <param name="y">The Y coordinate (0-3). Must be within the grid bounds.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="x"/> or <paramref name="y"/> is outside the valid grid range.</exception>
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

    /// <summary>
    /// Gets the X coordinate of this position (0-3).
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the Y coordinate of this position (0-3).
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the flattened array index of this position, suitable for bit-packing in a 16-bit integer.
    /// </summary>
    /// <remarks>
    /// This value is computed as Y * Width + X, resulting in a value from 0 to 15.
    /// </remarks>
    public int Index => Y * Width + X;
}

/// <summary>
/// A snapshot of a sector's properties, generated consistently for a given expedition seed and position.
/// </summary>
/// <remarks>
/// Each sector in a given expedition has deterministic properties based on the expedition's random seed
/// and the sector's position. This allows the same sector to always have the same properties when revisited.
/// </remarks>
/// <param name="StableId">A unique identifier for this sector snapshot within the expedition.</param>
/// <param name="Position">The grid position of this sector.</param>
/// <param name="Kind">The type of sector (e.g., debris field, ancient ruin).</param>
/// <param name="Danger">A difficulty/danger level for navigating this sector (affects combat probability).</param>
/// <param name="SalvageYield">The amount of salvageable resources available in this sector.</param>
/// <param name="FuelCache">The amount of fuel that can be recovered from this sector if salvaging succeeds.</param>
public readonly record struct SectorSnapshot(
    int StableId,
    SectorPosition Position,
    SectorKind Kind,
    int Danger,
    int SalvageYield,
    int FuelCache);

/// <summary>
/// Represents a player command to perform an action during an expedition turn.
/// </summary>
/// <remarks>
/// Commands include travel (with a direction), salvage, repair, and returning home. The direction field
/// is only meaningful for travel commands but is included in all instances for record simplicity.
/// Factory methods provide a clean API for constructing commands of each type.
/// </remarks>
/// <param name="Kind">The type of action to perform.</param>
/// <param name="Direction">The direction to travel (only meaningful for travel commands).</param>
public readonly record struct ExpeditionCommand(
    ExpeditionCommandKind Kind,
    TravelDirection Direction = TravelDirection.North)
{
    /// <summary>
    /// Creates a travel command that moves the ship in the specified direction.
    /// </summary>
    /// <param name="direction">The cardinal direction to travel.</param>
    /// <returns>A travel command moving in the given direction.</returns>
    public static ExpeditionCommand Travel(TravelDirection direction) =>
        new(ExpeditionCommandKind.Travel, direction);

    /// <summary>
    /// Gets a salvage command that attempts to recover resources from the current sector.
    /// </summary>
    public static ExpeditionCommand Salvage => new(ExpeditionCommandKind.Salvage);

    /// <summary>
    /// Gets a repair command that fixes damage to the ship's hull.
    /// </summary>
    public static ExpeditionCommand Repair => new(ExpeditionCommandKind.Repair);

    /// <summary>
    /// Gets a return home command that terminates the expedition.
    /// </summary>
    public static ExpeditionCommand ReturnHome => new(ExpeditionCommandKind.ReturnHome);
}

/// <summary>
/// Represents the current state of an active or completed expedition.
/// </summary>
/// <remarks>
/// The expedition state is immutable and created by the simulation engine. It tracks the ship's position,
/// resources (fuel, supplies, cargo), hull integrity, and progress through the exploration grid.
/// Visited and salvaged sectors are tracked using bit-packed flags for space efficiency.
/// </remarks>
public sealed class ExpeditionState
{
    /// <summary>
    /// Initializes a new expedition state with the given parameters.
    /// </summary>
    /// <remarks>
    /// This constructor is internal; new expedition states are created only by the simulation engine.
    /// </remarks>
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

    /// <summary>
    /// Gets the random seed used to generate sector properties, ensuring deterministic sector generation.
    /// </summary>
    public ulong Seed { get; }

    /// <summary>
    /// Gets the current turn number in the expedition (starting at 0).
    /// </summary>
    public int Turn { get; }

    /// <summary>
    /// Gets the current position of the ship on the exploration grid.
    /// </summary>
    public SectorPosition Position { get; }

    /// <summary>
    /// Gets the current fuel level of the ship.
    /// </summary>
    /// <remarks>
    /// Fuel is consumed by travel commands and can be replenished by salvaging certain sectors or using supplies.
    /// </remarks>
    public int Fuel { get; }

    /// <summary>
    /// Gets the current hull integrity of the ship (health points).
    /// </summary>
    /// <remarks>
    /// Hull damage is sustained during dangerous encounters. The ship can be repaired using supplies and repair commands.
    /// </remarks>
    public int Hull { get; }

    /// <summary>
    /// Gets the current supply level, used for repairs and resource recovery.
    /// </summary>
    public int Supplies { get; }

    /// <summary>
    /// Gets the current cargo load (recovered resources) being carried by the ship.
    /// </summary>
    /// <remarks>
    /// Cargo has a maximum capacity and salvage operations are limited by remaining cargo space.
    /// </remarks>
    public int Cargo { get; }

    /// <summary>
    /// Gets a bit-packed flag indicating which sectors have been visited by the ship.
    /// </summary>
    /// <remarks>
    /// Each bit in this ushort corresponds to a sector in the 4x4 grid, indexed by <see cref="SectorPosition.Index"/>.
    /// </remarks>
    public ushort VisitedSectors { get; }

    /// <summary>
    /// Gets a bit-packed flag indicating which sectors have been salvaged by the ship.
    /// </summary>
    /// <remarks>
    /// Each bit in this ushort corresponds to a sector. A sector can only be salvaged once per expedition.
    /// </remarks>
    public ushort SalvagedSectors { get; }

    /// <summary>
    /// Gets the overall status of the expedition (active, returned, or lost).
    /// </summary>
    public ExpeditionStatus Status { get; }

    /// <summary>
    /// Determines whether the ship has visited the specified sector before.
    /// </summary>
    /// <param name="position">The sector position to check.</param>
    /// <returns>True if the ship has visited this sector; otherwise, false.</returns>
    public bool HasVisited(SectorPosition position) =>
        (VisitedSectors & 1 << position.Index) != 0;

    /// <summary>
    /// Determines whether the ship has salvaged resources from the specified sector.
    /// </summary>
    /// <param name="position">The sector position to check.</param>
    /// <returns>True if the ship has successfully salvaged from this sector; otherwise, false.</returns>
    public bool HasSalvaged(SectorPosition position) =>
        (SalvagedSectors & 1 << position.Index) != 0;
}

/// <summary>
/// The result of executing a command during an expedition turn.
/// </summary>
/// <remarks>
/// The command result includes the new expedition state (whether the command was accepted or not),
/// whether the command was accepted, and the reason for any rejection. For successful commands,
/// it may also include details about resources gained or damage sustained.
/// </remarks>
/// <param name="State">The updated expedition state after the command execution.</param>
/// <param name="Accepted">True if the command was successfully executed; false if it was rejected.</param>
/// <param name="Rejection">The reason the command was rejected, if it was not accepted.</param>
/// <param name="HullDamage">The amount of hull damage sustained during the command (e.g., from encounters).</param>
/// <param name="CargoRecovered">The amount of cargo recovered during a salvage command.</param>
/// <param name="FuelRecovered">The amount of fuel recovered during a salvage command.</param>
public readonly record struct CommandResult(
    ExpeditionState State,
    bool Accepted,
    CommandRejection Rejection,
    int HullDamage = 0,
    int CargoRecovered = 0,
    int FuelRecovered = 0);
