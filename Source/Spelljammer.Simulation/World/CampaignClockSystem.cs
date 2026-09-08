namespace Spelljammer.Simulation.World;

/// <summary>Deterministically advances persistent campaign time without floating-point arithmetic.</summary>
/// <remarks>
/// Code flow: Simulation ticks are multiplied by the data-driven time-scale ratio, fractional seconds remain in state, and whole seconds advance the persistent campaign clock.
/// </remarks>
public static class CampaignClockSystem
{
    public static CampaignClockState Advance(
        CampaignClockState state,
        TimeScaleDefinition timeScale,
        int simulationTicks)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(timeScale);
        if (simulationTicks < 0 || state.ElapsedWorldSeconds < 0 || state.FractionRemainder < 0 ||
            timeScale.WorldSecondsNumerator <= 0 || timeScale.SimulationTicksDenominator <= 0 ||
            state.TimeScaleId != timeScale.TimeScaleId ||
            state.FractionRemainder >= timeScale.SimulationTicksDenominator)
        {
            throw new ArgumentException("Campaign clock state or time-scale definition is invalid.");
        }

        if (simulationTicks == 0)
        {
            return state;
        }

        long accumulated = checked(
            state.FractionRemainder + (long)timeScale.WorldSecondsNumerator * simulationTicks);
        long elapsedSeconds = checked(
            state.ElapsedWorldSeconds + accumulated / timeScale.SimulationTicksDenominator);
        int remainder = (int)(accumulated % timeScale.SimulationTicksDenominator);
        return state with
        {
            ElapsedWorldSeconds = elapsedSeconds,
            FractionRemainder = remainder,
        };
    }
}
