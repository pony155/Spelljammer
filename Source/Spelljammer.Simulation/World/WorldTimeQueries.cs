using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

public sealed record WorldDateTime(
    long Year,
    int Month,
    CalendarMonthId MonthId,
    string MonthNameKey,
    int Day,
    int DayOfWeekIndex,
    int Hour,
    int Minute,
    int Second);

/// <summary>Pure projections over the persistent campaign clock.</summary>
public static class WorldTimeQueries
{
    public static WorldDateTime GetDateTime(CampaignClockState clock, CalendarDefinition calendar)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(calendar);
        Validate(clock, calendar);

        long secondsPerDay = calendar.SecondsPerDay;
        long elapsedDays = clock.ElapsedWorldSeconds / secondsPerDay;
        long secondsWithinDay = clock.ElapsedWorldSeconds % secondsPerDay;
        long daysPerYear = calendar.DaysPerYear;
        long year = checked(calendar.StartingYear + elapsedDays / daysPerYear);
        long dayOfYear = elapsedDays % daysPerYear;

        int monthIndex = 0;
        while (dayOfYear >= calendar.Months[monthIndex].Days)
        {
            dayOfYear -= calendar.Months[monthIndex].Days;
            monthIndex++;
        }

        long secondsPerHour = checked((long)calendar.SecondsPerMinute * calendar.MinutesPerHour);
        int hour = (int)(secondsWithinDay / secondsPerHour);
        long secondsWithinHour = secondsWithinDay % secondsPerHour;
        int minute = (int)(secondsWithinHour / calendar.SecondsPerMinute);
        int second = (int)(secondsWithinHour % calendar.SecondsPerMinute);
        CalendarMonthDefinition month = calendar.Months[monthIndex];
        return new WorldDateTime(
            year,
            monthIndex + 1,
            month.CalendarMonthId,
            month.NameKey,
            (int)dayOfYear + 1,
            (int)(elapsedDays % calendar.DaysPerWeek),
            hour,
            minute,
            second);
    }

    public static long GetElapsedWorldDay(CampaignClockState clock, CalendarDefinition calendar)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(calendar);
        Validate(clock, calendar);

        return clock.ElapsedWorldSeconds / calendar.SecondsPerDay;
    }

    private static void Validate(CampaignClockState clock, CalendarDefinition calendar)
    {
        if (clock.ElapsedWorldSeconds < 0 || clock.CalendarId != calendar.CalendarId ||
            calendar.SecondsPerMinute <= 0 || calendar.MinutesPerHour <= 0 || calendar.HoursPerDay <= 0 ||
            calendar.DaysPerWeek <= 0 || calendar.Months.IsDefaultOrEmpty ||
            calendar.Months.Any(month => month.Days <= 0))
        {
            throw new ArgumentException("Campaign clock state or calendar definition is invalid.");
        }
    }
}
