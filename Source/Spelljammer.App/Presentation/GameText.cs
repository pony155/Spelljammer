using System.Globalization;
using System.IO;
using System.Reflection;
using Spelljammer.Localization;
using Spelljammer.Settings;

namespace Spelljammer.Presentation;

/// <summary>
/// Manages game text and localization strings for the UI.
/// </summary>
/// <remarks>
/// This class loads embedded localization artifacts for all supported languages
/// (English, French, Traditional Chinese) organized by UI sections and calendar presentation.
/// The localization service handles string lookup, fallbacks, and formatting.
/// </remarks>
internal sealed class GameText
{
    /// <summary>
    /// Resource names of embedded localization artifacts for each language and section.
    /// </summary>
    private static readonly string[] ResourceNames =
    [
        "Spelljammer.Localization.en-US.menu.sfloc",
        "Spelljammer.Localization.en-US.settings.sfloc",
        "Spelljammer.Localization.en-US.creation.sfloc",
        "Spelljammer.Localization.en-US.calendar.sfloc",
        "Spelljammer.Localization.fr-FR.menu.sfloc",
        "Spelljammer.Localization.fr-FR.settings.sfloc",
        "Spelljammer.Localization.fr-FR.creation.sfloc",
        "Spelljammer.Localization.fr-FR.calendar.sfloc",
        "Spelljammer.Localization.zh-Hant-TW.menu.sfloc",
        "Spelljammer.Localization.zh-Hant-TW.settings.sfloc",
        "Spelljammer.Localization.zh-Hant-TW.creation.sfloc",
        "Spelljammer.Localization.zh-Hant-TW.calendar.sfloc",
    ];

    private readonly LocalizationService localization;
    private readonly IReadOnlyCollection<LocalizationCatalog> catalogs;
    private CultureInfo culture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a GameText instance with localization service and catalogs.
    /// </summary>
    /// <remarks>
    /// This constructor is private; use the Load factory method to create instances.
    /// </remarks>
    private GameText(LocalizationService localization, IReadOnlyCollection<LocalizationCatalog> catalogs)
    {
        this.localization = localization;
        this.catalogs = catalogs;
    }

    /// <summary>
    /// Loads and initializes game text strings for a specific language.
    /// </summary>
    /// <remarks>
    /// Loads embedded localization artifacts from assembly resources, validates their size,
    /// and initializes the localization service with all required UI namespaces.
    /// </remarks>
    /// <param name="language">The language code to load (e.g., "en-US", "fr-FR", "zh-Hant-TW").</param>
    /// <returns>A new GameText instance with all strings loaded for the specified language.</returns>
    /// <exception cref="InvalidOperationException">Thrown if localization artifacts are missing or exceed size limits.</exception>
    internal static GameText Load(string language)
    {
        Assembly assembly = typeof(GameText).Assembly;
        List<LocalizationCatalog> catalogs = new(ResourceNames.Length);
        foreach (string resourceName in ResourceNames)
        {
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded localization artifact '{resourceName}' is unavailable.");
            if (stream.Length > LocalizationLimits.MaximumArtifactBytes)
            {
                throw new InvalidOperationException(
                    $"Embedded localization artifact '{resourceName}' exceeds its supported bound.");
            }

            byte[] artifact = new byte[stream.Length];
            stream.ReadExactly(artifact);
            Require(LocalizationArtifact.Decode(
                artifact,
                out LocalizationCatalog? catalog,
                out string error), error);
            catalogs.Add(catalog!);
        }

        LocalizationService service = new();
        Require(service.Initialize(new LocalizationConfig(
            "en-US",
            RequiredNamespaces: ["menu", "settings", "creation", "calendar"])),
            "Could not initialize the application localization service.");
        GameText result = new(service, catalogs);
        result.SetLanguage(language);
        return result;
    }

    internal void SetLanguage(string language)
    {
        LocaleId locale = LocaleId.Create(language);
        Require(localization.StageLocale(locale, catalogs, out LocaleGeneration? generation),
            $"Could not stage application locale '{language}'.");
        Require(localization.PublishLocale(generation!), $"Could not publish application locale '{language}'.");
        culture = CultureInfo.GetCultureInfo(language);
    }

    internal CultureInfo Culture => culture;

    internal void BeginFrame() => Require(
        localization.BeginFormattingFrame(),
        "Could not begin the application localization frame.");

    internal string Get(string name)
    {
        LocalizationStatus status = localization.GetStatic(LocalizationKey.Create(name), out LocalizedMessage? message);
        Require(status, $"Could not resolve localization key '{name}'.");
        return message!.Text;
    }

    internal string Format(string name, params LocalizationArgument[] arguments)
    {
        LocalizationStatus status = localization.Format(LocalizationKey.Create(name), arguments, out LocalizedMessage? message);
        Require(status, $"Could not format localization key '{name}'.");
        return message!.Text;
    }

    internal string Percent(int value) =>
        Format("settings.value.percent", LocalizationArgument.Integer("value", value));

    internal string Version(string value) =>
        Format("menu.version", LocalizationArgument.Text("version", value));

    internal string LanguageName(string language) => Get(language switch
    {
        "en-US" => "settings.value.language.en-us",
        "fr-FR" => "settings.value.language.fr-fr",
        "zh-Hant-TW" => "settings.value.language.zh-hant-tw",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    });

    internal string ResolutionName(GameResolutionChoice resolution) => resolution.IsDesktop
        ? Get("settings.value.resolution.desktop")
        : Format(
            "settings.value.resolution.pixels",
            LocalizationArgument.Integer("width", resolution.Width),
            LocalizationArgument.Integer("height", resolution.Height));

    internal string AccessibleOption(string setting, string value) => Format(
        "settings.accessibility.option",
        LocalizationArgument.Text("setting", setting),
        LocalizationArgument.Text("value", value));

    internal string Diagnostic(string name, string code) =>
        Format(name, LocalizationArgument.Text("code", code));

    private static void Require(LocalizationStatus status, string detail)
    {
        if (status != LocalizationStatus.Success)
        {
            throw new InvalidOperationException($"{detail} ({status}).");
        }
    }
}
