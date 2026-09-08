using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

/// <summary>Data-driven fixed-tick cadence and catch-up policy for an authoritative world.</summary>
public sealed record WorldTimeDefinition(
    WorldTimeId WorldTimeId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int TicksPerSecond,
    int MaximumCatchUpTicks)
    : ContentDefinition(WorldTimeId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);

/// <summary>World-owned content required to create and advance authoritative simulation state.</summary>
public interface IWorldContentCatalog : IContentCatalogIdentity
{
    ImmutableArray<WorldTimeDefinition> WorldTimes { get; }
    ImmutableArray<CalendarDefinition> Calendars { get; }
    ImmutableArray<TimeScaleDefinition> TimeScales { get; }

    bool TryGetWorldTime(WorldTimeId id, out WorldTimeDefinition? definition);
    bool TryGetCalendar(CalendarId id, out CalendarDefinition? definition);
    bool TryGetTimeScale(TimeScaleId id, out TimeScaleDefinition? definition);
}
