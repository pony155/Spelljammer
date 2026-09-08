using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;
using Spelljammer.Simulation.Items;

namespace Spelljammer.Simulation.Encounters;

/// <summary>
/// Defines encounter-owned battle units, objectives, and personal encounter state.
/// </summary>
/// <remarks>
/// Code flow: Persistent characters are projected into units, combat replaces unit and status state during the encounter, and explicit post-battle projection commits durable character changes.
/// </remarks>
public sealed record BattleUnitState(
    BattleUnitId Id,
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
    ImmutableDictionary<BattleUnitId, BattleUnitState> Units,
    ImmutableDictionary<ObjectiveId, ObjectiveState> Objectives,
    ImmutableHashSet<ContentId> ExplorationChanges,
    ImmutableHashSet<ContentId> DamagedObjects,
    bool Retreated,
    bool CleanedUp)
{
    public const int MaximumStatusesPerUnit = 128;
}
