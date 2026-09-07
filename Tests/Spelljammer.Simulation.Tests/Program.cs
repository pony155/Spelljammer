using System.Collections.Immutable;
using Spelljammer.Simulation;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

return SimulationContracts.Run();

internal static class SimulationContracts
{
    public static int Run()
    {
        EquivalentSeedsProduceEquivalentRuns();
        RejectedCommandsDoNotAdvanceState();
        SalvageIsFiniteAndRepairHasARealCost();
        FixedTickCadenceAndOrderingAreDeterministic();
        TacticalBoardAndEncounterCleanupAreBounded();
        ShipLoadoutPowerAndDamageAreAtomic();
        PersonalReactionsAndConsequencesPersist();
        Console.WriteLine("Space expedition simulation contracts passed.");
        return 0;
    }

    private static void EquivalentSeedsProduceEquivalentRuns()
    {
        ExpeditionSimulation simulation = new();
        ExpeditionCommand[] commands =
        [
            ExpeditionCommand.Travel(TravelDirection.North),
            ExpeditionCommand.Salvage,
            ExpeditionCommand.Travel(TravelDirection.East),
            ExpeditionCommand.Salvage,
        ];

        ExpeditionState first = simulation.Create(0x5eedUL);
        ExpeditionState second = simulation.Create(0x5eedUL);
        foreach (ExpeditionCommand command in commands)
        {
            first = simulation.Apply(first, command).State;
            second = simulation.Apply(second, command).State;
        }

        Equal(first.Turn, second.Turn, "Turn count diverged for an identical seed and command stream.");
        Equal(first.Position, second.Position, "Position diverged for an identical seed and command stream.");
        Equal(first.Fuel, second.Fuel, "Fuel diverged for an identical seed and command stream.");
        Equal(first.Hull, second.Hull, "Hull diverged for an identical seed and command stream.");
        Equal(first.Cargo, second.Cargo, "Cargo diverged for an identical seed and command stream.");
    }

    private static void RejectedCommandsDoNotAdvanceState()
    {
        ExpeditionSimulation simulation = new();
        ExpeditionState state = simulation.Create(7);
        CommandResult result = simulation.Apply(state, ExpeditionCommand.ReturnHome);

        False(result.Accepted, "An empty expedition was allowed to return with a prize.");
        Equal(CommandRejection.InsufficientPrize, result.Rejection, "The rejection reason was not stable.");
        True(ReferenceEquals(state, result.State), "A rejected command replaced authoritative state.");
    }

    private static void SalvageIsFiniteAndRepairHasARealCost()
    {
        ExpeditionSimulation simulation = new();
        ExpeditionState state = simulation.Create(11);
        state = simulation.Apply(state, ExpeditionCommand.Travel(TravelDirection.North)).State;

        CommandResult firstSalvage = simulation.Apply(state, ExpeditionCommand.Salvage);
        True(firstSalvage.Accepted, "A fresh sector could not be salvaged.");
        CommandResult repeatedSalvage = simulation.Apply(firstSalvage.State, ExpeditionCommand.Salvage);
        False(repeatedSalvage.Accepted, "A sector yielded unbounded salvage.");
        Equal(CommandRejection.AlreadySalvaged, repeatedSalvage.Rejection, "Repeat salvage reason was wrong.");

        if (firstSalvage.State.Hull < ExpeditionSimulation.MaximumHull &&
            firstSalvage.State.Cargo >= ExpeditionSimulation.RepairCargoCost)
        {
            CommandResult repair = simulation.Apply(firstSalvage.State, ExpeditionCommand.Repair);
            True(repair.Accepted, "A funded hull repair was rejected.");
            Equal(
                firstSalvage.State.Cargo - ExpeditionSimulation.RepairCargoCost,
                repair.State.Cargo,
                "Repair did not consume its documented cargo cost.");
        }
    }

    private static void FixedTickCadenceAndOrderingAreDeterministic()
    {
        ShipState ship = CreateShip(new ShipId("ship.first-voyage.player"), new TeamId("team.player"), "ship.path.arcane");
        VoyageWorld initial = VoyageWorld.Create(
            0x5eedUL,
            new ContentFingerprint(new string('a', 64)),
            ship.TeamId,
            [ship]);
        Equal(0, initial.Advance(8).AdvancedTicks, "Paused ship simulation advanced authoritative time.");

        VoyageCommand second = Command("command.test.second", VoyageCommandKind.Course, ship.Id.Value, ship.Id.Value, 20, 2);
        VoyageCommand first = Command("command.test.first", VoyageCommandKind.Course, ship.Id.Value, ship.Id.Value, 10, 1);
        VoyageWorld queued = initial.SetShipPause(false).Enqueue(second).World.Enqueue(first).World;
        Equal(first.Id, queued.Commands[0].Id, "Equal-tick commands were not stably priority ordered.");
        VoyageCommandResult stale = queued.Enqueue(first with { Id = new ContentId("command.test.stale"), TargetTick = -1 });
        False(stale.Accepted, "A stale command entered the authoritative queue.");
        True(ReferenceEquals(queued, stale.World), "A rejected command replaced the world instance.");
        VoyageWorld prepared = queued.Advance(2).World;
        True(prepared.ScheduledActions.All(value => value.Phase == ScheduledActionPhase.Recovering &&
            value.History.Contains(ScheduledActionPhase.Reserved) && value.History.Contains(ScheduledActionPhase.Committed)),
            "Scheduled actions did not preserve their transaction phases.");

        VoyageWorld cancellationCandidate = initial.SetShipPause(false).Enqueue(first).World;
        VoyageCommandResult cancelled = cancellationCandidate.Cancel(first.Id);
        True(cancelled.Accepted && cancelled.World.CommandHistory.Single().CancelledTick == 0,
            "Pre-commit cancellation was not retained in the replay log.");

        VoyageWorld batched = queued.Advance(8).World;
        VoyageWorld stepped = queued;
        for (int index = 0; index < 8; index++)
        {
            stepped = stepped.Advance(1).World;
        }

        VoyageWorldSnapshot batchedSnapshot = batched.Snapshot();
        VoyageWorldSnapshot steppedSnapshot = stepped.Snapshot();
        Equal(batchedSnapshot.Tick, steppedSnapshot.Tick, "Render cadence changed the committed tick.");
        Equal(batchedSnapshot.Ships[0].Position, steppedSnapshot.Ships[0].Position,
            "Render cadence changed fixed-point movement.");
        True(batchedSnapshot.RecentEvents.SequenceEqual(steppedSnapshot.RecentEvents),
            "Render cadence changed the committed event stream.");
        True(batchedSnapshot.RecentCommands.SequenceEqual(steppedSnapshot.RecentCommands),
            "Render cadence changed the replay command stream.");
    }

    private static void TacticalBoardAndEncounterCleanupAreBounded()
    {
        (TacticalBoard board, CellId entry, CellId exit) = CreateBoard();
        ActorId playerId = new("actor.first-voyage.scout");
        ActorId secondPlayerId = new("actor.first-voyage.engineer");
        ActorId thirdPlayerId = new("actor.first-voyage.medic");
        ActorId fourthPlayerId = new("actor.first-voyage.envoy");
        ActorId hostileId = new("actor.ruin.sentinel");
        board = board.Place(playerId, entry).Place(secondPlayerId, entry).Place(thirdPlayerId, entry)
            .Place(fourthPlayerId, entry).Place(hostileId, exit);
        Equal(3, board.FindPath(entry, exit, TacticalBoard.MaximumCells).Length, "Bounded hex path was not deterministic.");

        TeamId playerTeam = new("team.player");
        PersonalEncounterState encounter = new(
            new EncounterId("encounter.ruin.glass-observatory"),
            board,
            ImmutableDictionary<ActorId, PersonalActorState>.Empty
                .Add(playerId, Actor(playerId, playerTeam, entry))
                .Add(secondPlayerId, Actor(secondPlayerId, playerTeam, entry))
                .Add(thirdPlayerId, Actor(thirdPlayerId, playerTeam, entry))
                .Add(fourthPlayerId, Actor(fourthPlayerId, playerTeam, entry))
                .Add(hostileId, Actor(hostileId, new TeamId("team.ruin.sentinels"), exit) with { Surrendered = true }),
            ImmutableDictionary<ObjectiveId, ObjectiveState>.Empty.Add(new ObjectiveId("objective.ruin.extract-relic"), ObjectiveState.Active),
            ImmutableHashSet<ContentId>.Empty.Add(new ContentId("exploration.ruin.console-restored")),
            ImmutableHashSet<ContentId>.Empty.Add(new ContentId("object.ruin.ancient-defense")),
            false,
            false);
        PersonalEncounterState cleaned = EncounterLifecycle.Cleanup(encounter, playerTeam);
        True(cleaned.CleanedUp, "Resolved opposition did not permit encounter cleanup.");
        True(cleaned.Actors[hostileId].Prisoner, "A surrendered hostile was not retained as a prisoner consequence.");
        Equal(encounter.ExplorationChanges, cleaned.ExplorationChanges, "Cleanup discarded exploration changes.");
        Equal(encounter.DamagedObjects, cleaned.DamagedObjects, "Cleanup discarded damaged objects.");
    }

    private static void ShipLoadoutPowerAndDamageAreAtomic()
    {
        ShipState ship = CreateShip(new ShipId("ship.first-voyage.atomic"), new TeamId("team.player"), "ship.path.arcane");
        NetworkId network = new("network.aether");
        PowerAllocationResult powered = ShipPowerSystem.Allocate(ship, network, [.. ship.Modules.Select(value => value.InstanceId)]);
        True(powered.Ship.Modules.Any(value => value.Definition.MountId == new ContentId("mount.weapon") && value.IsPowered),
            "A valid priority list did not power the weapon battery.");

        InstalledModuleState selected = powered.Ship.Modules.Single(value => value.Definition.MountId == new ContentId("mount.weapon"));
        ShipDamageResult damaged = ShipDamageSystem.Apply(powered.Ship, 9, 20, selected.InstanceId);
        Equal(powered.Ship.Hull - 9, damaged.Ship.Hull, "Atomic damage publication charged the wrong hull damage.");
        True(damaged.Ship.Modules.Single(value => value.InstanceId == selected.InstanceId).Condition != ModuleCondition.Intact,
            "Selected module damage was not committed with hull damage.");
        Equal(ship.Hull, powered.Ship.Hull, "Damage mutated an earlier published ship snapshot.");

        ShipLoadoutResult invalid = ShipLoadoutSystem.Create(
            new ShipId("ship.first-voyage.invalid"),
            ship.TeamId,
            ship.Frame,
            new ContentId("ship.path.industrial"),
            ship.Modules.Select(value => value.Definition),
            ship.Modules.Single(value => value.Weapon is not null).Weapon!,
            ship.Resources);
        False(invalid.Accepted, "An incompatible technology-path loadout was published.");
    }

    private static void PersonalReactionsAndConsequencesPersist()
    {
        (TacticalBoard board, CellId entry, CellId exit) = CreateBoard();
        ActorId defenderId = new("actor.first-voyage.defender");
        ActorId attackerId = new("actor.ruin.attacker");
        PersonalActorState defender = Actor(defenderId, new TeamId("team.player"), entry) with
        {
            ActionPoints = 3,
            ReservedReactionPoints = 1,
            ReactionExpiresTick = 20,
        };
        PersonalActorState attacker = Actor(attackerId, new TeamId("team.ruin.sentinels"), exit) with { ActionPoints = 3 };
        PersonalEncounterState encounter = new(
            new EncounterId("encounter.ruin.glass-observatory"),
            board.Place(defenderId, entry).Place(attackerId, exit),
            ImmutableDictionary<ActorId, PersonalActorState>.Empty.Add(defenderId, defender).Add(attackerId, attacker),
            ImmutableDictionary<ObjectiveId, ObjectiveState>.Empty.Add(new ObjectiveId("objective.ruin.disable-defense"), ObjectiveState.Active),
            ImmutableHashSet<ContentId>.Empty,
            ImmutableHashSet<ContentId>.Empty,
            false,
            false);
        ShipState ship = CreateShip(new ShipId("ship.first-voyage.personal"), defender.TeamId, "ship.path.arcane");
        VoyageWorld world = VoyageWorld.Create(17, new ContentFingerprint(new string('b', 64)), defender.TeamId, [ship], encounter) with
        {
            ShipPaused = false,
            ReadyActors = [attackerId],
        };
        VoyageCommand attack = Command("command.test.reaction", VoyageCommandKind.PersonalRanged, attackerId.Value, defenderId.Value, 10, 1) with { Amount = 8 };
        world = world.Enqueue(attack).World.CommitReadyPlan().Advance(2).World;
        PersonalActorState after = world.PersonalEncounter!.Actors[defenderId];
        Equal(6, after.Health, "A reserved reaction did not mitigate the committed attack.");
        Equal(0, after.ReservedReactionPoints, "A reaction was not consumed atomically.");
        True(world.Events.Any(value => value.Kind == VoyageCommandKind.PersonalRanged && value.Succeeded),
            "Committed personal action was not preserved in the replay event stream.");
    }

    private static VoyageCommand Command(
        string id,
        VoyageCommandKind kind,
        ContentId issuer,
        ContentId target,
        int priority,
        ulong sequence) => new(
            new ContentId(id),
            kind,
            1,
            priority,
            issuer,
            target,
            new FixedVector2(FixedScalar.FromInt(1), FixedScalar.FromInt(0)),
            0,
            null,
            sequence);

    private static ShipState CreateShip(ShipId id, TeamId teamId, string path)
    {
        ContentId pathId = new(path);
        ShipFrameDefinition frame = new(
            new ShipFrameId("frame.test.wayfarer"), 1, 1, "frame.test.name", "frame.test.description",
            30, 1, 12, 12, [new ContentId("mount.power"), new ContentId("mount.weapon"), new ContentId("mount.shield")]);
        ShipModuleDefinition generator = Module("module.test.generator", "mount.power", "network.aether", 3, 10, 0, 0, pathId);
        ShipModuleDefinition battery = Module("module.test.battery", "mount.weapon", "network.aether", 2, 0, 1, 0, pathId);
        ShipModuleDefinition shield = Module("module.test.shield", "mount.shield", "network.aether", 2, 0, 0, 10, pathId);
        ShipWeaponConfigurationDefinition weapon = new(
            new ShipWeaponConfigurationId("ship.weapon.test.cannon"), 1, 1, "ship.weapon.test.name", "ship.weapon.test.description",
            new NetworkId("network.aether"), new ResourceId("resource.aether-charge"), 2, 8, 4, 10_000, 30_000, 4,
            new ContentId("damage.arcane"), new ContentId("area.single-target"), 2);
        ShipLoadoutResult result = ShipLoadoutSystem.Create(
            id, teamId, frame, pathId, [generator, battery, shield], weapon,
            ImmutableDictionary<ResourceId, int>.Empty
                .Add(new ResourceId("resource.aether-charge"), 8)
                .Add(new ResourceId("resource.spare-parts"), 2));
        True(result.Accepted, result.RejectionCode);
        return result.Ship!;
    }

    private static ShipModuleDefinition Module(
        string id,
        string mount,
        string network,
        int slots,
        int generated,
        int consumed,
        int shield,
        ContentId path) => new(
            new ModuleId(id), 1, 1, $"{id}.name", $"{id}.description", slots, 0, 10,
            new NetworkId(network), generated, consumed, new ContentId(mount), new ContentId("effect.ship.test"),
            0, shield, shield > 0 ? 2 : 0, shield > 0 ? 2 : 0, [path]);

    private static (TacticalBoard Board, CellId Entry, CellId Exit) CreateBoard()
    {
        CellId entry = new("cell.test.entry");
        CellId middle = new("cell.test.middle");
        CellId exit = new("cell.test.exit");
        BoardCellDefinition[] cells =
        [
            Cell(entry, "zone.test.entry", 0),
            Cell(middle, "zone.test.middle", 1),
            Cell(exit, "zone.test.exit", 2),
        ];
        ZoneLinkDefinition[] links =
        [
            Link("link.test.entry-middle", entry, middle, 1),
            Link("link.test.middle-exit", middle, exit, 1),
        ];
        PersonalBoardDefinition definition = new(
            new PersonalBoardId("board.test.encounter"), 1, 1, "board.test.name", "board.test.description", 8,
            [entry, middle, exit], [.. links.Select(value => value.LinkId)], [new ObjectiveId("objective.test.finish")], [entry, exit]);
        BoardValidationResult result = TacticalBoard.Create(definition, cells, links);
        True(result.Accepted, result.RejectionCode);
        return (result.Board!, entry, exit);
    }

    private static BoardCellDefinition Cell(CellId id, string zone, int q) => new(
        id, 1, 1, $"{id}.name", $"{id}.description", new ZoneId(zone), q, 0, 4, 20, 100,
        new ContentId("atmosphere.breathable"), new ContentId("gravity.standard"), []);

    private static ZoneLinkDefinition Link(string id, CellId from, CellId to, int retreat) => new(
        new LinkId(id), 1, 1, $"{id}.name", $"{id}.description", from, to, new ContentId("traversal.open"), 0, retreat);

    private static PersonalActorState Actor(ActorId id, TeamId team, CellId cell) => new(
        id, team, null, cell, 0, 100, 0, 10, false, false, false,
        PersonalLoadout.Create([]), []);

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
        }
    }
}
