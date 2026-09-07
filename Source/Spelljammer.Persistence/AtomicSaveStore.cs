using Spelljammer.Content.Compilation;

namespace Spelljammer.Persistence;

public interface ICampaignSaveFileSystem
{
    bool Exists(string path);
    long GetLength(string path);
    byte[] ReadAllBytes(string path);
    void WriteDurable(string path, ReadOnlySpan<byte> bytes);
    void Move(string source, string destination);
    void Replace(string source, string destination, string? recoveryPath);
    void Delete(string path);
}

public sealed class PhysicalCampaignSaveFileSystem : ICampaignSaveFileSystem
{
    public bool Exists(string path) => File.Exists(path);
    public long GetLength(string path) => new FileInfo(path).Length;
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public void WriteDurable(string path, ReadOnlySpan<byte> bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024,
            FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(true);
    }

    public void Move(string source, string destination) => File.Move(source, destination);
    public void Replace(string source, string destination, string? recoveryPath) =>
        File.Replace(source, destination, recoveryPath, true);
    public void Delete(string path) => File.Delete(path);
}

public sealed record SaveWriteResult(
    bool Succeeded,
    SaveDiagnosticCode Diagnostic,
    string TargetPath,
    string? RecoveryPath);

public sealed class CampaignSaveStore(ICampaignSaveFileSystem? fileSystem = null)
{
    private readonly ICampaignSaveFileSystem files = fileSystem ?? new PhysicalCampaignSaveFileSystem();

    public CampaignReadResult Read(
        string sourcePath,
        GameContentSnapshot content,
        IEnumerable<ContentCompatibilityRule>? compatibility = null,
        CampaignMigrationRegistry? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        string source = ResolveTarget(sourcePath);
        try
        {
            if (!files.Exists(source))
            {
                return FailedRead(SaveDiagnosticCode.IoFailure);
            }

            long length = files.GetLength(source);
            if (length > CampaignSaveLimits.MaximumSaveBytes)
            {
                return FailedRead(SaveDiagnosticCode.Oversized);
            }

            if (length < 0)
            {
                return FailedRead(SaveDiagnosticCode.Corrupt);
            }

            return CampaignSaveCodec.Decode(files.ReadAllBytes(source), content, compatibility, migrations);
        }
        catch (IOException)
        {
            return FailedRead(SaveDiagnosticCode.IoFailure);
        }
        catch (UnauthorizedAccessException)
        {
            return FailedRead(SaveDiagnosticCode.IoFailure);
        }
    }

    public SaveWriteResult Save(string targetPath, ReadOnlyMemory<byte> bytes)
    {
        string target = ResolveTarget(targetPath);
        string? directory = Path.GetDirectoryName(target);
        if (directory is null || !Directory.Exists(directory))
        {
            return new SaveWriteResult(false, SaveDiagnosticCode.IoFailure, target, null);
        }

        string temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.tmp-{Guid.NewGuid():N}");
        string recovery = target + ".recovery";
        try
        {
            files.WriteDurable(temporary, bytes.Span);
            byte[] staged = files.ReadAllBytes(temporary);
            SaveDiagnosticCode validation = CampaignSaveCodec.ValidateEnvelope(staged);
            if (validation != SaveDiagnosticCode.None)
            {
                return new SaveWriteResult(false, validation, target, null);
            }

            if (files.Exists(target))
            {
                files.Replace(temporary, target, recovery);
                return new SaveWriteResult(true, SaveDiagnosticCode.None, target, recovery);
            }

            files.Move(temporary, target);
            return new SaveWriteResult(true, SaveDiagnosticCode.None, target, null);
        }
        catch (IOException)
        {
            return new SaveWriteResult(false, SaveDiagnosticCode.IoFailure, target, files.Exists(recovery) ? recovery : null);
        }
        catch (UnauthorizedAccessException)
        {
            return new SaveWriteResult(false, SaveDiagnosticCode.IoFailure, target, files.Exists(recovery) ? recovery : null);
        }
        finally
        {
            if (files.Exists(temporary))
            {
                try
                {
                    files.Delete(temporary);
                }
                catch (IOException)
                {
                    // The exact orphan is retained for a later bounded cleanup attempt.
                }
                catch (UnauthorizedAccessException)
                {
                    // The exact orphan is retained for a later bounded cleanup attempt.
                }
            }
        }
    }

    public SaveWriteResult Recover(string targetPath)
    {
        string target = ResolveTarget(targetPath);
        string recovery = target + ".recovery";
        try
        {
            if (!files.Exists(recovery))
            {
                return new SaveWriteResult(false, SaveDiagnosticCode.IoFailure, target, null);
            }

            byte[] bytes = files.ReadAllBytes(recovery);
            SaveDiagnosticCode validation = CampaignSaveCodec.ValidateEnvelope(bytes);
            if (validation != SaveDiagnosticCode.None)
            {
                return new SaveWriteResult(false, validation, target, recovery);
            }

            string directory = Path.GetDirectoryName(target)!;
            string temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.recover-{Guid.NewGuid():N}");
            try
            {
                files.WriteDurable(temporary, bytes);
                if (files.Exists(target))
                {
                    files.Replace(temporary, target, null);
                }
                else
                {
                    files.Move(temporary, target);
                }
            }
            finally
            {
                if (files.Exists(temporary))
                {
                    files.Delete(temporary);
                }
            }

            return new SaveWriteResult(true, SaveDiagnosticCode.None, target, recovery);
        }
        catch (IOException)
        {
            return new SaveWriteResult(false, SaveDiagnosticCode.IoFailure, target, files.Exists(recovery) ? recovery : null);
        }
        catch (UnauthorizedAccessException)
        {
            return new SaveWriteResult(false, SaveDiagnosticCode.IoFailure, target, files.Exists(recovery) ? recovery : null);
        }
    }

    public bool CleanupRecovery(string targetPath)
    {
        string recovery = ResolveTarget(targetPath) + ".recovery";
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

    private static string ResolveTarget(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        if (Path.EndsInDirectorySeparator(full))
        {
            throw new ArgumentException("A save target must be a file.", nameof(path));
        }

        return full;
    }

    private static CampaignReadResult FailedRead(SaveDiagnosticCode diagnostic) => new(
        null,
        new ContentPreflightResult(ContentPreflightKind.Incompatible, diagnostic, null, [], [], []),
        diagnostic);
}
