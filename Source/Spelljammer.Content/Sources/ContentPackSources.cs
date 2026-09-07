namespace Spelljammer.Content.Sources;

public interface IContentPackSource
{
    IReadOnlyList<string> EnumerateFiles();

    byte[] ReadFile(string relativePath, int maximumBytes);
}

public sealed class DirectoryContentPackSource : IContentPackSource
{
    private readonly string root;

    public DirectoryContentPackSource(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        DirectoryInfo rootInfo = new(Path.GetFullPath(rootPath));
        root = (rootInfo.Exists ? rootInfo.ResolveLinkTarget(true)?.FullName : null) ?? rootInfo.FullName;
    }

    public IReadOnlyList<string> EnumerateFiles()
    {
        if (!Directory.Exists(root))
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.NotFound, null);
        }

        try
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
                .ToArray();
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.AccessDenied, null, exception);
        }
        catch (IOException exception)
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.ReadFailed, null, exception);
        }
    }

    public byte[] ReadFile(string relativePath, int maximumBytes)
    {
        string fullPath;
        try
        {
            fullPath = ResolveInsideRoot(relativePath);
        }
        catch (ArgumentException exception)
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.ReadFailed, relativePath, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.AccessDenied, relativePath, exception);
        }
        catch (IOException exception)
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.ReadFailed, relativePath, exception);
        }

        try
        {
            using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > maximumBytes)
            {
                throw new ContentSourceLimitException(relativePath);
            }

            byte[] bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            if (stream.Position != stream.Length)
            {
                throw new ContentSourceException(Diagnostics.ContentIoFailureKind.ChangedDuringRead, relativePath);
            }

            return bytes;
        }
        catch (ContentSourceException)
        {
            throw;
        }
        catch (FileNotFoundException exception)
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.ChangedDuringRead, relativePath, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.AccessDenied, relativePath, exception);
        }
        catch (IOException exception)
        {
            throw new ContentSourceException(Diagnostics.ContentIoFailureKind.ReadFailed, relativePath, exception);
        }
    }

    private string ResolveInsideRoot(string relativePath)
    {
        string platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string resolved = Path.GetFullPath(platformPath, root);
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Path escapes the configured root.", nameof(relativePath));
        }

        string current = root;
        string[] components = platformPath.Split(Path.DirectorySeparatorChar);
        for (int index = 0; index < components.Length; index++)
        {
            current = Path.Combine(current, components[index]);
            FileSystemInfo info = index == components.Length - 1 ? new FileInfo(current) : new DirectoryInfo(current);
            FileSystemInfo? target = info.Exists ? info.ResolveLinkTarget(true) : null;
            if (target is not null)
            {
                current = target.FullName;
                if (!current.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Path resolves outside the configured root.", nameof(relativePath));
                }
            }
        }

        return current;
    }
}

public class ContentSourceException : Exception
{
    public ContentSourceException(Diagnostics.ContentIoFailureKind kind, string? relativePath, Exception? innerException = null)
        : base("The configured content source could not be read.", innerException)
    {
        Kind = kind;
        RelativePath = relativePath;
    }

    public Diagnostics.ContentIoFailureKind Kind { get; }
    public string? RelativePath { get; }
}

public sealed class ContentSourceLimitException : Exception
{
    public ContentSourceLimitException(string relativePath)
        : base("The content source entry exceeds its byte limit.") => RelativePath = relativePath;

    public string RelativePath { get; }
}
