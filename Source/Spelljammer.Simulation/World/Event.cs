using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.World;

public sealed record Event(
    ContentId Id,
    long Tick,
    ContentId SourceId,
    ContentId TargetId,
    CommandKind Kind,
    bool Succeeded,
    int Amount,
    string ResultCode);
