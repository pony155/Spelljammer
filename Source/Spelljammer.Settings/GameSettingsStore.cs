namespace Spelljammer.Settings;

/// <summary>
/// Abstract interface for file system operations used by the settings store.
/// </summary>
/// <remarks>
/// This interface allows the settings store to be unit tested with mock file systems, or to be
/// adapted to different storage backends (e.g., cloud storage, sandboxed file systems).
/// </remarks>
public interface IGameSettingsFileSystem
{
    /// <summary>
    /// Determines whether a file exists at the specified path.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    bool Exists(string path);

    /// <summary>
    /// Gets the size in bytes of the file at the specified path.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The size of the file in bytes.</returns>
    /// <exception cref="IOException">Thrown if the file does not exist or cannot be accessed.</exception>
    long GetLength(string path);

    /// <summary>
    /// Reads the entire contents of a file into memory as a byte array.
    /// </summary>
    /// <param name="path">The file path to read.</param>
    /// <returns>A byte array containing the file contents.</returns>
    /// <exception cref="IOException">Thrown if the file cannot be read.</exception>
    byte[] ReadAllBytes(string path);

    /// <summary>
    /// Ensures that a directory exists, creating it and any missing parent directories if needed.
    /// </summary>
    /// <param name="path">The directory path to create or verify.</param>
    /// <exception cref="IOException">Thrown if the directory cannot be created.</exception>
    void EnsureDirectory(string path);

    /// <summary>
    /// Writes data to a new file with durability guarantees (e.g., write-through or fsync).
    /// </summary>
    /// <remarks>
    /// This method should create a new file and ensure the data is durable before returning,
    /// typically by flushing to the file system or using write-through mode.
    /// </remarks>
    /// <param name="path">The file path to write to. The file should not already exist.</param>
    /// <param name="bytes">The data to write.</param>
    /// <exception cref="IOException">Thrown if the file cannot be written or data durability cannot be ensured.</exception>
    void WriteDurable(string path, ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Moves (renames) a file from one path to another.
    /// </summary>
    /// <param name="source">The current file path.</param>
    /// <param name="destination">The new file path.</param>
    /// <exception cref="IOException">Thrown if the file cannot be moved.</exception>
    void Move(string source, string destination);

    /// <summary>
    /// Atomically replaces the destination file with the source file, optionally keeping a backup.
    /// </summary>
    /// <remarks>
    /// This operation should be atomic on the target platform (e.g., using the Win32 ReplaceFile API).
    /// If a recovery path is provided, the original destination file is moved there before replacement.
    /// </remarks>
    /// <param name="source">The file to promote (typically a temporary file).</param>
    /// <param name="destination">The target file to replace.</param>
    /// <param name="recoveryPath">The path to move the old destination file to, or null to discard it.</param>
    /// <exception cref="IOException">Thrown if the replacement cannot be performed.</exception>
    void Replace(string source, string destination, string? recoveryPath);

    /// <summary>
    /// Deletes a file.
    /// </summary>
    /// <param name="path">The file path to delete.</param>
    /// <exception cref="IOException">Thrown if the file cannot be deleted.</exception>
    void Delete(string path);
}

/// <summary>
/// Physical file system implementation for reading and writing game settings to the local file system.
/// </summary>
/// <remarks>
/// This implementation uses the Windows .NET File and Directory APIs directly. WriteDurable uses
/// FileOptions.WriteThrough to ensure data is written to disk before returning, and Replace uses
/// the native Win32 ReplaceFile API via File.Replace for atomic operations.
/// </remarks>
public sealed class PhysicalGameSettingsFileSystem : IGameSettingsFileSystem
{
    /// <inheritdoc/>
    public bool Exists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public long GetLength(string path) => new FileInfo(path).Length;

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    /// <inheritdoc/>
    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc/>
    public void WriteDurable(string path, ReadOnlySpan<byte> bytes)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            16 * 1024,
            FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(true);
    }

    /// <inheritdoc/>
    public void Move(string source, string destination) => File.Move(source, destination);

    /// <inheritdoc/>
    public void Replace(string source, string destination, string? recoveryPath) =>
        File.Replace(source, destination, recoveryPath, true);

    /// <inheritdoc/>
    public void Delete(string path) => File.Delete(path);
}

/// <summary>
/// Manages persistent storage and retrieval of game settings, including write durability and recovery.
/// </summary>
/// <remarks>
/// This class provides a high-level interface for reading, writing, and recovering game settings from storage.
/// All write operations use atomic file replacement with optional recovery files to protect against corruption
/// during a crash. The store can be constructed with a custom <see cref="IGameSettingsFileSystem"/> for testing
/// or alternative storage backends.
/// </remarks>
public sealed class GameSettingsStore(IGameSettingsFileSystem? fileSystem = null)
{
    private readonly IGameSettingsFileSystem files = fileSystem ?? new PhysicalGameSettingsFileSystem();

    /// <summary>
    /// Reads game settings from the specified file path.
    /// </summary>
    /// <remarks>
    /// This method performs several validation checks before decoding:
    /// - Checks if the file exists
    /// - Verifies the file size is within limits
    /// - Ensures the file is not empty or truncated
    /// - Decodes and validates the JSON content
    /// All errors are caught and returned as diagnostic codes to ensure the method never throws.
    /// </remarks>
    /// <param name="sourcePath">The file path to read from. Can be relative or absolute.</param>
    /// <returns>A <see cref="GameSettingsReadResult"/> containing the loaded profile and a diagnostic code.</returns>
    public GameSettingsReadResult Read(string sourcePath)
    {
        string source = ResolveFile(sourcePath);
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

    /// <summary>
    /// Writes a game settings profile to the specified file path with durability and recovery guarantees.
    /// </summary>
    /// <remarks>
    /// This method uses a safe atomic write pattern:
    /// 1. Encodes the settings to JSON
    /// 2. Writes to a temporary file with durability guarantees (WriteThrough)
    /// 3. Reads back and validates the temporary file to ensure data integrity
    /// 4. If a target file already exists, atomically replaces it while preserving a recovery backup
    /// 5. Otherwise, moves the temporary file to the target location
    /// If any step fails, the operation returns a diagnostic code and the temporary file is cleaned up.
    /// </remarks>
    /// <param name="targetPath">The file path to write to. Can be relative or absolute.</param>
    /// <param name="profile">The settings profile to write. Must pass <see cref="GameSettingsProfile.IsValid"/>.</param>
    /// <returns>A diagnostic code indicating success (<see cref="GameSettingsDiagnostic.None"/>) or the reason for failure.</returns>
    public GameSettingsDiagnostic Write(string targetPath, GameSettingsProfile profile)
    {
        if (!profile.IsValid)
        {
            return GameSettingsDiagnostic.InvalidValue;
        }

        string target = ResolveFile(targetPath);
        string directory = Path.GetDirectoryName(target)!;
        string temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.tmp-{Guid.NewGuid():N}");
        string recovery = target + ".recovery";
        try
        {
            files.EnsureDirectory(directory);
            byte[] bytes = GameSettingsCodec.Encode(profile);
            files.WriteDurable(temporary, bytes);
            GameSettingsReadResult staged = GameSettingsCodec.Decode(files.ReadAllBytes(temporary));
            if (!staged.Loaded || staged.Profile != profile)
            {
                return staged.Diagnostic == GameSettingsDiagnostic.None
                    ? GameSettingsDiagnostic.Corrupt
                    : staged.Diagnostic;
            }

            if (files.Exists(target))
            {
                files.Replace(temporary, target, recovery);
            }
            else
            {
                files.Move(temporary, target);
            }

            return GameSettingsDiagnostic.None;
        }
        catch (IOException)
        {
            return GameSettingsDiagnostic.IoFailure;
        }
        catch (UnauthorizedAccessException)
        {
            return GameSettingsDiagnostic.IoFailure;
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    /// <summary>
    /// Attempts to restore a settings file from its recovery backup if the main file is missing or corrupted.
    /// </summary>
    /// <remarks>
    /// Recovery files are created by the <see cref="Write"/> method when replacing an existing settings file.
    /// This method reads the recovery file, validates it, re-encodes it (to ensure consistency), and replaces
    /// the target file if the recovery is valid. If the target file does not exist, the recovery file is moved
    /// directly to the target location.
    /// </remarks>
    /// <param name="targetPath">The file path to recover. Can be relative or absolute.</param>
    /// <returns>A diagnostic code indicating success (<see cref="GameSettingsDiagnostic.None"/>) or the reason for failure.</returns>
    public GameSettingsDiagnostic Recover(string targetPath)
    {
        string target = ResolveFile(targetPath);
        string recovery = target + ".recovery";
        string directory = Path.GetDirectoryName(target)!;
        string temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.recover-{Guid.NewGuid():N}");
        try
        {
            if (!files.Exists(recovery))
            {
                return GameSettingsDiagnostic.Missing;
            }

            long length = files.GetLength(recovery);
            if (length > GameSettingsProfile.MaximumSerializedBytes)
            {
                return GameSettingsDiagnostic.Oversized;
            }

            GameSettingsReadResult candidate = GameSettingsCodec.Decode(files.ReadAllBytes(recovery));
            if (!candidate.Loaded)
            {
                return candidate.Diagnostic;
            }

            files.WriteDurable(temporary, GameSettingsCodec.Encode(candidate.Profile));
            GameSettingsReadResult staged = GameSettingsCodec.Decode(files.ReadAllBytes(temporary));
            if (!staged.Loaded || staged.Profile != candidate.Profile)
            {
                return staged.Diagnostic == GameSettingsDiagnostic.None
                    ? GameSettingsDiagnostic.Corrupt
                    : staged.Diagnostic;
            }

            if (files.Exists(target))
            {
                files.Replace(temporary, target, null);
            }
            else
            {
                files.Move(temporary, target);
            }

            return GameSettingsDiagnostic.None;
        }
        catch (IOException)
        {
            return GameSettingsDiagnostic.IoFailure;
        }
        catch (UnauthorizedAccessException)
        {
            return GameSettingsDiagnostic.IoFailure;
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    /// <summary>
    /// Attempts to delete a file, silently continuing if the deletion fails.
    /// </summary>
    /// <remarks>
    /// This method is used to clean up temporary files created during write operations. If deletion fails
    /// (due to I/O errors or permission issues), the file is left behind as an orphan but the operation
    /// continues. This prevents temporary files from blocking important operations.
    /// </remarks>
    /// <param name="path">The file path to delete.</param>
    private void TryDelete(string path)
    {
        try
        {
            if (files.Exists(path))
            {
                files.Delete(path);
            }
        }
        catch (IOException)
        {
            // Retain only this exact orphan when cleanup cannot complete.
        }
        catch (UnauthorizedAccessException)
        {
            // Retain only this exact orphan when cleanup cannot complete.
        }
    }

    /// <summary>
    /// Deletes the recovery backup file associated with the target settings file.
    /// </summary>
    /// <remarks>
    /// This method should be called after successfully loading settings from the target file to clean up
    /// the recovery backup. If cleanup fails, the recovery file is left in place and the method returns false.
    /// </remarks>
    /// <param name="targetPath">The settings file path whose recovery file should be deleted. Can be relative or absolute.</param>
    /// <returns>True if the recovery file was successfully deleted or did not exist; false if deletion failed.</returns>
    public bool CleanupRecovery(string targetPath)
    {
        string recovery = ResolveFile(targetPath) + ".recovery";
        try
        {
            if (files.Exists(recovery))
            {
                files.Delete(recovery);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves a settings file path to an absolute path and validates that it refers to a file, not a directory.
    /// </summary>
    /// <param name="path">The settings file path to resolve (relative or absolute).</param>
    /// <returns>The absolute, normalized file path.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is null, empty, whitespace-only, or ends with a directory separator.</exception>
    private static string ResolveFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        if (Path.EndsInDirectorySeparator(full))
        {
            throw new ArgumentException("A settings path must identify a file.", nameof(path));
        }

        return full;
    }
}

/// <summary>
/// Manages the active game settings for the current session, coordinating with persistent storage.
/// </summary>
/// <remarks>
/// This class maintains the currently-active settings profile and provides operations to load settings
/// from storage or apply new settings. It acts as a high-level facade over the <see cref="GameSettingsStore"/>,
/// ensuring that the in-memory active settings are always synchronized with persistent storage.
/// </remarks>
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
