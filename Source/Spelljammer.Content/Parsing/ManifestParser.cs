using System.Collections.Immutable;
using System.Text.Json;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Parsing;

internal static class ManifestParser
{
    private static readonly string[] Required =
    [
        "schemaVersion", "id", "version", "displayNameKey", "gameVersionRange", "dependencies",
        "loadAfter", "definitionRoots", "localizationRoots", "contentRevision",
    ];
    private static readonly HashSet<string> Allowed = new(Required, StringComparer.Ordinal);
    private static readonly HashSet<string> DependencyAllowed = new(["id", "versionRange"], StringComparer.Ordinal);

    public static PackManifest? Parse(byte[] bytes, ContentLimits limits, DiagnosticSink diagnostics)
    {
        using JsonDocument? document = StrictJson.Parse(bytes, null, "manifest.json", limits, diagnostics);
        if (document is null)
        {
            return null;
        }

        JsonElement root = document.RootElement;
        if (!SourceValidation.ValidateProperties(root, Allowed, Required, diagnostics, null, "manifest.json") ||
            !SourceValidation.TrySchemaVersion(root, diagnostics, null, "manifest.json"))
        {
            return null;
        }

        if (!SourceValidation.TryString(root, "id", out string idText) ||
            !ContentId.TryParse(idText, out ContentId id) ||
            !SourceValidation.IsPackId(id) ||
            !SourceValidation.TryString(root, "displayNameKey", out string displayNameKey) ||
            !SourceValidation.IsLocalizationKey(displayNameKey, limits))
        {
            diagnostics.Add(ContentDiagnosticCodes.IdInvalid, relativePath: "manifest.json");
            return null;
        }

        if (!SourceValidation.TryString(root, "version", out string versionText) ||
            !SemanticVersion.TryParse(versionText, out SemanticVersion version) ||
            !SourceValidation.TryString(root, "gameVersionRange", out string rangeText) ||
            !VersionRange.TryParse(rangeText, out VersionRange gameRange))
        {
            diagnostics.Add(ContentDiagnosticCodes.VersionInvalid, idText, "manifest.json");
            return null;
        }

        if (!TryPaths(root.GetProperty("definitionRoots"), limits.DefinitionRootsPerPack,
                out ImmutableArray<string> definitionRoots, out PathParseFailure definitionPathFailure))
        {
            AddPathFailure(definitionPathFailure, "definition-roots-per-pack", "/definitionRoots", idText, diagnostics);
            return null;
        }

        if (definitionRoots.IsEmpty)
        {
            diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, idText, "manifest.json", propertyPath: "/definitionRoots");
            return null;
        }

        if (!TryPaths(root.GetProperty("localizationRoots"), limits.LocalizationRootsPerPack,
                out ImmutableArray<string> localizationRoots, out PathParseFailure localizationPathFailure))
        {
            AddPathFailure(localizationPathFailure, "localization-roots-per-pack", "/localizationRoots", idText, diagnostics);
            return null;
        }

        if (!TryDependencies(root.GetProperty("dependencies"), out ImmutableArray<PackDependency> dependencies, diagnostics, idText) ||
            !TryPackIds(root.GetProperty("loadAfter"), out ImmutableArray<ContentId> loadAfter, diagnostics, idText))
        {
            return null;
        }

        if (!SourceValidation.TryPositiveInt(root, "contentRevision", out int contentRevision))
        {
            diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, idText, "manifest.json", propertyPath: "/contentRevision");
            return null;
        }

        if (idText == "spelljammer.base" && (!dependencies.IsEmpty || !loadAfter.IsEmpty))
        {
            diagnostics.Add(ContentDiagnosticCodes.SemanticInvalid, idText, "manifest.json");
            return null;
        }

        return new PackManifest(1, id, version, displayNameKey, gameRange, dependencies, loadAfter,
            definitionRoots, localizationRoots, contentRevision);
    }

    private static bool TryPaths(
        JsonElement element,
        int maximum,
        out ImmutableArray<string> paths,
        out PathParseFailure failure)
    {
        paths = [];
        failure = PathParseFailure.None;
        if (element.ValueKind != JsonValueKind.Array)
        {
            failure = PathParseFailure.Json;
            return false;
        }

        List<string> values = [];
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (values.Count == maximum)
            {
                failure = PathParseFailure.Limit;
                return false;
            }

            if (item.ValueKind != JsonValueKind.String)
            {
                failure = PathParseFailure.Json;
                return false;
            }

            string value = item.GetString()!;
            if (!SourceValidation.IsRelativePath(value))
            {
                failure = PathParseFailure.Invalid;
                return false;
            }

            if (!unique.Add(value))
            {
                failure = PathParseFailure.Duplicate;
                return false;
            }

            values.Add(value);
        }

        paths = [.. values.Order(StringComparer.Ordinal)];
        return true;
    }

    private static void AddPathFailure(
        PathParseFailure failure,
        string limitName,
        string propertyPath,
        string packId,
        DiagnosticSink diagnostics)
    {
        switch (failure)
        {
            case PathParseFailure.Json:
                diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, "manifest.json", propertyPath: propertyPath);
                break;
            case PathParseFailure.Limit:
                diagnostics.Limit(limitName, packId, "manifest.json");
                break;
            case PathParseFailure.Duplicate:
                diagnostics.Add(ContentDiagnosticCodes.CollectionDuplicate, packId, "manifest.json", propertyPath: propertyPath);
                break;
            default:
                diagnostics.Add(ContentDiagnosticCodes.PathInvalid, packId, "manifest.json", propertyPath: propertyPath);
                break;
        }
    }

    private static bool TryDependencies(
        JsonElement element,
        out ImmutableArray<PackDependency> dependencies,
        DiagnosticSink diagnostics,
        string packId)
    {
        dependencies = [];
        if (element.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, "manifest.json", propertyPath: "/dependencies");
            return false;
        }

        List<PackDependency> values = [];
        HashSet<ContentId> unique = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !SourceValidation.ValidateProperties(item, DependencyAllowed, ["id", "versionRange"], diagnostics, packId, "manifest.json"))
            {
                return false;
            }

            if (!SourceValidation.TryString(item, "id", out string idText) || !ContentId.TryParse(idText, out ContentId id) ||
                !SourceValidation.IsPackId(id))
            {
                diagnostics.Add(ContentDiagnosticCodes.IdInvalid, packId, "manifest.json", propertyPath: "/dependencies/id");
                return false;
            }

            if (!SourceValidation.TryString(item, "versionRange", out string rangeText) || !VersionRange.TryParse(rangeText, out VersionRange range))
            {
                diagnostics.Add(ContentDiagnosticCodes.VersionInvalid, packId, "manifest.json", propertyPath: "/dependencies/versionRange");
                return false;
            }

            if (!unique.Add(id))
            {
                diagnostics.Add(ContentDiagnosticCodes.CollectionDuplicate, packId, "manifest.json", propertyPath: "/dependencies");
                return false;
            }

            values.Add(new PackDependency(id, range));
        }

        dependencies = [.. values.OrderBy(value => value.Id)];
        return true;
    }

    private static bool TryPackIds(JsonElement element, out ImmutableArray<ContentId> ids, DiagnosticSink diagnostics, string packId)
    {
        ids = [];
        if (element.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, "manifest.json", propertyPath: "/loadAfter");
            return false;
        }

        List<ContentId> values = [];
        HashSet<ContentId> unique = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !ContentId.TryParse(item.GetString(), out ContentId id) || !SourceValidation.IsPackId(id))
            {
                diagnostics.Add(ContentDiagnosticCodes.IdInvalid, packId, "manifest.json", propertyPath: "/loadAfter");
                return false;
            }

            if (!unique.Add(id))
            {
                diagnostics.Add(ContentDiagnosticCodes.CollectionDuplicate, packId, "manifest.json", propertyPath: "/loadAfter");
                return false;
            }

            values.Add(id);
        }

        ids = [.. values.Order()];
        return true;
    }

    private enum PathParseFailure : byte
    {
        None,
        Json,
        Limit,
        Duplicate,
        Invalid
    }
}
