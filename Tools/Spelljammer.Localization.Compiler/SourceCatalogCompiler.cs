using System.Text;
using System.Text.Json;
using Spelljammer.Localization;

namespace Spelljammer.Tools.Localization;

public enum PseudoLocaleKind
{
    None,
    AccentedExpanded,
    KeyEcho
}

public sealed record CatalogCompilationResult(
    byte[] Artifact,
    string Locale,
    string Namespace,
    int MessageCount,
    string Fingerprint);

public sealed record CatalogCompletenessReport(
    string SourceLocale,
    string TranslationLocale,
    IReadOnlyList<string> MissingKeys,
    IReadOnlyList<string> ObsoleteKeys,
    IReadOnlyList<string> SchemaErrors)
{
    public bool IsComplete => MissingKeys.Count == 0 && SchemaErrors.Count == 0;
}

public static class SourceCatalogCompiler
{
    private static readonly HashSet<string> RootProperties =
    [
        "schemaVersion",
        "locale",
        "namespace",
        "fallbacks",
        "textDirection",
        "messages"
    ];

    private static readonly HashSet<string> MessageProperties =
    [
        "description",
        "message",
        "emptyAllowed",
        "arguments"
    ];

    private static readonly HashSet<string> ArgumentProperties =
    [
        "type",
        "scale",
        "sensitive",
        "values"
    ];

    public static LocalizationStatus Compile(
        ReadOnlyMemory<byte> sourceUtf8,
        PseudoLocaleKind pseudoLocale,
        out CatalogCompilationResult? result,
        out string error)
    {
        result = null;
        if (!Enum.IsDefined(pseudoLocale))
        {
            error = "Pseudo-locale mode is invalid.";
            return LocalizationStatus.InvalidArgument;
        }

        if (!TryParse(sourceUtf8, pseudoLocale, out LocalizationCatalogDefinition? definition, out error) || definition is null)
        {
            return LocalizationStatus.InvalidArgument;
        }

        LocalizationStatus status = LocalizationArtifact.Encode(definition, out byte[] artifact, out error);
        if (status != LocalizationStatus.Success)
        {
            return status;
        }

        status = LocalizationArtifact.Decode(artifact, out LocalizationCatalog? catalog, out error);
        if (status != LocalizationStatus.Success || catalog is null)
        {
            return status;
        }

        result = new CatalogCompilationResult(
            artifact,
            catalog.Locale.Tag,
            catalog.Namespace,
            catalog.Entries.Count,
            catalog.Fingerprint);
        return LocalizationStatus.Success;
    }

    public static LocalizationStatus CreateCompletenessReport(
        ReadOnlyMemory<byte> sourceUtf8,
        ReadOnlyMemory<byte> translationUtf8,
        out CatalogCompletenessReport? report,
        out string error)
    {
        report = null;
        if (!TryParse(sourceUtf8, PseudoLocaleKind.None, out LocalizationCatalogDefinition? source, out error) || source is null ||
            !TryParse(translationUtf8, PseudoLocaleKind.None, out LocalizationCatalogDefinition? translation, out error) || translation is null)
        {
            return LocalizationStatus.InvalidArgument;
        }

        if (source.Namespace != translation.Namespace)
        {
            error = "Source and translation namespaces do not match.";
            return LocalizationStatus.InvalidArgument;
        }

        string[] sourceKeys = source.Entries.Select(entry => entry.Key).Order(StringComparer.Ordinal).ToArray();
        string[] translationKeys = translation.Entries.Select(entry => entry.Key).Order(StringComparer.Ordinal).ToArray();
        Dictionary<string, LocalizationCatalogEntryDefinition> translations = translation.Entries
            .ToDictionary(entry => entry.Key, StringComparer.Ordinal);
        List<string> schemaErrors = [];
        foreach (LocalizationCatalogEntryDefinition sourceEntry in source.Entries)
        {
            if (translations.TryGetValue(sourceEntry.Key, out LocalizationCatalogEntryDefinition? translatedEntry) &&
                (!SchemasEqual(sourceEntry.Arguments ?? [], translatedEntry.Arguments ?? []) ||
                 MessageProgramCodec.GetStructureSignature(sourceEntry.Program!) !=
                 MessageProgramCodec.GetStructureSignature(translatedEntry.Program!)))
            {
                schemaErrors.Add($"{sourceEntry.Key}: argument schema or selection structure differs from source.");
            }
        }

        report = new CatalogCompletenessReport(
            source.Locale,
            translation.Locale,
            sourceKeys.Except(translationKeys, StringComparer.Ordinal).ToArray(),
            translationKeys.Except(sourceKeys, StringComparer.Ordinal).ToArray(),
            schemaErrors);
        error = string.Empty;
        return LocalizationStatus.Success;
    }

    private static bool TryParse(
        ReadOnlyMemory<byte> sourceUtf8,
        PseudoLocaleKind pseudoLocale,
        out LocalizationCatalogDefinition? definition,
        out string error)
    {
        definition = null;
        if (sourceUtf8.Length == 0 || sourceUtf8.Length > LocalizationLimits.MaximumArtifactBytes)
        {
            error = "Source catalog is empty or exceeds the supported size.";
            return false;
        }

        try
        {
            RejectDuplicateProperties(sourceUtf8.Span);
            using JsonDocument document = JsonDocument.Parse(sourceUtf8, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new CatalogSourceException("Catalog root must be an object.");
            }

            ValidateProperties(root, RootProperties, "catalog root");
            int schemaVersion = RequireInt32(root, "schemaVersion");
            if (schemaVersion != 1)
            {
                throw new CatalogSourceException($"Source schema {schemaVersion} is unsupported.");
            }

            string locale = RequireString(root, "locale", LocalizationLimits.MaximumLocaleTagBytes);
            string catalogNamespace = RequireString(root, "namespace", LocalizationLimits.MaximumNamespaceBytes);
            string directionName = RequireString(root, "textDirection", 3);
            TextDirection direction = directionName switch
            {
                "ltr" => TextDirection.LeftToRight,
                "rtl" => TextDirection.RightToLeft,
                _ => throw new CatalogSourceException("textDirection must be 'ltr' or 'rtl'.")
            };

            JsonElement fallbackElement = RequireProperty(root, "fallbacks");
            if (fallbackElement.ValueKind != JsonValueKind.Array ||
                fallbackElement.GetArrayLength() > LocalizationLimits.MaximumFallbackDepth)
            {
                throw new CatalogSourceException("fallbacks must be a bounded array.");
            }

            List<string> fallbacks = [];
            foreach (JsonElement fallback in fallbackElement.EnumerateArray())
            {
                if (fallback.ValueKind != JsonValueKind.String)
                {
                    throw new CatalogSourceException("Every fallback must be a locale-tag string.");
                }

                string value = fallback.GetString()!;
                if (Encoding.UTF8.GetByteCount(value) > LocalizationLimits.MaximumLocaleTagBytes)
                {
                    throw new CatalogSourceException("Fallback locale tag is too long.");
                }

                fallbacks.Add(value);
            }

            JsonElement messagesElement = RequireProperty(root, "messages");
            if (messagesElement.ValueKind != JsonValueKind.Object)
            {
                throw new CatalogSourceException("messages must be an object keyed by canonical message name.");
            }

            List<LocalizationCatalogEntryDefinition> entries = [];
            foreach (JsonProperty messageProperty in messagesElement.EnumerateObject())
            {
                if (entries.Count >= LocalizationLimits.MaximumKeysPerLocale ||
                    messageProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new CatalogSourceException("Message count is too large or an entry is not an object.");
                }

                ValidateProperties(messageProperty.Value, MessageProperties, $"message '{messageProperty.Name}'");
                string message = RequireString(
                    messageProperty.Value,
                    "message",
                    LocalizationLimits.MaximumMessageBytes);
                bool emptyAllowed = OptionalBoolean(messageProperty.Value, "emptyAllowed");
                IReadOnlyList<LocalizationArgumentSchema> arguments = ParseArguments(messageProperty.Value, messageProperty.Name);
                if (messageProperty.Value.TryGetProperty("description", out JsonElement description) &&
                    (description.ValueKind != JsonValueKind.String ||
                     Encoding.UTF8.GetByteCount(description.GetString()!) > 4096))
                {
                    throw new CatalogSourceException($"Description for '{messageProperty.Name}' must be a string of at most 4096 bytes.");
                }

                LocalizationStatus messageStatus = MessageParser.Compile(message, arguments, null,
                    out CompiledMessageProgram? program, out string messageError);
                if (messageStatus != LocalizationStatus.Success || program is null)
                {
                    throw new CatalogSourceException($"Message '{messageProperty.Name}': {messageError}");
                }

                if (pseudoLocale == PseudoLocaleKind.AccentedExpanded)
                {
                    messageStatus = MessageParser.Compile(message, arguments, PseudoLocalizeLiteral,
                        out program, out messageError);
                    if (messageStatus != LocalizationStatus.Success || program is null)
                    {
                        throw new CatalogSourceException($"Message '{messageProperty.Name}': {messageError}");
                    }
                }
                else if (pseudoLocale == PseudoLocaleKind.KeyEcho)
                {
                    messageStatus = MessageProgramCodec.CreateLiteral($"⟦{messageProperty.Name}⟧", out program, out messageError);
                    if (messageStatus != LocalizationStatus.Success || program is null)
                    {
                        throw new CatalogSourceException($"Message '{messageProperty.Name}': {messageError}");
                    }
                }

                entries.Add(new LocalizationCatalogEntryDefinition(
                    messageProperty.Name,
                    message,
                    emptyAllowed,
                    arguments,
                    program));
            }

            string outputLocale = pseudoLocale switch
            {
                PseudoLocaleKind.AccentedExpanded => "qps-ploc",
                PseudoLocaleKind.KeyEcho => "qps-keyecho",
                _ => locale
            };
            IReadOnlyList<string> outputFallbacks = pseudoLocale == PseudoLocaleKind.None
                ? fallbacks
                : [locale, .. fallbacks];
            definition = new LocalizationCatalogDefinition(
                outputLocale,
                catalogNamespace,
                direction,
                outputFallbacks,
                entries);
            error = string.Empty;
            return true;
        }
        catch (JsonException exception)
        {
            error = $"Malformed catalog JSON at byte {exception.BytePositionInLine}: {exception.Message}";
            return false;
        }
        catch (CatalogSourceException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string PseudoLocalizeLiteral(string message)
    {
        StringBuilder transformed = new(message.Length * 2);
        transformed.Append('⟦');
        foreach (char character in message)
        {
            transformed.Append(character switch
            {
                'a' => 'á',
                'A' => 'Á',
                'e' => 'ë',
                'E' => 'Ë',
                'i' => 'ï',
                'I' => 'Ï',
                'o' => 'ô',
                'O' => 'Ô',
                'u' => 'ü',
                'U' => 'Ü',
                'c' => 'ç',
                'C' => 'Ç',
                'n' => 'ñ',
                'N' => 'Ñ',
                _ => character
            });
        }

        int padding = Math.Max(2, message.Length / 3);
        transformed.Append('~', padding);
        transformed.Append('⟧');
        return transformed.ToString();
    }

    private static IReadOnlyList<LocalizationArgumentSchema> ParseArguments(
        JsonElement message,
        string key)
    {
        if (!message.TryGetProperty("arguments", out JsonElement argumentElement))
        {
            return [];
        }

        if (argumentElement.ValueKind != JsonValueKind.Object ||
            argumentElement.GetPropertyCount() > LocalizationLimits.MaximumArgumentsPerMessage)
        {
            throw new CatalogSourceException($"Arguments for '{key}' must be a bounded object.");
        }

        List<LocalizationArgumentSchema> arguments = [];
        foreach (JsonProperty property in argumentElement.EnumerateObject())
        {
            if (!LocalizationArgumentName.IsCanonical(property.Name))
            {
                throw new CatalogSourceException($"Argument name '{property.Name}' in '{key}' is not canonical.");
            }

            string typeName;
            int scale = 0;
            bool sensitive = false;
            List<string> selectValues = [];
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                typeName = property.Value.GetString()!;
            }
            else if (property.Value.ValueKind == JsonValueKind.Object)
            {
                ValidateProperties(property.Value, ArgumentProperties, $"argument '{property.Name}' in '{key}'");
                typeName = RequireString(property.Value, "type", 16);
                scale = OptionalInt32(property.Value, "scale");
                sensitive = OptionalBoolean(property.Value, "sensitive");
                if (property.Value.TryGetProperty("values", out JsonElement valuesElement))
                {
                    if (valuesElement.ValueKind != JsonValueKind.Array ||
                        valuesElement.GetArrayLength() > LocalizationLimits.MaximumBranchesPerSelection)
                    {
                        throw new CatalogSourceException($"Select values for '{property.Name}' must be a bounded array.");
                    }

                    foreach (JsonElement valueElement in valuesElement.EnumerateArray())
                    {
                        if (valueElement.ValueKind != JsonValueKind.String ||
                            !LocalizationArgumentName.IsCanonicalToken(valueElement.GetString()))
                        {
                            throw new CatalogSourceException($"Select value for '{property.Name}' is not canonical.");
                        }

                        selectValues.Add(valueElement.GetString()!);
                    }
                }
            }
            else
            {
                throw new CatalogSourceException($"Argument '{property.Name}' must be a type string or schema object.");
            }

            LocalizationValueKind kind = typeName switch
            {
                "integer" => LocalizationValueKind.Integer,
                "unsigned" => LocalizationValueKind.Unsigned,
                "fixed" => LocalizationValueKind.Fixed,
                "percent" => LocalizationValueKind.Percent,
                "text" => LocalizationValueKind.Text,
                "select" => LocalizationValueKind.Select,
                "boolean" => LocalizationValueKind.Boolean,
                "localizable" => LocalizationValueKind.Localizable,
                _ => throw new CatalogSourceException($"Argument '{property.Name}' has unsupported type '{typeName}'.")
            };
            bool scaled = kind is LocalizationValueKind.Fixed or LocalizationValueKind.Percent;
            if (scale is < 0 or > 9 || (!scaled && scale != 0) || (sensitive && kind != LocalizationValueKind.Text))
            {
                throw new CatalogSourceException($"Argument '{property.Name}' has invalid scale or sensitivity metadata.");
            }

            if (kind == LocalizationValueKind.Select)
            {
                if (selectValues.Count == 0)
                {
                    throw new CatalogSourceException($"Select argument '{property.Name}' requires an allow-list.");
                }

                if (selectValues.Distinct(StringComparer.Ordinal).Count() != selectValues.Count)
                {
                    throw new CatalogSourceException($"Select argument '{property.Name}' contains duplicate values.");
                }

                selectValues = selectValues.Order(StringComparer.Ordinal).ToList();
            }
            else if (selectValues.Count != 0)
            {
                throw new CatalogSourceException($"Only select argument '{property.Name}' may declare values.");
            }

            arguments.Add(new LocalizationArgumentSchema(
                property.Name,
                LocalizationArgumentName.ComputeId(property.Name),
                kind,
                scale,
                sensitive,
                selectValues));
        }

        return arguments.OrderBy(argument => argument.NameId)
            .ThenBy(argument => argument.Name, StringComparer.Ordinal).ToArray();
    }

    private static bool SchemasEqual(
        IReadOnlyList<LocalizationArgumentSchema> left,
        IReadOnlyList<LocalizationArgumentSchema> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; ++index)
        {
            LocalizationArgumentSchema first = left[index];
            LocalizationArgumentSchema second = right[index];
            if (first.Name != second.Name || first.NameId != second.NameId || first.Kind != second.Kind ||
                first.DecimalScale != second.DecimalScale || first.Sensitive != second.Sensitive ||
                !first.SelectValues.SequenceEqual(second.SelectValues, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> sourceUtf8)
    {
        Utf8JsonReader reader = new(sourceUtf8, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16
        });
        Stack<HashSet<string>> objects = [];
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                objects.Push(new HashSet<string>(StringComparer.Ordinal));
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                objects.Pop();
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                if (!objects.Peek().Add(propertyName))
                {
                    throw new CatalogSourceException($"Duplicate JSON property '{propertyName}'.");
                }
            }
        }
    }

    private static void ValidateProperties(JsonElement element, HashSet<string> allowed, string context)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new CatalogSourceException($"Unknown property '{property.Name}' in {context}.");
            }
        }
    }

    private static JsonElement RequireProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            throw new CatalogSourceException($"Required property '{name}' is missing.");
        }

        return value;
    }

    private static string RequireString(JsonElement element, string name, int maximumBytes)
    {
        JsonElement value = RequireProperty(element, name);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new CatalogSourceException($"Property '{name}' must be a string.");
        }

        string text = value.GetString()!;
        if (Encoding.UTF8.GetByteCount(text) > maximumBytes)
        {
            throw new CatalogSourceException($"Property '{name}' exceeds {maximumBytes} UTF-8 bytes.");
        }

        return text;
    }

    private static int RequireInt32(JsonElement element, string name)
    {
        JsonElement value = RequireProperty(element, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
        {
            throw new CatalogSourceException($"Property '{name}' must be a 32-bit integer.");
        }

        return number;
    }

    private static int OptionalInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return 0;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
        {
            throw new CatalogSourceException($"Property '{name}' must be a 32-bit integer.");
        }

        return number;
    }

    private static bool OptionalBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new CatalogSourceException($"Property '{name}' must be Boolean.")
        };
    }

    private sealed class CatalogSourceException : Exception
    {
        public CatalogSourceException(string message)
            : base(message)
        {
        }
    }
}
