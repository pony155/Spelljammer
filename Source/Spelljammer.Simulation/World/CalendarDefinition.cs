using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

/// <summary>
/// Defines data-driven calendar months and the calendar used to project campaign time.
/// </summary>
/// <remarks>
/// Code flow: Content supplies ordered month lengths and labels, validation derives year length, and time queries walk the months to produce a display date.
/// </remarks>
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
    int StartingMonth,
    int StartingDay,
    int StartingDayOfWeekIndex,
    int StartingHour,
    int StartingMinute,
    int StartingSecond,
    ImmutableArray<CalendarMonthDefinition> Months)
    : ContentDefinition(CalendarId.Value, SchemaVersion, Revision, NameKey, DescriptionKey)
{
    public long SecondsPerDay => checked((long)SecondsPerMinute * MinutesPerHour * HoursPerDay);

    public long DaysPerYear => Months.Sum(month => (long)month.Days);

    public long StartingDayOfYear => Months
        .Take(StartingMonth - 1)
        .Sum(month => (long)month.Days) + StartingDay - 1;

    public long StartingOffsetSeconds => checked(
        StartingDayOfYear * SecondsPerDay +
        (long)StartingHour * MinutesPerHour * SecondsPerMinute +
        (long)StartingMinute * SecondsPerMinute +
        StartingSecond);
}
