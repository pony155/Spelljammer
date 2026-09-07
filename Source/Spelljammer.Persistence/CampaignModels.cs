using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

namespace Spelljammer.Persistence;

public static class CampaignSaveVersions
{
    public const ushort Envelope = 1;
    public const ushort SaveSchema = 1;
    public const int WorldGenerator = 1;
    public const int Formula = 1;
    public const int Effect = 1;
}

public static class CampaignSaveLimits
{
    public const int MaximumSaveBytes = 8 * 1024 * 1024;
    public const int MaximumPreflightBytes = 256 * 1024;
    public const int MaximumPayloadBytes = MaximumSaveBytes - MaximumPreflightBytes - 64;
    public const int MaximumStringBytes = 1_024;
    public const int MaximumCollectionEntries = 4_096;
    public const int MaximumCharacters = 64;
    public const int MaximumShips = 32;
    public const int MaximumNestingDepth = 32;
    public const int MaximumRequiredDefinitions = 8_192;
    public const int MaximumRetainedCommands = 512;
    public const int MaximumRetainedEvents = 512;
    public const int MaximumRecoveryArtifacts = 1;
}

public enum SaveDiagnosticCode : byte
{
    None,
    Corrupt,
    Oversized,
    Unsupported,
    Truncated,
    ChecksumMismatch,
    MissingContent,
    IncompatibleContent,
    InvalidState,
    IoFailure,
    MigrationUnavailable,
    MigrationSourceMismatch,
    MigrationFailed,
}

public static class SaveDiagnosticCodes
{
    public const string Corrupt = "save.corrupt";
    public const string Oversized = "save.oversized";
    public const string Unsupported = "save.unsupported";
    public const string Truncated = "save.truncated";
    public const string ChecksumMismatch = "save.checksum-mismatch";
    public const string MissingContent = "save.content-missing";
    public const string IncompatibleContent = "save.content-incompatible";
    public const string InvalidState = "save.state-invalid";
    public const string IoFailure = "save.io-failure";
    public const string MigrationUnavailable = "save.migration-unavailable";
    public const string MigrationSourceMismatch = "save.migration-source-mismatch";
    public const string MigrationFailed = "save.migration-failed";

    public static string Stable(SaveDiagnosticCode code) => code switch
    {
        SaveDiagnosticCode.None => string.Empty,
        SaveDiagnosticCode.Corrupt => Corrupt,
        SaveDiagnosticCode.Oversized => Oversized,
        SaveDiagnosticCode.Unsupported => Unsupported,
        SaveDiagnosticCode.Truncated => Truncated,
        SaveDiagnosticCode.ChecksumMismatch => ChecksumMismatch,
        SaveDiagnosticCode.MissingContent => MissingContent,
        SaveDiagnosticCode.IncompatibleContent => IncompatibleContent,
        SaveDiagnosticCode.InvalidState => InvalidState,
        SaveDiagnosticCode.IoFailure => IoFailure,
        SaveDiagnosticCode.MigrationUnavailable => MigrationUnavailable,
        SaveDiagnosticCode.MigrationSourceMismatch => MigrationSourceMismatch,
        SaveDiagnosticCode.MigrationFailed => MigrationFailed,
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };
}

public sealed record CampaignPackLock(ContentId Id, SemanticVersion Version, int ContentRevision);

public sealed record CampaignContentLock(
    int BaseContentRevision,
    ImmutableArray<CampaignPackLock> Packs,
    ContentFingerprint ManifestFingerprint,
    ContentFingerprint SemanticFingerprint,
    ContentFingerprint EffectiveFingerprint,
    int GeneratorVersion,
    int FormulaVersion,
    int EffectVersion,
    ushort SaveSchemaVersion,
    ImmutableArray<ContentId> AppliedMigrationIds)
{
    public static CampaignContentLock Create(GameContentSnapshot snapshot, IEnumerable<ContentId>? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ImmutableArray<CampaignPackLock> packs = [.. snapshot.Packs.Select(value =>
            new CampaignPackLock(value.Id, value.Version, value.ContentRevision))];
        int baseRevision = packs.FirstOrDefault(value => value.Id == new ContentId("spelljammer.base"))?.ContentRevision ?? 0;
        string manifestText = string.Join('\n', packs.Select(value => $"{value.Id}|{value.Version}|{value.ContentRevision}"));
        ContentFingerprint manifestFingerprint = new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifestText))));
        ImmutableArray<ContentId> applied = [.. (migrations ?? []).Distinct().Order()];
        return new CampaignContentLock(
            baseRevision,
            packs,
            manifestFingerprint,
            snapshot.Fingerprint,
            snapshot.Fingerprint,
            CampaignSaveVersions.WorldGenerator,
            CampaignSaveVersions.Formula,
            CampaignSaveVersions.Effect,
            CampaignSaveVersions.SaveSchema,
            applied);
    }
}

public sealed record CampaignState(
    string GameBuild,
    CampaignContentLock ContentLock,
    ContentId CurrentLocationId,
    VoyageWorld Voyage,
    ImmutableArray<CharacterState> Characters)
{
    public const int MaximumGameBuildBytes = 128;
}

public enum ContentPreflightKind : byte
{
    Exact,
    Compatible,
    Migratable,
    Missing,
    Incompatible,
}

public sealed record ContentCompatibilityRule(
    ContentFingerprint SourceFingerprint,
    ContentFingerprint DestinationFingerprint);

public sealed record ContentPreflightResult(
    ContentPreflightKind Kind,
    SaveDiagnosticCode Diagnostic,
    CampaignContentLock? ContentLock,
    ImmutableArray<ContentId> MissingPackIds,
    ImmutableArray<ContentId> MissingDefinitionIds,
    ImmutableArray<ContentId> MigrationPath)
{
    public bool CanLoad => Kind is ContentPreflightKind.Exact or ContentPreflightKind.Compatible;
}

public sealed record CampaignReadResult(
    CampaignState? Campaign,
    ContentPreflightResult Preflight,
    SaveDiagnosticCode Diagnostic)
{
    public bool Succeeded => Campaign is not null;
}

public sealed record CampaignPublicationResult(
    CampaignState ActiveCampaign,
    bool Published,
    SaveDiagnosticCode Diagnostic,
    ContentPreflightResult Preflight);

public sealed class CampaignRegistry(CampaignState initial)
{
    private CampaignState active = initial ?? throw new ArgumentNullException(nameof(initial));

    public CampaignState Active => active;

    public CampaignPublicationResult Load(
        ReadOnlyMemory<byte> bytes,
        GameContentSnapshot content,
        IEnumerable<ContentCompatibilityRule>? compatibility = null,
        CampaignMigrationRegistry? migrations = null)
    {
        CampaignReadResult result = CampaignSaveCodec.Decode(bytes, content, compatibility, migrations);
        if (!result.Succeeded)
        {
            return new CampaignPublicationResult(active, false, result.Diagnostic, result.Preflight);
        }

        active = result.Campaign!;
        return new CampaignPublicationResult(active, true, SaveDiagnosticCode.None, result.Preflight);
    }
}
