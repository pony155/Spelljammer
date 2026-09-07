using System.Text.Json.Serialization;

namespace Spelljammer.Settings;

/// <summary>
/// Represents the complete set of game settings that can be persisted and applied during gameplay.
/// </summary>
/// <remarks>
/// This sealed record includes audio settings (master, music, effects volumes), visual settings (UI scale,
/// screen shake), accessibility settings (subtitles, reduced motion), and localization settings (language,
/// resolution). The record is immutable and can be validated for correctness using the <see cref="IsValid"/>
/// property before being serialized or applied.
/// </remarks>
/// <param name="SchemaVersion">The version of the settings schema; used for format evolution and migration.</param>
/// <param name="MasterVolume">Master volume level (0-100).</param>
/// <param name="MusicVolume">Music volume level (0-100).</param>
/// <param name="EffectsVolume">Sound effects volume level (0-100).</param>
/// <param name="Subtitles">Whether subtitles are enabled.</param>
/// <param name="ReducedMotion">Whether reduced motion mode is enabled for accessibility.</param>
/// <param name="ScreenShake">Whether screen shake effects are enabled.</param>
/// <param name="UiScalePercent">UI scale in percent (75-150).</param>
/// <param name="Language">The language code (e.g., "en-US", "fr-FR").</param>
/// <param name="Resolution">The display resolution identifier (e.g., "1920x1080", "desktop").</param>
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
    /// <summary>
    /// The current schema version that all settings must conform to.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>
    /// The minimum allowed volume level.
    /// </summary>
    public const int MinimumVolume = 0;

    /// <summary>
    /// The maximum allowed volume level.
    /// </summary>
    public const int MaximumVolume = 100;

    /// <summary>
    /// The minimum allowed UI scale percent.
    /// </summary>
    public const int MinimumUiScalePercent = 75;

    /// <summary>
    /// The maximum allowed UI scale percent.
    /// </summary>
    public const int MaximumUiScalePercent = 150;

    /// <summary>
    /// The maximum size in bytes for a serialized settings file.
    /// </summary>
    public const int MaximumSerializedBytes = 64 * 1024;

    /// <summary>
    /// The default settings profile with standard game defaults.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether this settings profile conforms to all constraints and is safe to apply.
    /// </summary>
    /// <remarks>
    /// Validation checks include: schema version matches current version, all volumes are within allowed ranges,
    /// UI scale is within acceptable bounds, language is supported, and resolution is recognized.
    /// </remarks>
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

/// <summary>
/// Represents a display resolution choice available to the player.
/// </summary>
/// <remarks>
/// Resolutions can be either a specific pixel dimension (Width x Height) or desktop-resolution (0x0),
/// which means the game should adapt to the current monitor resolution.
/// </remarks>
/// <param name="Id">A unique identifier for the resolution (e.g., "1920x1080", "desktop").</param>
/// <param name="Width">The pixel width of the resolution, or 0 for desktop-resolution.</param>
/// <param name="Height">The pixel height of the resolution, or 0 for desktop-resolution.</param>
public readonly record struct GameResolutionChoice(string Id, int Width, int Height)
{
    /// <summary>
    /// Gets a value indicating whether this resolution represents desktop (adaptive) mode.
    /// </summary>
    public bool IsDesktop => Width == 0 && Height == 0;
}

/// <summary>
/// Provides the set of supported language and resolution options for the game.
/// </summary>
public static class GameSettingsChoices
{
    /// <summary>
    /// The default language code used when no preference is specified.
    /// </summary>
    public const string DefaultLanguage = "en-US";

    /// <summary>
    /// The identifier for desktop-adaptive resolution mode.
    /// </summary>
    public const string DesktopResolution = "desktop";

    /// <summary>
    /// The read-only list of all supported language codes.
    /// </summary>
    public static IReadOnlyList<string> Languages { get; } = Array.AsReadOnly(
        [DefaultLanguage, "fr-FR", "zh-Hant-TW"]);

    /// <summary>
    /// The read-only list of all supported display resolutions.
    /// </summary>
    public static IReadOnlyList<GameResolutionChoice> Resolutions { get; } = Array.AsReadOnly(
        new GameResolutionChoice[]
        {
            new(DesktopResolution, 0, 0),
            new("1280x720", 1280, 720),
            new("1600x900", 1600, 900),
            new("1920x1080", 1920, 1080),
            new("2560x1440", 2560, 1440),
        });

    /// <summary>
    /// Determines whether the specified language code is supported.
    /// </summary>
    /// <param name="language">The language code to check (e.g., "en-US").</param>
    /// <returns>True if the language is in the supported <see cref="Languages"/> list; otherwise, false.</returns>
    public static bool IsSupportedLanguage(string? language) =>
        Languages.Contains(language, StringComparer.Ordinal);

    /// <summary>
    /// Attempts to find a resolution by its identifier.
    /// </summary>
    /// <param name="id">The resolution identifier to look up (e.g., "1920x1080", "desktop").</param>
    /// <param name="resolution">When this method returns true, contains the matching <see cref="GameResolutionChoice"/>; otherwise, contains the default value.</param>
    /// <returns>True if a resolution with the specified <paramref name="id"/> exists; otherwise, false.</returns>
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

/// <summary>
/// Enumeration of diagnostic codes that indicate the result of settings operations.
/// </summary>
public enum GameSettingsDiagnostic : byte
{
    /// <summary>
    /// The operation completed successfully with no issues.
    /// </summary>
    None,

    /// <summary>
    /// The settings file does not exist.
    /// </summary>
    Missing,

    /// <summary>
    /// The settings file exists but contains invalid or malformed JSON.
    /// </summary>
    Corrupt,

    /// <summary>
    /// The settings file exceeds the maximum allowed size.
    /// </summary>
    Oversized,

    /// <summary>
    /// The settings file has a schema version that is not supported by this game version.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The settings file contains values that violate constraints (e.g., volume out of range).
    /// </summary>
    InvalidValue,

    /// <summary>
    /// An I/O or permission error occurred while accessing the settings file.
    /// </summary>
    IoFailure,
}

/// <summary>
/// Utility class for working with <see cref="GameSettingsDiagnostic"/> values.
/// </summary>
public static class GameSettingsDiagnostics
{
    /// <summary>
    /// Converts a diagnostic code to a stable localization key that identifies the error or condition.
    /// </summary>
    /// <param name="diagnostic">The diagnostic code to convert.</param>
    /// <returns>A localization key (e.g., "settings.corrupt") that can be used to look up a user-facing error message.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="diagnostic"/> is an unrecognized value.</exception>
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

/// <summary>
/// The result of attempting to read game settings from storage.
/// </summary>
/// <param name="Profile">The settings profile that was read, or the default if loading failed.</param>
/// <param name="Loaded">True if the settings were successfully read and validated; false if an error occurred.</param>
/// <param name="Diagnostic">A diagnostic code describing the outcome (e.g., <see cref="GameSettingsDiagnostic.Missing"/>, <see cref="GameSettingsDiagnostic.Corrupt"/>).</param>
public sealed record GameSettingsReadResult(
    GameSettingsProfile Profile,
    bool Loaded,
    GameSettingsDiagnostic Diagnostic);

/// <summary>
/// The result of attempting to apply a new settings profile to the active configuration.
/// </summary>
/// <param name="ActiveProfile">The active settings profile after the operation (either the newly applied profile or the previous one).</param>
/// <param name="Applied">True if the new settings were successfully applied and persisted; false if the attempt failed.</param>
/// <param name="Diagnostic">A diagnostic code describing the outcome (e.g., <see cref="GameSettingsDiagnostic.IoFailure"/>, <see cref="GameSettingsDiagnostic.InvalidValue"/>).</param>
public sealed record GameSettingsApplyResult(
    GameSettingsProfile ActiveProfile,
    bool Applied,
    GameSettingsDiagnostic Diagnostic);
