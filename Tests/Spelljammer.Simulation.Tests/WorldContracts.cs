using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Ships;
using Spelljammer.Simulation.Effects;
using Spelljammer.Simulation.World;

public sealed partial class SimulationContracts
{
    private static readonly WorldTimeDefinition TestWorldTime = new(
        new WorldTimeId("world-time.test.standard"), 1, 1,
        "world-time.test.standard.name", "world-time.test.standard.description", 20, 8);
    private static readonly CalendarDefinition TestCalendar = new(
        new CalendarId("calendar.test.standard"), 1, 1,
        "calendar.test.standard.name", "calendar.test.standard.description",
        60, 60, 24, 7, 7421, 1, 1, 0, 0, 0, 0,
        [.. Enumerable.Range(1, 12).Select(index => new CalendarMonthDefinition(
            new CalendarMonthId($"calendar.month.test-{index}"), $"calendar.month.test-{index}.name", 30))]);
    private static readonly TimeScaleDefinition TestTimeScale = new(
        new TimeScaleId("time-scale.test.tactical"), 1, 1,
        "time-scale.test.tactical.name", "time-scale.test.tactical.description", 1, 20);

    private static void CampaignClockAndCalendarAreDeterministic()
    {
        CampaignClockState initial = CampaignClockState.Create(TestCalendar.CalendarId, TestTimeScale.TimeScaleId);
        CampaignClockState batched = CampaignClockSystem.Advance(initial, TestTimeScale, 20);
        CampaignClockState stepped = initial;
        for (int index = 0; index < 20; index++)
        {
            stepped = CampaignClockSystem.Advance(stepped, TestTimeScale, 1);
        }

        Equal(new CampaignClockState(1, 0, TestCalendar.CalendarId, TestTimeScale.TimeScaleId), batched,
            "Twenty tactical ticks did not advance exactly one world second.");
        Equal(batched, stepped, "Batched clock advancement changed the deterministic remainder.");

        WorldDateTime opening = WorldTimeQueries.GetDateTime(initial, TestCalendar);
        Equal(7421L, opening.Year, "The authored EAC opening year was not used.");
        Equal(1, opening.Month, "The authored EAC opening month was not used.");
        Equal(1, opening.Day, "The authored EAC opening day was not used.");
        Equal(0, opening.Hour, "The authored EAT opening hour was not used.");
        Equal(0, opening.Minute, "The authored EAT opening minute was not used.");
        Equal(0, opening.Second, "The authored EAT opening second was not used.");

        WorldDateTime lastSecondOfMinute = WorldTimeQueries.GetDateTime(
            initial with { ElapsedWorldSeconds = 59 }, TestCalendar);
        Equal(59, lastSecondOfMinute.Second, "EAT advanced before the sixtieth second.");
        WorldDateTime secondMinute = WorldTimeQueries.GetDateTime(
            initial with { ElapsedWorldSeconds = 60 }, TestCalendar);
        Equal(1, secondMinute.Minute, "Sixty EAT seconds did not advance one minute.");
        Equal(0, secondMinute.Second, "The second did not reset at the EAT minute boundary.");

        WorldDateTime lastSecondOfDay = WorldTimeQueries.GetDateTime(
            initial with { ElapsedWorldSeconds = TestCalendar.SecondsPerDay - 1 }, TestCalendar);
        Equal(23, lastSecondOfDay.Hour, "The EAT day did not end at hour 23.");
        Equal(59, lastSecondOfDay.Minute, "The EAT day did not end at minute 59.");
        Equal(59, lastSecondOfDay.Second, "The EAT day did not end at second 59.");
        WorldDateTime secondDay = WorldTimeQueries.GetDateTime(
            initial with { ElapsedWorldSeconds = TestCalendar.SecondsPerDay }, TestCalendar);
        Equal(2, secondDay.Day, "Twenty-four EAT hours did not advance one EAC day.");
        Equal(0, secondDay.Hour, "The EAT hour did not reset at the day boundary.");

        CampaignClockState secondMonth = initial with
        {
            ElapsedWorldSeconds = TestCalendar.SecondsPerDay * 30,
        };
        WorldDateTime monthBoundary = WorldTimeQueries.GetDateTime(secondMonth, TestCalendar);
        Equal(7421L, monthBoundary.Year, "Month rollover changed the campaign year.");
        Equal(2, monthBoundary.Month, "Calendar did not advance to the second authored month.");
        Equal(1, monthBoundary.Day, "Month rollover did not begin on day one.");

        CampaignClockState secondYear = initial with
        {
            ElapsedWorldSeconds = TestCalendar.SecondsPerDay * TestCalendar.DaysPerYear,
        };
        WorldDateTime yearBoundary = WorldTimeQueries.GetDateTime(secondYear, TestCalendar);
        Equal(7422L, yearBoundary.Year, "Calendar year rollover was not derived from authored months.");
        Equal(1, yearBoundary.Month, "Calendar year rollover did not return to the first month.");
        Equal(1, yearBoundary.Day, "Calendar year rollover did not begin on day one.");
    }

    private static void FixedTickCadenceAndOrderingAreDeterministic()
    {
        ShipState ship = CreateShip(new ShipId("ship.first-voyage.player"), new TeamId("team.player"), "ship.path.arcane");
        World initial = World.Create(
            0x5eedUL,
            new ContentFingerprint(new string('a', 64)),
            TestWorldTime,
            TestCalendar,
            TestTimeScale,
            ship.TeamId,
            [ship]);
        Equal(0, initial.Advance(8).AdvancedTicks, "Paused ship simulation advanced authoritative time.");
        World bounded = initial with
        {
            TimeDefinition = TestWorldTime with { MaximumCatchUpTicks = 2 },
            ShipPaused = false,
        };
        Equal(2, bounded.Advance(8).AdvancedTicks, "World ignored its data-driven catch-up limit.");

        WorldCommand second = CreateWorldCommand("command.test.second", WorldCommandKind.Course, ship.Id.Value, ship.Id.Value, 20, 2);
        WorldCommand first = CreateWorldCommand("command.test.first", WorldCommandKind.Course, ship.Id.Value, ship.Id.Value, 10, 1);
        World queued = initial.SetShipPause(false).Enqueue(second).World.Enqueue(first).World;
        Equal(first.Id, queued.Commands[0].Id, "Equal-tick commands were not stably priority ordered.");
        WorldCommandResult stale = queued.Enqueue(first with { Id = new ContentId("command.test.stale"), TargetTick = -1 });
        False(stale.Accepted, "A stale command entered the authoritative queue.");
        True(ReferenceEquals(queued, stale.World), "A rejected command replaced the world instance.");
        World prepared = queued.Advance(2).World;
        True(prepared.ScheduledActions.All(value => value.Phase == ScheduledActionPhase.Recovering &&
            value.History.Contains(ScheduledActionPhase.Reserved) && value.History.Contains(ScheduledActionPhase.Committed)),
            "Scheduled actions did not preserve their transaction phases.");

        World cancellationCandidate = initial.SetShipPause(false).Enqueue(first).World;
        WorldCommandResult cancelled = cancellationCandidate.Cancel(first.Id);
        True(cancelled.Accepted && cancelled.World.CommandHistory.Single().CancelledTick == 0,
            "Pre-commit cancellation was not retained in the replay log.");

        World batched = queued.Advance(8).World;
        World stepped = queued;
        for (int index = 0; index < 8; index++)
        {
            stepped = stepped.Advance(1).World;
        }

        WorldSnapshot batchedSnapshot = batched.Snapshot();
        WorldSnapshot steppedSnapshot = stepped.Snapshot();
        Equal(batchedSnapshot.Tick, steppedSnapshot.Tick, "Render cadence changed the committed tick.");
        Equal(batchedSnapshot.Clock, steppedSnapshot.Clock, "Render cadence changed campaign time.");
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
        BattleUnitId playerId = new("unit.first-voyage.scout");
        BattleUnitId secondPlayerId = new("unit.first-voyage.engineer");
        BattleUnitId thirdPlayerId = new("unit.first-voyage.medic");
        BattleUnitId fourthPlayerId = new("unit.first-voyage.envoy");
        BattleUnitId hostileId = new("unit.ruin.sentinel");
        board = board.Place(playerId, entry).Place(secondPlayerId, entry).Place(thirdPlayerId, entry)
            .Place(fourthPlayerId, entry).Place(hostileId, exit);
        Equal(3, board.FindPath(entry, exit, TacticalBoard.MaximumCells).Length, "Bounded hex path was not deterministic.");

        TeamId playerTeam = new("team.player");
        PersonalEncounterState encounter = new(
            new EncounterId("encounter.ruin.glass-observatory"),
            board,
            ImmutableDictionary<BattleUnitId, BattleUnitState>.Empty
                .Add(playerId, Unit(playerId, playerTeam, entry))
                .Add(secondPlayerId, Unit(secondPlayerId, playerTeam, entry))
                .Add(thirdPlayerId, Unit(thirdPlayerId, playerTeam, entry))
                .Add(fourthPlayerId, Unit(fourthPlayerId, playerTeam, entry))
                .Add(hostileId, Unit(hostileId, new TeamId("team.ruin.sentinels"), exit) with { Surrendered = true }),
            ImmutableDictionary<ObjectiveId, ObjectiveState>.Empty.Add(new ObjectiveId("objective.ruin.extract-relic"), ObjectiveState.Active),
            ImmutableHashSet<ContentId>.Empty.Add(new ContentId("exploration.ruin.console-restored")),
            ImmutableHashSet<ContentId>.Empty.Add(new ContentId("object.ruin.ancient-defense")),
            false,
            false);
        PersonalEncounterState cleaned = EncounterLifecycle.Cleanup(encounter, playerTeam);
        True(cleaned.CleanedUp, "Resolved opposition did not permit encounter cleanup.");
        True(cleaned.Units[hostileId].Prisoner, "A surrendered hostile was not retained as a prisoner consequence.");
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

    private static void PersonalCombatResolutionIsCommittedAtomically()
    {
        (TacticalBoard board, CellId entry, CellId exit) = CreateBoard();
        BattleUnitId defenderId = new("unit.first-voyage.defender");
        BattleUnitId attackerId = new("unit.ruin.attacker");
        BattleUnitState defender = Unit(defenderId, new TeamId("team.player"), entry) with {
            Turn = ActorTurn() with { CurrentActionPoints = 10 },
            ReservedReactionPoints = 1,
            ReactionExpiresTick = 20,
        };
        BattleUnitState attacker = Unit(attackerId, new TeamId("team.ruin.sentinels"), exit) with {
            Turn = ActorTurn() with { CurrentActionPoints = 10 },
        };
        PersonalEncounterState encounter = new(
            new EncounterId("encounter.ruin.glass-observatory"),
            board.Place(defenderId, entry).Place(attackerId, exit),
            ImmutableDictionary<BattleUnitId, BattleUnitState>.Empty.Add(defenderId, defender).Add(attackerId, attacker),
            ImmutableDictionary<ObjectiveId, ObjectiveState>.Empty.Add(new ObjectiveId("objective.ruin.disable-defense"), ObjectiveState.Active),
            ImmutableHashSet<ContentId>.Empty,
            ImmutableHashSet<ContentId>.Empty,
            false,
            false);
        ShipState ship = CreateShip(new ShipId("ship.first-voyage.personal"), defender.TeamId, "ship.path.arcane");
        World world = World.Create(
            17,
            new ContentFingerprint(new string('b', 64)),
            TestWorldTime,
            TestCalendar,
            TestTimeScale,
            defender.TeamId,
            [ship],
            encounter) with {
            ShipPaused = false,
            ReadyUnits = [attackerId],
        };
        WorldCommand attack = CreateWorldCommand("command.test.reaction", WorldCommandKind.PersonalRanged, attackerId.Value, defenderId.Value, 10, 1) with { Amount = 8 };
        World unresolved = world.Enqueue(attack).World.CommitReadyPlan().Advance(2).World;
        Equal(10, unresolved.PersonalEncounter!.Units[defenderId].Health,
            "A combat command without the domain resolver changed target health.");
        True(unresolved.Events.Any(value =>
                value.Kind == WorldCommandKind.PersonalRanged &&
                !value.Succeeded &&
                value.ResultCode == "command.personal-combat-resolver-required"),
            "A missing combat resolver did not produce the stable rejection event.");

        world = world.Enqueue(attack).World.CommitReadyPlan().Advance(2, new ReactionCombatResolver()).World;
        BattleUnitState after = world.PersonalEncounter!.Units[defenderId];
        Equal(6, after.Health, "A reserved reaction did not mitigate the committed attack.");
        Equal(0, after.ReservedReactionPoints, "A reaction was not consumed atomically.");
        True(world.Events.Any(value => value.Kind == WorldCommandKind.PersonalRanged && value.Succeeded),
            "Committed personal action was not preserved in the replay event stream.");
    }

    private sealed class ReactionCombatResolver : IPersonalCombatResolver
    {
        public PersonalCombatResolution Resolve(PersonalCombatContext context)
        {
            int damage = Math.Max(1, context.Command.Amount);
            BattleUnitState target = context.Target;
            if (target.ReservedReactionPoints > 0 && target.ReactionExpiresTick >= context.Tick)
            {
                damage = Math.Max(1, damage / 2);
                target = target with { ReservedReactionPoints = 0, ReactionExpiresTick = 0 };
            }

            int healthDamage = target.Defending ? Math.Max(1, damage / 2) : damage;
            target = target with {
                CharacterResources = target.CharacterResources.ApplyResourceDamage(
                    CharacterResourceIds.Health,
                    healthDamage),
            };
            BattleUnitState actor = context.Actor with {
                Turn = context.Actor.Turn.SpendActionPoints(5),
            };
            return new PersonalCombatResolution(true, string.Empty, actor, target, healthDamage);
        }
    }

    private static WorldCommand CreateWorldCommand(
        string id,
        WorldCommandKind kind,
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

    private static BattleUnitState Unit(BattleUnitId id, TeamId team, CellId cell) => new(
        id, team, null, cell, ActorTurn(), ActorResources(), false, false, false,
        new ItemSystemState([], [], []), []);

    private static CharacterTurnState ActorTurn() => CharacterTurnState.Create(new CharacterTurnRules(
        100,
        10,
        10,
        100,
        [new StaminaTurnMeterRule(50, 90), new StaminaTurnMeterRule(25, 80)],
        new Dictionary<ContentId, int> {
            [new ContentId("action.personal.defend")] = 4,
            [new ContentId("action.personal.engineering")] = 5,
            [new ContentId("action.personal.interact")] = 3,
            [new ContentId("action.personal.medicine")] = 3,
            [new ContentId("action.personal.melee")] = 5,
            [new ContentId("action.personal.move")] = 2,
            [new ContentId("action.personal.psionic")] = 5,
            [new ContentId("action.personal.ranged")] = 5,
            [new ContentId("action.personal.reserve-reaction")] = 4,
            [new ContentId("action.personal.retreat")] = 2,
            [new ContentId("action.personal.spell")] = 6,
            [new ContentId("action.personal.surrender")] = 1,
        }.ToImmutableDictionary()));

    private static CharacterResourceSet ActorResources() => CharacterResourceSet.Restore(
    [
        Resource(CharacterResourceIds.Health, 10, 10, 0, false),
        Resource(CharacterResourceIds.Stamina, 100, 100, 5, false),
        Resource(CharacterResourceIds.Mana, 100, 100, 1, false),
        Resource(CharacterResourceIds.Resolve, 100, 100, 2, false),
        Resource(CharacterResourceIds.Strain, 0, 100, 3, true),
    ]);

    private static CharacterResourceState Resource(ResourceId id, int current, int maximum, int recovery, bool accumulates) =>
        new(id, current, maximum, recovery, accumulates, [], []);
}
