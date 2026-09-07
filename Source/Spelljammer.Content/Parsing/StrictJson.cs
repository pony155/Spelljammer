using System.Text;
using System.Text.Json;
using Spelljammer.Content.Diagnostics;

namespace Spelljammer.Content.Parsing;

internal static class StrictJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static JsonDocument? Parse(
        byte[] bytes,
        string? packId,
        string relativePath,
        ContentLimits limits,
        DiagnosticSink diagnostics)
    {
        if (bytes.AsSpan().StartsWith("\ufeff"u8))
        {
            diagnostics.Add(ContentDiagnosticCodes.InvalidUtf8, packId, relativePath);
            return null;
        }

        try
        {
            _ = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            diagnostics.Add(ContentDiagnosticCodes.InvalidUtf8, packId, relativePath);
            return null;
        }

        string? duplicateProperty = null;
        string? exceededLimit = null;
        try
        {
            Utf8JsonReader reader = new(bytes, new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = limits.JsonNestingDepth + 1,
            });
            Stack<ContainerFrame> frames = new();
            int tokenCount = 0;
            while (reader.Read())
            {
                if (++tokenCount > limits.JsonTokensPerFile)
                {
                    exceededLimit = "json-tokens-per-file";
                    break;
                }

                if (reader.CurrentDepth > limits.JsonNestingDepth)
                {
                    exceededLimit = "json-nesting-depth";
                    break;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        CountArrayValue(frames, limits, ref exceededLimit);
                        frames.Push(ContainerFrame.Object());
                        break;
                    case JsonTokenType.StartArray:
                        CountArrayValue(frames, limits, ref exceededLimit);
                        frames.Push(ContainerFrame.Array());
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        frames.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                        {
                            ContainerFrame frame = frames.Peek();
                            if (++frame.Count > limits.PropertiesPerObject)
                            {
                                exceededLimit = "properties-per-object";
                            }

                            string name = reader.GetString()!;
                            if (!frame.Properties!.Add(name))
                            {
                                duplicateProperty = name;
                            }

                            if (Encoding.UTF8.GetByteCount(name) > limits.GenericSourceStringBytes)
                            {
                                exceededLimit = "generic-source-string-bytes";
                            }

                            break;
                        }
                    case JsonTokenType.String:
                        CountArrayValue(frames, limits, ref exceededLimit);
                        if (Encoding.UTF8.GetByteCount(reader.GetString()!) > limits.GenericSourceStringBytes)
                        {
                            exceededLimit = "generic-source-string-bytes";
                        }

                        break;
                    default:
                        CountArrayValue(frames, limits, ref exceededLimit);
                        break;
                }

                if (duplicateProperty is not null || exceededLimit is not null)
                {
                    break;
                }
            }
        }
        catch (JsonException)
        {
            diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath);
            return null;
        }

        if (duplicateProperty is not null)
        {
            diagnostics.Add(
                ContentDiagnosticCodes.JsonDuplicateProperty,
                packId,
                relativePath,
                propertyPath: "/" + duplicateProperty);
            return null;
        }

        if (exceededLimit is not null)
        {
            diagnostics.Limit(exceededLimit, packId, relativePath);
            return null;
        }

        try
        {
            JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = limits.JsonNestingDepth,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath);
                return null;
            }

            return document;
        }
        catch (JsonException)
        {
            diagnostics.Add(ContentDiagnosticCodes.JsonInvalid, packId, relativePath);
            return null;
        }
    }

    private static void CountArrayValue(Stack<ContainerFrame> frames, ContentLimits limits, ref string? exceededLimit)
    {
        if (frames.Count == 0 || !frames.Peek().IsArray)
        {
            return;
        }

        ContainerFrame frame = frames.Peek();
        if (++frame.Count > limits.EntriesPerArray)
        {
            exceededLimit = "entries-per-array";
        }
    }

    private sealed class ContainerFrame
    {
        private ContainerFrame(bool isArray)
        {
            IsArray = isArray;
            Properties = isArray ? null : new HashSet<string>(StringComparer.Ordinal);
        }

        public bool IsArray { get; }
        public HashSet<string>? Properties { get; }
        public int Count { get; set; }
        public static ContainerFrame Object() => new(false);
        public static ContainerFrame Array() => new(true);
    }
}
