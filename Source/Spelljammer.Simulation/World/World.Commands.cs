using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

namespace Spelljammer.Simulation.World;

public sealed partial record World
{
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
        return new WorldCommandResult(this with {
            Commands = queued,
            CommandHistory = CommandHistory.Add(new WorldCommandLogEntry(Tick, command, null)),
        }, true, string.Empty);
    }

    public WorldCommandResult Cancel(ContentId commandId)
    {
        WorldCommand? queued = Commands.FirstOrDefault(value => value.Id == commandId);
        if (queued is not null)
        {
            return new WorldCommandResult(this with {
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
        return new WorldCommandResult(this with {
            ScheduledActions = ScheduledActions.Replace(schedule, interrupted),
            CommandHistory = MarkCancelled(CommandHistory, commandId, Tick),
        }, true, string.Empty);
    }

    private bool TargetExists(WorldCommand command) =>
        command.TargetId == command.IssuerId || Ships.Keys.Any(value => value.Value == command.TargetId) ||
        PersonalEncounter?.Actors.Keys.Any(value => value.Value == command.TargetId) == true ||
        PersonalEncounter?.Board.Cells.Keys.Any(value => value.Value == command.TargetId) == true ||
        PersonalEncounter?.Objectives.Keys.Any(value => value.Value == command.TargetId) == true;

    private bool CanSubmitPersonal(ContentId issuerId) =>
        ActorIdFrom(issuerId, out ActorId actorId) && ReadyActors.Contains(actorId) &&
        PersonalEncounter?.Actors.TryGetValue(actorId, out PersonalActorState? actor) == true && actor.ActionPoints > 0;

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
