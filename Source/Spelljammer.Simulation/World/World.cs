using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Galaxy;
using Spelljammer.Simulation.Ships;

namespace Spelljammer.Simulation.World;

/// <summary>
/// Owns the immutable authoritative state for the deterministic Spelljammer simulation.
/// </summary>
/// <remarks>
/// Code flow: Content-backed state is created and validated, commands enter bounded queues, fixed-tick advancement returns replacement world values, and snapshots expose committed state.
/// </remarks>
public sealed partial record World(
    ulong Seed,
    ContentFingerprint ContentFingerprint,
    WorldTimeDefinition TimeDefinition,
    CalendarDefinition Calendar,
    TimeScaleDefinition TimeScale,
    CampaignClockState Clock,
    long Tick,
    ulong RandomSequence,
    TeamId PlayerTeamId,
    bool ShipPaused,
    bool PersonalPaused,
    ImmutableDictionary<ShipId, ShipState> Ships,
    PersonalEncounterState? PersonalEncounter,
    ImmutableArray<WorldCommand> Commands,
    ImmutableArray<WorldCommandLogEntry> CommandHistory,
    ImmutableArray<ScheduledAction> ScheduledActions,
    ImmutableArray<BattleUnitId> ReadyUnits,
    ImmutableArray<WorldEvent> Events)
{
    public const int MaximumCommands = 256;
    public const int MaximumCommandHistory = 512;
    public const int MaximumSchedules = 256;
    public const int MaximumEvents = 512;
    public const int MaximumReadyUnits = 64;

    /// <summary>The optional authoritative campaign galaxy for voyage-scale play.</summary>
    public GalaxyMapState? Galaxy { get; init; }

    public static World Create(
        ulong seed,
        ContentFingerprint fingerprint,
        WorldTimeDefinition timeDefinition,
        CalendarDefinition calendar,
        TimeScaleDefinition timeScale,
        TeamId playerTeamId,
        IEnumerable<ShipState> ships,
        PersonalEncounterState? encounter = null,
        GalaxyMapState? galaxy = null)
    {
        ArgumentNullException.ThrowIfNull(timeDefinition);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(timeScale);
        ImmutableDictionary<ShipId, ShipState> shipMap = ships.ToImmutableDictionary(value => value.Id);
        if (timeDefinition.TicksPerSecond <= 0 || timeDefinition.MaximumCatchUpTicks <= 0 ||
            calendar.SecondsPerMinute <= 0 || calendar.MinutesPerHour <= 0 || calendar.HoursPerDay <= 0 ||
            calendar.DaysPerWeek <= 0 || calendar.Months.IsDefaultOrEmpty ||
            timeScale.WorldSecondsNumerator <= 0 || timeScale.SimulationTicksDenominator <= 0 ||
            shipMap.Count is 0 or > 32 || encounter?.Units.Count > MaximumReadyUnits ||
            (galaxy is not null && !GalaxyValidator.Validate(galaxy).Accepted))
        {
            throw new InvalidOperationException("World configuration or capacity is invalid.");
        }

        return new World(
            seed,
            fingerprint,
            timeDefinition,
            calendar,
            timeScale,
            CampaignClockState.Create(calendar.CalendarId, timeScale.TimeScaleId),
            0,
            0,
            playerTeamId,
            true,
            false,
            shipMap,
            encounter,
            [],
            [],
            [],
            [],
            []) { Galaxy = galaxy };
    }

    public World SetShipPause(bool paused) => this with { ShipPaused = paused };

    public World CommitReadyPlan() => this with { PersonalPaused = false };

    public WorldSnapshot Snapshot() => new(
        Seed,
        ContentFingerprint,
        TimeDefinition,
        Calendar,
        TimeScale,
        Clock,
        Galaxy,
        Tick,
        ShipPaused,
        PersonalPaused,
        [.. Ships.Values.OrderBy(value => value.Id)],
        PersonalEncounter,
        ReadyUnits,
        ScheduledActions,
        CommandHistory.Length <= 64 ? CommandHistory : CommandHistory[^64..],
        Events.Length <= 64 ? Events : Events[^64..]);

    private World AddEvent(WorldCommand command, bool succeeded, int amount, string code)
    {
        WorldEvent value = new(
            new ContentId($"event.voyage.sequence-{RandomSequence % 1_000_000}"),
            Tick,
            command.IssuerId,
            command.TargetId,
            command.Kind,
            succeeded,
            amount,
            code);
        ImmutableArray<WorldEvent> events = Events.Length == MaximumEvents ? Events.RemoveAt(0).Add(value) : Events.Add(value);
        return this with { Events = events, RandomSequence = RandomSequence + 1 };
    }

    private static int IndexOf<T>(ImmutableArray<T> values, Func<T, bool> predicate)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (predicate(values[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
