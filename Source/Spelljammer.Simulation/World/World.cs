using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Ships;

namespace Spelljammer.Simulation.World;

public sealed record World(
    ulong Seed,
    ContentFingerprint ContentFingerprint,
    WorldTimeDefinition TimeDefinition,
    CalendarDefinition Calendar,
    TimeScaleDefinition TimeScale,
    CampaignClockState Clock,
    long Tick,
    ulong RandomSequence,
    TeamId PlayerTeamId,
    bool ShipPaused,
    bool PersonalPaused,
    ImmutableDictionary<ShipId, ShipState> Ships,
    PersonalEncounterState? PersonalEncounter,
    ImmutableArray<WorldCommand> Commands,
    ImmutableArray<WorldCommandLogEntry> CommandHistory,
    ImmutableArray<ScheduledAction> ScheduledActions,
    ImmutableArray<ActorId> ReadyActors,
    ImmutableArray<WorldEvent> Events)
{
    public const int MaximumCommands = 256;
    public const int MaximumCommandHistory = 512;
    public const int MaximumSchedules = 256;
    public const int MaximumEvents = 512;
    public const int MaximumReadyActors = 64;

    public static World Create(
        ulong seed,
        ContentFingerprint fingerprint,
        WorldTimeDefinition timeDefinition,
        CalendarDefinition calendar,
        TimeScaleDefinition timeScale,
        TeamId playerTeamId,
        IEnumerable<ShipState> ships,
        PersonalEncounterState? encounter = null)
    {
        ArgumentNullException.ThrowIfNull(timeDefinition);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(timeScale);
        ImmutableDictionary<ShipId, ShipState> shipMap = ships.ToImmutableDictionary(value => value.Id);
        if (timeDefinition.TicksPerSecond <= 0 || timeDefinition.MaximumCatchUpTicks <= 0 ||
            calendar.SecondsPerMinute <= 0 || calendar.MinutesPerHour <= 0 || calendar.HoursPerDay <= 0 ||
            calendar.DaysPerWeek <= 0 || calendar.Months.IsDefaultOrEmpty ||
            timeScale.WorldSecondsNumerator <= 0 || timeScale.SimulationTicksDenominator <= 0 ||
            shipMap.Count is 0 or > 32 || encounter?.Actors.Count > MaximumReadyActors)
        {
            throw new InvalidOperationException("World configuration or capacity is invalid.");
        }

        return new World(
            seed,
            fingerprint,
            timeDefinition,
            calendar,
            timeScale,
            CampaignClockState.Create(calendar.CalendarId, timeScale.TimeScaleId),
            0,
            0,
            playerTeamId,
            true,
            false,
            shipMap,
            encounter,
            [],
            [],
            [],
            [],
            []);
    }

    public World SetShipPause(bool paused) => this with { ShipPaused = paused };

    public World CommitReadyPlan() => this with { PersonalPaused = false };

    public WorldCommandResult Enqueue(WorldCommand command)
    {
        if (command.TargetTick < Tick)
        {
            return Rejected(this, "command.tick-stale");
        }

        if (Commands.Length >= MaximumCommands || CommandHistory.Length >= MaximumCommandHistory ||
            Commands.Any(value => value.Id == command.Id) || CommandHistory.Any(value => value.Command.Id == command.Id))
        {
            return Rejected(this, "command.queue-capacity");
        }

        if (!TargetExists(command))
        {
            return Rejected(this, "command.target-stale");
        }

        if (IsPersonal(command.Kind) && !CanSubmitPersonal(command.IssuerId))
        {
            return Rejected(this, "command.actor-not-ready");
        }

        ImmutableArray<WorldCommand> queued =
            [.. Commands.Append(command).OrderBy(value => value.TargetTick).ThenBy(value => value.Priority)
                .ThenBy(value => value.IssuerId).ThenBy(value => value.Sequence).ThenBy(value => value.Id)];
        return new WorldCommandResult(this with
        {
            Commands = queued,
            CommandHistory = CommandHistory.Add(new WorldCommandLogEntry(Tick, command, null)),
        }, true, string.Empty);
    }

    public WorldCommandResult Cancel(ContentId commandId)
    {
        WorldCommand? queued = Commands.FirstOrDefault(value => value.Id == commandId);
        if (queued is not null)
        {
            return new WorldCommandResult(this with
            {
                Commands = Commands.Remove(queued),
                CommandHistory = MarkCancelled(CommandHistory, commandId, Tick),
            }, true, string.Empty);
        }

        ScheduledAction? schedule = ScheduledActions.FirstOrDefault(value => value.Command.Id == commandId);
        if (schedule is null || schedule.Phase >= ScheduledActionPhase.Committed)
        {
            return Rejected(this, "command.cancellation-too-late");
        }

        ScheduledAction interrupted = schedule with { Phase = ScheduledActionPhase.Interrupted };
        return new WorldCommandResult(this with
        {
            ScheduledActions = ScheduledActions.Replace(schedule, interrupted),
            CommandHistory = MarkCancelled(CommandHistory, commandId, Tick),
        }, true, string.Empty);
    }

    public WorldAdvanceResult Advance(
        int requestedTicks,
        IPersonalCombatResolver? personalCombatResolver = null)
    {
        int ticks = Math.Clamp(requestedTicks, 0, TimeDefinition.MaximumCatchUpTicks);
        World world = this;
        int advanced = 0;
        for (int index = 0; index < ticks; index++)
        {
            if (world.ShipPaused || world.PersonalPaused)
            {
                break;
            }

            world = world.AdvanceOneTick(personalCombatResolver);
            advanced++;
        }

        return new WorldAdvanceResult(world, world.Snapshot(), advanced);
    }

    public WorldSnapshot Snapshot() => new(
        Seed,
        ContentFingerprint,
        TimeDefinition,
        Calendar,
        TimeScale,
        Clock,
        Tick,
        ShipPaused,
        PersonalPaused,
        [.. Ships.Values.OrderBy(value => value.Id)],
        PersonalEncounter,
        ReadyActors,
        ScheduledActions,
        CommandHistory.Length <= 64 ? CommandHistory : CommandHistory[^64..],
        Events.Length <= 64 ? Events : Events[^64..]);

    private World AdvanceOneTick(IPersonalCombatResolver? personalCombatResolver)
    {
        long nextTick = Tick + 1;
        World world = this with
        {
            Tick = nextTick,
            Clock = CampaignClockSystem.Advance(Clock, TimeScale, 1),
        };
        WorldCommand[] due = [.. world.Commands.Where(value => value.TargetTick <= nextTick)];
        world = world with { Commands = [.. world.Commands.Except(due)] };
        foreach (WorldCommand command in due)
        {
            world = world.DeclareAndReserve(command);
        }

        foreach (ScheduledAction schedule in world.ScheduledActions.OrderBy(value => value.CommitTick)
                     .ThenBy(value => value.Command.Priority).ThenBy(value => value.Command.IssuerId)
                     .ThenBy(value => value.Command.Sequence).ToArray())
        {
            if (schedule.Phase is ScheduledActionPhase.Interrupted or ScheduledActionPhase.Completed)
            {
                world = world with { ScheduledActions = world.ScheduledActions.Remove(schedule) };
                continue;
            }

            if (schedule.RecoverTick <= nextTick)
            {
                int completionIndex = IndexOf(world.ScheduledActions, value => value.Command.Id == schedule.Command.Id);
                if (completionIndex >= 0)
                {
                    world = world with
                    {
                        ScheduledActions = world.ScheduledActions.SetItem(completionIndex, schedule with
                        {
                            Phase = ScheduledActionPhase.Completed,
                            History = schedule.History.Add(ScheduledActionPhase.Completed),
                        }),
                    };
                }

                continue;
            }

            if (schedule.CommitTick <= nextTick && schedule.Phase < ScheduledActionPhase.Committed)
            {
                world = world.Commit(schedule, personalCombatResolver);
            }
        }

        world = world.UpdateShips();
        world = world.UpdatePersonalTimeline();
        return world;
    }

    private World DeclareAndReserve(WorldCommand command)
    {
        if (ScheduledActions.Length >= MaximumSchedules)
        {
            return AddEvent(command, false, 0, "command.queue-capacity");
        }

        ResourceId? resourceId = null;
        int reserved = 0;
        if (command.Kind == WorldCommandKind.Fire && TryShip(command.IssuerId, out ShipState? ship))
        {
            InstalledModuleState? battery = ship!.Modules.SingleOrDefault(value => value.Weapon is not null);
            if (battery?.Weapon is null || battery.Condition == ModuleCondition.Disabled ||
                battery.WeaponReadiness != WeaponReadiness.Ready || battery.ReadyTick > Tick)
            {
                return AddEvent(command, false, 0, "command.weapon-not-ready");
            }

            resourceId = battery.Weapon.ResourceId;
            reserved = battery.Weapon.ResourceCost;
            ship.Resources.TryGetValue(resourceId.Value, out int available);
            if (available < reserved)
            {
                return AddEvent(command, false, 0, "command.resource-insufficient");
            }
        }

        ScheduledAction action = new(
            command,
            ScheduledActionPhase.Preparing,
            Tick + 1,
            Tick + 2,
            resourceId,
            reserved,
            [ScheduledActionPhase.Declared, ScheduledActionPhase.Validated, ScheduledActionPhase.Reserved, ScheduledActionPhase.Preparing]);
        return this with { ScheduledActions = ScheduledActions.Add(action) };
    }

    private World Commit(
        ScheduledAction schedule,
        IPersonalCombatResolver? personalCombatResolver)
    {
        World committed = IsPersonal(schedule.Command.Kind)
            ? CommitPersonal(schedule.Command, personalCombatResolver)
            : CommitShip(schedule.Command, schedule.ReservedResourceId, schedule.ReservedAmount);
        int index = IndexOf(committed.ScheduledActions, value => value.Command.Id == schedule.Command.Id);
        if (index >= 0)
        {
            committed = committed with
            {
                ScheduledActions = committed.ScheduledActions.SetItem(index, schedule with
                {
                    Phase = ScheduledActionPhase.Recovering,
                    History = schedule.History.Add(ScheduledActionPhase.Committed).Add(ScheduledActionPhase.Recovering),
                }),
            };
        }

        return committed;
    }

    private World CommitShip(WorldCommand command, ResourceId? reservedResource, int reservedAmount)
    {
        if (!TryShip(command.IssuerId, out ShipState? actor))
        {
            return AddEvent(command, false, 0, "command.actor-missing");
        }

        ShipState ship = actor!;
        switch (command.Kind)
        {
            case WorldCommandKind.Scan:
                if (!TryShip(command.TargetId, out ShipState? scanned))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                ShipContactState contact = new(
                    scanned!.Id,
                    new ContentId("knowledge.contact.scanned"),
                    Tick,
                    ShipGeometry.Range(ship.Position, scanned.Position) != ShipRange.Beyond,
                    command.OptionId is ContentId witnessId && ActorIdFrom(witnessId, out ActorId witness)
                        ? ImmutableHashSet.Create(witness)
                        : ImmutableHashSet<ActorId>.Empty);
                ship = ship with { Contacts = ship.Contacts.SetItem(scanned.Id, contact) };
                break;
            case WorldCommandKind.Course:
            case WorldCommandKind.Thrust:
                ship = ship with { Velocity = command.Vector };
                break;
            case WorldCommandKind.Turn:
                ship = ship with { HeadingMilliDegrees = NormalizeHeading(ship.HeadingMilliDegrees + command.Amount) };
                break;
            case WorldCommandKind.Brake:
                ship = ship with { Velocity = FixedVector2.Zero };
                break;
            case WorldCommandKind.Intercept:
                if (!TryShip(command.TargetId, out ShipState? intercepted))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                ship = ship with { Velocity = Direction(ship.Position, intercepted!.Position) };
                break;
            case WorldCommandKind.Fire:
                return CommitFire(command, ship, reservedResource, reservedAmount);
            case WorldCommandKind.Ram:
                return CommitRam(command, ship);
            case WorldCommandKind.RaiseShield:
            case WorldCommandKind.LowerShield:
                bool raised = command.Kind == WorldCommandKind.RaiseShield;
                ship = ship with
                {
                    Modules = [.. ship.Modules.Select(value => value.Definition.ShieldValue > 0 ? value with { ShieldRaised = raised } : value)],
                };
                break;
            case WorldCommandKind.Defend:
                ship = ship with { Defending = true };
                break;
            case WorldCommandKind.DamageControl:
                ship = RepairModule(ship, command.OptionId);
                break;
            case WorldCommandKind.Signal:
                ship = ship with { PersistentEvidence = ship.PersistentEvidence.Add(new ContentId("evidence.ship.signal")) };
                break;
            case WorldCommandKind.Retreat:
                ship = ship with { Disengaged = true, PersistentEvidence = ship.PersistentEvidence.Add(new ContentId("evidence.ship.escape")) };
                break;
            default:
                return AddEvent(command, false, 0, "command.action-unknown");
        }

        return (this with { Ships = Ships.SetItem(ship.Id, ship) }).AddEvent(command, true, command.Amount, string.Empty);
    }

    private World CommitFire(WorldCommand command, ShipState attacker, ResourceId? resourceId, int resourceCost)
    {
        if (!TryShip(command.TargetId, out ShipState? target) || resourceId is null ||
            !attacker.Contacts.TryGetValue(target!.Id, out ShipContactState? contact) || !contact.HasFiringSolution)
        {
            return AddEvent(command, false, 0, "command.firing-solution-required");
        }

        int batteryIndex = IndexOf(attacker.Modules, value => value.Weapon is not null);
        InstalledModuleState battery = attacker.Modules[batteryIndex];
        ShipWeaponConfigurationDefinition weapon = battery.Weapon!;
        long distance = Math.Max(
            Math.Abs(attacker.Position.X.Raw - target.Position.X.Raw),
            Math.Abs(attacker.Position.Y.Raw - target.Position.Y.Raw));
        if (distance > weapon.MaximumRange)
        {
            return AddEvent(command, false, 0, "command.target-out-of-range");
        }

        attacker.Resources.TryGetValue(resourceId.Value, out int available);
        if (available < resourceCost)
        {
            return AddEvent(command, false, 0, "command.resource-insufficient");
        }

        ContentId moduleTarget = target.Modules.OrderBy(value => value.InstanceId).First().InstanceId;
        ShipDamageResult damage = ShipDamageSystem.Apply(target, weapon.Damage, weapon.ArmorPenetration, moduleTarget);
        attacker = attacker with
        {
            Resources = attacker.Resources.SetItem(resourceId.Value, available - resourceCost),
            Modules = attacker.Modules.SetItem(batteryIndex, battery with
            {
                WeaponReadiness = WeaponReadiness.Reloading,
                ReadyTick = Tick + Math.Max(weapon.ReloadTicks, weapon.RateOfFireTicks),
            }),
            PersistentEvidence = attacker.PersistentEvidence.Add(new ContentId("evidence.ship.weapon-fired")),
        };
        return (this with
        {
            Ships = Ships.SetItem(attacker.Id, attacker).SetItem(target.Id, damage.Ship),
        }).AddEvent(command, true, damage.Event.HullDamage, string.Empty);
    }

    private World CommitRam(WorldCommand command, ShipState attacker)
    {
        if (!TryShip(command.TargetId, out ShipState? target) || ShipGeometry.Range(attacker.Position, target!.Position) != ShipRange.Contact)
        {
            return AddEvent(command, false, 0, "command.target-out-of-range");
        }

        ShipDamageResult targetDamage = ShipDamageSystem.Apply(target, Math.Max(1, command.Amount), 2, target.Modules[0].InstanceId);
        ShipDamageResult selfDamage = ShipDamageSystem.Apply(attacker, Math.Max(1, command.Amount / 2), 0, attacker.Modules[0].InstanceId);
        return (this with
        {
            Ships = Ships.SetItem(attacker.Id, selfDamage.Ship).SetItem(target.Id, targetDamage.Ship),
        }).AddEvent(command, true, targetDamage.Event.HullDamage, string.Empty);
    }

    private World CommitPersonal(
        WorldCommand command,
        IPersonalCombatResolver? personalCombatResolver)
    {
        if (PersonalEncounter is null || !ActorIdFrom(command.IssuerId, out ActorId actorId) ||
            !PersonalEncounter.Actors.TryGetValue(actorId, out PersonalActorState? actor) ||
            actor.ActionPoints <= 0 || actor.IsIncapacitated)
        {
            return AddEvent(command, false, 0, "command.actor-not-ready");
        }

        PersonalEncounterState encounter = PersonalEncounter;
        PersonalActorState updated = actor;
        int eventAmount = command.Amount;
        if (command.Kind == WorldCommandKind.PersonalEndActivation)
        {
            updated = updated with { Turn = updated.Turn.EndActivation() };
            encounter = encounter with { Actors = encounter.Actors.SetItem(actorId, updated) };
            ImmutableArray<ActorId> remaining = ReadyActors.Remove(actorId);
            bool remainPaused = remaining.Any(id => encounter.Actors[id].TeamId == PlayerTeamId);
            return (this with { PersonalEncounter = encounter, ReadyActors = remaining, PersonalPaused = remainPaused })
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
                    encounter = encounter with { Board = encounter.Board.Move(actorId, destination, TacticalBoard.MaximumCells) };
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
                updated = updated with
                {
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
                if (!ActorIdFrom(command.TargetId, out ActorId patientId) || !encounter.Actors.TryGetValue(patientId, out PersonalActorState? patient))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                encounter = encounter with
                {
                    Actors = encounter.Actors.SetItem(patientId, patient with
                    {
                        Injuries = [.. patient.Injuries.Select(value => value with { Stabilized = true })],
                    }),
                };
                break;
            case WorldCommandKind.PersonalInteract:
                if (command.OptionId is ContentId objectiveId && ObjectiveIdFrom(objectiveId, out ObjectiveId objective) &&
                    encounter.Objectives.ContainsKey(objective))
                {
                    encounter = encounter with
                    {
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
                if (!ActorIdFrom(command.TargetId, out ActorId targetId) || !encounter.Actors.TryGetValue(targetId, out PersonalActorState? target))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                if (targetId == actorId || personalCombatResolver is null)
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
                PersonalActorState resolvedTarget = resolution.Target;
                if (resolvedTarget.IsIncapacitated && !wasIncapacitated &&
                    !resolvedTarget.Injuries.Any(value => value.Severity == InjurySeverity.Incapacitating))
                {
                    resolvedTarget = resolvedTarget with
                    {
                        Injuries = resolvedTarget.Injuries.Add(new InjuryState(
                            new ContentId("injury.combat.incapacitated"),
                            InjurySeverity.Incapacitating,
                            false)),
                    };
                }

                updated = resolution.Actor;
                encounter = encounter with
                {
                    Actors = encounter.Actors.SetItem(targetId, resolvedTarget),
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
        encounter = encounter with { Actors = encounter.Actors.SetItem(actorId, updated) };
        ImmutableArray<ActorId> ready = updated.ActionPoints == 0 ? ReadyActors.Remove(actorId) : ReadyActors;
        bool pause = ready.Any(id => encounter.Actors[id].TeamId == PlayerTeamId);
        return (this with { PersonalEncounter = encounter, ReadyActors = ready, PersonalPaused = pause })
            .AddEvent(
                command,
                true,
                eventAmount,
                string.Empty);
    }

    private World UpdateShips()
    {
        ImmutableDictionary<ShipId, ShipState>.Builder ships = Ships.ToBuilder();
        foreach (ShipState original in Ships.Values.OrderBy(value => value.Id))
        {
            ShipState ship = original with { Position = original.Position + original.Velocity };
            foreach (NetworkId network in ship.Modules.Select(value => value.Definition.NetworkId).Distinct().Order())
            {
                ship = ShipPowerSystem.Allocate(ship, network, [.. ship.Modules.Select(value => value.InstanceId)]).Ship;
            }

            ship = ship with
            {
                Modules = [.. ship.Modules.Select(value => value.Weapon is not null && value.WeaponReadiness == WeaponReadiness.Reloading && value.ReadyTick <= Tick
                    ? value with { WeaponReadiness = WeaponReadiness.Ready }
                    : value)],
            };
            ships[ship.Id] = ship;
        }

        return this with { Ships = ships.ToImmutable() };
    }

    private World UpdatePersonalTimeline()
    {
        if (PersonalEncounter is null)
        {
            return this;
        }

        PersonalEncounterState encounter = PersonalEncounter;
        ImmutableArray<ActorId>.Builder becameReady = ImmutableArray.CreateBuilder<ActorId>();
        ImmutableDictionary<ActorId, PersonalActorState>.Builder actors = encounter.Actors.ToBuilder();
        foreach (PersonalActorState actor in encounter.Actors.Values.OrderBy(value => value.Id))
        {
            PersonalActorState current = actor.ReactionExpiresTick > 0 && actor.ReactionExpiresTick < Tick
                ? actor with { ReservedReactionPoints = 0, ReactionExpiresTick = 0 }
                : actor;
            if (current.IsIncapacitated || current.Surrendered || ReadyActors.Contains(current.Id))
            {
                actors[current.Id] = current;
                continue;
            }

            CharacterResourceSet recoveredResources = current.CharacterResources.RecoverOneTick();
            CharacterTurnState advancedTurn = current.Turn.AddTurnMeter(
                recoveredResources.GetResourcePercentage(CharacterResourceIds.Stamina));
            if (advancedTurn.CanActivate)
            {
                becameReady.Add(current.Id);
                actors[current.Id] = current with
                {
                    Turn = advancedTurn.BeginActivation(),
                    CharacterResources = recoveredResources,
                    Defending = false,
                };
            }
            else
            {
                actors[current.Id] = current with
                {
                    Turn = advancedTurn,
                    CharacterResources = recoveredResources,
                };
            }
        }

        ImmutableArray<ActorId> ready =
            [.. ReadyActors.AddRange(becameReady).Distinct().OrderBy(id => actors[id].TeamId == PlayerTeamId ? 0 : 1).ThenBy(id => id)];
        bool personalPause = ready.Any(id => actors[id].TeamId == PlayerTeamId &&
            !ScheduledActions.Any(value => value.Command.IssuerId == id.Value && value.Phase < ScheduledActionPhase.Committed));
        return this with
        {
            PersonalEncounter = encounter with { Actors = actors.ToImmutable() },
            ReadyActors = ready,
            PersonalPaused = personalPause,
        };
    }

    private ShipState RepairModule(ShipState ship, ContentId? instanceId)
    {
        if (instanceId is not ContentId id)
        {
            return ship;
        }

        int index = IndexOf(ship.Modules, value => value.InstanceId == id);
        ResourceId parts = new("resource.spare-parts");
        ship.Resources.TryGetValue(parts, out int available);
        if (index < 0 || available <= 0)
        {
            return ship;
        }

        InstalledModuleState module = ship.Modules[index];
        int integrity = Math.Min(module.Definition.MaximumIntegrity, module.Integrity + 3);
        return ship with
        {
            Resources = ship.Resources.SetItem(parts, available - 1),
            Modules = ship.Modules.SetItem(index, module with
            {
                Integrity = integrity,
                Condition = integrity == module.Definition.MaximumIntegrity ? ModuleCondition.Intact : ModuleCondition.Damaged,
                WeaponReadiness = module.Weapon is null ? module.WeaponReadiness : WeaponReadiness.Ready,
            }),
        };
    }

    private World AddEvent(WorldCommand command, bool succeeded, int amount, string code)
    {
        WorldEvent value = new(
            new ContentId($"event.voyage.sequence-{RandomSequence % 1_000_000}"),
            Tick,
            command.IssuerId,
            command.TargetId,
            command.Kind,
            succeeded,
            amount,
            code);
        ImmutableArray<WorldEvent> events = Events.Length == MaximumEvents ? Events.RemoveAt(0).Add(value) : Events.Add(value);
        return this with { Events = events, RandomSequence = RandomSequence + 1 };
    }

    private bool TargetExists(WorldCommand command) =>
        command.TargetId == command.IssuerId || Ships.Keys.Any(value => value.Value == command.TargetId) ||
        PersonalEncounter?.Actors.Keys.Any(value => value.Value == command.TargetId) == true ||
        PersonalEncounter?.Board.Cells.Keys.Any(value => value.Value == command.TargetId) == true ||
        PersonalEncounter?.Objectives.Keys.Any(value => value.Value == command.TargetId) == true;

    private bool CanSubmitPersonal(ContentId issuerId) =>
        ActorIdFrom(issuerId, out ActorId actorId) && ReadyActors.Contains(actorId) &&
        PersonalEncounter?.Actors.TryGetValue(actorId, out PersonalActorState? actor) == true && actor.ActionPoints > 0;

    private static ContentId PersonalActionId(WorldCommandKind kind) => kind switch
    {
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

    private static bool HasStableEncounterIdentity(PersonalActorState original, PersonalActorState resolved) =>
        original.Id == resolved.Id &&
        original.TeamId == resolved.TeamId &&
        original.CharacterId == resolved.CharacterId &&
        original.CellId == resolved.CellId &&
        original.Surrendered == resolved.Surrendered &&
        original.Prisoner == resolved.Prisoner;

    private bool TryShip(ContentId id, out ShipState? ship)
    {
        foreach ((ShipId shipId, ShipState value) in Ships)
        {
            if (shipId.Value == id)
            {
                ship = value;
                return true;
            }
        }

        ship = null;
        return false;
    }

    private static bool IsPersonal(WorldCommandKind kind) => kind >= WorldCommandKind.PersonalMove;

    private static int NormalizeHeading(int value)
    {
        int result = value % 360_000;
        return result < 0 ? result + 360_000 : result;
    }

    private static FixedVector2 Direction(FixedVector2 from, FixedVector2 to) => new(
        new FixedScalar(Math.Sign(to.X.Raw - from.X.Raw) * FixedScalar.Scale),
        new FixedScalar(Math.Sign(to.Y.Raw - from.Y.Raw) * FixedScalar.Scale));

    private static bool ActorIdFrom(ContentId id, out ActorId value)
    {
        if (id.ToString().StartsWith("actor.", StringComparison.Ordinal))
        {
            value = new ActorId(id);
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

    private static int IndexOf<T>(ImmutableArray<T> values, Func<T, bool> predicate)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (predicate(values[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static ImmutableArray<WorldCommandLogEntry> MarkCancelled(
        ImmutableArray<WorldCommandLogEntry> history,
        ContentId commandId,
        long tick)
    {
        int index = IndexOf(history, value => value.Command.Id == commandId);
        return index < 0 ? history : history.SetItem(index, history[index] with { CancelledTick = tick });
    }

    private static WorldCommandResult Rejected(World world, string code) => new(world, false, code);
}
