using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Galaxy;

public readonly record struct StarSystemId : IComparable<StarSystemId>
{
    public StarSystemId(ContentId value) => Value = Require(value, "system.");
    public StarSystemId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(StarSystemId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();

    private static ContentId Require(ContentId value, string prefix) =>
        value.IsValid && value.ToString().StartsWith(prefix, StringComparison.Ordinal)
            ? value
            : throw new ArgumentException($"Identifier must begin with '{prefix}'.", nameof(value));
}

public readonly record struct StarwayId : IComparable<StarwayId>
{
    public StarwayId(ContentId value) => Value = Require(value, "starway.");
    public StarwayId(string value) : this(new ContentId(value)) { }
    public ContentId Value { get; }
    public bool IsValid => Value.IsValid;
    public int CompareTo(StarwayId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();

    private static ContentId Require(ContentId value, string prefix) =>
        value.IsValid && value.ToString().StartsWith(prefix, StringComparison.Ordinal)
            ? value
            : throw new ArgumentException($"Identifier must begin with '{prefix}'.", nameof(value));
}

public enum GalaxyKnowledgeLevel : byte
{
    Unknown,
    Rumored,
    Detected,
    Surveyed,
    Charted,
}

public sealed record StarSystemState(
    StarSystemId Id,
    int Ordinal,
    int Region,
    int DisplayX,
    int DisplayY,
    ContentId ArchetypeId);

public sealed record StarwayState(
    StarwayId Id,
    StarSystemId FirstSystemId,
    StarSystemId SecondSystemId,
    int TravelTime,
    int FuelCost,
    int Danger);

public static class GalaxyLimits
{
    public const int MaximumSystems = 1_024;
    public const int MaximumStarways = 4_096;
    public const int MaximumStarwaysPerSystem = 16;
    public const int MaximumChangedSites = 4_096;
}

public sealed record GalaxyTopology(
    int GeneratorVersion,
    ulong Seed,
    ImmutableDictionary<StarSystemId, StarSystemState> Systems,
    ImmutableDictionary<StarwayId, StarwayState> Starways)
{
    public IEnumerable<StarwayState> StarwaysFrom(StarSystemId id) =>
        Starways.Values
            .Where(value => value.FirstSystemId == id || value.SecondSystemId == id)
            .OrderBy(value => value.Id);
}

public sealed record GalaxyKnowledgeState(
    ImmutableDictionary<StarSystemId, GalaxyKnowledgeLevel> Systems)
{
    public GalaxyKnowledgeLevel LevelOf(StarSystemId id) =>
        Systems.TryGetValue(id, out GalaxyKnowledgeLevel value) ? value : GalaxyKnowledgeLevel.Unknown;

    public GalaxyKnowledgeState WithLevel(StarSystemId id, GalaxyKnowledgeLevel level)
    {
        if (!id.IsValid || !Enum.IsDefined(level))
        {
            throw new ArgumentException("Galaxy knowledge update is invalid.", nameof(id));
        }

        GalaxyKnowledgeLevel current = LevelOf(id);
        return level <= current ? this : this with { Systems = Systems.SetItem(id, level) };
    }
}

public sealed record StarwayDynamicState(
    bool IsBlocked,
    int DangerModifier,
    ContentId? ControllingFactionId);

public sealed record GalaxyDynamicState(
    ImmutableDictionary<StarwayId, StarwayDynamicState> Starways,
    ImmutableHashSet<ContentId> ChangedSiteIds)
{
    public static GalaxyDynamicState Empty { get; } = new(
        ImmutableDictionary<StarwayId, StarwayDynamicState>.Empty,
        ImmutableHashSet<ContentId>.Empty);

    public StarwayDynamicState StateOf(StarwayId id) =>
        Starways.TryGetValue(id, out StarwayDynamicState? value)
            ? value
            : new(false, 0, null);
}

public sealed record GalaxyState(
    GalaxyTopology Topology,
    GalaxyKnowledgeState Knowledge,
    GalaxyDynamicState Dynamic)
{
    public GalaxyKnowledgeLevel KnowledgeOf(StarSystemId id) => Knowledge.LevelOf(id);

    public IEnumerable<StarwayState> StarwaysFrom(StarSystemId id) => Topology.StarwaysFrom(id);

    public GalaxyState WithKnowledge(StarSystemId id, GalaxyKnowledgeLevel level)
    {
        if (!Topology.Systems.ContainsKey(id))
        {
            throw new ArgumentException("Galaxy knowledge update is invalid.", nameof(id));
        }

        return this with { Knowledge = Knowledge.WithLevel(id, level) };
    }
}

public sealed record VoyageNavigationState(
    StarSystemId CurrentSystemId,
    StarwayId? ActiveStarwayId,
    ImmutableArray<StarwayId> PlannedRoute,
    int RouteProgress,
    long DepartureTick,
    long ArrivalTick)
{
    public static VoyageNavigationState AtAnchor(StarSystemId systemId) =>
        new(systemId, null, [], 0, 0, 0);
}

public enum GalaxyValidationCode : byte
{
    None,
    InvalidHeader,
    CapacityExceeded,
    InvalidSystem,
    InvalidStarway,
    DuplicateConnection,
    DegreeExceeded,
    UnreachableSystem,
    InvalidKnowledge,
    InvalidDynamicState,
}

public sealed record GalaxyValidationResult(GalaxyValidationCode Code)
{
    public bool Accepted => Code == GalaxyValidationCode.None;
}
