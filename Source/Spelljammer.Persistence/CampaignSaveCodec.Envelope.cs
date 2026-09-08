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
using Spelljammer.Simulation.World;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Persistence;

/// <summary>
/// Reads, writes, checksums, compresses, and preflights the campaign save envelope.
/// </summary>
/// <remarks>
/// Code flow: Writes serialize metadata and payload into bounded sections with checksums; reads validate the header and integrity before decompression, compatibility preflight, and domain mapping.
/// </remarks>
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
            !IsSupportedSaveSchema(contentLock.SaveSchemaVersion) ||
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
        ImmutableArray<ContentId> schemaMigrationPath =
            contentLock.SaveSchemaVersion == CampaignSaveVersions.SaveSchema
                ? []
                : [CampaignSaveSchemaMigrations.BattleUnitsMigrationId];
        bool packsExact = contentLock.Packs.SequenceEqual(available.Packs);
        if (packsExact && contentLock.ManifestFingerprint == available.ManifestFingerprint &&
            contentLock.SemanticFingerprint == content.Fingerprint && contentLock.EffectiveFingerprint == content.Fingerprint)
        {
            bool currentSchema = contentLock.SaveSchemaVersion == CampaignSaveVersions.SaveSchema;
            return new ContentPreflightResult(
                currentSchema ? ContentPreflightKind.Exact : ContentPreflightKind.Compatible,
                SaveDiagnosticCode.None,
                contentLock,
                [],
                [],
                schemaMigrationPath);
        }

        if (compatibility?.Any(rule => rule.SourceFingerprint == contentLock.EffectiveFingerprint &&
                rule.DestinationFingerprint == content.Fingerprint) == true)
        {
            return new ContentPreflightResult(
                ContentPreflightKind.Compatible,
                SaveDiagnosticCode.None,
                contentLock,
                [],
                [],
                schemaMigrationPath);
        }

        ImmutableArray<ContentId> path = migrations?.FindPath(contentLock.EffectiveFingerprint, content.Fingerprint) ?? [];
        if (!path.IsEmpty)
        {
            return new ContentPreflightResult(
                ContentPreflightKind.Migratable,
                SaveDiagnosticCode.None,
                contentLock,
                [],
                [],
                schemaMigrationPath.AddRange(path));
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
            CampaignContentLock savedLock = preflight.ContentLock!;
            ReadOnlyMemory<byte> migratedPayload = CampaignSaveSchemaMigrations.Migrate(
                payloadBytes,
                savedLock.SaveSchemaVersion,
                JsonOptions);
            CampaignPayloadDto payload = JsonSerializer.Deserialize<CampaignPayloadDto>(migratedPayload.Span, JsonOptions) ??
                throw new InvalidOperationException("Campaign payload is empty.");
            bool addCompatibleDefinitions = preflight.Kind == ContentPreflightKind.Compatible &&
                savedLock.EffectiveFingerprint != content.Fingerprint;
            CampaignState campaign = FromDto(
                metadata!, savedLock, payload, content, addCompatibleDefinitions);
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
        if (envelope != CampaignSaveVersions.Envelope || !IsSupportedSaveSchema(schema))
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

            if (metadata.ContentLock.SaveSchemaVersion != schema)
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

    private static bool IsSupportedSaveSchema(ushort schema) =>
        schema >= CampaignSaveVersions.MinimumMigratableSaveSchema &&
        schema <= CampaignSaveVersions.SaveSchema;
}
