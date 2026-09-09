using System.Collections.Immutable;

namespace Spelljammer.Simulation.Galaxy;

public enum GalaxyRoutePreference : byte
{
    TravelTime,
    Fuel,
    KnownDanger,
}

public enum GalaxyRouteCode : byte
{
    None,
    InvalidRequest,
    DestinationUnknown,
    CapacityExceeded,
    NoRoute,
}

public sealed record GalaxyRouteResult(
    GalaxyRouteCode Code,
    ImmutableArray<StarSystemId> Systems,
    ImmutableArray<StarwayId> Starways,
    int TotalCost)
{
    public bool Succeeded => Code == GalaxyRouteCode.None;
}

public static class GalaxyRoutePlanner
{
    public const int DefaultMaximumExpansions = 1_024;

    public static GalaxyRouteResult Plan(
        GalaxyMapState galaxy,
        StarSystemId origin,
        StarSystemId destination,
        GalaxyRoutePreference preference,
        int maximumExpansions = DefaultMaximumExpansions)
    {
        ArgumentNullException.ThrowIfNull(galaxy);
        if (!galaxy.Systems.ContainsKey(origin) || !galaxy.Systems.ContainsKey(destination) ||
            origin == destination || !Enum.IsDefined(preference) || maximumExpansions <= 0)
        {
            return Failed(GalaxyRouteCode.InvalidRequest);
        }

        if (galaxy.KnowledgeOf(destination) < GalaxyKnowledgeLevel.Detected)
        {
            return Failed(GalaxyRouteCode.DestinationUnknown);
        }

        PriorityQueue<StarSystemId, (int Cost, string Id)> pending = new();
        Dictionary<StarSystemId, int> costs = new() { [origin] = 0 };
        Dictionary<StarSystemId, (StarSystemId Previous, StarwayId Starway)> previous = [];
        pending.Enqueue(origin, (0, origin.ToString()));
        int expansions = 0;
        while (pending.TryDequeue(out StarSystemId current, out (int Cost, string Id) priority))
        {
            if (!costs.TryGetValue(current, out int currentCost) || currentCost != priority.Cost)
            {
                continue;
            }

            if (++expansions > maximumExpansions)
            {
                return Failed(GalaxyRouteCode.CapacityExceeded);
            }

            if (current == destination)
            {
                return Build(origin, destination, currentCost, previous);
            }

            foreach (StarwayState starway in galaxy.StarwaysFrom(current))
            {
                StarSystemId next = starway.FirstSystemId == current ? starway.SecondSystemId : starway.FirstSystemId;
                if (next != destination && galaxy.KnowledgeOf(next) < GalaxyKnowledgeLevel.Detected)
                {
                    continue;
                }

                int edgeCost = preference switch
                {
                    GalaxyRoutePreference.TravelTime => starway.TravelTime,
                    GalaxyRoutePreference.Fuel => starway.FuelCost,
                    GalaxyRoutePreference.KnownDanger => galaxy.KnowledgeOf(next) >= GalaxyKnowledgeLevel.Surveyed
                        ? Math.Max(1, starway.Danger)
                        : 1,
                    _ => throw new InvalidOperationException(),
                };
                int candidate = checked(currentCost + edgeCost);
                if (!costs.TryGetValue(next, out int known) || candidate < known)
                {
                    costs[next] = candidate;
                    previous[next] = (current, starway.Id);
                    pending.Enqueue(next, (candidate, next.ToString()));
                }
            }
        }

        return Failed(GalaxyRouteCode.NoRoute);
    }

    private static GalaxyRouteResult Build(
        StarSystemId origin,
        StarSystemId destination,
        int cost,
        Dictionary<StarSystemId, (StarSystemId Previous, StarwayId Starway)> previous)
    {
        List<StarSystemId> systems = [destination];
        List<StarwayId> starways = [];
        StarSystemId current = destination;
        while (current != origin)
        {
            (StarSystemId prior, StarwayId route) = previous[current];
            starways.Add(route);
            systems.Add(prior);
            current = prior;
        }

        systems.Reverse();
        starways.Reverse();
        return new(GalaxyRouteCode.None, [.. systems], [.. starways], cost);
    }

    private static GalaxyRouteResult Failed(GalaxyRouteCode code) => new(code, [], [], 0);
}
