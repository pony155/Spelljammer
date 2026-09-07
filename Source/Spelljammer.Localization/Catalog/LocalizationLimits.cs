namespace Spelljammer.Localization;

public static class LocalizationLimits
{
    public const int MaximumArtifactBytes = 32 * 1024 * 1024;
    public const int MaximumArgumentsPerMessage = 32;
    public const int MaximumBranchesPerSelection = 64;
    public const int MaximumCanonicalKeyBytes = 127;
    public const int MaximumCatalogsPerLocale = 64;
    public const int MaximumCatalogsPerGeneration = MaximumCatalogsPerLocale * (MaximumFallbackDepth + 1);
    public const int MaximumDiagnostics = 1024;
    public const int MaximumFallbackDepth = 8;
    public const int MaximumFormattedMessageBytes = 256 * 1024;
    public const int MaximumFormatsPerFrame = 4096;
    public const int MaximumKeysPerLocale = 100_000;
    public const int MaximumLocaleTagBytes = 63;
    public const int MaximumMessageBytes = 64 * 1024;
    public const int MaximumMessageNesting = 8;
    public const int MaximumNamespaceBytes = 63;
    public const int MaximumStaticCacheBytes = 16 * 1024 * 1024;
}
