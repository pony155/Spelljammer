using Spelljammer.Simulation.Content;

namespace Spelljammer.Presentation;

internal sealed record CharacterCreationChoice(
    string TextId,
    CharacterId CharacterId,
    RaceId RaceId,
    HeritageId HeritageId,
    BackgroundId BackgroundId);

internal sealed record CharacterCreationSelection(CharacterCreationChoice Choice, ulong Seed);

internal static class CharacterCreationChoices
{
    internal static IReadOnlyList<CharacterCreationChoice> All { get; } = Array.AsReadOnly(
        new CharacterCreationChoice[]
        {
            Choice("human", "character.first-voyage.human", "race.human", "heritage.human.hearthworld"),
            Choice("elf", "character.first-voyage.elf", "race.elf", "heritage.elf.dawnweave"),
            Choice("half-elf", "character.first-voyage.half-elf", "race.half-elf", "heritage.half-elf.concord"),
            Choice("dwarf", "character.first-voyage.dwarf", "race.dwarf", "heritage.dwarf.cometdelver"),
            Choice("orc", "character.first-voyage.orc", "race.orc", "heritage.orc.redwake"),
            Choice("gnome", "character.first-voyage.gnome", "race.gnome", "heritage.gnome.coilwhisper"),
            Choice("goblin", "character.first-voyage.goblin", "race.goblin", "heritage.goblin.hullrunner"),
            Choice("somnari", "character.first-voyage.somnari", "race.somnari", "heritage.somnari.chorusborn"),
            Choice("veyr", "character.first-voyage.veyr", "race.veyr", "heritage.veyr.crimson-court"),
            Choice("eidolon", "character.first-voyage.eidolon", "race.eidolon", "heritage.eidolon.reliquary-bound"),
            Choice("tharun", "character.first-voyage.tharun", "race.tharun", "heritage.tharun.startrail"),
        });

    private static CharacterCreationChoice Choice(
        string textId,
        string characterId,
        string raceId,
        string heritageId) => new(
            textId,
            new CharacterId(characterId),
            new RaceId(raceId),
            new HeritageId(heritageId),
            new BackgroundId("background.expedition-veteran"));
}
