using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

/// <summary>
/// Applies non-attack encounter transitions such as surrender, capture, and objective completion.
/// </summary>
/// <remarks>
/// Code flow: A requested transition resolves the target unit or objective, validates its current state, and returns a replacement encounter with the accepted lifecycle change.
/// </remarks>
public static class EncounterLifecycle
{
    public static PersonalEncounterState TakePrisoner(PersonalEncounterState encounter, BattleUnitId unitId)
    {
        if (!encounter.Units.TryGetValue(unitId, out BattleUnitState? unit) ||
            (!unit.Surrendered && !unit.IsIncapacitated))
        {
            throw new InvalidOperationException("Only surrendered or incapacitated actors can be taken prisoner.");
        }

        return encounter with {
            Units = encounter.Units.SetItem(unitId, unit with { Prisoner = true }),
        };
    }

    public static PersonalEncounterState Cleanup(PersonalEncounterState encounter, TeamId playerTeamId)
    {
        bool objectivesResolved = encounter.Objectives.Values.All(value =>
            value is ObjectiveState.Completed or ObjectiveState.Failed or ObjectiveState.Abandoned);
        bool oppositionResolved = encounter.Units.Values
            .Where(value => value.TeamId != playerTeamId)
            .All(value => value.IsIncapacitated || value.Surrendered || value.Prisoner);
        if (!encounter.Retreated && !objectivesResolved && !oppositionResolved)
        {
            throw new InvalidOperationException("An active encounter cannot be cleaned up.");
        }

        ImmutableDictionary<BattleUnitId, BattleUnitState> units = encounter.Units.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.TeamId == playerTeamId || !pair.Value.Surrendered
                ? pair.Value
                : pair.Value with { Prisoner = true });
        return encounter with { Units = units, CleanedUp = true };
    }
}
