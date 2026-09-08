using System.Text.Json;
using System.Text.Json.Nodes;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Persistence;

/// <summary>
/// Applies explicit structural migrations between campaign save schema versions.
/// </summary>
/// <remarks>
/// Code flow: Envelope decoding identifies the source schema, a registered JSON transformation renames or reshapes fields, and the current-schema payload proceeds to normal DTO decoding and validation.
/// </remarks>
internal static class CampaignSaveSchemaMigrations
{
    public static readonly ContentId BattleUnitsMigrationId = new("migration.save.v13-battle-units");

    public static ReadOnlyMemory<byte> Migrate(
        ReadOnlyMemory<byte> payload,
        ushort sourceSchema,
        JsonSerializerOptions options)
    {
        if (sourceSchema == CampaignSaveVersions.SaveSchema)
        {
            return payload;
        }

        if (sourceSchema != 12)
        {
            throw new InvalidOperationException("Campaign save schema has no registered migration.");
        }

        JsonObject root = JsonNode.Parse(payload.Span)?.AsObject() ??
            throw new JsonException("Campaign payload is empty.");
        JsonObject world = root["world"]?.AsObject() ??
            throw new JsonException("Campaign world is missing.");
        Rename(world, "readyActorIds", "readyUnitIds");
        if (world["personalEncounter"] is JsonObject encounter)
        {
            Rename(encounter, "actors", "units");
        }

        RewriteLegacyUnitIds(root, null);
        return JsonSerializer.SerializeToUtf8Bytes(root, options);
    }

    private static void Rename(JsonObject owner, string oldName, string newName)
    {
        JsonNode? value = owner[oldName] ?? throw new JsonException($"Campaign property '{oldName}' is missing.");
        owner.Remove(oldName);
        owner[newName] = value;
    }

    private static void RewriteLegacyUnitIds(JsonNode node, string? propertyName)
    {
        switch (node)
        {
            case JsonObject value:
                foreach ((string name, JsonNode? child) in value.ToArray())
                {
                    if (child is not null)
                    {
                        RewriteLegacyUnitIds(child, name);
                    }
                }
                break;
            case JsonArray value:
                foreach (JsonNode? child in value)
                {
                    if (child is not null)
                    {
                        RewriteLegacyUnitIds(child, propertyName);
                    }
                }
                break;
            case JsonValue value when value.TryGetValue(out string? text) &&
                IsIdentityProperty(propertyName) &&
                text.StartsWith("actor.", StringComparison.Ordinal):
                value.ReplaceWith(JsonValue.Create($"unit.{text["actor.".Length..]}"));
                break;
        }
    }

    private static bool IsIdentityProperty(string? name) => name is
        "id" or "sourceId" or "targetId" or "issuerId" or "optionId" or
        "readyUnitIds" or "witnessIds";
}
