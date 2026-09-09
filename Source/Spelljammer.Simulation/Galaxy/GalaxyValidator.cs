namespace Spelljammer.Simulation.Galaxy;

public static class GalaxyValidator
{
    public static GalaxyValidationResult Validate(GalaxyMapState? galaxy)
    {
        if (galaxy is null || galaxy.GeneratorVersion <= 0 || !galaxy.CurrentSystemId.IsValid)
        {
            return new(GalaxyValidationCode.InvalidHeader);
        }

        if (galaxy.Systems.Count is 0 or > GalaxyMapState.MaximumSystems ||
            galaxy.Starways.Count > GalaxyMapState.MaximumStarways)
        {
            return new(GalaxyValidationCode.CapacityExceeded);
        }

        if (!galaxy.Systems.ContainsKey(galaxy.CurrentSystemId) ||
            galaxy.Systems.Any(pair => pair.Key != pair.Value.Id || pair.Value.Ordinal < 0 ||
                pair.Value.Region < 0 || !pair.Value.ArchetypeId.IsValid))
        {
            return new(GalaxyValidationCode.InvalidSystem);
        }

        HashSet<(StarSystemId, StarSystemId)> connections = [];
        Dictionary<StarSystemId, int> degrees = galaxy.Systems.Keys.ToDictionary(id => id, _ => 0);
        foreach (StarwayState starway in galaxy.Starways.Values.OrderBy(value => value.Id))
        {
            if (!starway.Id.IsValid || starway.FirstSystemId == starway.SecondSystemId ||
                !galaxy.Systems.ContainsKey(starway.FirstSystemId) || !galaxy.Systems.ContainsKey(starway.SecondSystemId) ||
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

            if (++degrees[first] > GalaxyMapState.MaximumStarwaysPerSystem ||
                ++degrees[second] > GalaxyMapState.MaximumStarwaysPerSystem)
            {
                return new(GalaxyValidationCode.DegreeExceeded);
            }
        }

        if (galaxy.Knowledge.Any(pair => !galaxy.Systems.ContainsKey(pair.Key) || !Enum.IsDefined(pair.Value)))
        {
            return new(GalaxyValidationCode.InvalidKnowledge);
        }

        HashSet<StarSystemId> reached = [galaxy.CurrentSystemId];
        Queue<StarSystemId> pending = new();
        pending.Enqueue(galaxy.CurrentSystemId);
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

        return reached.Count == galaxy.Systems.Count
            ? new(GalaxyValidationCode.None)
            : new(GalaxyValidationCode.UnreachableSystem);
    }
}

