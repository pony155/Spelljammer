using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spelljammer.Settings;

public static class GameSettingsCodec
{
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

    private static readonly string[] CurrentProperties =
    [
        .. Version1Properties,
        "language",
        "resolution",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
        WriteIndented = true,
    };

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

    private sealed record Version1Profile(
        int SchemaVersion,
        int MasterVolume,
        int MusicVolume,
        int EffectsVolume,
        bool Subtitles,
        bool ReducedMotion,
        bool ScreenShake,
        int UiScalePercent);

    private static GameSettingsReadResult Failed(GameSettingsDiagnostic diagnostic) =>
        new(GameSettingsProfile.Default, false, diagnostic);
}
