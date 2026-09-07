using System.Globalization;
using System.IO;
using System.Reflection;
using Spelljammer.Localization;
using Spelljammer.Settings;

namespace Spelljammer.Presentation;

internal sealed class GameText
{
    private static readonly string[] ResourceNames =
    [
        "Spelljammer.Localization.en-US.menu.sfloc",
        "Spelljammer.Localization.en-US.settings.sfloc",
        "Spelljammer.Localization.en-US.creation.sfloc",
        "Spelljammer.Localization.fr-FR.menu.sfloc",
        "Spelljammer.Localization.fr-FR.settings.sfloc",
        "Spelljammer.Localization.fr-FR.creation.sfloc",
        "Spelljammer.Localization.zh-Hant-TW.menu.sfloc",
        "Spelljammer.Localization.zh-Hant-TW.settings.sfloc",
        "Spelljammer.Localization.zh-Hant-TW.creation.sfloc",
    ];

    private readonly LocalizationService localization;
    private readonly IReadOnlyCollection<LocalizationCatalog> catalogs;
    private CultureInfo culture = CultureInfo.InvariantCulture;

    private GameText(LocalizationService localization, IReadOnlyCollection<LocalizationCatalog> catalogs)
    {
        this.localization = localization;
        this.catalogs = catalogs;
    }

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
            RequiredNamespaces: ["menu", "settings", "creation"])),
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
