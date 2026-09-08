using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

public sealed record VoyageEvent(
    ContentId Id,
    long Tick,
    ContentId SourceId,
    ContentId TargetId,
    VoyageCommandKind Kind,
    bool Succeeded,
    int Amount,
    string ResultCode);
