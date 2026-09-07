using System.Text.Json;
using Spelljammer.Localization;
using Spelljammer.Tools.Localization;

try
{
    return await RunAsync(args);
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine($"I/O error: {exception.Message}");
    return 74;
}

static async Task<int> RunAsync(string[] arguments)
{
    if (arguments.Length == 3 && arguments[0] == "compile")
    {
        return await CompileAsync(arguments[1], arguments[2], PseudoLocaleKind.None);
    }

    if (arguments.Length == 4 && arguments[0] == "pseudo" &&
        Enum.TryParse(arguments[1], ignoreCase: true, out PseudoLocaleKind pseudo) && pseudo != PseudoLocaleKind.None)
    {
        return await CompileAsync(arguments[2], arguments[3], pseudo);
    }

    if (arguments.Length == 3 && arguments[0] == "report")
    {
        byte[] source = await File.ReadAllBytesAsync(arguments[1]);
        byte[] translation = await File.ReadAllBytesAsync(arguments[2]);
        LocalizationStatus status = SourceCatalogCompiler.CreateCompletenessReport(
            source,
            translation,
            out CatalogCompletenessReport? report,
            out string error);
        if (status != LocalizationStatus.Success || report is null)
        {
            return Fail(status, error);
        }

        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report.IsComplete ? 0 : 2;
    }

    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  SpriteForge.Localization.Compiler compile <input.sfloc.json> <output.sfloc>");
    Console.Error.WriteLine("  SpriteForge.Localization.Compiler pseudo <AccentedExpanded|KeyEcho> <input.sfloc.json> <output.sfloc>");
    Console.Error.WriteLine("  SpriteForge.Localization.Compiler report <source.sfloc.json> <translation.sfloc.json>");
    return 64;
}

static async Task<int> CompileAsync(string inputPath, string outputPath, PseudoLocaleKind pseudo)
{
    byte[] source = await File.ReadAllBytesAsync(inputPath);
    LocalizationStatus status = SourceCatalogCompiler.Compile(source, pseudo, out CatalogCompilationResult? result, out string error);
    if (status != LocalizationStatus.Success || result is null)
    {
        return Fail(status, error);
    }

    string? outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
    if (!string.IsNullOrEmpty(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    await File.WriteAllBytesAsync(outputPath, result.Artifact);
    Console.WriteLine($"{result.Locale}/{result.Namespace}: {result.MessageCount} messages, SHA-256 {result.Fingerprint}");
    return 0;
}

static int Fail(LocalizationStatus status, string error)
{
    Console.Error.WriteLine($"{status}: {error}");
    return 1;
}
