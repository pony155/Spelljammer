using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spelljammer.Settings;

/// <summary>
/// Provides serialization and deserialization of game settings to and from JSON.
/// </summary>
/// <remarks>
/// This codec handles both current-version settings and legacy version 1 settings, automatically migrating
/// old settings to the current schema when needed. The codec also validates settings before and after
/// serialization to ensure integrity and prevent corruption.
/// Code flow: UTF-8 JSON is shape-checked and migrated into the current profile, then validated before publication;
/// writes validate first and emit a deterministic current-schema document.
/// </remarks>
public static class GameSettingsCodec
{
    /// <summary>
    /// The JSON property names used in legacy version 1 settings (without language and resolution).
    /// </summary>
    private static readonly string[] Version1Properties =
    [
        "schemaVersion",
        "masterVolume",
        "musicVolume",
        "effectsVolume",
        "subtitles",
        "reducedMotion",
        "screenShake",
        "uiScalePercent",
    ];

    /// <summary>
    /// The JSON property names used in the current version of settings.
    /// </summary>
    private static readonly string[] CurrentProperties =
    [
        .. Version1Properties,
        "language",
        "resolution",
    ];

    /// <summary>
    /// JSON serializer options configured for settings serialization.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
        WriteIndented = true,
    };

    /// <summary>
    /// Encodes a valid game settings profile to a UTF-8 JSON byte array.
    /// </summary>
    /// <param name="profile">The settings profile to encode. Must not be null and must pass <see cref="GameSettingsProfile.IsValid"/>.</param>
    /// <returns>The UTF-8 encoded JSON representation of the profile.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the profile fails validation checks.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the encoded bytes exceed <see cref="GameSettingsProfile.MaximumSerializedBytes"/>.</exception>
    public static byte[] Encode(GameSettingsProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.IsValid)
        {
            throw new ArgumentException("Game settings contain an unsupported schema or invalid value.", nameof(profile));
        }

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(profile, JsonOptions);
        if (bytes.Length > GameSettingsProfile.MaximumSerializedBytes)
        {
            throw new InvalidOperationException("Game settings exceed the serialized byte limit.");
        }

        return bytes;
    }

    /// <summary>
    /// Decodes a UTF-8 JSON byte array into a game settings profile.
    /// </summary>
    /// <remarks>
    /// This method performs extensive validation including:
    /// - Checking size constraints before parsing
    /// - Verifying JSON structure and property names
    /// - Handling legacy version 1 settings with automatic migration
    /// - Validating all settings values against constraints
    /// All errors are captured and returned as diagnostic codes rather than exceptions.
    /// </remarks>
    /// <param name="bytes">The UTF-8 JSON bytes to decode.</param>
    /// <returns>A <see cref="GameSettingsReadResult"/> containing the decoded profile (or default if decode failed) and a diagnostic code.</returns>
    public static GameSettingsReadResult Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > GameSettingsProfile.MaximumSerializedBytes)
        {
            return Failed(GameSettingsDiagnostic.Oversized);
        }

        if (bytes.IsEmpty)
        {
            return Failed(GameSettingsDiagnostic.Corrupt);
        }

        try
        {
            HashSet<string> properties = CollectProperties(bytes.Span);
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out JsonElement schemaElement) ||
                !schemaElement.TryGetInt32(out int schemaVersion))
            {
                return Failed(GameSettingsDiagnostic.Corrupt);
            }

            if (schemaVersion == 1)
            {
                return DecodeVersion1(bytes.Span, properties);
            }

            if (schemaVersion != GameSettingsProfile.CurrentSchemaVersion)
            {
                return Failed(GameSettingsDiagnostic.Unsupported);
            }

            if (!properties.SetEquals(CurrentProperties))
            {
                return Failed(GameSettingsDiagnostic.Corrupt);
            }

            GameSettingsProfile? profile = JsonSerializer.Deserialize<GameSettingsProfile>(bytes.Span, JsonOptions);
            if (profile is null)
            {
                return Failed(GameSettingsDiagnostic.Corrupt);
            }

            return profile.IsValid
                ? new GameSettingsReadResult(profile, true, GameSettingsDiagnostic.None)
                : Failed(GameSettingsDiagnostic.InvalidValue);
        }
        catch (JsonException)
        {
            return Failed(GameSettingsDiagnostic.Corrupt);
        }
        catch (InvalidOperationException)
        {
            return Failed(GameSettingsDiagnostic.Corrupt);
        }
    }

    /// <summary>
    /// Decodes and migrates legacy version 1 settings to the current schema version.
    /// </summary>
    /// <remarks>
    /// Version 1 settings lacked language and resolution properties. This migration adds default values
    /// (English and desktop resolution) to make the settings compatible with the current schema.
    /// </remarks>
    /// <param name="bytes">The UTF-8 JSON bytes of the version 1 settings.</param>
    /// <param name="properties">A set of property names found in the JSON document.</param>
    /// <returns>A <see cref="GameSettingsReadResult"/> containing the migrated profile.</returns>
    private static GameSettingsReadResult DecodeVersion1(ReadOnlySpan<byte> bytes, HashSet<string> properties)
    {
        if (!properties.SetEquals(Version1Properties))
        {
            return Failed(GameSettingsDiagnostic.Corrupt);
        }

        Version1Profile? legacy = JsonSerializer.Deserialize<Version1Profile>(bytes, JsonOptions);
        if (legacy is null || legacy.SchemaVersion != 1)
        {
            return Failed(GameSettingsDiagnostic.Corrupt);
        }

        GameSettingsProfile migrated = new(
            GameSettingsProfile.CurrentSchemaVersion,
            legacy.MasterVolume,
            legacy.MusicVolume,
            legacy.EffectsVolume,
            legacy.Subtitles,
            legacy.ReducedMotion,
            legacy.ScreenShake,
            legacy.UiScalePercent,
            GameSettingsChoices.DefaultLanguage,
            GameSettingsChoices.DesktopResolution);
        return migrated.IsValid
            ? new GameSettingsReadResult(migrated, true, GameSettingsDiagnostic.None)
            : Failed(GameSettingsDiagnostic.InvalidValue);
    }

    /// <summary>
    /// Collects all property names from a JSON document, validating that there is exactly one root object
    /// and no duplicate properties.
    /// </summary>
    /// <remarks>
    /// This method is used to ensure the JSON structure is well-formed before deserializing, helping detect
    /// corruption or tampering. It validates that the document contains a single root object with no duplicates.
    /// </remarks>
    /// <param name="json">The UTF-8 JSON bytes to scan.</param>
    /// <returns>A set of all property names found in the root object.</returns>
    /// <exception cref="JsonException">Thrown if the JSON is malformed or contains duplicate properties.</exception>
    private static HashSet<string> CollectProperties(ReadOnlySpan<byte> json)
    {
        Utf8JsonReader reader = new(json, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 8,
        });
        Stack<HashSet<string>> objects = new();
        HashSet<string>? rootProperties = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                HashSet<string> properties = new(StringComparer.Ordinal);
                if (objects.Count == 0)
                {
                    rootProperties = properties;
                }

                objects.Push(properties);
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                objects.Pop();
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string property = reader.GetString() ?? throw new JsonException();
                if (objects.Count == 0 || !objects.Peek().Add(property))
                {
                    throw new JsonException("Duplicate settings property.");
                }
            }
        }

        if (rootProperties is null || objects.Count != 0)
        {
            throw new JsonException("Settings document must contain exactly one complete root object.");
        }

        return rootProperties;
    }

    /// <summary>
    /// Represents the legacy version 1 settings format used by older game versions.
    /// </summary>
    /// <remarks>
    /// Version 1 lacked the <c>language</c> and <c>resolution</c> properties that are required by the
    /// current schema. This type is used only for deserialization and migration purposes.
    /// </remarks>
    private sealed record Version1Profile(
        int SchemaVersion,
        int MasterVolume,
        int MusicVolume,
        int EffectsVolume,
        bool Subtitles,
        bool ReducedMotion,
        bool ScreenShake,
        int UiScalePercent);

    /// <summary>
    /// Creates a failed read result with the default settings profile and the given diagnostic code.
    /// </summary>
    /// <param name="diagnostic">The diagnostic code indicating why the read failed.</param>
    /// <returns>A <see cref="GameSettingsReadResult"/> with <see cref="GameSettingsReadResult.Loaded"/> set to false.</returns>
    private static GameSettingsReadResult Failed(GameSettingsDiagnostic diagnostic) =>
        new(GameSettingsProfile.Default, false, diagnostic);
}
