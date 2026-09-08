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
/// Encodes and decodes campaign state to/from binary save files with validation and compression.
/// </summary>
/// <remarks>
/// The codec uses a structured binary format with:
/// - 52-byte header (magic number, checksums, metadata)
/// - Preflight section: discriminator, game build, required definitions
/// - Payload section: compressed campaign state JSON
///
/// All sections are validated against size limits and integrity checksums.
/// </remarks>
public static partial class CampaignSaveCodec
{
    private const int HeaderBytes = 52;
    private const string DocumentDiscriminator = "spelljammer.campaign-save.v1";
    private static readonly byte[] Magic = "SJSAVE01"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = CampaignSaveLimits.MaximumNestingDepth,
        WriteIndented = false,
    };

    /// <summary>
    /// Encodes a campaign state into a binary save file format.
    /// </summary>
    /// <remarks>
    /// The campaign state is validated before encoding. All content definitions referenced by
    /// the campaign are identified and stored in the save file header for dependency tracking.
    /// </remarks>
    /// <param name="campaign">The campaign state to encode.</param>
    /// <param name="content">The content definitions snapshot used in this campaign.</param>
    /// <returns>Binary save file data ready to be written to disk.</returns>
    /// <exception cref="ArgumentNullException">Thrown if campaign or content is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if campaign is invalid or exceeds size limits.</exception>
    public static byte[] Encode(CampaignState campaign, GameContentSnapshot content)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(content);
        if (!CampaignValidator.TryValidate(campaign, content, out ContentId? missingId))
        {
            throw new InvalidOperationException($"Campaign state is invalid{(missingId is null ? "." : $": {missingId}.")}");
        }

        string[] required = [.. CampaignValidator.RequiredDefinitions(campaign, content).Select(value => value.ToString())];
        if (required.Length > CampaignSaveLimits.MaximumRequiredDefinitions)
        {
            throw new InvalidOperationException("Campaign definition references exceed capacity.");
        }

        SavePreflightDto preflight = new()
        {
            Discriminator = DocumentDiscriminator,
            GameBuild = campaign.GameBuild,
            ContentLock = ToDto(campaign.ContentLock),
            RequiredDefinitionIds = required,
        };
        byte[] preflightBytes = JsonSerializer.SerializeToUtf8Bytes(preflight, JsonOptions);
        byte[] payloadBytes = JsonSerializer.SerializeToUtf8Bytes(ToDto(campaign, content), JsonOptions);
        if (preflightBytes.Length > CampaignSaveLimits.MaximumPreflightBytes ||
            payloadBytes.Length > CampaignSaveLimits.MaximumPayloadBytes ||
            HeaderBytes + preflightBytes.Length + payloadBytes.Length > CampaignSaveLimits.MaximumSaveBytes)
        {
            throw new InvalidOperationException("Campaign save exceeds its bounded envelope.");
        }

        byte[] result = new byte[HeaderBytes + preflightBytes.Length + payloadBytes.Length];
        Magic.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8, 2), CampaignSaveVersions.Envelope);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10, 2), CampaignSaveVersions.SaveSchema);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)preflightBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), (uint)payloadBytes.Length);
        preflightBytes.CopyTo(result, HeaderBytes);
        payloadBytes.CopyTo(result, HeaderBytes + preflightBytes.Length);
        SHA256.HashData(result.AsSpan(HeaderBytes)).CopyTo(result, 20);
        return result;
    }


}
