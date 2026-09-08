using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Spelljammer.Content.Compilation;
using Spelljammer.Persistence;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Effects;

public sealed partial class PersistenceContracts
{
    private const int SaveHeaderBytes = 52;

    private static void BattleUnitProjectionRoundTripsCharacterState()
    {
        GameContentSnapshot content = Compile(false);
        CampaignState campaign = CreateCampaign(content);
        BattleUnitState savedUnit = campaign.World.PersonalEncounter!.Units.Values.Single();
        CharacterState character = campaign.Characters.Single(value => value.Id == savedUnit.CharacterId);

        CharacterState committed = BattleUnitProjection.Commit(character, savedUnit, content);
        Equal(savedUnit.CharacterResources, committed.CharacterResources,
            "Battle-unit resources were not committed to the character.");
        Equal(savedUnit.Injuries, committed.Injuries,
            "Battle-unit injuries were not committed to the character.");
        True(committed.Statuses.Instances.All(value => value.TargetId.Value == character.Id.Value),
            "Committed Status targets did not return to character identity.");

        BattleUnitId derivedId = BattleUnitId.Derive(
            campaign.World.PersonalEncounter.Id,
            character.Id.Value,
            0);
        BattleUnitState projected = BattleUnitProjection.Project(
            derivedId,
            savedUnit.TeamId,
            committed,
            savedUnit.CellId,
            content);
        Equal(character.Id, projected.CharacterId!.Value,
            "Projected battle unit lost its persistent character source.");
        True(projected.Statuses.Instances.All(value => value.TargetId.Value == derivedId.Value),
            "Projected Status targets did not use battle-unit identity.");
    }

    private static void Schema12ActorSavesMigrateToBattleUnits()
    {
        GameContentSnapshot content = Compile(false);
        byte[] current = CampaignSaveCodec.Encode(CreateCampaign(content), content);
        byte[] legacy = ConvertToSchema12(current);

        Equal(SaveDiagnosticCode.None, CampaignSaveCodec.ValidateEnvelope(legacy),
            "A supported schema-12 envelope was rejected.");
        ContentPreflightResult preflight = CampaignSaveCodec.Preflight(legacy, content);
        Equal(ContentPreflightKind.Compatible, preflight.Kind,
            "Schema-12 save was not reported as an in-place compatible migration.");
        Equal(new ContentId("migration.save.v13-battle-units"), preflight.MigrationPath.Single(),
            "Schema migration ID changed.");

        CampaignReadResult migrated = CampaignSaveCodec.Decode(legacy, content);
        True(migrated.Succeeded, migrated.Diagnostic.ToString());
        Equal(CampaignSaveVersions.SaveSchema, migrated.Campaign!.ContentLock.SaveSchemaVersion,
            "Migrated campaign retained the old save schema.");
        True(migrated.Campaign.ContentLock.AppliedMigrationIds.Contains(
                new ContentId("migration.save.v13-battle-units")),
            "Migrated campaign did not retain its schema migration ID.");
        True(migrated.Campaign.World.PersonalEncounter!.Units.Keys.All(value =>
                value.ToString().StartsWith("unit.", StringComparison.Ordinal)),
            "Legacy actor identities were not migrated to battle-unit identities.");
    }

    private static byte[] ConvertToSchema12(byte[] current)
    {
        int preflightLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(current.AsSpan(12, 4)));
        int payloadLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(current.AsSpan(16, 4)));
        JsonObject preflight = JsonNode.Parse(current.AsSpan(SaveHeaderBytes, preflightLength))!.AsObject();
        JsonObject payload = JsonNode.Parse(current.AsSpan(SaveHeaderBytes + preflightLength, payloadLength))!.AsObject();
        preflight["contentLock"]!["saveSchemaVersion"] = 12;

        JsonObject world = payload["world"]!.AsObject();
        Rename(world, "readyUnitIds", "readyActorIds");
        if (world["personalEncounter"] is JsonObject encounter)
        {
            Rename(encounter, "units", "actors");
        }

        foreach (JsonObject character in payload["characters"]!.AsArray().Select(value => value!.AsObject()))
        {
            character.Remove("injuries");
        }

        RewriteUnitIdsAsActors(payload, null);
        byte[] preflightBytes = Encoding.UTF8.GetBytes(preflight.ToJsonString());
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload.ToJsonString());
        byte[] result = new byte[SaveHeaderBytes + preflightBytes.Length + payloadBytes.Length];
        current.AsSpan(0, 10).CopyTo(result);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10, 2), 12);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12, 4), (uint)preflightBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16, 4), (uint)payloadBytes.Length);
        preflightBytes.CopyTo(result, SaveHeaderBytes);
        payloadBytes.CopyTo(result, SaveHeaderBytes + preflightBytes.Length);
        SHA256.HashData(result.AsSpan(SaveHeaderBytes)).CopyTo(result, 20);
        return result;
    }

    private static void Rename(JsonObject owner, string oldName, string newName)
    {
        JsonNode value = owner[oldName] ?? throw new InvalidOperationException();
        owner.Remove(oldName);
        owner[newName] = value;
    }

    private static void RewriteUnitIdsAsActors(JsonNode node, string? propertyName)
    {
        switch (node)
        {
            case JsonObject value:
                foreach ((string name, JsonNode? child) in value.ToArray())
                {
                    if (child is not null)
                    {
                        RewriteUnitIdsAsActors(child, name);
                    }
                }
                break;
            case JsonArray value:
                foreach (JsonNode? child in value)
                {
                    if (child is not null)
                    {
                        RewriteUnitIdsAsActors(child, propertyName);
                    }
                }
                break;
            case JsonValue value when value.TryGetValue(out string? text) &&
                IsUnitIdentityProperty(propertyName) &&
                text.StartsWith("unit.", StringComparison.Ordinal):
                value.ReplaceWith(JsonValue.Create($"actor.{text["unit.".Length..]}"));
                break;
        }
    }

    private static bool IsUnitIdentityProperty(string? name) => name is
        "id" or "sourceId" or "targetId" or "issuerId" or "optionId" or
        "readyActorIds" or "witnessIds";
}
