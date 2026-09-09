using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Ships;

namespace Spelljammer.Simulation.World;

/// <summary>
/// Advances the authoritative world through a bounded number of fixed simulation ticks.
/// </summary>
/// <remarks>
/// Code flow: Each tick drains due commands and scheduled actions in stable order, advances ships, opponents, encounters, and campaign time, then emits a new snapshot.
/// </remarks>
public sealed partial record World
{
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

    private World AdvanceOneTick(IPersonalCombatResolver? personalCombatResolver)
    {
        long nextTick = Tick + 1;
        World world = this with {
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
                    world = world with {
                        ScheduledActions = world.ScheduledActions.SetItem(completionIndex, schedule with {
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

        world = world.UpdateVoyage();
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
        else if (command.Kind == WorldCommandKind.BeginVoyage)
        {
            if (!TryResolveNextLeg(command, out ShipState? voyageShip, out _, out VoyageLegQuote? quote, out string rejection))
            {
                return AddEvent(command, false, 0, rejection);
            }

            resourceId = quote!.ResourceId;
            reserved = quote.ResourceCost;
            voyageShip!.Resources.TryGetValue(resourceId.Value, out int available);
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
            : IsVoyage(schedule.Command.Kind)
                ? CommitVoyage(schedule.Command, schedule.ReservedResourceId, schedule.ReservedAmount)
                : CommitShip(schedule.Command, schedule.ReservedResourceId, schedule.ReservedAmount);
        int index = IndexOf(committed.ScheduledActions, value => value.Command.Id == schedule.Command.Id);
        if (index >= 0)
        {
            committed = committed with {
                ScheduledActions = committed.ScheduledActions.SetItem(index, schedule with {
                    Phase = ScheduledActionPhase.Recovering,
                    History = schedule.History.Add(ScheduledActionPhase.Committed).Add(ScheduledActionPhase.Recovering),
                }),
            };
        }

        return committed;
    }
}
