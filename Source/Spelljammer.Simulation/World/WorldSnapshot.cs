using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Ships;

namespace Spelljammer.Simulation.World;

/// <summary>
/// Defines immutable world snapshots and the result of advancing the simulation.
/// </summary>
/// <remarks>
/// Code flow: World advancement commits commands and scheduled actions, then captures authoritative ships, encounters, clock, events, and deterministic identity for consumers.
/// </remarks>
public sealed record WorldSnapshot(
    ulong Seed,
    ContentFingerprint ContentFingerprint,
    WorldTimeDefinition TimeDefinition,
    CalendarDefinition Calendar,
    TimeScaleDefinition TimeScale,
    CampaignClockState Clock,
    long Tick,
    bool ShipPaused,
    bool PersonalPaused,
    ImmutableArray<ShipState> Ships,
    PersonalEncounterState? PersonalEncounter,
    ImmutableArray<BattleUnitId> ReadyUnits,
    ImmutableArray<ScheduledAction> Actions,
    ImmutableArray<WorldCommandLogEntry> RecentCommands,
    ImmutableArray<WorldEvent> RecentEvents);

public sealed record WorldAdvanceResult(World World, WorldSnapshot Snapshot, int AdvancedTicks);
