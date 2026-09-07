using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

/// <summary>
/// The protagonist and NPCs currently travelling as one active crew.
/// </summary>
public sealed record CrewRoster(
    ContentFingerprint ContentFingerprint,
    ScenarioId ScenarioId,
    CharacterId ProtagonistId,
    ImmutableArray<CharacterState> Members);

public enum RecruitmentFailure : byte
{
    None,
    ContentMismatch,
    ScenarioMissing,
    ScenarioMismatch,
    CharacterMissing,
    ProtagonistCannotBeRecruited,
    AlreadyRecruited,
    RosterFull,
    RosterInvalid,
}

public sealed record RecruitmentResult(CrewRoster? Roster, RecruitmentFailure Failure, CharacterId? RelatedCharacterId)
{
    public bool Succeeded => Roster is not null;
}

/// <summary>
/// Creates an active crew around a protagonist and recruits NPCs under scenario-authored limits.
/// </summary>
public static class CrewRecruitmentSystem
{
    public static RecruitmentResult Create(CharacterState protagonist, ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(protagonist);
        ArgumentNullException.ThrowIfNull(catalog);
        if (protagonist.ContentFingerprint != catalog.Fingerprint)
        {
            return Failure(RecruitmentFailure.ContentMismatch, protagonist.Id);
        }

        if (!TryValidateCharacter(protagonist, catalog, out RecruitmentFailure failure))
        {
            return Failure(failure, protagonist.Id);
        }

        if (!catalog.TryGetScenario(protagonist.ScenarioId, out ScenarioDefinition? scenario))
        {
            return Failure(RecruitmentFailure.ScenarioMissing, protagonist.Id);
        }

        if (scenario!.MaximumRosterSize < 1)
        {
            return Failure(RecruitmentFailure.RosterInvalid, protagonist.Id);
        }

        CrewRoster roster = new(
            catalog.Fingerprint,
            protagonist.ScenarioId,
            protagonist.Id,
            [protagonist]);
        return new RecruitmentResult(roster, RecruitmentFailure.None, null);
    }

    public static RecruitmentResult Recruit(
        CrewRoster roster,
        CharacterState npc,
        ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(catalog);
        if (roster.ContentFingerprint != catalog.Fingerprint || npc.ContentFingerprint != catalog.Fingerprint)
        {
            return Failure(RecruitmentFailure.ContentMismatch, npc.Id);
        }

        if (!catalog.TryGetScenario(roster.ScenarioId, out ScenarioDefinition? scenario))
        {
            return Failure(RecruitmentFailure.ScenarioMissing, npc.Id);
        }

        if (!IsValid(roster, scenario!))
        {
            return Failure(RecruitmentFailure.RosterInvalid, npc.Id);
        }

        if (!TryValidateCharacter(npc, catalog, out RecruitmentFailure failure))
        {
            return Failure(failure, npc.Id);
        }

        if (npc.ScenarioId != roster.ScenarioId)
        {
            return Failure(RecruitmentFailure.ScenarioMismatch, npc.Id);
        }

        if (npc.Id == roster.ProtagonistId)
        {
            return Failure(RecruitmentFailure.ProtagonistCannotBeRecruited, npc.Id);
        }

        if (roster.Members.Any(member => member.Id == npc.Id))
        {
            return Failure(RecruitmentFailure.AlreadyRecruited, npc.Id);
        }

        if (roster.Members.Length >= scenario!.MaximumRosterSize)
        {
            return Failure(RecruitmentFailure.RosterFull, npc.Id);
        }

        return new RecruitmentResult(
            roster with { Members = roster.Members.Add(npc) },
            RecruitmentFailure.None,
            null);
    }

    private static bool TryValidateCharacter(
        CharacterState character,
        ICharacterContentCatalog catalog,
        out RecruitmentFailure failure)
    {
        if (!catalog.TryGetCharacter(character.Id, out CharacterDefinition? definition))
        {
            failure = RecruitmentFailure.CharacterMissing;
            return false;
        }

        if (!definition!.ScenarioIds.Contains(character.ScenarioId))
        {
            failure = RecruitmentFailure.ScenarioMismatch;
            return false;
        }

        failure = RecruitmentFailure.None;
        return true;
    }

    private static bool IsValid(CrewRoster roster, ScenarioDefinition scenario) =>
        roster.Members.Length is > 0 &&
        roster.Members.Length <= scenario.MaximumRosterSize &&
        roster.Members.Select(member => member.Id).Distinct().Count() == roster.Members.Length &&
        roster.Members.Any(member => member.Id == roster.ProtagonistId) &&
        roster.Members.All(member =>
            member.ContentFingerprint == roster.ContentFingerprint && member.ScenarioId == roster.ScenarioId);

    private static RecruitmentResult Failure(RecruitmentFailure failure, CharacterId characterId) =>
        new(null, failure, characterId);
}
