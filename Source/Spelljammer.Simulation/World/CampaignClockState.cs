using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

/// <summary>Persistent absolute campaign time and its deterministic fractional-second remainder.</summary>
/// <remarks>
/// Code flow: World creation initializes calendar and scale identities, clock advancement returns replacement state, and queries project elapsed seconds into an EAC/EAT date.
/// </remarks>
public sealed record CampaignClockState(
    long ElapsedWorldSeconds,
    int FractionRemainder,
    CalendarId CalendarId,
    TimeScaleId TimeScaleId)
{
    public static CampaignClockState Create(CalendarId calendarId, TimeScaleId timeScaleId) =>
        new(0, 0, calendarId, timeScaleId);
}
