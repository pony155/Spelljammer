using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

public readonly record struct FixedScalar : IComparable<FixedScalar>
{
    public const long Scale = 1_000;
    public const long MaximumMagnitude = 1_000_000_000 * Scale;

    public FixedScalar(long raw)
    {
        if (raw is < -MaximumMagnitude or > MaximumMagnitude)
        {
            throw new ArgumentOutOfRangeException(nameof(raw));
        }

        Raw = raw;
    }

    public long Raw { get; }
    public int CompareTo(FixedScalar other) => Raw.CompareTo(other.Raw);
    public static FixedScalar FromInt(int value) => new(checked(value * Scale));
    public static FixedScalar operator +(FixedScalar left, FixedScalar right) => new(checked(left.Raw + right.Raw));
    public static FixedScalar operator -(FixedScalar left, FixedScalar right) => new(checked(left.Raw - right.Raw));
    public static FixedScalar operator *(FixedScalar value, int multiplier) => new(checked(value.Raw * multiplier));
}

public readonly record struct FixedVector2(FixedScalar X, FixedScalar Y)
{
    public static FixedVector2 Zero => new(new FixedScalar(0), new FixedScalar(0));
    public static FixedVector2 operator +(FixedVector2 left, FixedVector2 right) => new(left.X + right.X, left.Y + right.Y);
    public static FixedVector2 operator -(FixedVector2 left, FixedVector2 right) => new(left.X - right.X, left.Y - right.Y);
}

public sealed record BoardValidationResult(TacticalBoard? Board, string RejectionCode)
{
    public bool Accepted => Board is not null;
}

public sealed record TacticalBoard(
    PersonalBoardDefinition Definition,
    ImmutableDictionary<CellId, BoardCellDefinition> Cells,
    ImmutableArray<ZoneLinkDefinition> Links,
    ImmutableDictionary<CellId, ImmutableArray<ActorId>> Occupants)
{
    public const int MaximumCells = 256;
    public const int MaximumLinks = 1_024;

    public static BoardValidationResult Create(
        PersonalBoardDefinition definition,
        IEnumerable<BoardCellDefinition> cells,
        IEnumerable<ZoneLinkDefinition> links)
    {
        BoardCellDefinition[] orderedCells = [.. cells.OrderBy(value => value.CellId)];
        ZoneLinkDefinition[] orderedLinks = [.. links.OrderBy(value => value.LinkId)];
        if (orderedCells.Length is 0 or > MaximumCells || orderedLinks.Length > MaximumLinks ||
            definition.CellIds.Length != orderedCells.Length || definition.LinkIds.Length != orderedLinks.Length ||
            definition.MaximumOccupants is < 1 or > 256 || definition.RequiredObjectiveIds.IsEmpty ||
            definition.RetreatCellIds.IsEmpty || orderedCells.Select(value => value.CellId).Distinct().Count() != orderedCells.Length ||
            orderedCells.Select(value => (value.Q, value.R)).Distinct().Count() != orderedCells.Length)
        {
            return new BoardValidationResult(null, "encounter.board-invalid");
        }

        ImmutableDictionary<CellId, BoardCellDefinition> byId = orderedCells.ToImmutableDictionary(value => value.CellId);
        if (!definition.CellIds.All(byId.ContainsKey) || !definition.RetreatCellIds.All(byId.ContainsKey) ||
            orderedCells.Any(value => value.Capacity is < 1 or > 8 || value.Cover is < 0 or > 100 ||
                value.Visibility is < 0 or > 100 || value.HazardTags.Length > 64) ||
            orderedLinks.Any(value => !byId.ContainsKey(value.FromCellId) || !byId.ContainsKey(value.ToCellId) ||
                !value.AccessId.IsValid || value.FromCellId == value.ToCellId || value.OneWay is < 0 or > 1 ||
                value.AllowsRetreat is < 0 or > 1) ||
            definition.RetreatCellIds.Any(retreat => !orderedLinks.Any(link => link.AllowsRetreat == 1 &&
                (link.FromCellId == retreat || link.ToCellId == retreat))))
        {
            return new BoardValidationResult(null, "encounter.board-invalid");
        }

        TacticalBoard candidate = new(definition, byId, [.. orderedLinks], ImmutableDictionary<CellId, ImmutableArray<ActorId>>.Empty);
        if (orderedCells.Skip(1).Any(value => candidate.FindPath(orderedCells[0].CellId, value.CellId, MaximumCells).IsEmpty))
        {
            return new BoardValidationResult(null, "encounter.board-disconnected");
        }

        return new BoardValidationResult(candidate, string.Empty);
    }

    public TacticalBoard Place(ActorId actorId, CellId cellId)
    {
        if (!Cells.TryGetValue(cellId, out BoardCellDefinition? cell) ||
            Occupants.Values.SelectMany(value => value).Contains(actorId))
        {
            throw new InvalidOperationException("Encounter placement is invalid.");
        }

        ImmutableArray<ActorId> occupants = Occupants.GetValueOrDefault(cellId, []);
        if (occupants.Length >= cell.Capacity || Occupants.Values.Sum(value => value.Length) >= Definition.MaximumOccupants)
        {
            throw new InvalidOperationException("Encounter placement exceeds capacity.");
        }

        return this with { Occupants = Occupants.SetItem(cellId, [.. occupants.Append(actorId).Order()]) };
    }

    public TacticalBoard Move(ActorId actorId, CellId destination, int maximumVisited)
    {
        CellId origin = Occupants.Single(pair => pair.Value.Contains(actorId)).Key;
        if (FindPath(origin, destination, maximumVisited).IsEmpty)
        {
            throw new InvalidOperationException("No bounded legal path exists.");
        }

        TacticalBoard removed = this with
        {
            Occupants = Occupants.SetItem(origin, Occupants[origin].Remove(actorId)),
        };
        return removed.Place(actorId, destination);
    }

    public ImmutableArray<CellId> FindPath(CellId start, CellId goal, int maximumVisited)
    {
        if (!Cells.ContainsKey(start) || !Cells.ContainsKey(goal) || maximumVisited is < 1 or > MaximumCells)
        {
            return [];
        }

        Queue<CellId> frontier = new();
        Dictionary<CellId, CellId?> previous = [];
        frontier.Enqueue(start);
        previous[start] = null;
        while (frontier.Count > 0 && previous.Count <= maximumVisited)
        {
            CellId current = frontier.Dequeue();
            if (current == goal)
            {
                List<CellId> result = [];
                for (CellId? cursor = goal; cursor is CellId value; cursor = previous[value])
                {
                    result.Add(value);
                }

                result.Reverse();
                return [.. result];
            }

            foreach (CellId adjacent in Adjacent(current).Where(value => !previous.ContainsKey(value)).Order())
            {
                if (previous.Count >= maximumVisited)
                {
                    break;
                }

                previous[adjacent] = current;
                frontier.Enqueue(adjacent);
            }
        }

        return [];
    }

    private IEnumerable<CellId> Adjacent(CellId cellId)
    {
        foreach (ZoneLinkDefinition link in Links)
        {
            if (link.FromCellId == cellId)
            {
                yield return link.ToCellId;
            }

            if (link.OneWay == 0 && link.ToCellId == cellId)
            {
                yield return link.FromCellId;
            }
        }
    }
}

public enum EquipmentCondition : byte
{
    Ready,
    Depleted,
    Damaged,
}

public sealed record EquipmentState(
    EquipmentId Id,
    ContentId SlotId,
    EquipmentCondition Condition,
    int ResourceRemaining);

public sealed record PersonalLoadout(ImmutableDictionary<ContentId, EquipmentState> Slots)
{
    public const int MaximumSlots = 5;

    public static PersonalLoadout Create(IEnumerable<EquipmentDefinition> definitions)
    {
        EquipmentDefinition[] values = [.. definitions];
        if (values.Length > MaximumSlots || values.Select(value => value.SlotId).Distinct().Count() != values.Length)
        {
            throw new InvalidOperationException("Personal loadout is invalid or exceeds capacity.");
        }

        return new PersonalLoadout(values.ToImmutableDictionary(
            value => value.SlotId,
            value => new EquipmentState(
                value.EquipmentId,
                value.SlotId,
                value.InitialStateId == new ContentId("equipment-state.damaged")
                    ? EquipmentCondition.Damaged
                    : value.ResourceCapacity == 0 ? EquipmentCondition.Depleted : EquipmentCondition.Ready,
                value.ResourceCapacity)));
    }
}

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
    int TurnMeter,
    int TurnRate,
    int ActionPoints,
    int Health,
    bool Defending,
    bool Surrendered,
    bool Prisoner,
    PersonalLoadout Loadout,
    ImmutableArray<InjuryState> Injuries)
{
    public bool IsIncapacitated => Health <= 0 || Injuries.Any(value => value.Severity == InjurySeverity.Incapacitating && !value.Stabilized);
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

public sealed record ActiveEffectState(
    EffectId Id,
    ContentId SourceId,
    ActorId TargetId,
    long ExpiresTick,
    int Stacks);

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
    public const int MaximumActiveEffects = 128;
    public ImmutableArray<ActiveEffectState> ActiveEffects { get; init; } = [];

    public PersonalEncounterState AddEffect(ActiveEffectState effect)
    {
        if (ActiveEffects.Length >= MaximumActiveEffects || effect.Stacks is < 1 or > 16)
        {
            throw new InvalidOperationException("Active effect capacity or stack limit was exceeded.");
        }

        return this with { ActiveEffects = ActiveEffects.Add(effect) };
    }
}

public static class EncounterLifecycle
{
    public static PersonalEncounterState TakePrisoner(PersonalEncounterState encounter, ActorId actorId)
    {
        if (!encounter.Actors.TryGetValue(actorId, out PersonalActorState? actor) ||
            (!actor.Surrendered && !actor.IsIncapacitated))
        {
            throw new InvalidOperationException("Only surrendered or incapacitated actors can be taken prisoner.");
        }

        return encounter with
        {
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
