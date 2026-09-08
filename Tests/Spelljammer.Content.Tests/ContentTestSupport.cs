using System.Text;
using System.Text.Json;
using System.Collections.Immutable;
using Spelljammer.Content;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;

internal static partial class ContentContracts
{
    private static string Describe(RosterSnapshot roster, GameContentSnapshot snapshot) => string.Join(
        ';',
        roster.Characters.Select(character =>
            character.Id + ":" + string.Join(',', character.Capabilities.Snapshot(snapshot).Abilities.Select(value => value.Value))));

    private static Dictionary<string, byte[]> Clone(Dictionary<string, byte[]> source) =>
        source.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);

    private static void ReplaceText(Dictionary<string, byte[]> files, string path, string oldValue, string newValue)
    {
        string text = Encoding.UTF8.GetString(files[path]);
        files[path] = Encoding.UTF8.GetBytes(text.Replace(oldValue, newValue, StringComparison.Ordinal));
    }

    private static void ApplyMutations(JsonElement testCase, Dictionary<string, byte[]> files, List<string> duplicateEntries)
    {
        if (testCase.TryGetProperty("removeFiles", out JsonElement removals))
        {
            foreach (JsonElement removal in removals.EnumerateArray())
            {
                files.Remove(removal.GetString()!);
            }
        }

        if (testCase.TryGetProperty("writeText", out JsonElement writes))
        {
            foreach (JsonElement write in writes.EnumerateArray())
            {
                files[write.GetProperty("path").GetString()!] = Encoding.UTF8.GetBytes(write.GetProperty("text").GetString()!);
            }
        }

        if (testCase.TryGetProperty("writeHex", out JsonElement hexWrites))
        {
            foreach (JsonElement write in hexWrites.EnumerateArray())
            {
                files[write.GetProperty("path").GetString()!] = Convert.FromHexString(write.GetProperty("bytes").GetString()!);
            }
        }

        if (testCase.TryGetProperty("duplicateFiles", out JsonElement duplicates))
        {
            duplicateEntries.AddRange(duplicates.EnumerateArray().Select(item => item.GetString()!));
        }
    }

    private static Dictionary<string, byte[]> ReadFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);

    private static SemanticVersion ParseVersion(string value)
    {
        True(SemanticVersion.TryParse(value, out SemanticVersion version), "Fixture game version is invalid.");
        return version;
    }

    private static ContentCompilationResult CompileDirectory(string root) =>
        new GameContentCompiler().Compile([new DirectoryContentPackSource(root)], GameVersion);

    private static ContentLimits ApplyLimitOverrides(JsonElement testCase, ContentLimits defaults)
    {
        if (!testCase.TryGetProperty("limitOverrides", out JsonElement overrides))
        {
            return defaults;
        }

        ContentLimits result = defaults;
        if (overrides.TryGetProperty("manifestBytes", out JsonElement manifestBytes))
        {
            result = result with { ManifestBytes = manifestBytes.GetInt32() };
        }

        if (overrides.TryGetProperty("tagsPerDefinition", out JsonElement tags))
        {
            result = result with { TagsPerDefinition = tags.GetInt32() };
        }

        if (overrides.TryGetProperty("referencesPerDefinition", out JsonElement references))
        {
            result = result with { ReferencesPerDefinition = references.GetInt32() };
        }

        return result;
    }

    private static string Primary(ContentCompilationResult result) =>
        result.Diagnostics.FirstOrDefault()?.Code ?? result.IoFailure?.Kind.ToString() ?? "Compilation failed without a diagnostic.";

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

    private static void Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class MemoryPackSource(Dictionary<string, byte[]> files, IReadOnlyList<string> duplicates) : IContentPackSource
    {
        public IReadOnlyList<string> EnumerateFiles() => [.. files.Keys, .. duplicates];

        public byte[] ReadFile(string relativePath, int maximumBytes)
        {
            byte[] bytes = files[relativePath];
            if (bytes.Length > maximumBytes)
            {
                throw new ContentSourceLimitException(relativePath);
            }

            return bytes.ToArray();
        }
    }
}
