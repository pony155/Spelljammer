using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Persistence;

public static partial class CampaignSaveCodec
{
    public static SaveDiagnosticCode ValidateEnvelope(ReadOnlyMemory<byte> bytes) =>
        TryReadEnvelope(bytes, out _, out _, out SaveDiagnosticCode diagnostic) ? SaveDiagnosticCode.None : diagnostic;

    public static ContentPreflightResult Preflight(
        ReadOnlyMemory<byte> bytes,
        GameContentSnapshot content,
        IEnumerable<ContentCompatibilityRule>? compatibility = null,
        CampaignMigrationRegistry? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!TryReadEnvelope(bytes, out SavePreflightDto? metadata, out _, out SaveDiagnosticCode diagnostic))
        {
            return FailedPreflight(diagnostic);
        }

        CampaignContentLock contentLock;
        ImmutableArray<ContentId> required;
        try
        {
            contentLock = FromDto(metadata!.ContentLock);
            required = ParseIds(metadata.RequiredDefinitionIds, CampaignSaveLimits.MaximumRequiredDefinitions);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return FailedPreflight(SaveDiagnosticCode.Corrupt);
        }

        if (metadata.Discriminator != DocumentDiscriminator ||
            contentLock.SaveSchemaVersion != CampaignSaveVersions.SaveSchema ||
            contentLock.GeneratorVersion != CampaignSaveVersions.WorldGenerator ||
            contentLock.FormulaVersion != CampaignSaveVersions.Formula ||
            contentLock.EffectVersion != CampaignSaveVersions.Effect)
        {
            return new ContentPreflightResult(ContentPreflightKind.Incompatible, SaveDiagnosticCode.Unsupported,
                contentLock, [], [], []);
        }

        ImmutableHashSet<ContentId> availablePacks = content.Packs.Select(value => value.Id).ToImmutableHashSet();
        ImmutableArray<ContentId> missingPacks = [.. contentLock.Packs.Select(value => value.Id)
            .Where(id => !availablePacks.Contains(id)).Distinct().Order()];
        ImmutableArray<ContentId> missingDefinitions = [.. required.Where(id => !content.TryGetDefinition(id, out _)).Distinct().Order()];
        if (!missingPacks.IsEmpty || !missingDefinitions.IsEmpty)
        {
            return new ContentPreflightResult(ContentPreflightKind.Missing, SaveDiagnosticCode.MissingContent,
                contentLock, missingPacks, missingDefinitions, []);
        }

        CampaignContentLock available = CampaignContentLock.Create(content);
        bool packsExact = contentLock.Packs.SequenceEqual(available.Packs);
        if (packsExact && contentLock.ManifestFingerprint == available.ManifestFingerprint &&
            contentLock.SemanticFingerprint == content.Fingerprint && contentLock.EffectiveFingerprint == content.Fingerprint)
        {
            return new ContentPreflightResult(ContentPreflightKind.Exact, SaveDiagnosticCode.None, contentLock, [], [], []);
        }

        if (compatibility?.Any(rule => rule.SourceFingerprint == contentLock.EffectiveFingerprint &&
                rule.DestinationFingerprint == content.Fingerprint) == true)
        {
            return new ContentPreflightResult(ContentPreflightKind.Compatible, SaveDiagnosticCode.None, contentLock, [], [], []);
        }

        ImmutableArray<ContentId> path = migrations?.FindPath(contentLock.EffectiveFingerprint, content.Fingerprint) ?? [];
        if (!path.IsEmpty)
        {
            return new ContentPreflightResult(ContentPreflightKind.Migratable, SaveDiagnosticCode.None, contentLock, [], [], path);
        }

        return new ContentPreflightResult(ContentPreflightKind.Incompatible, SaveDiagnosticCode.IncompatibleContent,
            contentLock, [], [], []);
    }

    public static CampaignReadResult Decode(
        ReadOnlyMemory<byte> bytes,
        GameContentSnapshot content,
        IEnumerable<ContentCompatibilityRule>? compatibility = null,
        CampaignMigrationRegistry? migrations = null)
    {
        ContentPreflightResult preflight = Preflight(bytes, content, compatibility, migrations);
        if (!preflight.CanLoad)
        {
            return new CampaignReadResult(null, preflight, preflight.Diagnostic);
        }

        if (!TryReadEnvelope(bytes, out SavePreflightDto? metadata, out ReadOnlyMemory<byte> payloadBytes, out SaveDiagnosticCode diagnostic))
        {
            return new CampaignReadResult(null, preflight, diagnostic);
        }

        try
        {
            CampaignPayloadDto payload = JsonSerializer.Deserialize<CampaignPayloadDto>(payloadBytes.Span, JsonOptions) ??
                throw new InvalidOperationException("Campaign payload is empty.");
            CampaignState campaign = FromDto(
                metadata!, preflight.ContentLock!, payload, content, preflight.Kind == ContentPreflightKind.Compatible);
            if (!CampaignValidator.TryValidate(campaign, content, out _))
            {
                throw new InvalidOperationException("Campaign invariants failed.");
            }

            return new CampaignReadResult(campaign, preflight, SaveDiagnosticCode.None);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return new CampaignReadResult(null, preflight, SaveDiagnosticCode.InvalidState);
        }
    }

    private static bool TryReadEnvelope(
        ReadOnlyMemory<byte> bytes,
        out SavePreflightDto? metadata,
        out ReadOnlyMemory<byte> payload,
        out SaveDiagnosticCode diagnostic)
    {
        metadata = null;
        payload = default;
        diagnostic = SaveDiagnosticCode.None;
        if (bytes.Length > CampaignSaveLimits.MaximumSaveBytes)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Oversized);
        }

        if (bytes.Length < HeaderBytes)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Truncated);
        }

        ReadOnlySpan<byte> span = bytes.Span;
        if (!span[..8].SequenceEqual(Magic))
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Corrupt);
        }

        ushort envelope = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(8, 2));
        ushort schema = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(10, 2));
        if (envelope != CampaignSaveVersions.Envelope || schema != CampaignSaveVersions.SaveSchema)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Unsupported);
        }

        int preflightLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12, 4)));
        int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(16, 4)));
        if (preflightLength < 2 || payloadLength < 2 || preflightLength > CampaignSaveLimits.MaximumPreflightBytes ||
            payloadLength > CampaignSaveLimits.MaximumPayloadBytes)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Oversized);
        }

        long expected = (long)HeaderBytes + preflightLength + payloadLength;
        if (expected != bytes.Length)
        {
            return Fail(out diagnostic, expected > bytes.Length ? SaveDiagnosticCode.Truncated : SaveDiagnosticCode.Corrupt);
        }

        ReadOnlyMemory<byte> preflightBytes = bytes.Slice(HeaderBytes, preflightLength);
        payload = bytes.Slice(HeaderBytes + preflightLength, payloadLength);
        if (!SHA256.HashData(span[HeaderBytes..]).AsSpan().SequenceEqual(span.Slice(20, 32)))
        {
            return Fail(out diagnostic, SaveDiagnosticCode.ChecksumMismatch);
        }

        try
        {
            ValidateJsonShape(preflightBytes.Span);
            ValidateJsonShape(payload.Span);
            metadata = JsonSerializer.Deserialize<SavePreflightDto>(preflightBytes.Span, JsonOptions);
            if (metadata is null || Encoding.UTF8.GetByteCount(metadata.GameBuild) is 0 or > CampaignState.MaximumGameBuildBytes)
            {
                return Fail(out diagnostic, SaveDiagnosticCode.Corrupt);
            }
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            return Fail(out diagnostic, SaveDiagnosticCode.Corrupt);
        }

        return true;

    }

    private static bool Fail(out SaveDiagnosticCode diagnostic, SaveDiagnosticCode value)
    {
        diagnostic = value;
        return false;
    }

    private static void ValidateJsonShape(ReadOnlySpan<byte> json)
    {
        Utf8JsonReader reader = new(json, new JsonReaderOptions {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = CampaignSaveLimits.MaximumNestingDepth,
        });
        Stack<HashSet<string>> objectProperties = new();
        int values = 0;
        while (reader.Read())
        {
            if (++values > CampaignSaveLimits.MaximumCollectionEntries * 64)
            {
                throw new InvalidOperationException("JSON token capacity exceeded.");
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
            }
            else if (reader.TokenType == JsonTokenType.EndObject)
            {
                objectProperties.Pop();
            }
            else if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string name = reader.GetString() ?? throw new JsonException();
                if (objectProperties.Count == 0 || !objectProperties.Peek().Add(name))
                {
                    throw new JsonException("Duplicate property.");
                }
            }
            else if (reader.TokenType == JsonTokenType.String && reader.HasValueSequence && reader.ValueSequence.Length > CampaignSaveLimits.MaximumStringBytes)
            {
                throw new InvalidOperationException("String capacity exceeded.");
            }
            else if (reader.TokenType == JsonTokenType.String && !reader.HasValueSequence && reader.ValueSpan.Length > CampaignSaveLimits.MaximumStringBytes)
            {
                throw new InvalidOperationException("String capacity exceeded.");
            }
        }
    }
}
