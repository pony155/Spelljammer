using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Encounters;

/// <summary>
/// Creates an encounter-scoped battle unit from a persistent character and
/// commits the battle-owned state back at an explicit transaction boundary.
/// </summary>
public static class BattleUnitProjection
{
    public static BattleUnitState Project(
        BattleUnitId unitId,
        TeamId teamId,
        CharacterState character,
        CellId cellId,
        ICharacterStateCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!unitId.IsValid || !teamId.IsValid || !cellId.IsValid)
        {
            throw new ArgumentException("Battle-unit projection identity is invalid.");
        }

        character.ValidateForContent(catalog);
        if (!character.CanAct)
        {
            throw new InvalidOperationException("A character that cannot act cannot be projected into battle.");
        }

        CharacterResourceProfileDefinition profile = GetResourceProfile(character, catalog);
        return new BattleUnitState(
            unitId,
            teamId,
            character.Id,
            cellId,
            CharacterTurnState.Create(profile.TurnRules),
            character.CharacterResources,
            false,
            false,
            false,
            character.Items,
            character.Injuries) {
            Statuses = Retarget(character.Statuses, new StatusTargetId(unitId.Value)),
        };
    }

    public static CharacterState Commit(
        CharacterState character,
        BattleUnitState unit,
        ICharacterStateCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(catalog);
        if (unit.CharacterId != character.Id)
        {
            throw new InvalidOperationException("Battle unit does not project the supplied character.");
        }

        if (!unit.Id.IsValid || !unit.TeamId.IsValid || !unit.CellId.IsValid ||
            unit.Statuses.Instances.Any(value => value.TargetId.Value != unit.Id.Value))
        {
            throw new InvalidOperationException("Battle unit identity or Status ownership is invalid.");
        }

        CharacterResourceProfileDefinition profile = GetResourceProfile(character, catalog);
        unit.Turn.Validate(profile.TurnRules);

        CharacterState committed = character with {
            CharacterResources = unit.CharacterResources,
            Items = unit.Items,
            Statuses = Retarget(unit.Statuses, new StatusTargetId(character.Id.Value)),
            Injuries = unit.Injuries,
            CanAct = !unit.IsIncapacitated && !unit.Prisoner,
        };
        committed.ValidateForContent(catalog);
        return committed;
    }

    private static StatusState Retarget(StatusState state, StatusTargetId targetId) => new(
        [.. state.Instances.Select(value => value with { TargetId = targetId })]);

    private static CharacterResourceProfileDefinition GetResourceProfile(
        CharacterState character,
        ICharacterStateCatalog catalog)
    {
        if (!catalog.TryGetScenario(character.ScenarioId, out ScenarioDefinition? scenario) ||
            scenario!.CharacterResourceProfileId is not CharacterResourceProfileId profileId ||
            !catalog.TryGetCharacterResourceProfile(profileId, out CharacterResourceProfileDefinition? profile))
        {
            throw new InvalidOperationException("Character resource profile is missing.");
        }

        return profile!;
    }
}
