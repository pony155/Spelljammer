using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Persistence;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Galaxy;
using Spelljammer.Simulation.World;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

public sealed partial class PersistenceContracts
{
    private static void ExactCampaignRoundTripsCanonically()
    {
        GameContentSnapshot content = Compile(false);
        CampaignState campaign = CreateCampaign(content);
        byte[] first = CampaignSaveCodec.Encode(campaign, content);
        byte[] second = CampaignSaveCodec.Encode(campaign, content);
        True(first.AsSpan().SequenceEqual(second), "Identical authoritative state produced different save bytes.");
        True(first.Length <= CampaignSaveLimits.MaximumSaveBytes, "A minimal campaign exceeded the save bound.");

        ContentPreflightResult preflight = CampaignSaveCodec.Preflight(first, content);
        Equal(ContentPreflightKind.Exact, preflight.Kind, "Exact content did not pass preflight.");
        CampaignReadResult loaded = CampaignSaveCodec.Decode(first, content);
        True(loaded.Succeeded, loaded.Diagnostic.ToString());
        Equal(campaign.World.Seed, loaded.Campaign!.World.Seed, "World seed did not round-trip.");
        Equal(campaign.World.Tick, loaded.Campaign.World.Tick, "World tick did not round-trip.");
        Equal(campaign.World.Clock, loaded.Campaign.World.Clock, "Campaign clock did not round-trip.");
        Equal(campaign.CurrentLocationId, loaded.Campaign.CurrentLocationId, "Current location did not round-trip.");
        Equal(campaign.ProtagonistId, loaded.Campaign.ProtagonistId, "Protagonist identity did not round-trip.");
        Equal(campaign.Characters.Length, loaded.Campaign.Characters.Length, "Roster did not round-trip.");
        Equal(campaign.Characters.Length, loaded.Campaign.World.Characters.Count,
            "Authoritative World characters did not round-trip.");
        VoyageNavigationState expectedNavigation = campaign.World.VoyageNavigation!;
        VoyageNavigationState actualNavigation = loaded.Campaign.World.VoyageNavigation!;
        Equal(expectedNavigation.CurrentSystemId, actualNavigation.CurrentSystemId,
            "Voyage current system did not round-trip.");
        True(expectedNavigation.ActiveStarwayId == actualNavigation.ActiveStarwayId &&
                expectedNavigation.PlannedRoute.SequenceEqual(actualNavigation.PlannedRoute) &&
                expectedNavigation.RouteProgress == actualNavigation.RouteProgress &&
                expectedNavigation.DepartureTick == actualNavigation.DepartureTick &&
                expectedNavigation.ArrivalTick == actualNavigation.ArrivalTick,
            "Voyage navigation did not round-trip.");
        True(campaign.World.Galaxy!.Topology.Systems.Values.OrderBy(value => value.Id).SequenceEqual(
                loaded.Campaign.World.Galaxy!.Topology.Systems.Values.OrderBy(value => value.Id)),
            "Galaxy topology did not round-trip.");
        Equal(campaign.World.Galaxy.Dynamic.Starways.Single(),
            loaded.Campaign.World.Galaxy.Dynamic.Starways.Single(),
            "Dynamic Starway state did not round-trip.");
        True(campaign.World.Galaxy.Dynamic.ChangedSiteIds.SetEquals(
                loaded.Campaign.World.Galaxy.Dynamic.ChangedSiteIds),
            "Changed galaxy sites did not round-trip.");
        Equal(campaign.World.Ships.Values.Single().Modules.Length,
            loaded.Campaign.World.Ships.Values.Single().Modules.Length, "Ship modules did not round-trip.");
        True(loaded.Campaign.World.PersonalEncounter!.Units.Values.Single().Injuries.Single().Stabilized,
            "A stabilized injury did not round-trip.");
        True(loaded.Campaign.World.Commands.Length == 1 && loaded.Campaign.World.CommandHistory.Length == 1,
            "Queued work and retained history did not round-trip.");
        InventoryEntryId ammunitionEntryId = new(Guid.Parse("88888888-8888-8888-8888-888888888888"));
        Equal(12, loaded.Campaign.Characters.SelectMany(value => value.Items.InventoryEntries)
            .Single(value => value.EntryId == ammunitionEntryId).Stack.Quantity,
            "A character ammunition stack did not round-trip.");
        Equal(12, loaded.Campaign.World.PersonalEncounter.Units.Values
            .SelectMany(value => value.Items.InventoryEntries)
            .Single(value => value.EntryId == ammunitionEntryId).Stack.Quantity,
            "An encounter ammunition stack did not round-trip.");
        StatusInstanceId savedStatusId = new(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        Equal(2, loaded.Campaign.Characters.SelectMany(value => value.Statuses.Instances)
            .Single(value => value.InstanceId == savedStatusId).RemainingDuration,
            "A character Status did not round-trip.");
        Equal(2, loaded.Campaign.World.PersonalEncounter.Units.Values
            .SelectMany(value => value.Statuses.Instances)
            .Single(value => value.InstanceId == savedStatusId).RemainingDuration,
            "An encounter Status did not round-trip.");
    }

    private static void CorruptionAndMissingContentFailPreflight()
    {
        GameContentSnapshot baseContent = Compile(false);
        byte[] valid = CampaignSaveCodec.Encode(CreateCampaign(baseContent), baseContent);
        byte[] truncated = valid[..^1];
        Equal(SaveDiagnosticCode.Truncated, CampaignSaveCodec.ValidateEnvelope(truncated), "Truncation diagnostic changed.");
        byte[] corrupt = valid.ToArray();
        corrupt[^1] ^= 0xff;
        Equal(SaveDiagnosticCode.ChecksumMismatch, CampaignSaveCodec.ValidateEnvelope(corrupt), "Checksum diagnostic changed.");
        byte[] oversized = new byte[CampaignSaveLimits.MaximumSaveBytes + 1];
        Equal(SaveDiagnosticCode.Oversized, CampaignSaveCodec.ValidateEnvelope(oversized), "Oversize diagnostic changed.");
        byte[] unsupported = valid.ToArray();
        unsupported[10] = 0xff;
        Equal(SaveDiagnosticCode.Unsupported, CampaignSaveCodec.ValidateEnvelope(unsupported), "Unsupported schema diagnostic changed.");
        byte[] obsoleteSchema = valid.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(
            obsoleteSchema.AsSpan(10, 2), checked((ushort)(CampaignSaveVersions.MinimumMigratableSaveSchema - 1)));
        Equal(SaveDiagnosticCode.Unsupported, CampaignSaveCodec.ValidateEnvelope(obsoleteSchema),
            "A save older than the migration floor was accepted.");

        GameContentSnapshot additive = Compile(true);
        byte[] additiveSave = CampaignSaveCodec.Encode(CreateCampaign(additive), additive);
        ContentPreflightResult missing = CampaignSaveCodec.Preflight(additiveSave, baseContent);
        Equal(ContentPreflightKind.Missing, missing.Kind, "Missing pack did not stop at preflight.");
        True(missing.MissingPackIds.Contains(new ContentId("mod.starwrights")), "Missing pack ID was not reported.");
        True(missing.MissingDefinitionIds.Contains(new ContentId("skill.mod.starwrights.gravimetry")),
            "Missing definition ID was not reported.");
    }

    private static void CompatibilityIsExplicitAndLoadable()
    {
        GameContentSnapshot source = Compile(false);
        GameContentSnapshot destination = Compile(true);
        byte[] bytes = CampaignSaveCodec.Encode(CreateCampaign(source), source);
        ContentCompatibilityRule rule = new(source.Fingerprint, destination.Fingerprint);
        Equal(ContentPreflightKind.Incompatible, CampaignSaveCodec.Preflight(bytes, destination).Kind,
            "Changed content was accepted without an explicit rule.");
        ContentPreflightResult compatible = CampaignSaveCodec.Preflight(bytes, destination, [rule]);
        Equal(ContentPreflightKind.Compatible, compatible.Kind, "Explicit compatibility was not recognized.");
        CampaignReadResult loaded = CampaignSaveCodec.Decode(bytes, destination, [rule]);
        True(loaded.Succeeded, loaded.Diagnostic.ToString());
        Equal(destination.Skills.Length, loaded.Campaign!.Characters[0].Capabilities.Snapshot(destination).Skills.Length,
            "Compatible load did not reconstruct the destination Skill registry.");
    }

    private static void FailedLoadPreservesActiveCampaign()
    {
        GameContentSnapshot content = Compile(false);
        CampaignState initial = CreateCampaign(content);
        CampaignRegistry registry = new(initial);
        byte[] corrupt = CampaignSaveCodec.Encode(initial, content);
        corrupt[^1] ^= 1;
        CampaignPublicationResult result = registry.Load(corrupt, content);
        False(result.Published, "A corrupt campaign was published.");
        True(ReferenceEquals(initial, registry.Active), "Failed load replaced the active campaign.");
    }
}
