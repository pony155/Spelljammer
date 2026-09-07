using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Content.Compilation;

public sealed record CapabilityDisplayValue(ContentId Id, string Label, int Value, ushort Practice = 0);

public sealed record CharacterRosterDisplayRow(
    CharacterId Id,
    string Name,
    string Race,
    string Heritage,
    string Background,
    string PositionId,
    ImmutableArray<CapabilityDisplayValue> Attributes,
    ImmutableArray<CapabilityDisplayValue> Skills);

public sealed record CharacterRosterDisplay(
    ContentFingerprint Fingerprint,
    ImmutableArray<CharacterRosterDisplayRow> Characters,
    string? DisabledReason);

public static class RosterInspection
{
    public static CharacterRosterDisplay Project(
        RosterSnapshot roster,
        GameContentSnapshot content,
        Func<string, string> localize,
        string? rejectionCode = null)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(localize);
        if (roster.ContentFingerprint != content.Fingerprint)
        {
            throw new InvalidOperationException("Roster and content fingerprints do not match.");
        }

        ImmutableArray<CharacterRosterDisplayRow>.Builder rows = ImmutableArray.CreateBuilder<CharacterRosterDisplayRow>(roster.Characters.Length);
        foreach (CharacterState character in roster.Characters)
        {
            CharacterCapabilitySnapshot values = character.Capabilities.Snapshot(content);
            if (!content.TryGetCharacter(character.Id, out CharacterDefinition? definition) ||
                !content.TryGetRace(character.RaceId, out RaceDefinition? race) ||
                !content.TryGetHeritage(character.HeritageId, out HeritageDefinition? heritage) ||
                !content.TryGetBackground(character.BackgroundId, out BackgroundDefinition? background))
            {
                throw new InvalidOperationException("Roster character references a missing definition.");
            }

            rows.Add(new CharacterRosterDisplayRow(
                character.Id,
                localize(definition!.NameKey),
                localize(race!.NameKey),
                localize(heritage!.NameKey),
                localize(background!.NameKey),
                character.PositionId.ToString(),
                [.. values.Attributes.Select((value, index) => new CapabilityDisplayValue(
                    value.Id.Value, localize(content.Attributes[index].NameKey), value.Value))],
                [.. values.Skills.Select((value, index) => new CapabilityDisplayValue(
                    value.Id.Value, localize(content.Skills[index].NameKey), value.Value, value.Practice))]));
        }

        return new CharacterRosterDisplay(
            roster.ContentFingerprint,
            rows.MoveToImmutable(),
            rejectionCode is null ? null : localize(rejectionCode));
    }
}
