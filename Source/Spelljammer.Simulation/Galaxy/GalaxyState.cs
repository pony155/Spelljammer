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

public sealed record GalaxyMapState(
    int GeneratorVersion,
    ulong Seed,
    StarSystemId CurrentSystemId,
    ImmutableDictionary<StarSystemId, StarSystemState> Systems,
    ImmutableDictionary<StarwayId, StarwayState> Starways,
    ImmutableDictionary<StarSystemId, GalaxyKnowledgeLevel> Knowledge)
{
    public const int MaximumSystems = 1_024;
    public const int MaximumStarways = 4_096;
    public const int MaximumStarwaysPerSystem = 16;

    public GalaxyKnowledgeLevel KnowledgeOf(StarSystemId id) =>
        Knowledge.TryGetValue(id, out GalaxyKnowledgeLevel value) ? value : GalaxyKnowledgeLevel.Unknown;

    public IEnumerable<StarwayState> StarwaysFrom(StarSystemId id) =>
        Starways.Values
            .Where(value => value.FirstSystemId == id || value.SecondSystemId == id)
            .OrderBy(value => value.Id);

    public GalaxyMapState WithKnowledge(StarSystemId id, GalaxyKnowledgeLevel level)
    {
        if (!Systems.ContainsKey(id) || !Enum.IsDefined(level))
        {
            throw new ArgumentException("Galaxy knowledge update is invalid.", nameof(id));
        }

        GalaxyKnowledgeLevel current = KnowledgeOf(id);
        return level <= current ? this : this with { Knowledge = Knowledge.SetItem(id, level) };
    }
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
}

public sealed record GalaxyValidationResult(GalaxyValidationCode Code)
{
    public bool Accepted => Code == GalaxyValidationCode.None;
}

