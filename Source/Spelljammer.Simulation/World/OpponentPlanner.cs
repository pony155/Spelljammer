using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Ships;

namespace Spelljammer.Simulation.World;

public static class OpponentPlanner
{
    public const int MaximumCandidates = 8;

    public static WorldCommand Plan(ShipState opponent, ShipState player, long tick, ulong sequence)
    {
        bool canFire = opponent.Contacts.TryGetValue(player.Id, out ShipContactState? contact) && contact.HasFiringSolution &&
            opponent.Modules.Any(value => value.WeaponReadiness == WeaponReadiness.Ready);
        WorldCommandKind kind = canFire ? WorldCommandKind.Fire : WorldCommandKind.Intercept;
        ContentId target = player.Id.Value;
        if (opponent.Hull <= opponent.Frame.MaximumHull / 4)
        {
            kind = WorldCommandKind.Retreat;
            target = opponent.Id.Value;
        }

        return new WorldCommand(
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
