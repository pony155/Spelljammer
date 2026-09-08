using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;
using Spelljammer.Simulation.Items;

namespace Spelljammer.Simulation.Encounters;

public enum InjurySeverity : byte
{
    Minor,
    Serious,
    Incapacitating,
}

public sealed record InjuryState(ContentId Id, InjurySeverity Severity, bool Stabilized);

public sealed record PersonalActorState(
    ActorId Id,
    TeamId TeamId,
    CharacterId? CharacterId,
    CellId CellId,
    CharacterTurnState Turn,
    CharacterResourceSet CharacterResources,
    bool Defending,
    bool Surrendered,
    bool Prisoner,
    ItemSystemState Items,
    ImmutableArray<InjuryState> Injuries)
{
    public StatusState Statuses { get; init; } = StatusState.Empty;
    public int TurnMeter => Turn.CurrentTurnMeter;
    public int TurnRate => Turn.BaseTurnMeterGain;
    public int ActionPoints => Turn.CurrentActionPoints;
    public int Health => CharacterResources.GetCurrentValue(CharacterResourceIds.Health);
    public bool IsIncapacitated => Health <= 0 ||
        Injuries.Any(value => value.Severity == InjurySeverity.Incapacitating && !value.Stabilized);
    public int ReservedReactionPoints { get; init; }
    public long ReactionExpiresTick { get; init; }

    public static PersonalActorState Create(
        ActorId actorId,
        TeamId teamId,
        CharacterState character,
        CellId cellId,
        CharacterResourceProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(profile);
        character.CharacterResources.Validate(profile);
        return new PersonalActorState(
            actorId,
            teamId,
            character.Id,
            cellId,
            CharacterTurnState.Create(profile.TurnRules),
            character.CharacterResources,
            false,
            false,
            false,
            character.Items,
            []) {
            Statuses = new StatusState([.. character.Statuses.Instances.Select(value => value with
            {
                TargetId = actorId.Value,
            })]),
        };
    }
}

public enum ObjectiveState : byte
{
    Active,
    Completed,
    Failed,
    Abandoned,
}

public sealed record PersonalEncounterState(
    EncounterId Id,
    TacticalBoard Board,
    ImmutableDictionary<ActorId, PersonalActorState> Actors,
    ImmutableDictionary<ObjectiveId, ObjectiveState> Objectives,
    ImmutableHashSet<ContentId> ExplorationChanges,
    ImmutableHashSet<ContentId> DamagedObjects,
    bool Retreated,
    bool CleanedUp)
{
    public const int MaximumStatusesPerActor = 128;
}
