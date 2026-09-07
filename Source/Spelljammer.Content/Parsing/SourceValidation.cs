using System.Text;
using System.Text.Json;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Parsing;

internal static class SourceValidation
{
    public static bool ValidateProperties(
        JsonElement element,
        IReadOnlySet<string> allowed,
        IReadOnlyList<string> required,
        DiagnosticSink diagnostics,
        string? packId,
        string path)
    {
        foreach (string property in required)
        {
            if (!element.TryGetProperty(property, out _))
            {
                diagnostics.Add(ContentDiagnosticCodes.RequiredPropertyMissing, packId, path, propertyPath: "/" + property);
                return false;
            }
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                diagnostics.Add(ContentDiagnosticCodes.UnknownProperty, packId, path, propertyPath: "/" + property.Name);
                return false;
            }
        }

        return true;
    }

    public static bool TrySchemaVersion(JsonElement element, DiagnosticSink diagnostics, string? packId, string path)
    {
        JsonElement value = element.GetProperty("schemaVersion");
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int schemaVersion))
        {
            diagnostics.Add(ContentDiagnosticCodes.ValueOutOfRange, packId, path, propertyPath: "/schemaVersion");
            return false;
        }

        if (schemaVersion != 1)
        {
            diagnostics.Add(ContentDiagnosticCodes.SchemaUnsupported, packId, path, propertyPath: "/schemaVersion");
            return false;
        }

        return true;
    }

    public static bool TryString(JsonElement element, string property, out string value)
    {
        JsonElement item = element.GetProperty(property);
        if (item.ValueKind == JsonValueKind.String)
        {
            value = item.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public static bool TryPositiveInt(JsonElement element, string property, out int value)
    {
        JsonElement item = element.GetProperty(property);
        value = 0;
        return item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out value) && value > 0;
    }

    public static bool IsLocalizationKey(string? value, ContentLimits limits)
    {
        if (value is null || Encoding.UTF8.GetByteCount(value) > limits.LocalizationKeyBytes)
        {
            return false;
        }

        string[] segments = value.Split('.');
        return segments.Length >= 2 && segments.All(segment =>
            segment.Length != 0 &&
            segment[0] is >= 'a' and <= 'z' &&
            segment[^1] != '-' &&
            segment.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' || character == '-'));
    }

    public static bool IsRelativePath(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Contains('\\', StringComparison.Ordinal) || value.Contains('\0') ||
            value.StartsWith('/') || value.Contains(":", StringComparison.Ordinal))
        {
            return false;
        }

        string[] components = value.Split('/');
        return components.All(component => component.Length != 0 && component is not "." and not "..");
    }

    public static bool IsPackId(ContentId id)
    {
        string value = id.ToString();
        if (value == "spelljammer.base")
        {
            return true;
        }

        string[] segments = value.Split('.');
        return segments.Length == 2 && segments[0] == "mod";
    }

    public static bool IsIdSegment(string value) =>
        value.Length is > 0 and <= ContentId.MaximumLength &&
        value[0] is >= 'a' and <= 'z' &&
        value[^1] != '-' &&
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' || character == '-') &&
        !value.Contains("--", StringComparison.Ordinal);
}
