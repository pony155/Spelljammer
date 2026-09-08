using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

public static class EncounterLifecycle
{
    public static PersonalEncounterState TakePrisoner(PersonalEncounterState encounter, ActorId actorId)
    {
        if (!encounter.Actors.TryGetValue(actorId, out PersonalActorState? actor) ||
            (!actor.Surrendered && !actor.IsIncapacitated))
        {
            throw new InvalidOperationException("Only surrendered or incapacitated actors can be taken prisoner.");
        }

        return encounter with {
            Actors = encounter.Actors.SetItem(actorId, actor with { Prisoner = true }),
        };
    }

    public static PersonalEncounterState Cleanup(PersonalEncounterState encounter, TeamId playerTeamId)
    {
        bool objectivesResolved = encounter.Objectives.Values.All(value =>
            value is ObjectiveState.Completed or ObjectiveState.Failed or ObjectiveState.Abandoned);
        bool oppositionResolved = encounter.Actors.Values
            .Where(value => value.TeamId != playerTeamId)
            .All(value => value.IsIncapacitated || value.Surrendered || value.Prisoner);
        if (!encounter.Retreated && !objectivesResolved && !oppositionResolved)
        {
            throw new InvalidOperationException("An active encounter cannot be cleaned up.");
        }

        ImmutableDictionary<ActorId, PersonalActorState> actors = encounter.Actors.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.TeamId == playerTeamId || !pair.Value.Surrendered
                ? pair.Value
                : pair.Value with { Prisoner = true });
        return encounter with { Actors = actors, CleanedUp = true };
    }
}
