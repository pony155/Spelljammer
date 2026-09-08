namespace Spelljammer.Storage;

/// <summary>Minimal file-system boundary required by transactional local-file stores.</summary>
public interface IAtomicFileSystem
{
    bool Exists(string path);
    long GetLength(string path);
    byte[] ReadAllBytes(string path);
    void EnsureDirectory(string path);
    void WriteDurable(string path, ReadOnlySpan<byte> bytes);
    void Move(string source, string destination);
    void Replace(string source, string destination, string? recoveryPath);
    void Delete(string path);
}

/// <summary>Physical implementation using durable writes and the platform atomic replace operation.</summary>
public class PhysicalAtomicFileSystem : IAtomicFileSystem
{
    public bool Exists(string path) => File.Exists(path);
    public long GetLength(string path) => new FileInfo(path).Length;
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    public void WriteDurable(string path, ReadOnlySpan<byte> bytes)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(true);
    }

    public void Move(string source, string destination) => File.Move(source, destination);

    public void Replace(string source, string destination, string? recoveryPath) =>
        File.Replace(source, destination, recoveryPath, true);

    public void Delete(string path) => File.Delete(path);
}

public sealed record AtomicValidation<TDiagnostic>(
    bool Accepted,
    TDiagnostic Diagnostic,
    ReadOnlyMemory<byte> Bytes);

public sealed record AtomicFileResult<TDiagnostic>(
    bool Succeeded,
    TDiagnostic Diagnostic,
    string TargetPath,
    string? RecoveryPath);

/// <summary>
/// Shared durable stage, read-back validation, atomic promotion, recovery, and bounded cleanup workflow.
/// Domain stores retain ownership of encoding and diagnostics.
/// </summary>
public sealed class AtomicFileStore<TDiagnostic>(
    IAtomicFileSystem files,
    TDiagnostic success,
    TDiagnostic missing,
    TDiagnostic oversized,
    TDiagnostic ioFailure)
    where TDiagnostic : struct, Enum
{
    public AtomicFileResult<TDiagnostic> Write(
        string targetPath,
        ReadOnlyMemory<byte> bytes,
        Func<ReadOnlyMemory<byte>, AtomicValidation<TDiagnostic>> validate,
        bool createDirectory)
    {
        ArgumentNullException.ThrowIfNull(validate);
        string target = ResolveFile(targetPath);
        string directory = Path.GetDirectoryName(target)!;
        string temporary = TemporaryPath(target, "tmp");
        string recovery = target + ".recovery";
        try
        {
            if (createDirectory)
            {
                files.EnsureDirectory(directory);
            }
            else if (!Directory.Exists(directory))
            {
                return Failed(target, ioFailure);
            }

            files.WriteDurable(temporary, bytes.Span);
            AtomicValidation<TDiagnostic> staged = validate(files.ReadAllBytes(temporary));
            if (!staged.Accepted)
            {
                return Failed(target, staged.Diagnostic);
            }

            if (files.Exists(target))
            {
                files.Replace(temporary, target, recovery);
                return new AtomicFileResult<TDiagnostic>(true, success, target, recovery);
            }

            files.Move(temporary, target);
            return new AtomicFileResult<TDiagnostic>(true, success, target, null);
        }
        catch (IOException)
        {
            return Failed(target, ioFailure, files.Exists(recovery) ? recovery : null);
        }
        catch (UnauthorizedAccessException)
        {
            return Failed(target, ioFailure, files.Exists(recovery) ? recovery : null);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public AtomicFileResult<TDiagnostic> Recover(
        string targetPath,
        long maximumBytes,
        Func<ReadOnlyMemory<byte>, AtomicValidation<TDiagnostic>> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);
        string target = ResolveFile(targetPath);
        string recovery = target + ".recovery";
        string temporary = TemporaryPath(target, "recover");
        try
        {
            if (!files.Exists(recovery))
            {
                return Failed(target, missing);
            }

            long length = files.GetLength(recovery);
            if (length < 0)
            {
                return Failed(target, ioFailure, recovery);
            }

            if (length > maximumBytes)
            {
                return Failed(target, oversized, recovery);
            }

            AtomicValidation<TDiagnostic> candidate = validate(files.ReadAllBytes(recovery));
            if (!candidate.Accepted)
            {
                return Failed(target, candidate.Diagnostic, recovery);
            }

            files.WriteDurable(temporary, candidate.Bytes.Span);
            AtomicValidation<TDiagnostic> staged = validate(files.ReadAllBytes(temporary));
            if (!staged.Accepted)
            {
                return Failed(target, staged.Diagnostic, recovery);
            }

            if (files.Exists(target))
            {
                files.Replace(temporary, target, null);
            }
            else
            {
                files.Move(temporary, target);
            }

            return new AtomicFileResult<TDiagnostic>(true, success, target, recovery);
        }
        catch (IOException)
        {
            return Failed(target, ioFailure, files.Exists(recovery) ? recovery : null);
        }
        catch (UnauthorizedAccessException)
        {
            return Failed(target, ioFailure, files.Exists(recovery) ? recovery : null);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public bool CleanupRecovery(string targetPath)
    {
        string recovery = ResolveFile(targetPath) + ".recovery";
        if (!files.Exists(recovery))
        {
            return true;
        }

        try
        {
            files.Delete(recovery);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string ResolveFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        if (Path.EndsInDirectorySeparator(full))
        {
            throw new ArgumentException("A storage target must identify a file.", nameof(path));
        }

        return full;
    }

    private static string TemporaryPath(string target, string operation) =>
        Path.Combine(
            Path.GetDirectoryName(target)!,
            $".{Path.GetFileName(target)}.{operation}-{Guid.NewGuid():N}");

    private AtomicFileResult<TDiagnostic> Failed(
        string target,
        TDiagnostic diagnostic,
        string? recovery = null) =>
        new(false, diagnostic, target, recovery);

    private void TryDelete(string path)
    {
        try
        {
            if (files.Exists(path))
            {
                files.Delete(path);
            }
        }
        catch (IOException)
        {
            // Retain only this exact orphan when bounded cleanup cannot complete.
        }
        catch (UnauthorizedAccessException)
        {
            // Retain only this exact orphan when bounded cleanup cannot complete.
        }
    }
}
