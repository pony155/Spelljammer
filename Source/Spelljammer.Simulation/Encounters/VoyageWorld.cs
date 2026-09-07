using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

public static class VoyageTime
{
    public const int TicksPerSecond = 20;
    public const int MaximumCatchUpTicks = 8;
    public const int TurnMeterThreshold = 1_000;
    public const int ActionPointsPerActivation = 3;
}

public enum VoyageCommandKind : byte
{
    Scan,
    Course,
    Thrust,
    Turn,
    Brake,
    Intercept,
    Fire,
    Ram,
    RaiseShield,
    LowerShield,
    Defend,
    DamageControl,
    Signal,
    Retreat,
    PersonalMove,
    PersonalDefend,
    PersonalReserveReaction,
    PersonalMelee,
    PersonalRanged,
    PersonalSpell,
    PersonalPsychic,
    PersonalEngineering,
    PersonalMedicine,
    PersonalInteract,
    PersonalSurrender,
    PersonalRetreat,
}

public enum ScheduledActionPhase : byte
{
    Declared,
    Validated,
    Reserved,
    Preparing,
    Committed,
    Recovering,
    Completed,
    Interrupted,
}

public sealed record VoyageCommand(
    ContentId Id,
    VoyageCommandKind Kind,
    long TargetTick,
    int Priority,
    ContentId IssuerId,
    ContentId TargetId,
    FixedVector2 Vector,
    int Amount,
    ContentId? OptionId,
    ulong Sequence);

public sealed record ScheduledAction(
    VoyageCommand Command,
    ScheduledActionPhase Phase,
    long CommitTick,
    long RecoverTick,
    ResourceId? ReservedResourceId,
    int ReservedAmount,
    ImmutableArray<ScheduledActionPhase> History);

public sealed record VoyageEvent(
    ContentId Id,
    long Tick,
    ContentId SourceId,
    ContentId TargetId,
    VoyageCommandKind Kind,
    bool Succeeded,
    int Amount,
    string ResultCode);

public sealed record VoyageCommandLogEntry(long SubmittedTick, VoyageCommand Command, long? CancelledTick);

public sealed record VoyageWorldSnapshot(
    ulong Seed,
    ContentFingerprint ContentFingerprint,
    long Tick,
    bool ShipPaused,
    bool PersonalPaused,
    ImmutableArray<ShipState> Ships,
    PersonalEncounterState? PersonalEncounter,
    ImmutableArray<ActorId> ReadyActors,
    ImmutableArray<ScheduledAction> Actions,
    ImmutableArray<VoyageCommandLogEntry> RecentCommands,
    ImmutableArray<VoyageEvent> RecentEvents);

public sealed record VoyageCommandResult(VoyageWorld World, bool Accepted, string RejectionCode);

public sealed record VoyageAdvanceResult(VoyageWorld World, VoyageWorldSnapshot Snapshot, int AdvancedTicks);

public sealed record VoyageWorld(
    ulong Seed,
    ContentFingerprint ContentFingerprint,
    long Tick,
    ulong RandomSequence,
    TeamId PlayerTeamId,
    bool ShipPaused,
    bool PersonalPaused,
    ImmutableDictionary<ShipId, ShipState> Ships,
    PersonalEncounterState? PersonalEncounter,
    ImmutableArray<VoyageCommand> Commands,
    ImmutableArray<VoyageCommandLogEntry> CommandHistory,
    ImmutableArray<ScheduledAction> ScheduledActions,
    ImmutableArray<ActorId> ReadyActors,
    ImmutableArray<VoyageEvent> Events)
{
    public const int MaximumCommands = 256;
    public const int MaximumCommandHistory = 512;
    public const int MaximumSchedules = 256;
    public const int MaximumEvents = 512;
    public const int MaximumReadyActors = 64;

    public static VoyageWorld Create(
        ulong seed,
        ContentFingerprint fingerprint,
        TeamId playerTeamId,
        IEnumerable<ShipState> ships,
        PersonalEncounterState? encounter = null)
    {
        ImmutableDictionary<ShipId, ShipState> shipMap = ships.ToImmutableDictionary(value => value.Id);
        if (shipMap.Count is 0 or > 32 || encounter?.Actors.Count > MaximumReadyActors)
        {
            throw new InvalidOperationException("Voyage world capacity is invalid.");
        }

        return new VoyageWorld(
            seed,
            fingerprint,
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

    public VoyageWorld SetShipPause(bool paused) => this with { ShipPaused = paused };

    public VoyageWorld CommitReadyPlan() => this with { PersonalPaused = false };

    public VoyageCommandResult Enqueue(VoyageCommand command)
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

        ImmutableArray<VoyageCommand> queued =
            [.. Commands.Append(command).OrderBy(value => value.TargetTick).ThenBy(value => value.Priority)
                .ThenBy(value => value.IssuerId).ThenBy(value => value.Sequence).ThenBy(value => value.Id)];
        return new VoyageCommandResult(this with
        {
            Commands = queued,
            CommandHistory = CommandHistory.Add(new VoyageCommandLogEntry(Tick, command, null)),
        }, true, string.Empty);
    }

    public VoyageCommandResult Cancel(ContentId commandId)
    {
        VoyageCommand? queued = Commands.FirstOrDefault(value => value.Id == commandId);
        if (queued is not null)
        {
            return new VoyageCommandResult(this with
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
        return new VoyageCommandResult(this with
        {
            ScheduledActions = ScheduledActions.Replace(schedule, interrupted),
            CommandHistory = MarkCancelled(CommandHistory, commandId, Tick),
        }, true, string.Empty);
    }

    public VoyageAdvanceResult Advance(int requestedTicks)
    {
        int ticks = Math.Clamp(requestedTicks, 0, VoyageTime.MaximumCatchUpTicks);
        VoyageWorld world = this;
        int advanced = 0;
        for (int index = 0; index < ticks; index++)
        {
            if (world.ShipPaused || world.PersonalPaused)
            {
                break;
            }

            world = world.AdvanceOneTick();
            advanced++;
        }

        return new VoyageAdvanceResult(world, world.Snapshot(), advanced);
    }

    public VoyageWorldSnapshot Snapshot() => new(
        Seed,
        ContentFingerprint,
        Tick,
        ShipPaused,
        PersonalPaused,
        [.. Ships.Values.OrderBy(value => value.Id)],
        PersonalEncounter,
        ReadyActors,
        ScheduledActions,
        CommandHistory.Length <= 64 ? CommandHistory : CommandHistory[^64..],
        Events.Length <= 64 ? Events : Events[^64..]);

    private VoyageWorld AdvanceOneTick()
    {
        long nextTick = Tick + 1;
        VoyageWorld world = this with { Tick = nextTick };
        VoyageCommand[] due = [.. world.Commands.Where(value => value.TargetTick <= nextTick)];
        world = world with { Commands = [.. world.Commands.Except(due)] };
        foreach (VoyageCommand command in due)
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
                world = world.Commit(schedule);
            }
        }

        world = world.UpdateShips();
        world = world.UpdatePersonalTimeline();
        return world;
    }

    private VoyageWorld DeclareAndReserve(VoyageCommand command)
    {
        if (ScheduledActions.Length >= MaximumSchedules)
        {
            return AddEvent(command, false, 0, "command.queue-capacity");
        }

        ResourceId? resourceId = null;
        int reserved = 0;
        if (command.Kind == VoyageCommandKind.Fire && TryShip(command.IssuerId, out ShipState? ship))
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

    private VoyageWorld Commit(ScheduledAction schedule)
    {
        VoyageWorld committed = IsPersonal(schedule.Command.Kind)
            ? CommitPersonal(schedule.Command)
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

    private VoyageWorld CommitShip(VoyageCommand command, ResourceId? reservedResource, int reservedAmount)
    {
        if (!TryShip(command.IssuerId, out ShipState? actor))
        {
            return AddEvent(command, false, 0, "command.actor-missing");
        }

        ShipState ship = actor!;
        switch (command.Kind)
        {
            case VoyageCommandKind.Scan:
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
            case VoyageCommandKind.Course:
            case VoyageCommandKind.Thrust:
                ship = ship with { Velocity = command.Vector };
                break;
            case VoyageCommandKind.Turn:
                ship = ship with { HeadingMilliDegrees = NormalizeHeading(ship.HeadingMilliDegrees + command.Amount) };
                break;
            case VoyageCommandKind.Brake:
                ship = ship with { Velocity = FixedVector2.Zero };
                break;
            case VoyageCommandKind.Intercept:
                if (!TryShip(command.TargetId, out ShipState? intercepted))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                ship = ship with { Velocity = Direction(ship.Position, intercepted!.Position) };
                break;
            case VoyageCommandKind.Fire:
                return CommitFire(command, ship, reservedResource, reservedAmount);
            case VoyageCommandKind.Ram:
                return CommitRam(command, ship);
            case VoyageCommandKind.RaiseShield:
            case VoyageCommandKind.LowerShield:
                bool raised = command.Kind == VoyageCommandKind.RaiseShield;
                ship = ship with
                {
                    Modules = [.. ship.Modules.Select(value => value.Definition.ShieldValue > 0 ? value with { ShieldRaised = raised } : value)],
                };
                break;
            case VoyageCommandKind.Defend:
                ship = ship with { Defending = true };
                break;
            case VoyageCommandKind.DamageControl:
                ship = RepairModule(ship, command.OptionId);
                break;
            case VoyageCommandKind.Signal:
                ship = ship with { PersistentEvidence = ship.PersistentEvidence.Add(new ContentId("evidence.ship.signal")) };
                break;
            case VoyageCommandKind.Retreat:
                ship = ship with { Disengaged = true, PersistentEvidence = ship.PersistentEvidence.Add(new ContentId("evidence.ship.escape")) };
                break;
            default:
                return AddEvent(command, false, 0, "command.action-unknown");
        }

        return (this with { Ships = Ships.SetItem(ship.Id, ship) }).AddEvent(command, true, command.Amount, string.Empty);
    }

    private VoyageWorld CommitFire(VoyageCommand command, ShipState attacker, ResourceId? resourceId, int resourceCost)
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

    private VoyageWorld CommitRam(VoyageCommand command, ShipState attacker)
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

    private VoyageWorld CommitPersonal(VoyageCommand command)
    {
        if (PersonalEncounter is null || !ActorIdFrom(command.IssuerId, out ActorId actorId) ||
            !PersonalEncounter.Actors.TryGetValue(actorId, out PersonalActorState? actor) ||
            actor.ActionPoints <= 0 || actor.IsIncapacitated)
        {
            return AddEvent(command, false, 0, "command.actor-not-ready");
        }

        PersonalEncounterState encounter = PersonalEncounter;
        PersonalActorState updated = actor;
        int apCost = command.Kind is VoyageCommandKind.PersonalMove or VoyageCommandKind.PersonalReserveReaction ? 1 : 2;
        if (updated.ActionPoints < apCost)
        {
            return AddEvent(command, false, 0, "command.action-points-insufficient");
        }

        switch (command.Kind)
        {
            case VoyageCommandKind.PersonalMove:
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
            case VoyageCommandKind.PersonalDefend:
                updated = updated with { Defending = true };
                encounter = encounter.AddEffect(new ActiveEffectState(
                    new EffectId("effect.personal.defending"), command.Id, actorId, Tick + VoyageTime.TicksPerSecond, 1));
                break;
            case VoyageCommandKind.PersonalReserveReaction:
                updated = updated with { ReservedReactionPoints = 1, ReactionExpiresTick = Tick + VoyageTime.TicksPerSecond };
                encounter = encounter.AddEffect(new ActiveEffectState(
                    new EffectId("effect.personal.reaction"), command.Id, actorId, Tick + VoyageTime.TicksPerSecond, 1));
                break;
            case VoyageCommandKind.PersonalSurrender:
                updated = updated with { Surrendered = true };
                break;
            case VoyageCommandKind.PersonalRetreat:
                if (!encounter.Board.Definition.RetreatCellIds.Contains(updated.CellId))
                {
                    return AddEvent(command, false, 0, "command.retreat-unavailable");
                }

                encounter = encounter with { Retreated = true };
                break;
            case VoyageCommandKind.PersonalMedicine:
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
            case VoyageCommandKind.PersonalInteract:
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
            case VoyageCommandKind.PersonalEngineering:
                encounter = encounter with { ExplorationChanges = encounter.ExplorationChanges.Add(new ContentId("exploration.ruin.defense-disabled")) };
                break;
            case VoyageCommandKind.PersonalMelee:
            case VoyageCommandKind.PersonalRanged:
            case VoyageCommandKind.PersonalSpell:
            case VoyageCommandKind.PersonalPsychic:
                if (!ActorIdFrom(command.TargetId, out ActorId targetId) || !encounter.Actors.TryGetValue(targetId, out PersonalActorState? target))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                int damage = Math.Max(1, command.Amount);
                bool reacted = target.ReservedReactionPoints > 0 && target.ReactionExpiresTick >= Tick;
                if (reacted)
                {
                    damage = Math.Max(1, damage / 2);
                    target = target with { ReservedReactionPoints = 0, ReactionExpiresTick = 0 };
                }

                int health = Math.Max(0, target.Health - (target.Defending ? Math.Max(1, damage / 2) : damage));
                ImmutableArray<InjuryState> injuries = target.Injuries;
                if (health == 0 && !target.IsIncapacitated)
                {
                    injuries = injuries.Add(new InjuryState(new ContentId("injury.combat.incapacitated"), InjurySeverity.Incapacitating, false));
                }

                encounter = encounter with
                {
                    Actors = encounter.Actors.SetItem(targetId, target with { Health = health, Injuries = injuries }),
                    DamagedObjects = command.OptionId is ContentId objectId ? encounter.DamagedObjects.Add(objectId) : encounter.DamagedObjects,
                };
                break;
            default:
                return AddEvent(command, false, 0, "command.action-unknown");
        }

        updated = updated with { ActionPoints = updated.ActionPoints - apCost };
        encounter = encounter with { Actors = encounter.Actors.SetItem(actorId, updated) };
        ImmutableArray<ActorId> ready = updated.ActionPoints == 0 ? ReadyActors.Remove(actorId) : ReadyActors;
        bool pause = ready.Any(id => encounter.Actors[id].TeamId == PlayerTeamId);
        return (this with { PersonalEncounter = encounter, ReadyActors = ready, PersonalPaused = pause })
            .AddEvent(command, true, command.Amount, string.Empty);
    }

    private VoyageWorld UpdateShips()
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

    private VoyageWorld UpdatePersonalTimeline()
    {
        if (PersonalEncounter is null)
        {
            return this;
        }

        PersonalEncounterState encounter = PersonalEncounter;
        encounter = encounter with { ActiveEffects = [.. encounter.ActiveEffects.Where(value => value.ExpiresTick >= Tick)] };
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

            int meter = Math.Min(VoyageTime.TurnMeterThreshold, current.TurnMeter + current.TurnRate);
            if (meter == VoyageTime.TurnMeterThreshold)
            {
                becameReady.Add(current.Id);
                actors[current.Id] = current with
                {
                    TurnMeter = 0,
                    ActionPoints = VoyageTime.ActionPointsPerActivation,
                    Defending = false,
                };
            }
            else
            {
                actors[current.Id] = current with { TurnMeter = meter };
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

    private VoyageWorld AddEvent(VoyageCommand command, bool succeeded, int amount, string code)
    {
        VoyageEvent value = new(
            new ContentId($"event.voyage.sequence-{RandomSequence % 1_000_000}"),
            Tick,
            command.IssuerId,
            command.TargetId,
            command.Kind,
            succeeded,
            amount,
            code);
        ImmutableArray<VoyageEvent> events = Events.Length == MaximumEvents ? Events.RemoveAt(0).Add(value) : Events.Add(value);
        return this with { Events = events, RandomSequence = RandomSequence + 1 };
    }

    private bool TargetExists(VoyageCommand command) =>
        command.TargetId == command.IssuerId || Ships.Keys.Any(value => value.Value == command.TargetId) ||
        PersonalEncounter?.Actors.Keys.Any(value => value.Value == command.TargetId) == true ||
        PersonalEncounter?.Board.Cells.Keys.Any(value => value.Value == command.TargetId) == true ||
        PersonalEncounter?.Objectives.Keys.Any(value => value.Value == command.TargetId) == true;

    private bool CanSubmitPersonal(ContentId issuerId) =>
        ActorIdFrom(issuerId, out ActorId actorId) && ReadyActors.Contains(actorId) &&
        PersonalEncounter?.Actors.TryGetValue(actorId, out PersonalActorState? actor) == true && actor.ActionPoints > 0;

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

    private static bool IsPersonal(VoyageCommandKind kind) => kind >= VoyageCommandKind.PersonalMove;

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

    private static ImmutableArray<VoyageCommandLogEntry> MarkCancelled(
        ImmutableArray<VoyageCommandLogEntry> history,
        ContentId commandId,
        long tick)
    {
        int index = IndexOf(history, value => value.Command.Id == commandId);
        return index < 0 ? history : history.SetItem(index, history[index] with { CancelledTick = tick });
    }

    private static VoyageCommandResult Rejected(VoyageWorld world, string code) => new(world, false, code);
}

public static class OpponentPlanner
{
    public const int MaximumCandidates = 8;

    public static VoyageCommand Plan(ShipState opponent, ShipState player, long tick, ulong sequence)
    {
        bool canFire = opponent.Contacts.TryGetValue(player.Id, out ShipContactState? contact) && contact.HasFiringSolution &&
            opponent.Modules.Any(value => value.WeaponReadiness == WeaponReadiness.Ready);
        VoyageCommandKind kind = canFire ? VoyageCommandKind.Fire : VoyageCommandKind.Intercept;
        ContentId target = player.Id.Value;
        if (opponent.Hull <= opponent.Frame.MaximumHull / 4)
        {
            kind = VoyageCommandKind.Retreat;
            target = opponent.Id.Value;
        }

        return new VoyageCommand(
            new ContentId($"command.opponent.sequence-{sequence % 1_000_000}"),
            kind,
            tick,
            100,
            opponent.Id.Value,
            target,
            FixedVector2.Zero,
            4,
            null,
            sequence);
    }
}
