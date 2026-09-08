using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

namespace Spelljammer.Simulation.World;

public sealed partial record World
{
    private World CommitPersonal(
        WorldCommand command,
        IPersonalCombatResolver? personalCombatResolver)
    {
        if (PersonalEncounter is null || !BattleUnitIdFrom(command.IssuerId, out BattleUnitId unitId) ||
            !PersonalEncounter.Units.TryGetValue(unitId, out BattleUnitState? actor) ||
            actor.ActionPoints <= 0 || actor.IsIncapacitated)
        {
            return AddEvent(command, false, 0, "command.actor-not-ready");
        }

        PersonalEncounterState encounter = PersonalEncounter;
        BattleUnitState updated = actor;
        int eventAmount = command.Amount;
        if (command.Kind == WorldCommandKind.PersonalEndActivation)
        {
            updated = updated with { Turn = updated.Turn.EndActivation() };
            encounter = encounter with { Units = encounter.Units.SetItem(unitId, updated) };
            ImmutableArray<BattleUnitId> remaining = ReadyUnits.Remove(unitId);
            bool remainPaused = remaining.Any(id => encounter.Units[id].TeamId == PlayerTeamId);
            return (this with { PersonalEncounter = encounter, ReadyUnits = remaining, PersonalPaused = remainPaused })
                .AddEvent(command, true, 0, string.Empty);
        }

        bool isCombat = IsPersonalCombat(command.Kind);
        int apCost = 0;
        if (!isCombat)
        {
            try
            {
                apCost = updated.Turn.GetActionPointCost(PersonalActionId(command.Kind));
            }
            catch (KeyNotFoundException)
            {
                return AddEvent(command, false, 0, "command.action-unknown");
            }

            if (!updated.Turn.CanSpendActionPoints(apCost))
            {
                return AddEvent(command, false, 0, "command.action-points-insufficient");
            }
        }

        switch (command.Kind)
        {
            case WorldCommandKind.PersonalMove:
                if (!CellIdFrom(command.TargetId, out CellId destination))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                try
                {
                    encounter = encounter with { Board = encounter.Board.Move(unitId, destination, TacticalBoard.MaximumCells) };
                }
                catch (InvalidOperationException)
                {
                    return AddEvent(command, false, 0, "command.path-unavailable");
                }

                updated = updated with { CellId = destination };
                break;
            case WorldCommandKind.PersonalDefend:
                updated = updated with { Defending = true };
                break;
            case WorldCommandKind.PersonalReserveReaction:
                updated = updated with {
                    ReservedReactionPoints = 1,
                    ReactionExpiresTick = Tick + TimeDefinition.TicksPerSecond,
                };
                break;
            case WorldCommandKind.PersonalSurrender:
                updated = updated with { Surrendered = true };
                break;
            case WorldCommandKind.PersonalRetreat:
                if (!encounter.Board.Definition.RetreatCellIds.Contains(updated.CellId))
                {
                    return AddEvent(command, false, 0, "command.retreat-unavailable");
                }

                encounter = encounter with { Retreated = true };
                break;
            case WorldCommandKind.PersonalMedicine:
                if (!BattleUnitIdFrom(command.TargetId, out BattleUnitId patientId) || !encounter.Units.TryGetValue(patientId, out BattleUnitState? patient))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                encounter = encounter with {
                    Units = encounter.Units.SetItem(patientId, patient with {
                        Injuries = [.. patient.Injuries.Select(value => value with { Stabilized = true })],
                    }),
                };
                break;
            case WorldCommandKind.PersonalInteract:
                if (command.OptionId is ContentId objectiveId && ObjectiveIdFrom(objectiveId, out ObjectiveId objective) &&
                    encounter.Objectives.ContainsKey(objective))
                {
                    encounter = encounter with {
                        Objectives = encounter.Objectives.SetItem(objective, ObjectiveState.Completed),
                        ExplorationChanges = encounter.ExplorationChanges.Add(new ContentId("exploration.ruin.console-restored")),
                    };
                }
                break;
            case WorldCommandKind.PersonalEngineering:
                encounter = encounter with { ExplorationChanges = encounter.ExplorationChanges.Add(new ContentId("exploration.ruin.defense-disabled")) };
                break;
            case WorldCommandKind.PersonalMelee:
            case WorldCommandKind.PersonalRanged:
            case WorldCommandKind.PersonalSpell:
            case WorldCommandKind.PersonalPsionic:
                if (!BattleUnitIdFrom(command.TargetId, out BattleUnitId targetId) || !encounter.Units.TryGetValue(targetId, out BattleUnitState? target))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                if (targetId == unitId || personalCombatResolver is null)
                {
                    return AddEvent(
                        command,
                        false,
                        0,
                        personalCombatResolver is null
                            ? "command.personal-combat-resolver-required"
                            : CombatRejectionCodes.TargetIllegal);
                }

                PersonalCombatResolution resolution;
                try
                {
                    resolution = personalCombatResolver.Resolve(new PersonalCombatContext(
                        command,
                        encounter,
                        actor,
                        target!,
                        Tick,
                        Seed,
                        RandomSequence));
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    return AddEvent(command, false, 0, CombatRejectionCodes.ResolutionFailed);
                }

                if (!resolution.Accepted)
                {
                    return AddEvent(
                        command,
                        false,
                        0,
                        string.IsNullOrWhiteSpace(resolution.RejectionCode)
                            ? CombatRejectionCodes.ResultInvalid
                            : resolution.RejectionCode);
                }

                if (!HasStableEncounterIdentity(actor, resolution.Actor) ||
                    !HasStableEncounterIdentity(target, resolution.Target) ||
                    resolution.Actor.ActionPoints < 0 ||
                    resolution.Actor.ActionPoints > actor.ActionPoints ||
                    resolution.EventAmount < 0)
                {
                    return AddEvent(command, false, 0, CombatRejectionCodes.ResultInvalid);
                }

                bool wasIncapacitated = target.IsIncapacitated;
                BattleUnitState resolvedTarget = resolution.Target;
                if (resolvedTarget.IsIncapacitated && !wasIncapacitated &&
                    !resolvedTarget.Injuries.Any(value => value.Severity == InjurySeverity.Incapacitating))
                {
                    resolvedTarget = resolvedTarget with {
                        Injuries = resolvedTarget.Injuries.Add(new InjuryState(
                            new ContentId("injury.combat.incapacitated"),
                            InjurySeverity.Incapacitating,
                            false)),
                    };
                }

                updated = resolution.Actor;
                encounter = encounter with {
                    Units = encounter.Units.SetItem(targetId, resolvedTarget),
                    DamagedObjects = resolution.DamagedObjectId is ContentId objectId
                        ? encounter.DamagedObjects.Add(objectId)
                        : encounter.DamagedObjects,
                };
                eventAmount = resolution.EventAmount;
                apCost = 0;
                break;
            default:
                return AddEvent(command, false, 0, "command.action-unknown");
        }

        if (!isCombat)
        {
            updated = updated with { Turn = updated.Turn.SpendActionPoints(apCost) };
        }
        encounter = encounter with { Units = encounter.Units.SetItem(unitId, updated) };
        ImmutableArray<BattleUnitId> ready = updated.ActionPoints == 0 ? ReadyUnits.Remove(unitId) : ReadyUnits;
        bool pause = ready.Any(id => encounter.Units[id].TeamId == PlayerTeamId);
        return (this with { PersonalEncounter = encounter, ReadyUnits = ready, PersonalPaused = pause })
            .AddEvent(
                command,
                true,
                eventAmount,
                string.Empty);
    }

    private World UpdatePersonalTimeline()
    {
        if (PersonalEncounter is null)
        {
            return this;
        }

        PersonalEncounterState encounter = PersonalEncounter;
        ImmutableArray<BattleUnitId>.Builder becameReady = ImmutableArray.CreateBuilder<BattleUnitId>();
        ImmutableDictionary<BattleUnitId, BattleUnitState>.Builder units = encounter.Units.ToBuilder();
        foreach (BattleUnitState actor in encounter.Units.Values.OrderBy(value => value.Id))
        {
            BattleUnitState current = actor.ReactionExpiresTick > 0 && actor.ReactionExpiresTick < Tick
                ? actor with { ReservedReactionPoints = 0, ReactionExpiresTick = 0 }
                : actor;
            if (current.IsIncapacitated || current.Surrendered || ReadyUnits.Contains(current.Id))
            {
                units[current.Id] = current;
                continue;
            }

            CharacterResourceSet recoveredResources = current.CharacterResources.RecoverOneTick();
            CharacterTurnState advancedTurn = current.Turn.AddTurnMeter(
                recoveredResources.GetResourcePercentage(CharacterResourceIds.Stamina));
            if (advancedTurn.CanActivate)
            {
                becameReady.Add(current.Id);
                units[current.Id] = current with {
                    Turn = advancedTurn.BeginActivation(),
                    CharacterResources = recoveredResources,
                    Defending = false,
                };
            }
            else
            {
                units[current.Id] = current with {
                    Turn = advancedTurn,
                    CharacterResources = recoveredResources,
                };
            }
        }

        ImmutableArray<BattleUnitId> ready =
            [.. ReadyUnits.AddRange(becameReady).Distinct().OrderBy(id => units[id].TeamId == PlayerTeamId ? 0 : 1).ThenBy(id => id)];
        bool personalPause = ready.Any(id => units[id].TeamId == PlayerTeamId &&
            !ScheduledActions.Any(value => value.Command.IssuerId == id.Value && value.Phase < ScheduledActionPhase.Committed));
        return this with {
            PersonalEncounter = encounter with { Units = units.ToImmutable() },
            ReadyUnits = ready,
            PersonalPaused = personalPause,
        };
    }

    private static ContentId PersonalActionId(WorldCommandKind kind) => kind switch {
        WorldCommandKind.PersonalMove => new("action.personal.move"),
        WorldCommandKind.PersonalDefend => new("action.personal.defend"),
        WorldCommandKind.PersonalReserveReaction => new("action.personal.reserve-reaction"),
        WorldCommandKind.PersonalMelee => new("action.personal.melee"),
        WorldCommandKind.PersonalRanged => new("action.personal.ranged"),
        WorldCommandKind.PersonalSpell => new("action.personal.spell"),
        WorldCommandKind.PersonalPsionic => new("action.personal.psionic"),
        WorldCommandKind.PersonalEngineering => new("action.personal.engineering"),
        WorldCommandKind.PersonalMedicine => new("action.personal.medicine"),
        WorldCommandKind.PersonalInteract => new("action.personal.interact"),
        WorldCommandKind.PersonalSurrender => new("action.personal.surrender"),
        WorldCommandKind.PersonalRetreat => new("action.personal.retreat"),
        _ => throw new KeyNotFoundException("The command is not a personal action."),
    };

    private static bool IsPersonalCombat(WorldCommandKind kind) => kind is
        WorldCommandKind.PersonalMelee or
        WorldCommandKind.PersonalRanged or
        WorldCommandKind.PersonalSpell or
        WorldCommandKind.PersonalPsionic;

    private static bool HasStableEncounterIdentity(BattleUnitState original, BattleUnitState resolved) =>
        original.Id == resolved.Id &&
        original.TeamId == resolved.TeamId &&
        original.CharacterId == resolved.CharacterId &&
        original.CellId == resolved.CellId &&
        original.Surrendered == resolved.Surrendered &&
        original.Prisoner == resolved.Prisoner;

    private static bool BattleUnitIdFrom(ContentId id, out BattleUnitId value)
    {
        if (id.ToString().StartsWith("unit.", StringComparison.Ordinal))
        {
            value = new BattleUnitId(id);
            return true;
        }

        value = default;
        return false;
    }

    private static bool CellIdFrom(ContentId id, out CellId value)
    {
        if (id.ToString().StartsWith("cell.", StringComparison.Ordinal))
        {
            value = new CellId(id);
            return true;
        }

        value = default;
        return false;
    }

    private static bool ObjectiveIdFrom(ContentId id, out ObjectiveId value)
    {
        if (id.ToString().StartsWith("objective.", StringComparison.Ordinal))
        {
            value = new ObjectiveId(id);
            return true;
        }

        value = default;
        return false;
    }

    private static bool IsPersonal(WorldCommandKind kind) => kind >= WorldCommandKind.PersonalMove;
}
