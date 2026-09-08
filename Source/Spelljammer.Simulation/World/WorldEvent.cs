using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

/// <summary>
/// Records deterministic outcomes emitted while authoritative world commands are committed.
/// </summary>
/// <remarks>
/// Code flow: Command resolution captures source, target, outcome, amount, and tick into an event that is appended to the bounded world event history.
/// </remarks>
public sealed record WorldEvent(
    ContentId Id,
    long Tick,
    ContentId SourceId,
    ContentId TargetId,
    WorldCommandKind Kind,
    bool Succeeded,
    int Amount,
    string ResultCode);
