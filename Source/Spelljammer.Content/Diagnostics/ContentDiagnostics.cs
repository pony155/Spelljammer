using System.Collections.Immutable;
using System.Text;

namespace Spelljammer.Content.Diagnostics;

public static class ContentDiagnosticCodes
{
    public const string ManifestMissing = "CONTENT_MANIFEST_MISSING";
    public const string ManifestMultiple = "CONTENT_MANIFEST_MULTIPLE";
    public const string InvalidUtf8 = "CONTENT_INVALID_UTF8";
    public const string JsonInvalid = "CONTENT_JSON_INVALID";
    public const string JsonDuplicateProperty = "CONTENT_JSON_DUPLICATE_PROPERTY";
    public const string SchemaUnsupported = "CONTENT_SCHEMA_UNSUPPORTED";
    public const string RequiredPropertyMissing = "CONTENT_REQUIRED_PROPERTY_MISSING";
    public const string UnknownProperty = "CONTENT_UNKNOWN_PROPERTY";
    public const string IdInvalid = "CONTENT_ID_INVALID";
    public const string PathInvalid = "CONTENT_PATH_INVALID";
    public const string PackIdDuplicate = "CONTENT_PACK_ID_DUPLICATE";
    public const string VersionInvalid = "CONTENT_VERSION_INVALID";
    public const string GameVersionIncompatible = "CONTENT_GAME_VERSION_INCOMPATIBLE";
    public const string DependencyMissing = "CONTENT_DEPENDENCY_MISSING";
    public const string DependencyVersionMismatch = "CONTENT_DEPENDENCY_VERSION_MISMATCH";
    public const string DependencyCycle = "CONTENT_DEPENDENCY_CYCLE";
    public const string NamespaceViolation = "CONTENT_NAMESPACE_VIOLATION";
    public const string DefinitionIdDuplicate = "CONTENT_DEFINITION_ID_DUPLICATE";
    public const string ReferenceUnknown = "CONTENT_REFERENCE_UNKNOWN";
    public const string KindMismatch = "CONTENT_KIND_MISMATCH";
    public const string ValueOutOfRange = "CONTENT_VALUE_OUT_OF_RANGE";
    public const string CollectionDuplicate = "CONTENT_COLLECTION_DUPLICATE";
    public const string SemanticInvalid = "CONTENT_SEMANTIC_INVALID";
    public const string LimitExceeded = "CONTENT_LIMIT_EXCEEDED";
    public const string LocalizationKeyMissing = "CONTENT_LOCALIZATION_KEY_MISSING";
}

public enum ContentDiagnosticSeverity : byte
{
    Error
}

public enum ContentDiagnosticArgumentKind : byte
{
    SafeId,
    Integer,
    Version,
    LimitName,
    RelativePath,
    AuthoredToken
}

public readonly record struct ContentDiagnosticArgument(ContentDiagnosticArgumentKind Kind, string Value)
{
    public static ContentDiagnosticArgument SafeId(string value) => new(ContentDiagnosticArgumentKind.SafeId, value);
    public static ContentDiagnosticArgument Integer(long value) => new(ContentDiagnosticArgumentKind.Integer, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static ContentDiagnosticArgument Version(string value) => new(ContentDiagnosticArgumentKind.Version, value);
    public static ContentDiagnosticArgument Limit(string value) => new(ContentDiagnosticArgumentKind.LimitName, value);
    public static ContentDiagnosticArgument Path(string value) => new(ContentDiagnosticArgumentKind.RelativePath, value);
    public static ContentDiagnosticArgument Token(string value) => new(ContentDiagnosticArgumentKind.AuthoredToken, value);
}

public sealed record ContentDiagnostic(
    string Code,
    ContentDiagnosticSeverity Severity,
    string? PackId,
    string? RelativePath,
    string? DefinitionId,
    string? PropertyPath,
    ImmutableArray<ContentDiagnosticArgument> Arguments);

public enum ContentIoFailureKind : byte
{
    NotFound,
    AccessDenied,
    ChangedDuringRead,
    ReadFailed
}

public sealed record ContentIoFailure(ContentIoFailureKind Kind, string? RelativePath);

internal sealed class DiagnosticSink
{
    private readonly ContentLimits limits;
    private readonly List<ContentDiagnostic> diagnostics = [];

    public DiagnosticSink(ContentLimits limits) => this.limits = limits;

    public bool HasErrors => diagnostics.Count != 0;

    public ImmutableArray<ContentDiagnostic> ToImmutable() => [.. diagnostics];

    public void Add(
        string code,
        string? packId = null,
        string? relativePath = null,
        string? definitionId = null,
        string? propertyPath = null,
        params ContentDiagnosticArgument[] arguments)
    {
        if (diagnostics.Count >= limits.RetainedDiagnostics)
        {
            return;
        }

        ImmutableArray<ContentDiagnosticArgument> safeArguments =
            [.. arguments.Select(SanitizeArgument)];
        diagnostics.Add(new ContentDiagnostic(
            code,
            ContentDiagnosticSeverity.Error,
            SanitizeToken(packId, limits.StableIdBytes),
            SanitizePath(relativePath),
            SanitizeToken(definitionId, limits.StableIdBytes),
            SanitizePath(propertyPath),
            safeArguments));
    }

    public void Limit(string limitName, string? packId = null, string? relativePath = null) =>
        Add(ContentDiagnosticCodes.LimitExceeded, packId, relativePath, arguments: ContentDiagnosticArgument.Limit(limitName));

    private ContentDiagnosticArgument SanitizeArgument(ContentDiagnosticArgument argument) =>
        argument with { Value = SanitizeToken(argument.Value, limits.DiagnosticArgumentBytes) ?? string.Empty };

    private static string? SanitizePath(string? value) =>
        SanitizeToken(value?.Replace('\\', '/'), 512);

    private static string? SanitizeToken(string? value, int maximumBytes)
    {
        if (value is null)
        {
            return null;
        }

        StringBuilder result = new();
        int bytes = 0;
        foreach (Rune rune in value.EnumerateRunes())
        {
            int runeBytes = rune.Utf8SequenceLength;
            if (bytes + runeBytes > maximumBytes)
            {
                break;
            }

            result.Append(rune);
            bytes += runeBytes;
        }

        return result.ToString();
    }
}
