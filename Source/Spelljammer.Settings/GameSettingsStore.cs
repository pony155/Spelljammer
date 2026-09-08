using Spelljammer.Storage;

namespace Spelljammer.Settings;

/// <summary>Settings specialization of the shared transactional file-system boundary.</summary>
public interface IGameSettingsFileSystem : IAtomicFileSystem;

/// <summary>Physical settings file system retained as a compatibility-friendly construction type.</summary>
public sealed class PhysicalGameSettingsFileSystem : PhysicalAtomicFileSystem, IGameSettingsFileSystem;

/// <summary>Reads, validates, atomically publishes, and recovers the game settings profile.</summary>
public sealed class GameSettingsStore
{
    private readonly IGameSettingsFileSystem files;
    private readonly AtomicFileStore<GameSettingsDiagnostic> atomicFiles;

    public GameSettingsStore(IGameSettingsFileSystem? fileSystem = null)
    {
        files = fileSystem ?? new PhysicalGameSettingsFileSystem();
        atomicFiles = new AtomicFileStore<GameSettingsDiagnostic>(
            files,
            GameSettingsDiagnostic.None,
            GameSettingsDiagnostic.Missing,
            GameSettingsDiagnostic.Oversized,
            GameSettingsDiagnostic.IoFailure);
    }

    public GameSettingsReadResult Read(string sourcePath)
    {
        string source = AtomicFileStore<GameSettingsDiagnostic>.ResolveFile(sourcePath);
        try
        {
            if (!files.Exists(source))
            {
                return new GameSettingsReadResult(GameSettingsProfile.Default, false, GameSettingsDiagnostic.Missing);
            }

            long length = files.GetLength(source);
            if (length > GameSettingsProfile.MaximumSerializedBytes)
            {
                return new GameSettingsReadResult(GameSettingsProfile.Default, false, GameSettingsDiagnostic.Oversized);
            }

            if (length <= 0)
            {
                return new GameSettingsReadResult(GameSettingsProfile.Default, false, GameSettingsDiagnostic.Corrupt);
            }

            return GameSettingsCodec.Decode(files.ReadAllBytes(source));
        }
        catch (IOException)
        {
            return new GameSettingsReadResult(GameSettingsProfile.Default, false, GameSettingsDiagnostic.IoFailure);
        }
        catch (UnauthorizedAccessException)
        {
            return new GameSettingsReadResult(GameSettingsProfile.Default, false, GameSettingsDiagnostic.IoFailure);
        }
    }

    public GameSettingsDiagnostic Write(string targetPath, GameSettingsProfile profile)
    {
        if (!profile.IsValid)
        {
            return GameSettingsDiagnostic.InvalidValue;
        }

        byte[] encoded = GameSettingsCodec.Encode(profile);
        AtomicFileResult<GameSettingsDiagnostic> result = atomicFiles.Write(
            targetPath,
            encoded,
            candidate => Validate(candidate, profile),
            createDirectory: true);
        return result.Diagnostic;
    }

    public GameSettingsDiagnostic Recover(string targetPath)
    {
        AtomicFileResult<GameSettingsDiagnostic> result = atomicFiles.Recover(
            targetPath,
            GameSettingsProfile.MaximumSerializedBytes,
            candidate =>
            {
                GameSettingsReadResult decoded = GameSettingsCodec.Decode(candidate);
                return decoded.Loaded
                    ? new AtomicValidation<GameSettingsDiagnostic>(
                        true,
                        GameSettingsDiagnostic.None,
                        GameSettingsCodec.Encode(decoded.Profile))
                    : new AtomicValidation<GameSettingsDiagnostic>(
                        false,
                        decoded.Diagnostic,
                        ReadOnlyMemory<byte>.Empty);
            });
        return result.Diagnostic;
    }

    public bool CleanupRecovery(string targetPath) => atomicFiles.CleanupRecovery(targetPath);

    private static AtomicValidation<GameSettingsDiagnostic> Validate(
        ReadOnlyMemory<byte> candidate,
        GameSettingsProfile expected)
    {
        GameSettingsReadResult decoded = GameSettingsCodec.Decode(candidate);
        if (!decoded.Loaded || decoded.Profile != expected)
        {
            GameSettingsDiagnostic diagnostic = decoded.Diagnostic == GameSettingsDiagnostic.None
                ? GameSettingsDiagnostic.Corrupt
                : decoded.Diagnostic;
            return new AtomicValidation<GameSettingsDiagnostic>(false, diagnostic, ReadOnlyMemory<byte>.Empty);
        }

        return new AtomicValidation<GameSettingsDiagnostic>(
            true,
            GameSettingsDiagnostic.None,
            candidate);
    }
}

public sealed class GameSettingsRegistry
{
    private readonly GameSettingsStore store;

    /// <summary>
    /// Initializes a new game settings registry with the specified initial profile.
    /// </summary>
    /// <param name="initial">The initial active settings profile. Must be valid (pass <see cref="GameSettingsProfile.IsValid"/>).</param>
    /// <param name="store">The settings store to use for persistence, or null to create a new physical store.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="initial"/> is null or invalid.</exception>
    public GameSettingsRegistry(GameSettingsProfile initial, GameSettingsStore? store = null)
    {
        Active = initial?.IsValid == true
            ? initial
            : throw new ArgumentException("Initial game settings are invalid.", nameof(initial));
        this.store = store ?? new GameSettingsStore();
    }

    /// <summary>
    /// Gets the currently active game settings profile.
    /// </summary>
    /// <remarks>
    /// This profile is updated whenever <see cref="Apply"/> succeeds in persisting new settings to storage.
    /// </remarks>
    public GameSettingsProfile Active { get; private set; }

    /// <summary>
    /// Loads game settings from the specified file path and creates a new registry with those settings.
    /// </summary>
    /// <remarks>
    /// If loading fails, a registry is created with the default settings profile, and the diagnostic code
    /// indicates the reason for failure. The caller should check the diagnostic to determine whether to
    /// prompt the user or retry loading.
    /// </remarks>
    /// <param name="path">The file path to load settings from.</param>
    /// <param name="store">The settings store to use, or null to create a new physical store.</param>
    /// <returns>A tuple containing the newly created registry and a diagnostic code indicating the load result.</returns>
    public static (GameSettingsRegistry Registry, GameSettingsDiagnostic Diagnostic) Load(
        string path,
        GameSettingsStore? store = null)
    {
        GameSettingsStore source = store ?? new GameSettingsStore();
        GameSettingsReadResult result = source.Read(path);
        return (new GameSettingsRegistry(result.Profile, source), result.Diagnostic);
    }

    /// <summary>
    /// Applies a new settings profile, persisting it to the specified file path and updating the active profile.
    /// </summary>
    /// <remarks>
    /// If the candidate profile is invalid or if persistence fails, the active profile is not changed and
    /// a diagnostic code is returned indicating the reason for failure. Only if persistence succeeds is
    /// the active profile updated.
    /// </remarks>
    /// <param name="path">The file path to write the new settings to.</param>
    /// <param name="candidate">The new settings profile to apply.</param>
    /// <returns>A <see cref="GameSettingsApplyResult"/> containing the active profile (updated or unchanged) and a diagnostic code.</returns>
    public GameSettingsApplyResult Apply(string path, GameSettingsProfile candidate)
    {
        if (!candidate.IsValid)
        {
            return new GameSettingsApplyResult(Active, false, GameSettingsDiagnostic.InvalidValue);
        }

        GameSettingsDiagnostic diagnostic = store.Write(path, candidate);
        if (diagnostic != GameSettingsDiagnostic.None)
        {
            return new GameSettingsApplyResult(Active, false, diagnostic);
        }

        Active = candidate;
        return new GameSettingsApplyResult(Active, true, GameSettingsDiagnostic.None);
    }
}

/// <summary>
/// Provides paths to game settings files on the local file system.
/// </summary>
public static class GameSettingsPath
{
    /// <summary>
    /// Gets the path to the current user's game settings file.
    /// </summary>
    /// <remarks>
    /// The settings file is stored in the local application data directory, typically:
    /// <c>%LOCALAPPDATA%\Spelljammer\settings.v1.json</c> on Windows.
    /// </remarks>
    public static string CurrentUser => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Spelljammer",
        "settings.v1.json");
}
