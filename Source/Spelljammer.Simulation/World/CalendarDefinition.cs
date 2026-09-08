using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

public sealed record CalendarMonthDefinition(
    CalendarMonthId CalendarMonthId,
    string NameKey,
    int Days);

/// <summary>Data-driven calendar used to project absolute campaign time into a display date.</summary>
public sealed record CalendarDefinition(
    CalendarId CalendarId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int SecondsPerMinute,
    int MinutesPerHour,
    int HoursPerDay,
    int DaysPerWeek,
    long StartingYear,
    ImmutableArray<CalendarMonthDefinition> Months)
    : ContentDefinition(CalendarId.Value, SchemaVersion, Revision, NameKey, DescriptionKey)
{
    public long SecondsPerDay => checked((long)SecondsPerMinute * MinutesPerHour * HoursPerDay);

    public long DaysPerYear => Months.Sum(month => (long)month.Days);
}
