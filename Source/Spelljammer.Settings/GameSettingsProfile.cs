using System.Text.Json.Serialization;

namespace Spelljammer.Settings;

public sealed record GameSettingsProfile(
    int SchemaVersion,
    int MasterVolume,
    int MusicVolume,
    int EffectsVolume,
    bool Subtitles,
    bool ReducedMotion,
    bool ScreenShake,
    int UiScalePercent,
    string Language,
    string Resolution)
{
    public const int CurrentSchemaVersion = 2;
    public const int MinimumVolume = 0;
    public const int MaximumVolume = 100;
    public const int MinimumUiScalePercent = 75;
    public const int MaximumUiScalePercent = 150;
    public const int MaximumSerializedBytes = 64 * 1024;

    public static GameSettingsProfile Default { get; } = new(
        CurrentSchemaVersion,
        80,
        65,
        80,
        true,
        false,
        true,
        100,
        GameSettingsChoices.DefaultLanguage,
        GameSettingsChoices.DesktopResolution);

    [JsonIgnore]
    public bool IsValid =>
        SchemaVersion == CurrentSchemaVersion &&
        MasterVolume is >= MinimumVolume and <= MaximumVolume &&
        MusicVolume is >= MinimumVolume and <= MaximumVolume &&
        EffectsVolume is >= MinimumVolume and <= MaximumVolume &&
        UiScalePercent is >= MinimumUiScalePercent and <= MaximumUiScalePercent &&
        GameSettingsChoices.IsSupportedLanguage(Language) &&
        GameSettingsChoices.TryGetResolution(Resolution, out _);
}

public readonly record struct GameResolutionChoice(string Id, int Width, int Height)
{
    public bool IsDesktop => Width == 0 && Height == 0;
}

public static class GameSettingsChoices
{
    public const string DefaultLanguage = "en-US";
    public const string DesktopResolution = "desktop";

    public static IReadOnlyList<string> Languages { get; } = Array.AsReadOnly(
        [DefaultLanguage, "fr-FR", "zh-Hant-TW"]);

    public static IReadOnlyList<GameResolutionChoice> Resolutions { get; } = Array.AsReadOnly(
        new GameResolutionChoice[]
        {
            new(DesktopResolution, 0, 0),
            new("1280x720", 1280, 720),
            new("1600x900", 1600, 900),
            new("1920x1080", 1920, 1080),
            new("2560x1440", 2560, 1440),
        });

    public static bool IsSupportedLanguage(string? language) =>
        Languages.Contains(language, StringComparer.Ordinal);

    public static bool TryGetResolution(string? id, out GameResolutionChoice resolution)
    {
        foreach (GameResolutionChoice candidate in Resolutions)
        {
            if (string.Equals(candidate.Id, id, StringComparison.Ordinal))
            {
                resolution = candidate;
                return true;
            }
        }

        resolution = default;
        return false;
    }
}

public enum GameSettingsDiagnostic : byte
{
    None,
    Missing,
    Corrupt,
    Oversized,
    Unsupported,
    InvalidValue,
    IoFailure,
}

public static class GameSettingsDiagnostics
{
    public static string Stable(GameSettingsDiagnostic diagnostic) => diagnostic switch
    {
        GameSettingsDiagnostic.None => string.Empty,
        GameSettingsDiagnostic.Missing => "settings.missing",
        GameSettingsDiagnostic.Corrupt => "settings.corrupt",
        GameSettingsDiagnostic.Oversized => "settings.oversized",
        GameSettingsDiagnostic.Unsupported => "settings.unsupported",
        GameSettingsDiagnostic.InvalidValue => "settings.value-invalid",
        GameSettingsDiagnostic.IoFailure => "settings.io-failure",
        _ => throw new ArgumentOutOfRangeException(nameof(diagnostic)),
    };
}

public sealed record GameSettingsReadResult(
    GameSettingsProfile Profile,
    bool Loaded,
    GameSettingsDiagnostic Diagnostic);

public sealed record GameSettingsApplyResult(
    GameSettingsProfile ActiveProfile,
    bool Applied,
    GameSettingsDiagnostic Diagnostic);
