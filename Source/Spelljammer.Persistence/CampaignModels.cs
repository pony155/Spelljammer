using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

namespace Spelljammer.Persistence;

/// <summary>
/// Version constants for campaign save file components.
/// </summary>
/// <remarks>
/// These version numbers are used to track format compatibility and enable migrations when save formats change.
/// </remarks>
public static class CampaignSaveVersions
{
    /// <summary>Version of the save file envelope (header and container format).</summary>
    public const ushort Envelope = 1;

    /// <summary>
    /// Version of the save schema. Version 7 persists encounter resources and the data-driven turn economy.
    /// </summary>
    public const ushort SaveSchema = 9;
    public const ushort OldestSupportedSaveSchema = 7;
    public const ushort ItemInstanceSaveSchema = 8;

    /// <summary>Version of the world generation algorithm used in this save.</summary>
    public const int WorldGenerator = 1;

    /// <summary>Version of the formula/calculation system used in this save.</summary>
    public const int Formula = 1;

    /// <summary>Version of the effect/status system used in this save.</summary>
    public const int Effect = 1;
}

/// <summary>
/// Enforced size and structural limits for campaign save files.
/// </summary>
/// <remarks>
/// These limits prevent malformed saves from consuming excessive memory or containing too many nested structures.
/// They also protect against intentional or accidental buffer overflow attacks.
/// </remarks>
public static class CampaignSaveLimits
{
    /// <summary>Maximum total size of a campaign save file in bytes (8 MB).</summary>
    public const int MaximumSaveBytes = 8 * 1024 * 1024;

    /// <summary>Maximum size of the preflight metadata section (checksums, headers, etc.) in bytes (256 KB).</summary>
    public const int MaximumPreflightBytes = 256 * 1024;

    /// <summary>Maximum size of the actual game state payload in bytes.</summary>
    public const int MaximumPayloadBytes = MaximumSaveBytes - MaximumPreflightBytes - 64;

    /// <summary>Maximum length of any individual string in the save file in bytes (1 KB).</summary>
    public const int MaximumStringBytes = 1_024;

    /// <summary>Maximum number of entries in any collection (array, list) in the save.</summary>
    public const int MaximumCollectionEntries = 4_096;

    /// <summary>
    /// Defensive decoding ceiling for character records. Gameplay roster capacity comes from the active scenario.
    /// </summary>
    public const int MaximumCharacters = 64;

    /// <summary>Maximum number of ships that can be stored in a campaign.</summary>
    public const int MaximumShips = 32;

    /// <summary>Maximum depth of nested data structures (objects within objects).</summary>
    public const int MaximumNestingDepth = 32;

    /// <summary>Maximum number of content definitions that may be referenced by a save.</summary>
    public const int MaximumRequiredDefinitions = 8_192;

    /// <summary>Maximum number of commands retained for undo/redo functionality.</summary>
    public const int MaximumRetainedCommands = 512;

    /// <summary>Maximum number of events retained for history/replay.</summary>
    public const int MaximumRetainedEvents = 512;

    /// <summary>Maximum number of recovery artifact backups kept for crash recovery.</summary>
    public const int MaximumRecoveryArtifacts = 1;
}

/// <summary>
/// Diagnostic codes indicating the outcome of a save file load or validation operation.
/// </summary>
public enum SaveDiagnosticCode : byte
{
    /// <summary>Save was loaded or validated successfully.</summary>
    None,

    /// <summary>Save file contents are malformed or corrupted (invalid JSON, truncated records, etc.).</summary>
    Corrupt,

    /// <summary>Save file exceeds the maximum allowed size.</summary>
    Oversized,

    /// <summary>Save file uses an unsupported version number.</summary>
    Unsupported,

    /// <summary>Save file was truncated or incomplete.</summary>
    Truncated,

    /// <summary>Save file checksum does not match the expected value (data corruption detected).</summary>
    ChecksumMismatch,

    /// <summary>Save references content (skills, spells, etc.) that is not available in the current game version.</summary>
    MissingContent,

    /// <summary>Save references content whose definitions have changed incompatibly.</summary>
    IncompatibleContent,

    /// <summary>Save contains game state that violates rules or constraints (negative resources, impossible values).</summary>
    InvalidState,

    /// <summary>An I/O error occurred while reading or writing the save file.</summary>
    IoFailure,

    /// <summary>No migration path exists to upgrade this old save format to the current version.</summary>
    MigrationUnavailable,

    /// <summary>Save appears to be from a different game version or variant than what migration expected.</summary>
    MigrationSourceMismatch,

    /// <summary>The migration process itself failed (corruption during upgrade).</summary>
    MigrationFailed,
}

/// <summary>
/// Utility class for working with <see cref="SaveDiagnosticCode"/> values.
/// </summary>
public static class SaveDiagnosticCodes
{
    /// <summary>Localization key for corrupt save file.</summary>
    public const string Corrupt = "save.corrupt";

    /// <summary>Localization key for save file too large.</summary>
    public const string Oversized = "save.oversized";

    /// <summary>Localization key for unsupported save version.</summary>
    public const string Unsupported = "save.unsupported";

    /// <summary>Localization key for truncated/incomplete save file.</summary>
    public const string Truncated = "save.truncated";

    /// <summary>Localization key for checksum mismatch.</summary>
    public const string ChecksumMismatch = "save.checksum-mismatch";

    /// <summary>Localization key for missing content.</summary>
    public const string MissingContent = "save.content-missing";

    /// <summary>Localization key for incompatible content.</summary>
    public const string IncompatibleContent = "save.content-incompatible";

    /// <summary>Localization key for invalid game state.</summary>
    public const string InvalidState = "save.state-invalid";

    /// <summary>Localization key for I/O failure.</summary>
    public const string IoFailure = "save.io-failure";

    /// <summary>Localization key for migration unavailable.</summary>
    public const string MigrationUnavailable = "save.migration-unavailable";

    /// <summary>Localization key for migration source mismatch.</summary>
    public const string MigrationSourceMismatch = "save.migration-source-mismatch";

    /// <summary>Localization key for migration failure.</summary>
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
    CharacterId ProtagonistId,
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
