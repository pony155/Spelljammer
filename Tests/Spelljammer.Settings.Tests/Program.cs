using System.Text;
using Spelljammer.Settings;

return SettingsContracts.Run();

internal static class SettingsContracts
{
    public static int Run()
    {
        ProfileRoundTripsDeterministically();
        Version1ProfilesMigrateToSafeDisplayDefaults();
        InvalidDocumentsReturnStableDefaults();
        FailedReplacementRetainsActiveSettings();
        Console.WriteLine("Game settings contracts passed.");
        return 0;
    }

    private static void ProfileRoundTripsDeterministically()
    {
        GameSettingsProfile profile = GameSettingsProfile.Default with
        {
            MasterVolume = 55,
            ReducedMotion = true,
            UiScalePercent = 125,
            Language = "fr-FR",
            Resolution = "1600x900",
        };
        byte[] first = GameSettingsCodec.Encode(profile);
        byte[] second = GameSettingsCodec.Encode(profile);
        True(first.AsSpan().SequenceEqual(second), "Identical settings produced different bytes.");
        False(Encoding.UTF8.GetString(first).Contains("isValid", StringComparison.Ordinal),
            "Computed validation state leaked into the persisted schema.");
        GameSettingsReadResult decoded = GameSettingsCodec.Decode(first);
        True(decoded.Loaded, decoded.Diagnostic.ToString());
        Equal(profile, decoded.Profile, "Settings did not round-trip.");
    }

    private static void Version1ProfilesMigrateToSafeDisplayDefaults()
    {
        True(GameSettingsChoices.IsSupportedLanguage("zh-Hant-TW"),
            "Traditional Chinese is not exposed as a stable settings choice.");
        GameSettingsReadResult migrated = GameSettingsCodec.Decode(Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"masterVolume\":55,\"musicVolume\":45,\"effectsVolume\":35," +
            "\"subtitles\":false,\"reducedMotion\":true,\"screenShake\":false,\"uiScalePercent\":125}"));
        True(migrated.Loaded, migrated.Diagnostic.ToString());
        Equal(GameSettingsProfile.CurrentSchemaVersion, migrated.Profile.SchemaVersion,
            "Migrated settings retained the legacy schema.");
        Equal(55, migrated.Profile.MasterVolume, "Migration lost an existing preference.");
        Equal(GameSettingsChoices.DefaultLanguage, migrated.Profile.Language,
            "Migration did not choose the safe language default.");
        Equal(GameSettingsChoices.DesktopResolution, migrated.Profile.Resolution,
            "Migration did not choose the safe display default.");
    }

    private static void InvalidDocumentsReturnStableDefaults()
    {
        GameSettingsReadResult duplicate = GameSettingsCodec.Decode(
            Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"schemaVersion\":1}"));
        Equal(GameSettingsDiagnostic.Corrupt, duplicate.Diagnostic, "Duplicate property diagnostic changed.");
        Equal(GameSettingsProfile.Default, duplicate.Profile, "Corrupt settings did not use safe defaults.");

        GameSettingsReadResult incomplete = GameSettingsCodec.Decode(
            Encoding.UTF8.GetBytes("{\"schemaVersion\":2}"));
        Equal(GameSettingsDiagnostic.Corrupt, incomplete.Diagnostic, "Incomplete settings diagnostic changed.");

        GameSettingsReadResult unsupported = GameSettingsCodec.Decode(Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":3}"));
        Equal(GameSettingsDiagnostic.Unsupported, unsupported.Diagnostic, "Unsupported schema diagnostic changed.");

        GameSettingsReadResult invalidLanguage = GameSettingsCodec.Decode(Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":2,\"masterVolume\":80,\"musicVolume\":65,\"effectsVolume\":80," +
            "\"subtitles\":true,\"reducedMotion\":false,\"screenShake\":true,\"uiScalePercent\":100," +
            "\"language\":\"invalid\",\"resolution\":\"desktop\"}"));
        Equal(GameSettingsDiagnostic.InvalidValue, invalidLanguage.Diagnostic,
            "Unsupported language diagnostic changed.");

        GameSettingsReadResult invalidResolution = GameSettingsCodec.Decode(Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":2,\"masterVolume\":80,\"musicVolume\":65,\"effectsVolume\":80," +
            "\"subtitles\":true,\"reducedMotion\":false,\"screenShake\":true,\"uiScalePercent\":100," +
            "\"language\":\"en-US\",\"resolution\":\"1234x567\"}"));
        Equal(GameSettingsDiagnostic.InvalidValue, invalidResolution.Diagnostic,
            "Unsupported resolution diagnostic changed.");

        byte[] oversized = new byte[GameSettingsProfile.MaximumSerializedBytes + 1];
        Equal(GameSettingsDiagnostic.Oversized, GameSettingsCodec.Decode(oversized).Diagnostic,
            "Oversized settings diagnostic changed.");
    }

    private static void FailedReplacementRetainsActiveSettings()
    {
        MemoryFileSystem files = new();
        GameSettingsStore store = new(files);
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "settings.v1.json"));
        GameSettingsProfile initial = GameSettingsProfile.Default;
        files.Seed(path, GameSettingsCodec.Encode(initial));
        GameSettingsRegistry registry = new(initial, store);

        files.FailNextReplace = true;
        GameSettingsProfile candidate = initial with { MasterVolume = 10 };
        GameSettingsApplyResult failed = registry.Apply(path, candidate);
        False(failed.Applied, "Injected replacement failure was reported as success.");
        Equal(GameSettingsDiagnostic.IoFailure, failed.Diagnostic, "Replacement failure diagnostic changed.");
        True(ReferenceEquals(initial, registry.Active), "Failed apply replaced the active profile.");
        Equal(initial, GameSettingsCodec.Decode(files.ReadAllBytes(path)).Profile,
            "Failed apply altered the settings file.");

        GameSettingsApplyResult applied = registry.Apply(path, candidate);
        True(applied.Applied, applied.Diagnostic.ToString());
        Equal(candidate, registry.Active, "Successful apply did not publish the profile.");
        True(files.Exists(path + ".recovery"), "Replacement did not preserve one recovery artifact.");

        files.CorruptNextWrite = true;
        Equal(GameSettingsDiagnostic.Corrupt, store.Recover(path), "Corrupt recovery staging was published.");
        Equal(candidate, GameSettingsCodec.Decode(files.ReadAllBytes(path)).Profile,
            "Corrupt recovery staging altered the active settings file.");

        Equal(GameSettingsDiagnostic.None, store.Recover(path), "Recovery failed.");
        Equal(initial, GameSettingsCodec.Decode(files.ReadAllBytes(path)).Profile,
            "Recovery did not restore the previous settings.");
        True(store.CleanupRecovery(path), "Recovery cleanup failed.");
        False(files.Exists(path + ".recovery"), "Recovery cleanup retained the exact artifact.");
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
        }
    }

    private sealed class MemoryFileSystem : IGameSettingsFileSystem
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);

        public bool FailNextReplace { get; set; }
        public bool CorruptNextWrite { get; set; }
        public bool Exists(string path) => files.ContainsKey(path);
        public long GetLength(string path) => files[path].LongLength;
        public byte[] ReadAllBytes(string path) => files[path].ToArray();
        public void EnsureDirectory(string path) { }
        public void Seed(string path, byte[] bytes) => files[path] = bytes.ToArray();
        public void WriteDurable(string path, ReadOnlySpan<byte> bytes)
        {
            files.Add(path, CorruptNextWrite ? [0] : bytes.ToArray());
            CorruptNextWrite = false;
        }

        public void Move(string source, string destination)
        {
            files.Add(destination, files[source]);
            files.Remove(source);
        }

        public void Replace(string source, string destination, string? recoveryPath)
        {
            if (FailNextReplace)
            {
                FailNextReplace = false;
                throw new IOException("Injected replacement failure.");
            }

            if (recoveryPath is not null)
            {
                files[recoveryPath] = files[destination].ToArray();
            }

            files[destination] = files[source];
            files.Remove(source);
        }

        public void Delete(string path) => files.Remove(path);
    }
}
