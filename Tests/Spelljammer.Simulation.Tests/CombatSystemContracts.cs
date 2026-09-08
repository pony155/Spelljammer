using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.World;

internal static partial class SimulationContracts
{
    private static void CombatSystemRoutesAndRejectsAtomically()
    {
        (TacticalBoard board, CellId entry, CellId exit) = CreateBoard();
        ActorId actorId = new("actor.combat.attacker");
        ActorId targetId = new("actor.combat.target");
        PersonalActorState actor = Actor(actorId, new TeamId("team.combat.attackers"), entry) with {
            Turn = ActorTurn() with { CurrentActionPoints = 10 },
        };
        PersonalActorState target = Actor(targetId, new TeamId("team.combat.targets"), exit) with {
            Turn = ActorTurn() with { CurrentActionPoints = 10 },
        };
        PersonalEncounterState encounter = new(
            new EncounterId("encounter.combat.contract"),
            board.Place(actorId, entry).Place(targetId, exit),
            ImmutableDictionary<ActorId, PersonalActorState>.Empty.Add(actorId, actor).Add(targetId, target),
            ImmutableDictionary<ObjectiveId, ObjectiveState>.Empty,
            ImmutableHashSet<ContentId>.Empty,
            ImmutableHashSet<ContentId>.Empty,
            false,
            false);
        VoyageCommand command = Command(
            "command.combat.contract",
            VoyageCommandKind.PersonalMelee,
            actorId.Value,
            targetId.Value,
            10,
            1);
        PersonalCombatContext context = new(command, encounter, actor, target, 1, 17, 1);

        CombatSystem combat = new([new ContractCombatActionSystem()]);
        PersonalCombatResolution accepted = combat.Resolve(context);
        True(accepted.Accepted, accepted.RejectionCode);
        Equal(7, accepted.Actor.ActionPoints, "CombatSystem did not route the action-system result.");
        Equal(8, accepted.Target.Health, "CombatSystem did not preserve the resolved target state.");

        CombatSystem unavailable = new([]);
        PersonalCombatResolution missing = unavailable.Resolve(context);
        False(missing.Accepted, "CombatSystem accepted an unregistered combat action.");
        Equal(CombatRejectionCodes.ActionUnavailable, missing.RejectionCode,
            "An unavailable character-combat action returned the wrong rejection code.");
        True(ReferenceEquals(actor, missing.Actor) && ReferenceEquals(target, missing.Target),
            "A rejected combat action did not preserve the original actor states.");

        CombatSystem invalid = new([new InvalidContractCombatActionSystem()]);
        PersonalCombatResolution rejected = invalid.Resolve(context);
        False(rejected.Accepted, "CombatSystem published a structurally invalid action result.");
        Equal(CombatRejectionCodes.ResultInvalid, rejected.RejectionCode,
            "An invalid character-combat result returned the wrong rejection code.");
        True(ReferenceEquals(actor, rejected.Actor) && ReferenceEquals(target, rejected.Target),
            "An invalid combat result leaked partial state changes.");
    }

    private sealed class ContractCombatActionSystem : ICharacterCombatActionSystem
    {
        public VoyageCommandKind CommandKind => VoyageCommandKind.PersonalMelee;

        public PersonalCombatResolution Resolve(PersonalCombatContext context) => new(
            true,
            CombatRejectionCodes.None,
            context.Actor with { Turn = context.Actor.Turn.SpendActionPoints(3) },
            context.Target with {
                CharacterResources = context.Target.CharacterResources.ApplyResourceDamage(
                    CharacterResourceIds.Health,
                    2),
            },
            2);
    }

    private sealed class InvalidContractCombatActionSystem : ICharacterCombatActionSystem
    {
        public VoyageCommandKind CommandKind => VoyageCommandKind.PersonalMelee;

        public PersonalCombatResolution Resolve(PersonalCombatContext context) => new(
            true,
            CombatRejectionCodes.None,
            context.Actor with { TeamId = context.Target.TeamId },
            context.Target,
            0);
    }
}
