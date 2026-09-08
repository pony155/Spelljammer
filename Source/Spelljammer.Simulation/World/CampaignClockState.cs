using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

/// <summary>Persistent absolute campaign time and its deterministic fractional-second remainder.</summary>
public sealed record CampaignClockState(
    long ElapsedWorldSeconds,
    int FractionRemainder,
    CalendarId CalendarId,
    TimeScaleId TimeScaleId)
{
    public static CampaignClockState Create(CalendarId calendarId, TimeScaleId timeScaleId) =>
        new(0, 0, calendarId, timeScaleId);
}
