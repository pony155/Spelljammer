using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;

namespace Spelljammer.Content.Compilation;

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
