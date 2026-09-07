using System.Collections.Immutable;
using Spelljammer.Content.Diagnostics;

namespace Spelljammer.Content.Compilation;

/// <summary>
/// The result of compiling and validating content packs.
/// </summary>
/// <remarks>
/// Compilation succeeds when all packs load, parse, and validate without errors.
/// Compilation may succeed with warnings (diagnostics) but a valid snapshot.
/// Compilation fails if there are parsing errors or IO failures.
/// </remarks>
/// <param name="Snapshot">The compiled content snapshot if compilation succeeded; null if it failed.</param>
/// <param name="Diagnostics">Array of diagnostic messages (warnings, errors, info) from compilation.</param>
/// <param name="IoFailure">IO error details if an I/O failure occurred during compilation.</param>
public sealed record ContentCompilationResult(
    GameContentSnapshot? Snapshot,
    ImmutableArray<ContentDiagnostic> Diagnostics,
    ContentIoFailure? IoFailure)
{
    /// <summary>
    /// Gets whether compilation succeeded (snapshot created, no errors, no IO failures).
    /// </summary>
    public bool Succeeded => Snapshot is not null && Diagnostics.IsEmpty && IoFailure is null;
}
