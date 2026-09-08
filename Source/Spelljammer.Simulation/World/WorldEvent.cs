using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

public sealed record WorldEvent(
    ContentId Id,
    long Tick,
    ContentId SourceId,
    ContentId TargetId,
    WorldCommandKind Kind,
    bool Succeeded,
    int Amount,
    string ResultCode);
