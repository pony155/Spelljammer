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
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

internal static partial class PersistenceContracts
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
        Equal(campaign.Voyage.Seed, loaded.Campaign!.Voyage.Seed, "Voyage seed did not round-trip.");
        Equal(campaign.Voyage.Tick, loaded.Campaign.Voyage.Tick, "Voyage tick did not round-trip.");
        Equal(campaign.CurrentLocationId, loaded.Campaign.CurrentLocationId, "Current location did not round-trip.");
        Equal(campaign.ProtagonistId, loaded.Campaign.ProtagonistId, "Protagonist identity did not round-trip.");
        Equal(campaign.Characters.Length, loaded.Campaign.Characters.Length, "Roster did not round-trip.");
        Equal(campaign.Voyage.Ships.Values.Single().Modules.Length,
            loaded.Campaign.Voyage.Ships.Values.Single().Modules.Length, "Ship modules did not round-trip.");
        True(loaded.Campaign.Voyage.PersonalEncounter!.Actors.Values.Single().Injuries.Single().Stabilized,
            "A stabilized injury did not round-trip.");
        True(loaded.Campaign.Voyage.Commands.Length == 1 && loaded.Campaign.Voyage.CommandHistory.Length == 1,
            "Queued work and retained history did not round-trip.");
        InventoryEntryId ammunitionEntryId = new(Guid.Parse("88888888-8888-8888-8888-888888888888"));
        Equal(12, loaded.Campaign.Characters.SelectMany(value => value.Items.InventoryEntries)
            .Single(value => value.EntryId == ammunitionEntryId).Stack.Quantity,
            "A character ammunition stack did not round-trip.");
        Equal(12, loaded.Campaign.Voyage.PersonalEncounter.Actors.Values
            .SelectMany(value => value.Items.InventoryEntries)
            .Single(value => value.EntryId == ammunitionEntryId).Stack.Quantity,
            "An encounter ammunition stack did not round-trip.");
        StatusInstanceId savedStatusId = new(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        Equal(2, loaded.Campaign.Characters.SelectMany(value => value.Statuses.Instances)
            .Single(value => value.InstanceId == savedStatusId).RemainingDuration,
            "A character Status did not round-trip.");
        Equal(2, loaded.Campaign.Voyage.PersonalEncounter.Actors.Values
            .SelectMany(value => value.Statuses.Instances)
            .Single(value => value.InstanceId == savedStatusId).RemainingDuration,
            "An encounter Status did not round-trip.");
    }

    private static void SchemaSevenEquipmentMigratesToItemInstances()
    {
        GameContentSnapshot content = Compile(false);
        CampaignState campaign = CreateCampaign(content);
        byte[] legacy = AsSchemaSevenSave(CampaignSaveCodec.Encode(campaign, content));
        Equal(SaveDiagnosticCode.None, CampaignSaveCodec.ValidateEnvelope(legacy),
            "A supported schema 7 envelope was rejected.");
        CampaignReadResult loaded = CampaignSaveCodec.Decode(legacy, content);
        True(loaded.Succeeded, loaded.Diagnostic.ToString());
        Equal(CampaignSaveVersions.SaveSchema, loaded.Campaign!.ContentLock.SaveSchemaVersion,
            "Schema 7 load did not publish the current save schema.");
        True(loaded.Campaign.Characters.All(character => !character.Items.ItemInstances.IsEmpty),
            "Legacy character equipment IDs were not migrated to item instances.");
        True(!loaded.Campaign.Voyage.PersonalEncounter!.Actors.Values.Single().Items.ItemInstances.IsEmpty,
            "Legacy encounter equipment was not migrated to item instances.");
    }

    private static byte[] AsSchemaSevenSave(byte[] current)
    {
        const int headerBytes = 52;
        int preflightLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(current.AsSpan(12, 4)));
        int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(current.AsSpan(16, 4)));
        JsonObject preflight = JsonNode.Parse(Encoding.UTF8.GetString(current, headerBytes, preflightLength))!.AsObject();
        preflight["contentLock"]!["saveSchemaVersion"] = CampaignSaveVersions.OldestSupportedSaveSchema;
        JsonObject payload = JsonNode.Parse(Encoding.UTF8.GetString(current, headerBytes + preflightLength, payloadLength))!.AsObject();
        foreach (JsonNode? characterNode in payload["characters"]!.AsArray())
        {
            JsonObject character = characterNode!.AsObject();
            character["equipmentIds"] = LegacyDefinitionIds(character["items"]!);
            string characterId = character["id"]!.GetValue<string>();
            character["activeEffects"] = new JsonArray
            {
                new JsonObject
                {
                    ["effectId"] = "effect.legacy.capability",
                    ["sourceId"] = "feat.legacy.source",
                    ["actorId"] = characterId,
                    ["targetId"] = characterId,
                    ["startTick"] = 0,
                    ["endTick"] = 1,
                    ["scopeId"] = "scope.legacy",
                },
            };
            character.Remove("items");
        }

        JsonNode? encounter = payload["world"]!["personalEncounter"];
        if (encounter is not null)
        {
            string encounterActorId = encounter["actors"]![0]!["id"]!.GetValue<string>();
            encounter["activeEffects"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "effect.legacy.encounter",
                    ["sourceId"] = "command.legacy.source",
                    ["targetId"] = encounterActorId,
                    ["expiresTick"] = 1,
                    ["stacks"] = 1,
                },
            };
            foreach (JsonNode? actorNode in encounter["actors"]!.AsArray())
            {
                JsonObject actor = actorNode!.AsObject();
                JsonArray equipment = [];
                foreach (JsonNode? id in LegacyDefinitionIds(actor["items"]!))
                {
                    equipment.Add(new JsonObject
                    {
                        ["slotId"] = "equipment-slot.utility",
                        ["equipmentId"] = id!.GetValue<string>(),
                        ["condition"] = 0,
                        ["resourceRemaining"] = 0,
                    });
                }

                actor["equipment"] = equipment;
                actor.Remove("items");
            }
        }

        byte[] preflightBytes = Encoding.UTF8.GetBytes(preflight.ToJsonString());
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        byte[] legacy = new byte[headerBytes + preflightBytes.Length + payloadBytes.Length];
        current.AsSpan(0, 10).CopyTo(legacy);
        BinaryPrimitives.WriteUInt16LittleEndian(legacy.AsSpan(10, 2), CampaignSaveVersions.OldestSupportedSaveSchema);
        BinaryPrimitives.WriteUInt32LittleEndian(legacy.AsSpan(12, 4), (uint)preflightBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(legacy.AsSpan(16, 4), (uint)payloadBytes.Length);
        preflightBytes.CopyTo(legacy, headerBytes);
        payloadBytes.CopyTo(legacy, headerBytes + preflightBytes.Length);
        SHA256.HashData(legacy.AsSpan(headerBytes)).CopyTo(legacy, 20);
        return legacy;
    }

    private static JsonArray LegacyDefinitionIds(JsonNode items)
    {
        JsonArray result = [];
        foreach (JsonNode? item in items["itemInstances"]!.AsArray())
        {
            result.Add(item!["definitionId"]!.GetValue<string>());
        }

        return result;
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
