namespace Spelljammer.Content;

public sealed record ContentLimits
{
    public static ContentLimits Version1 { get; } = new();

    public int EnabledPacks { get; init; } = 64;
    public int PackEdges { get; init; } = 256;
    public int DependencyDepth { get; init; } = 32;
    public int DefinitionRootsPerPack { get; init; } = 8;
    public int LocalizationRootsPerPack { get; init; } = 8;
    public int DefinitionFilesPerPack { get; init; } = 4_096;
    public int DefinitionFilesPerSet { get; init; } = 32_768;
    public int ManifestBytes { get; init; } = 65_536;
    public int DefinitionFileBytes { get; init; } = 1_048_576;
    public long DefinitionBytesPerPack { get; init; } = 268_435_456;
    public long DefinitionBytesPerSet { get; init; } = 1_073_741_824;
    public int JsonNestingDepth { get; init; } = 32;
    public int JsonTokensPerFile { get; init; } = 131_072;
    public int PropertiesPerObject { get; init; } = 256;
    public int EntriesPerArray { get; init; } = 4_096;
    public int StableIdBytes { get; init; } = 127;
    public int LocalizationKeyBytes { get; init; } = 127;
    public int GenericSourceStringBytes { get; init; } = 4_096;
    public int DefinitionsPerKind { get; init; } = 16_384;
    public int DefinitionsPerSet { get; init; } = 65_535;
    public int TagsPerDefinition { get; init; } = 64;
    public int ReferencesPerDefinition { get; init; } = 256;
    public int ReferencesPerSet { get; init; } = 1_048_576;
    public int GraphNodes { get; init; } = 65_535;
    public int GraphEdges { get; init; } = 262_144;
    public int ValidationTraversalDepth { get; init; } = 64;
    public int RetainedDiagnostics { get; init; } = 1_000;
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
