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
    private static ContentPreflightResult FailedPreflight(SaveDiagnosticCode diagnostic) => new(
        ContentPreflightKind.Incompatible, diagnostic, null, [], [], []);

    private static ImmutableArray<ContentId> ParseIds(IEnumerable<string> values, int maximum)
    {
        string[] items = [.. values];
        if (items.Length > maximum || items.Distinct(StringComparer.Ordinal).Count() != items.Length)
        {
            throw new InvalidOperationException("ID collection exceeds capacity or contains duplicates.");
        }

        return [.. items.Select(value => new ContentId(value))];
    }

    private static ContentLockDto ToDto(CampaignContentLock value) => new()
    {
        BaseContentRevision = value.BaseContentRevision,
        Packs = [.. value.Packs.Select(pack => new PackLockDto
        {
            Id = pack.Id.ToString(),
            Version = pack.Version.ToString(),
            ContentRevision = pack.ContentRevision,
        })],
        ManifestFingerprint = value.ManifestFingerprint.ToString(),
        SemanticFingerprint = value.SemanticFingerprint.ToString(),
        EffectiveFingerprint = value.EffectiveFingerprint.ToString(),
        GeneratorVersion = value.GeneratorVersion,
        FormulaVersion = value.FormulaVersion,
        EffectVersion = value.EffectVersion,
        SaveSchemaVersion = value.SaveSchemaVersion,
        AppliedMigrationIds = [.. value.AppliedMigrationIds.Order().Select(id => id.ToString())],
    };

    private static CampaignContentLock FromDto(ContentLockDto value)
    {
        if (value.Packs.Length is 0 or > CampaignSaveLimits.MaximumCollectionEntries ||
            value.Packs.Select(pack => pack.Id).Distinct(StringComparer.Ordinal).Count() != value.Packs.Length)
        {
            throw new InvalidOperationException("Pack lock is invalid.");
        }

        ImmutableArray<CampaignPackLock> packs = [.. value.Packs.Select(pack =>
        {
            if (!SemanticVersion.TryParse(pack.Version, out SemanticVersion version) || pack.ContentRevision <= 0)
            {
                throw new InvalidOperationException("Pack version is invalid.");
            }

            return new CampaignPackLock(new ContentId(pack.Id), version, pack.ContentRevision);
        })];
        return new CampaignContentLock(
            value.BaseContentRevision,
            packs,
            new ContentFingerprint(value.ManifestFingerprint),
            new ContentFingerprint(value.SemanticFingerprint),
            new ContentFingerprint(value.EffectiveFingerprint),
            value.GeneratorVersion,
            value.FormulaVersion,
            value.EffectVersion,
            value.SaveSchemaVersion,
            ParseIds(value.AppliedMigrationIds, CampaignSaveLimits.MaximumCollectionEntries));
    }
}
