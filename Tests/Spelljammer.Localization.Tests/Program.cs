using System.Text;
using Spelljammer.Localization;
using Spelljammer.Tools.Localization;

return await LocalizationTests.RunAsync();

internal static class LocalizationTests
{
    public static async Task<int> RunAsync()
    {
        StableIdentityRejectsInvalidNamesAndCollisions();
        CompilerIsStrictAndDeterministic();
        ArtifactRejectsCorruption();
        RuntimeUsesExplicitFallbackAndAtomicPublication();
        RuntimeFormatsTypedMessagesAndBoundsWork();
        PinnedLocaleProfilesFormatWithoutHostCulture();
        CompilerReportsTranslationSchemaMismatch();
        PseudoLocalesAndCompletenessAreDeterministic();
        await RuntimeRejectsNonOwnerThreadAsync();
        Console.WriteLine("Localization Phase 1 and 2 tests passed.");
        return 0;
    }

    private static void StableIdentityRejectsInvalidNamesAndCollisions()
    {
        LocalizationKey first = LocalizationKey.Create("ui.campaign.end-turn");
        LocalizationKey second = LocalizationKey.Create("ui.campaign.end-turn");
        Equal(first, second, "Equal canonical keys must have equal stable identities.");
        False(LocalizationKey.TryCreate("UI.Not-Canonical", out _, out _), "Non-canonical key was accepted.");
        False(LocaleId.TryCreate("en-us", out _, out _), "Non-canonical locale casing was accepted.");

        StableNameRegistry registry = new();
        True(registry.TryAdd(42, "ui.first.value", out _), "First registry insertion failed.");
        False(registry.TryAdd(42, "ui.second.value", out string collision), "Deliberate collision was accepted.");
        True(collision.Contains("collision", StringComparison.OrdinalIgnoreCase), "Collision diagnostic is not actionable.");
    }

    private static void CompilerIsStrictAndDeterministic()
    {
        byte[] source = Source("en-US", [],
            "\"ui.test.alpha\":{\"description\":\"Alpha\",\"message\":\"Alpha\"}," +
            "\"ui.test.beta\":{\"message\":\"Beta\"}");
        Success(SourceCatalogCompiler.Compile(source, PseudoLocaleKind.None, out CatalogCompilationResult? first, out string error), error);
        Success(SourceCatalogCompiler.Compile(source, PseudoLocaleKind.None, out CatalogCompilationResult? second, out error), error);
        SequenceEqual(first!.Artifact, second!.Artifact, "Equivalent source did not produce identical artifacts.");

        byte[] duplicate = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"schemaVersion\":1,\"locale\":\"en-US\",\"namespace\":\"ui\",\"fallbacks\":[],\"textDirection\":\"ltr\",\"messages\":{}}");
        Equal(LocalizationStatus.InvalidArgument,
            SourceCatalogCompiler.Compile(duplicate, PseudoLocaleKind.None, out _, out error),
            "Duplicate JSON member was accepted.");
        True(error.Contains("Duplicate", StringComparison.Ordinal), "Duplicate-member error lacks context.");

        byte[] invalidFormat = Source("en-US", [],
            "\"ui.test.bad\":{\"arguments\":{\"name\":\"text\"},\"message\":\"Hello {missing}\"}");
        Equal(LocalizationStatus.InvalidArgument,
            SourceCatalogCompiler.Compile(invalidFormat, PseudoLocaleKind.None, out _, out _),
            "Unknown format argument was accepted.");

        byte[] incompletePlural = Source("en-US", [],
            "\"ui.test.bad-plural\":{\"arguments\":{\"count\":\"integer\"},\"message\":\"{count, plural, other {# units}}\"}");
        Equal(LocalizationStatus.InvalidArgument,
            SourceCatalogCompiler.Compile(incompletePlural, PseudoLocaleKind.None, out _, out error),
            "A plural without the locale-required 'one' category was accepted.");
        True(error.Contains("one", StringComparison.Ordinal), "Missing plural-category error lacks context.");
    }

    private static void ArtifactRejectsCorruption()
    {
        LocalizationCatalogDefinition definition = new(
            "en-US",
            "ui",
            TextDirection.LeftToRight,
            [],
            [new LocalizationCatalogEntryDefinition("ui.test.value", "Value")]);
        Success(LocalizationArtifact.Encode(definition, out byte[] artifact, out string error), error);
        artifact[^1] ^= 0x7f;
        Equal(LocalizationStatus.DataCorrupt,
            LocalizationArtifact.Decode(artifact, out _, out error),
            "Checksum corruption was accepted.");
        True(error.Contains("checksum", StringComparison.OrdinalIgnoreCase), "Corruption error did not identify checksum failure.");
    }

    private static void RuntimeUsesExplicitFallbackAndAtomicPublication()
    {
        LocalizationCatalog english = CompileCatalog(Source("en-US", [],
            "\"ui.test.source-only\":{\"message\":\"Source\"}," +
            "\"ui.test.shared\":{\"message\":\"English\"}"));
        LocalizationCatalog french = CompileCatalog(Source("fr-FR", ["en-US"],
            "\"ui.test.shared\":{\"message\":\"Français\"}"));

        LocalizationService service = new();
        Success(service.Initialize(new LocalizationConfig("en-US", RequiredNamespaces: ["ui"])), "Initialize failed.");
        Success(service.StageLocale(LocaleId.Create("fr-FR"), [english, french], out LocaleGeneration? generation), "Stage failed.");
        Success(service.PublishLocale(generation!), "Publish failed.");

        Success(service.GetStatic(LocalizationKey.Create("ui.test.shared"), out LocalizedMessage? selected), "Selected lookup failed.");
        Equal("Français", selected!.Text, "Selected locale did not override fallback.");
        Equal("fr-FR", selected.ResolvedLocale.Tag, "Resolved-locale metadata is wrong.");

        Success(service.GetStatic(LocalizationKey.Create("ui.test.source-only"), out LocalizedMessage? fallback), "Fallback lookup failed.");
        Equal("Source", fallback!.Text, "Source fallback text is wrong.");
        Equal("en-US", fallback.ResolvedLocale.Tag, "Fallback metadata is wrong.");

        ulong publishedGeneration = service.GetLocaleSnapshot().Generation;
        Equal(LocalizationStatus.ItemNotFound,
            service.StageLocale(LocaleId.Create("de-DE"), [english, french], out _),
            "Missing locale unexpectedly staged.");
        Equal(publishedGeneration, service.GetLocaleSnapshot().Generation, "Failed stage changed the published generation.");

        Equal(LocalizationStatus.ItemNotFound,
            service.GetStatic(LocalizationKey.Create("ui.test.missing"), out LocalizedMessage? missing),
            "Development marker lookup status is wrong.");
        Equal("[missing:ui.test.missing]", missing!.Text, "Missing-key marker is wrong.");
        True(service.GetDiagnostics().Records.Any(record => record.FallbackDepth > 0), "Fallback was not recorded in diagnostics.");
        Success(service.Shutdown(), "Shutdown failed.");
    }

    private static void RuntimeFormatsTypedMessagesAndBoundsWork()
    {
        byte[] source = Source("en-US", [],
            "\"ui.test.summary\":{" +
            "\"arguments\":{" +
            "\"count\":\"integer\"," +
            "\"ratio\":{\"type\":\"percent\",\"scale\":4}," +
            "\"role\":{\"type\":\"select\",\"values\":[\"attacker\",\"defender\"]}}," +
            "\"message\":\"{count, plural, =0 {No units} one {# unit} other {# units}} — {ratio, percent} — {role, select, attacker {Attacker} defender {Defender} other {Unknown}}\"}," +
            "\"ui.test.ordinal\":{\"arguments\":{\"rank\":\"integer\"},\"message\":\"{rank, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}\"}," +
            "\"ui.test.nested\":{\"arguments\":{\"detail\":\"localizable\"},\"message\":\"Status: {detail}\"}," +
            "\"ui.test.ready\":{\"message\":\"Ready\"}");
        LocalizationCatalog catalog = CompileCatalog(source);
        LocalizationService service = new();
        Success(service.Initialize(new LocalizationConfig("en-US", MaximumFormatsPerFrame: 3)), "Initialize failed.");
        Success(service.StageLocale(LocaleId.Create("en-US"), [catalog], out LocaleGeneration? generation), "Stage failed.");
        Success(service.PublishLocale(generation!), "Publish failed.");

        Success(service.Format(LocalizationKey.Create("ui.test.summary"),
        [
            LocalizationArgument.Select("role", "defender"),
            LocalizationArgument.Integer("count", 2),
            LocalizationArgument.Percent("ratio", 1250, 4)
        ], out LocalizedMessage? summary), "Typed formatting failed.");
        Equal("2 units — 12.50% — Defender", summary!.Text, "Typed formatting output is wrong.");
        Equal("other", service.GetDiagnostics().Records.Last().PluralCategory!, "Plural diagnostic is wrong.");
        Equal("CLDR-48.2.0", summary.LanguageProfile.LocaleDataVersion, "Language profile data version is wrong.");
        Equal("bb8b91669fa8c4c07974c07bff22bccd54080ededdfb9906e377340a9c801c1a",
            summary.LanguageProfile.LocaleDataHash, "Language profile data hash is wrong.");

        Success(service.Format(LocalizationKey.Create("ui.test.summary"),
        [
            LocalizationArgument.Integer("count", 0),
            LocalizationArgument.Percent("ratio", 0, 4),
            LocalizationArgument.Select("role", "attacker")
        ], out LocalizedMessage? exact), "Exact plural formatting failed.");
        Equal("No units — 0.00% — Attacker", exact!.Text, "Exact plural branch did not take priority.");

        Success(service.Format(LocalizationKey.Create("ui.test.ordinal"),
            [LocalizationArgument.Integer("rank", 23)], out LocalizedMessage? ordinal), "Ordinal formatting failed.");
        Equal("23rd", ordinal!.Text, "Ordinal formatting output is wrong.");
        Equal(LocalizationStatus.OutOfResource,
            service.Format(LocalizationKey.Create("ui.test.ordinal"),
                [LocalizationArgument.Integer("rank", 1)], out _),
            "Per-frame format budget was not enforced.");

        Success(service.BeginFormattingFrame(), "Format budget reset failed.");
        Success(service.Format(LocalizationKey.Create("ui.test.nested"),
            [LocalizationArgument.Localizable("detail", LocalizationKey.Create("ui.test.ready"))],
            out LocalizedMessage? nested), "Nested localizable formatting failed.");
        Equal("Status: Ready", nested!.Text, "Nested localizable output is wrong.");

        Equal(LocalizationStatus.InvalidArgument,
            service.Format(LocalizationKey.Create("ui.test.summary"),
                [LocalizationArgument.Integer("count", 1)], out LocalizedMessage? invalid),
            "Argument mismatch was accepted.");
        Equal("[format:ui.test.summary]", invalid!.Text, "Development format marker is wrong.");
        Equal(LocalizationStatus.InvalidArgument,
            service.GetStatic(LocalizationKey.Create("ui.test.summary"), out _),
            "Dynamic message was returned through GetStatic.");
        Success(service.Shutdown(), "Shutdown failed.");
    }

    private static void CompilerReportsTranslationSchemaMismatch()
    {
        byte[] source = Source("en-US", [],
            "\"ui.test.count\":{\"arguments\":{\"count\":\"integer\"},\"message\":\"{count, number}\"}");
        byte[] translation = Source("fr-FR", ["en-US"],
            "\"ui.test.count\":{\"arguments\":{\"count\":\"unsigned\"},\"message\":\"{count, number}\"}");
        Success(SourceCatalogCompiler.CreateCompletenessReport(source, translation,
            out CatalogCompletenessReport? report, out string error), error);
        False(report!.IsComplete, "Schema-mismatched translation was reported complete.");
        Equal(1, report.SchemaErrors.Count, "Schema mismatch report count is wrong.");
    }

    private static void PinnedLocaleProfilesFormatWithoutHostCulture()
    {
        Equal("12 345,67", FormatOne("fr-FR",
            "{value, number}",
            "{\"value\":{\"type\":\"fixed\",\"scale\":2}}",
            LocalizationArgument.Fixed("value", 1_234_567, 2)),
            "French fixed-decimal formatting is wrong.");
        Equal("22 бойца", FormatOne("ru-RU",
            "{count, plural, one {# боец} few {# бойца} many {# бойцов} other {# бойца}}",
            "{\"count\":\"integer\"}",
            LocalizationArgument.Integer("count", 22)),
            "Russian cardinal formatting is wrong.");
        Equal("٣ وحدات", FormatOne("ar",
            "{count, plural, zero {لا وحدات} one {وحدة} two {وحدتان} few {# وحدات} many {# وحدة} other {# وحدة}}",
            "{\"count\":\"integer\"}",
            LocalizationArgument.Integer("count", 3),
            TextDirection.RightToLeft),
            "Arabic digits or cardinal formatting is wrong.");
        Equal("12,345.67", FormatOne("zh-Hant-TW",
            "{value, number}",
            "{\"value\":{\"type\":\"fixed\",\"scale\":2}}",
            LocalizationArgument.Fixed("value", 1_234_567, 2)),
            "Chinese fixed-decimal formatting is wrong.");
    }

    private static string FormatOne(
        string locale,
        string message,
        string argumentSchema,
        LocalizationArgument argument,
        TextDirection direction = TextDirection.LeftToRight)
    {
        string directionName = direction == TextDirection.LeftToRight ? "ltr" : "rtl";
        byte[] source = Encoding.UTF8.GetBytes(
            $"{{\"schemaVersion\":1,\"locale\":\"{locale}\",\"namespace\":\"ui\",\"fallbacks\":[],\"textDirection\":\"{directionName}\",\"messages\":{{\"ui.test.value\":{{\"arguments\":{argumentSchema},\"message\":\"{message}\"}}}}}}");
        LocalizationCatalog catalog = CompileCatalog(source);
        LocalizationService service = new();
        Success(service.Initialize(new LocalizationConfig(locale)), "Locale service initialization failed.");
        Success(service.StageLocale(LocaleId.Create(locale), [catalog], out LocaleGeneration? generation), "Locale stage failed.");
        Success(service.PublishLocale(generation!), "Locale publication failed.");
        Success(service.Format(LocalizationKey.Create("ui.test.value"), [argument], out LocalizedMessage? formatted),
            "Locale formatting failed.");
        Success(service.Shutdown(), "Locale service shutdown failed.");
        return formatted!.Text;
    }

    private static void PseudoLocalesAndCompletenessAreDeterministic()
    {
        byte[] source = Source("en-US", [],
            "\"ui.test.alpha\":{\"message\":\"Alpha\"}," +
            "\"ui.test.beta\":{\"message\":\"Beta\"}");
        Success(SourceCatalogCompiler.Compile(source, PseudoLocaleKind.AccentedExpanded, out CatalogCompilationResult? pseudo, out string error), error);
        Success(LocalizationArtifact.Decode(pseudo!.Artifact, out LocalizationCatalog? catalog, out error), error);
        Equal("qps-ploc", catalog!.Locale.Tag, "Pseudo locale tag is wrong.");
        Equal("en-US", catalog.Fallbacks[0].Tag, "Pseudo locale does not explicitly fall back to source.");
        True(catalog.Entries.All(entry => entry.StaticMessage!.StartsWith('⟦')), "Pseudo transformation was not applied.");

        Success(SourceCatalogCompiler.Compile(source, PseudoLocaleKind.KeyEcho, out CatalogCompilationResult? keyEcho, out error), error);
        Success(LocalizationArtifact.Decode(keyEcho!.Artifact, out LocalizationCatalog? keyEchoCatalog, out error), error);
        Equal("qps-keyecho", keyEchoCatalog!.Locale.Tag, "Key-echo locale tag is wrong.");
        True(keyEchoCatalog.Entries.All(entry => entry.StaticMessage == $"⟦{entry.Key.Name}⟧"), "Key-echo output is wrong.");

        byte[] translation = Source("fr-FR", ["en-US"],
            "\"ui.test.alpha\":{\"message\":\"Alpha FR\"}," +
            "\"ui.test.obsolete\":{\"message\":\"Obsolete\"}");
        Success(SourceCatalogCompiler.CreateCompletenessReport(source, translation, out CatalogCompletenessReport? report, out error), error);
        SequenceEqual(["ui.test.beta"], report!.MissingKeys, "Missing-key report is wrong.");
        SequenceEqual(["ui.test.obsolete"], report.ObsoleteKeys, "Obsolete-key report is wrong.");
    }

    private static async Task RuntimeRejectsNonOwnerThreadAsync()
    {
        LocalizationService service = new();
        Success(service.Initialize(new LocalizationConfig("en-US")), "Initialize failed.");
        LocalizationStatus status = await Task.Run(() =>
            service.StageLocale(LocaleId.Create("en-US"), [], out _));
        Equal(LocalizationStatus.WrongThread, status, "Non-owner thread accessed service lifecycle.");
        Success(service.Shutdown(), "Shutdown failed.");
    }

    private static LocalizationCatalog CompileCatalog(byte[] source)
    {
        Success(SourceCatalogCompiler.Compile(source, PseudoLocaleKind.None, out CatalogCompilationResult? result, out string error), error);
        Success(LocalizationArtifact.Decode(result!.Artifact, out LocalizationCatalog? catalog, out error), error);
        return catalog!;
    }

    private static byte[] Source(string locale, IReadOnlyList<string> fallbacks, string messages)
    {
        string fallbackJson = string.Join(',', fallbacks.Select(value => $"\"{value}\""));
        return Encoding.UTF8.GetBytes(
            $"{{\"schemaVersion\":1,\"locale\":\"{locale}\",\"namespace\":\"ui\",\"fallbacks\":[{fallbackJson}],\"textDirection\":\"ltr\",\"messages\":{{{messages}}}}}");
    }

    private static void Success(LocalizationStatus status, string message) =>
        Equal(LocalizationStatus.Success, status, message);

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', received '{actual}'.");
        }
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(message);
        }
    }
}
