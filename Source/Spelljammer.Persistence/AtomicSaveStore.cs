using Spelljammer.Content.Compilation;
using Spelljammer.Storage;

namespace Spelljammer.Persistence;

/// <summary>Campaign-save specialization of the shared transactional file-system boundary.</summary>
public interface ICampaignSaveFileSystem : IAtomicFileSystem;

/// <summary>Physical campaign-save file system retained as a compatibility-friendly construction type.</summary>
public sealed class PhysicalCampaignSaveFileSystem : PhysicalAtomicFileSystem, ICampaignSaveFileSystem;

/// <summary>
/// The result of a campaign save write operation with diagnostic and recovery information.
/// </summary>
public sealed record SaveWriteResult(
    bool Succeeded,
    SaveDiagnosticCode Diagnostic,
    string TargetPath,
    string? RecoveryPath);

public sealed class CampaignSaveStore
{
    private readonly ICampaignSaveFileSystem files;
    private readonly AtomicFileStore<SaveDiagnosticCode> atomicFiles;

    public CampaignSaveStore(ICampaignSaveFileSystem? fileSystem = null)
    {
        files = fileSystem ?? new PhysicalCampaignSaveFileSystem();
        atomicFiles = new AtomicFileStore<SaveDiagnosticCode>(
            files,
            SaveDiagnosticCode.None,
            SaveDiagnosticCode.IoFailure,
            SaveDiagnosticCode.Oversized,
            SaveDiagnosticCode.IoFailure);
    }

    public CampaignReadResult Read(
        string sourcePath,
        GameContentSnapshot content,
        IEnumerable<ContentCompatibilityRule>? compatibility = null,
        CampaignMigrationRegistry? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        string source = AtomicFileStore<SaveDiagnosticCode>.ResolveFile(sourcePath);
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
        AtomicFileResult<SaveDiagnosticCode> result = atomicFiles.Write(
            targetPath,
            bytes,
            candidate =>
            {
                SaveDiagnosticCode diagnostic = CampaignSaveCodec.ValidateEnvelope(candidate);
                return new AtomicValidation<SaveDiagnosticCode>(
                    diagnostic == SaveDiagnosticCode.None,
                    diagnostic,
                    candidate);
            },
            createDirectory: false);
        return new SaveWriteResult(
            result.Succeeded,
            result.Diagnostic,
            result.TargetPath,
            result.RecoveryPath);
    }

    public SaveWriteResult Recover(string targetPath)
    {
        AtomicFileResult<SaveDiagnosticCode> result = atomicFiles.Recover(
            targetPath,
            CampaignSaveLimits.MaximumSaveBytes,
            candidate =>
            {
                SaveDiagnosticCode diagnostic = CampaignSaveCodec.ValidateEnvelope(candidate);
                return new AtomicValidation<SaveDiagnosticCode>(
                    diagnostic == SaveDiagnosticCode.None,
                    diagnostic,
                    candidate);
            });
        return new SaveWriteResult(
            result.Succeeded,
            result.Diagnostic,
            result.TargetPath,
            result.RecoveryPath);
    }

    public bool CleanupRecovery(string targetPath) => atomicFiles.CleanupRecovery(targetPath);

    private static CampaignReadResult FailedRead(SaveDiagnosticCode diagnostic) => new(
        null,
        new ContentPreflightResult(ContentPreflightKind.Incompatible, diagnostic, null, [], [], []),
        diagnostic);
}
