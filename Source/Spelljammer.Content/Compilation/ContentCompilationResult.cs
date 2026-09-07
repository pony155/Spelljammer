using System.Collections.Immutable;
using Spelljammer.Content.Diagnostics;

namespace Spelljammer.Content.Compilation;

public sealed record ContentCompilationResult(
    GameContentSnapshot? Snapshot,
    ImmutableArray<ContentDiagnostic> Diagnostics,
    ContentIoFailure? IoFailure)
{
    public bool Succeeded => Snapshot is not null && Diagnostics.IsEmpty && IoFailure is null;
}
