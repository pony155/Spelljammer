namespace Spelljammer.Settings;

public interface IGameSettingsFileSystem
{
    bool Exists(string path);
    long GetLength(string path);
    byte[] ReadAllBytes(string path);
    void EnsureDirectory(string path);
    void WriteDurable(string path, ReadOnlySpan<byte> bytes);
    void Move(string source, string destination);
    void Replace(string source, string destination, string? recoveryPath);
    void Delete(string path);
}

public sealed class PhysicalGameSettingsFileSystem : IGameSettingsFileSystem
{
    public bool Exists(string path) => File.Exists(path);
    public long GetLength(string path) => new FileInfo(path).Length;
    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

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

    public void Move(string source, string destination) => File.Move(source, destination);
    public void Replace(string source, string destination, string? recoveryPath) =>
        File.Replace(source, destination, recoveryPath, true);
    public void Delete(string path) => File.Delete(path);
}

public sealed class GameSettingsStore(IGameSettingsFileSystem? fileSystem = null)
{
    private readonly IGameSettingsFileSystem files = fileSystem ?? new PhysicalGameSettingsFileSystem();

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

public sealed class GameSettingsRegistry
{
    private readonly GameSettingsStore store;

    public GameSettingsRegistry(GameSettingsProfile initial, GameSettingsStore? store = null)
    {
        Active = initial?.IsValid == true
            ? initial
            : throw new ArgumentException("Initial game settings are invalid.", nameof(initial));
        this.store = store ?? new GameSettingsStore();
    }

    public GameSettingsProfile Active { get; private set; }

    public static (GameSettingsRegistry Registry, GameSettingsDiagnostic Diagnostic) Load(
        string path,
        GameSettingsStore? store = null)
    {
        GameSettingsStore source = store ?? new GameSettingsStore();
        GameSettingsReadResult result = source.Read(path);
        return (new GameSettingsRegistry(result.Profile, source), result.Diagnostic);
    }

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

public static class GameSettingsPath
{
    public static string CurrentUser => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Spelljammer",
        "settings.v1.json");
}
