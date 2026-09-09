using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Galaxy;

public static class GalaxyValidator
{
    public static GalaxyValidationResult Validate(GalaxyState? galaxy)
    {
        if (galaxy?.Topology is null || galaxy.Knowledge is null || galaxy.Dynamic is null ||
            galaxy.Topology.GeneratorVersion <= 0)
        {
            return new(GalaxyValidationCode.InvalidHeader);
        }

        GalaxyTopology topology = galaxy.Topology;
        if (topology.Systems.Count is 0 or > GalaxyLimits.MaximumSystems ||
            topology.Starways.Count > GalaxyLimits.MaximumStarways)
        {
            return new(GalaxyValidationCode.CapacityExceeded);
        }

        if (topology.Systems.Any(pair => pair.Key != pair.Value.Id || pair.Value.Ordinal < 0 ||
                pair.Value.Region < 0 || !pair.Value.ArchetypeId.IsValid))
        {
            return new(GalaxyValidationCode.InvalidSystem);
        }

        HashSet<(StarSystemId, StarSystemId)> connections = [];
        Dictionary<StarSystemId, int> degrees = topology.Systems.Keys.ToDictionary(id => id, _ => 0);
        foreach (StarwayState starway in topology.Starways.Values.OrderBy(value => value.Id))
        {
            if (!starway.Id.IsValid || starway.FirstSystemId == starway.SecondSystemId ||
                !topology.Systems.ContainsKey(starway.FirstSystemId) || !topology.Systems.ContainsKey(starway.SecondSystemId) ||
                starway.TravelTime <= 0 || starway.FuelCost <= 0 || starway.Danger < 0)
            {
                return new(GalaxyValidationCode.InvalidStarway);
            }

            (StarSystemId first, StarSystemId second) = starway.FirstSystemId.CompareTo(starway.SecondSystemId) < 0
                ? (starway.FirstSystemId, starway.SecondSystemId)
                : (starway.SecondSystemId, starway.FirstSystemId);
            if (!connections.Add((first, second)))
            {
                return new(GalaxyValidationCode.DuplicateConnection);
            }

            if (++degrees[first] > GalaxyLimits.MaximumStarwaysPerSystem ||
                ++degrees[second] > GalaxyLimits.MaximumStarwaysPerSystem)
            {
                return new(GalaxyValidationCode.DegreeExceeded);
            }
        }

        if (galaxy.Knowledge.Systems.Any(pair => !topology.Systems.ContainsKey(pair.Key) || !Enum.IsDefined(pair.Value)))
        {
            return new(GalaxyValidationCode.InvalidKnowledge);
        }

        if (galaxy.Dynamic.Starways.Any(pair => !topology.Starways.ContainsKey(pair.Key) ||
                pair.Value.DangerModifier is < -100 or > 100 ||
                (pair.Value.ControllingFactionId is ContentId factionId && !factionId.IsValid)) ||
            galaxy.Dynamic.ChangedSiteIds.Count > GalaxyLimits.MaximumChangedSites ||
            galaxy.Dynamic.ChangedSiteIds.Any(id => !id.IsValid))
        {
            return new(GalaxyValidationCode.InvalidDynamicState);
        }

        StarSystemId firstSystemId = topology.Systems.Keys.Min();
        HashSet<StarSystemId> reached = [firstSystemId];
        Queue<StarSystemId> pending = new();
        pending.Enqueue(firstSystemId);
        while (pending.TryDequeue(out StarSystemId current))
        {
            foreach (StarwayState route in galaxy.StarwaysFrom(current))
            {
                StarSystemId next = route.FirstSystemId == current ? route.SecondSystemId : route.FirstSystemId;
                if (reached.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }

        return reached.Count == topology.Systems.Count
            ? new(GalaxyValidationCode.None)
            : new(GalaxyValidationCode.UnreachableSystem);
    }

    public static bool ValidateNavigation(GalaxyState galaxy, VoyageNavigationState? navigation)
    {
        if (navigation is null || !galaxy.Topology.Systems.ContainsKey(navigation.CurrentSystemId) ||
            navigation.RouteProgress < 0 || navigation.DepartureTick < 0 ||
            navigation.ArrivalTick < navigation.DepartureTick ||
            navigation.PlannedRoute.Length > GalaxyLimits.MaximumSystems ||
            navigation.PlannedRoute.Any(id => !galaxy.Topology.Starways.ContainsKey(id)))
        {
            return false;
        }

        return navigation.ActiveStarwayId is null
            ? navigation.RouteProgress == 0 && navigation.DepartureTick == navigation.ArrivalTick
            : galaxy.Topology.Starways.ContainsKey(navigation.ActiveStarwayId.Value) &&
                !galaxy.Dynamic.StateOf(navigation.ActiveStarwayId.Value).IsBlocked;
    }
}
