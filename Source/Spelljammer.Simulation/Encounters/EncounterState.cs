using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

/// <summary>
/// A fixed-point scalar value with a scale factor for sub-integer precision.
/// </summary>
/// <remarks>
/// Uses fixed-point arithmetic (1000x scale) to avoid floating-point precision issues in game calculations.
/// Values range from -1 billion to +1 billion before scaling. Supports comparison and basic arithmetic operations.
/// </remarks>
public readonly record struct FixedScalar : IComparable<FixedScalar>
{
    /// <summary>
    /// The scale factor for fixed-point representation (1000).
    /// </summary>
    public const long Scale = 1_000;

    /// <summary>
    /// The maximum magnitude of a raw fixed-point value (±1 billion * 1000).
    /// </summary>
    public const long MaximumMagnitude = 1_000_000_000 * Scale;

    /// <summary>
    /// Initializes a fixed-point scalar from a raw value.
    /// </summary>
    /// <param name="raw">The raw fixed-point value (may be in range ±1 billion * 1000).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the value exceeds the maximum magnitude.</exception>
    public FixedScalar(long raw)
    {
        if (raw is < -MaximumMagnitude or > MaximumMagnitude)
        {
            throw new ArgumentOutOfRangeException(nameof(raw));
        }

        Raw = raw;
    }

    /// <summary>Gets the raw fixed-point value (multiply by 0.001 to get decimal).</summary>
    public long Raw { get; }

    /// <summary>Compares this scalar with another using their raw values.</summary>
    public int CompareTo(FixedScalar other) => Raw.CompareTo(other.Raw);

    /// <summary>Creates a fixed-point scalar from an integer value.</summary>
    /// <param name="value">The integer value to convert (multiplied by scale internally).</param>
    public static FixedScalar FromInt(int value) => new(checked(value * Scale));

    /// <summary>Adds two fixed-point scalars with overflow checking.</summary>
    public static FixedScalar operator +(FixedScalar left, FixedScalar right) => new(checked(left.Raw + right.Raw));

    /// <summary>Subtracts two fixed-point scalars with overflow checking.</summary>
    public static FixedScalar operator -(FixedScalar left, FixedScalar right) => new(checked(left.Raw - right.Raw));

    /// <summary>Multiplies a fixed-point scalar by an integer multiplier with overflow checking.</summary>
    public static FixedScalar operator *(FixedScalar value, int multiplier) => new(checked(value.Raw * multiplier));
}

/// <summary>
/// A 2D vector with fixed-point components for tactical positioning.
/// </summary>
public readonly record struct FixedVector2(FixedScalar X, FixedScalar Y)
{
    /// <summary>Gets the zero vector (0, 0).</summary>
    public static FixedVector2 Zero => new(new FixedScalar(0), new FixedScalar(0));

    /// <summary>Adds two vectors component-wise.</summary>
    public static FixedVector2 operator +(FixedVector2 left, FixedVector2 right) => new(left.X + right.X, left.Y + right.Y);

    /// <summary>Subtracts two vectors component-wise.</summary>
    public static FixedVector2 operator -(FixedVector2 left, FixedVector2 right) => new(left.X - right.X, left.Y - right.Y);
}

/// <summary>
/// The result of attempting to validate and create a tactical board.
/// </summary>
/// <remarks>
/// If validation succeeds, the Board property contains the validated tactical board. If validation fails,
/// RejectionCode contains a localization key describing the validation error.
/// </remarks>
/// <param name="Board">The validated tactical board, or null if validation failed.</param>
/// <param name="RejectionCode">A localization key describing the validation error (empty string if accepted).</param>
public sealed record BoardValidationResult(TacticalBoard? Board, string RejectionCode)
{
    /// <summary>Gets whether the board was successfully validated and created.</summary>
    public bool Accepted => Board is not null;
}

/// <summary>
/// Represents the tactical combat board where an encounter takes place.
/// </summary>
/// <remarks>
/// The tactical board is a hex or square grid populated with cells that have properties like capacity, cover, visibility,
/// and hazards. Links between cells define movement paths. The board is immutable and fully validated at creation.
/// </remarks>
/// <param name="Definition">The definition that describes this board's structure and rules.</param>
/// <param name="Cells">Dictionary of all cells on the board, indexed by cell ID.</param>
/// <param name="Links">Array of all links (movement paths) between cells.</param>
/// <param name="Occupants">Dictionary mapping cells to the actors occupying them.</param>
public sealed record TacticalBoard(
    PersonalBoardDefinition Definition,
    ImmutableDictionary<CellId, BoardCellDefinition> Cells,
    ImmutableArray<ZoneLinkDefinition> Links,
    ImmutableDictionary<CellId, ImmutableArray<ActorId>> Occupants)
{
    /// <summary>The maximum number of cells allowed on a tactical board.</summary>
    public const int MaximumCells = 256;

    /// <summary>The maximum number of links (movement paths) allowed on a tactical board.</summary>
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
    CharacterTurnState Turn,
    CharacterResourceSet CharacterResources,
    bool Defending,
    bool Surrendered,
    bool Prisoner,
    PersonalLoadout Loadout,
    ImmutableArray<InjuryState> Injuries)
{
    public int TurnMeter => Turn.CurrentTurnMeter;
    public int TurnRate => Turn.BaseTurnMeterGain;
    public int ActionPoints => Turn.CurrentActionPoints;
    public int Health => CharacterResources.GetCurrentValue(CharacterResourceIds.Health);
    public bool IsIncapacitated => Health <= 0 || Injuries.Any(value => value.Severity == InjurySeverity.Incapacitating && !value.Stabilized);
    public int ReservedReactionPoints { get; init; }
    public long ReactionExpiresTick { get; init; }

    public static PersonalActorState Create(
        ActorId actorId,
        TeamId teamId,
        CharacterState character,
        CellId cellId,
        CharacterResourceProfileDefinition profile,
        PersonalLoadout loadout)
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
            loadout,
            []);
    }
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
