namespace Spelljammer.Content;

/// <summary>
/// Configurable limits and bounds for content compilation and validation.
/// </summary>
/// <remarks>
/// These limits protect against resource exhaustion, prevent pathological JSON parsing,
/// and enforce reasonable content pack structures. All limits have version 1 defaults
/// that represent production constraints.
/// </remarks>
public sealed record ContentLimits
{
    /// <summary>
    /// Gets the production version 1 limits (baseline constraints for the current game version).
    /// </summary>
    public static ContentLimits Version1 { get; } = new();

    /// <summary>Maximum number of content packs that can be enabled simultaneously.</summary>
    public int EnabledPacks { get; init; } = 64;

    /// <summary>Maximum number of dependency edges between content packs.</summary>
    public int PackEdges { get; init; } = 256;

    /// <summary>Maximum depth of pack dependency chains.</summary>
    public int DependencyDepth { get; init; } = 32;

    /// <summary>Maximum definition root directories per pack.</summary>
    public int DefinitionRootsPerPack { get; init; } = 8;

    /// <summary>Maximum localization root directories per pack.</summary>
    public int LocalizationRootsPerPack { get; init; } = 8;

    /// <summary>Maximum definition files per individual pack.</summary>
    public int DefinitionFilesPerPack { get; init; } = 4_096;

    /// <summary>Maximum definition files across entire content set.</summary>
    public int DefinitionFilesPerSet { get; init; } = 32_768;

    /// <summary>Maximum bytes for a single manifest file.</summary>
    public int ManifestBytes { get; init; } = 65_536;

    /// <summary>Maximum bytes for a single definition file (1 MB).</summary>
    public int DefinitionFileBytes { get; init; } = 1_048_576;

    /// <summary>Maximum total definition bytes per pack (256 MB).</summary>
    public long DefinitionBytesPerPack { get; init; } = 268_435_456;

    /// <summary>Maximum total definition bytes across entire set (1 GB).</summary>
    public long DefinitionBytesPerSet { get; init; } = 1_073_741_824;

    /// <summary>Maximum JSON nesting depth for definition files.</summary>
    public int JsonNestingDepth { get; init; } = 32;

    /// <summary>Maximum JSON tokens per definition file.</summary>
    public int JsonTokensPerFile { get; init; } = 131_072;

    /// <summary>Maximum properties per JSON object.</summary>
    public int PropertiesPerObject { get; init; } = 256;

    /// <summary>Maximum entries per JSON array.</summary>
    public int EntriesPerArray { get; init; } = 4_096;

    /// <summary>Maximum bytes for stable ID strings (content.id format).</summary>
    public int StableIdBytes { get; init; } = 127;

    /// <summary>Maximum bytes for localization key strings.</summary>
    public int LocalizationKeyBytes { get; init; } = 127;

    /// <summary>Maximum bytes for generic source strings in definitions.</summary>
    public int GenericSourceStringBytes { get; init; } = 4_096;

    /// <summary>Maximum definitions of a single kind (e.g., all spells).</summary>
    public int DefinitionsPerKind { get; init; } = 16_384;

    /// <summary>Maximum total definitions in entire content set (65,535).</summary>
    public int DefinitionsPerSet { get; init; } = 65_535;

    /// <summary>Maximum tags per definition.</summary>
    public int TagsPerDefinition { get; init; } = 64;

    /// <summary>Maximum references from one definition to others.</summary>
    public int ReferencesPerDefinition { get; init; } = 256;

    /// <summary>Maximum total references across entire content set.</summary>
    public int ReferencesPerSet { get; init; } = 1_048_576;

    /// <summary>Maximum nodes in the dependency graph.</summary>
    public int GraphNodes { get; init; } = 65_535;

    /// <summary>Maximum edges in the dependency graph.</summary>
    public int GraphEdges { get; init; } = 262_144;

    /// <summary>Maximum depth for validation graph traversal (cycle detection, reachability).</summary>
    public int ValidationTraversalDepth { get; init; } = 64;

    /// <summary>Maximum number of diagnostics to retain in memory.</summary>
    public int RetainedDiagnostics { get; init; } = 1_000;

    /// <summary>Maximum bytes for diagnostic argument values.</summary>
    public int DiagnosticArgumentBytes { get; init; } = 512;

    internal void Validate()
    {
        long[] values = [.. Values()];
        long[] version1Values = [.. Version1.Values()];
        for (int index = 0; index < values.Length; index++)
        {
            if (values[index] <= 0 || values[index] > version1Values[index])
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ContentLimits),
                    "Content limits must be positive and may only reduce the version 1 production bounds.");
            }
        }
    }

    private IEnumerable<long> Values()
    {
        yield return EnabledPacks;
        yield return PackEdges;
        yield return DependencyDepth;
        yield return DefinitionRootsPerPack;
        yield return LocalizationRootsPerPack;
        yield return DefinitionFilesPerPack;
        yield return DefinitionFilesPerSet;
        yield return ManifestBytes;
        yield return DefinitionFileBytes;
        yield return DefinitionBytesPerPack;
        yield return DefinitionBytesPerSet;
        yield return JsonNestingDepth;
        yield return JsonTokensPerFile;
        yield return PropertiesPerObject;
        yield return EntriesPerArray;
        yield return StableIdBytes;
        yield return LocalizationKeyBytes;
        yield return GenericSourceStringBytes;
        yield return DefinitionsPerKind;
        yield return DefinitionsPerSet;
        yield return TagsPerDefinition;
        yield return ReferencesPerDefinition;
        yield return ReferencesPerSet;
        yield return GraphNodes;
        yield return GraphEdges;
        yield return ValidationTraversalDepth;
        yield return RetainedDiagnostics;
        yield return DiagnosticArgumentBytes;
    }
}
