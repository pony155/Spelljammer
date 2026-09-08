using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;

namespace Spelljammer.Content.Compilation;

/// <summary>
/// Owns the currently published immutable game-content snapshot.
/// </summary>
/// <remarks>
/// Code flow: Candidate pack sources compile into a complete result, failed candidates leave the current snapshot untouched, and successful snapshots replace it atomically for readers.
/// </remarks>
public sealed class GameContentRegistry
{
    private GameContentSnapshot? current;

    public GameContentSnapshot? Current => Volatile.Read(ref current);

    public ContentCompilationResult CompileAndPublish(
        IReadOnlyList<IContentPackSource> packSources,
        SemanticVersion gameVersion,
        ContentLimits? limits = null)
    {
        ContentCompilationResult result = new GameContentCompiler(limits).Compile(packSources, gameVersion);
        if (result.Succeeded)
        {
            Volatile.Write(ref current, result.Snapshot);
        }

        return result;
    }
}
