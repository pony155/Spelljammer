using System.Text.Json;
using Spelljammer.Content.Diagnostics;

namespace Spelljammer.Content.Parsing;

internal static class DefaultLocaleCatalogParser
{
    public const string DefaultLocale = "en-US";

    public static bool TryReadKeys(
        byte[] bytes,
        string packId,
        string relativePath,
        ContentLimits limits,
        DiagnosticSink diagnostics,
        out IReadOnlyList<string> keys)
    {
        keys = [];
        using JsonDocument? document = StrictJson.Parse(bytes, packId, relativePath, limits, diagnostics);
        if (document is null)
        {
            return false;
        }

        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("locale", out JsonElement locale) ||
            locale.ValueKind != JsonValueKind.String ||
            locale.GetString() != DefaultLocale ||
            !root.TryGetProperty("messages", out JsonElement messages) ||
            messages.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath);
            return false;
        }

        List<string> result = [];
        foreach (JsonProperty message in messages.EnumerateObject())
        {
            if (!SourceValidation.IsLocalizationKey(message.Name, limits))
            {
                diagnostics.Add(ContentDiagnosticCodes.IdInvalid, packId, relativePath, propertyPath: "/messages");
                return false;
            }

            result.Add(message.Name);
        }

        keys = result;
        return true;
    }
}
