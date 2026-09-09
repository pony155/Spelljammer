using System.Collections.Immutable;
using Spelljammer.Simulation.Galaxy;

public sealed partial class SimulationContracts
{
    private static void GalaxyGenerationAndRoutingAreDeterministic()
    {
        GalaxyGenerationResult firstResult = GalaxyGenerator.Generate(0x5eedUL);
        GalaxyGenerationResult secondResult = GalaxyGenerator.Generate(0x5eedUL);
        True(firstResult.Succeeded, $"First galaxy generation failed: {firstResult.Code}.");
        True(secondResult.Succeeded, $"Second galaxy generation failed: {secondResult.Code}.");

        GalaxyMapState first = firstResult.Galaxy!;
        GalaxyMapState second = secondResult.Galaxy!;
        Equal(16, first.Systems.Count, "The voyage galaxy did not contain sixteen systems.");
        True(GalaxyValidator.Validate(first).Accepted, "The generated galaxy failed validation.");
        True(first.Systems.Values.OrderBy(value => value.Id)
                .SequenceEqual(second.Systems.Values.OrderBy(value => value.Id)),
            "The same seed produced different systems.");
        True(first.Starways.Values.OrderBy(value => value.Id)
                .SequenceEqual(second.Starways.Values.OrderBy(value => value.Id)),
            "The same seed produced different Starways.");
        True(first.StarwaysFrom(first.CurrentSystemId).Count() >= 2,
            "The starting anchorage did not have two outward Starways.");

        StarSystemId destination = first.Systems.Values.OrderBy(value => value.Ordinal).Last().Id;
        GalaxyRouteResult unknown = GalaxyRoutePlanner.Plan(
            first, first.CurrentSystemId, destination, GalaxyRoutePreference.TravelTime);
        Equal(GalaxyRouteCode.DestinationUnknown, unknown.Code,
            "Route planning exposed an unknown destination.");

        ImmutableDictionary<StarSystemId, GalaxyKnowledgeLevel> charted = first.Systems.Keys
            .ToImmutableDictionary(id => id, _ => GalaxyKnowledgeLevel.Charted);
        GalaxyMapState knownGalaxy = first with { Knowledge = charted };
        GalaxyRouteResult route = GalaxyRoutePlanner.Plan(
            knownGalaxy, knownGalaxy.CurrentSystemId, destination, GalaxyRoutePreference.TravelTime);
        True(route.Succeeded, $"A connected charted galaxy did not produce a route: {route.Code}.");
        Equal(knownGalaxy.CurrentSystemId, route.Systems[0], "The route began at the wrong system.");
        Equal(destination, route.Systems[^1], "The route ended at the wrong system.");
        Equal(route.Systems.Length - 1, route.Starways.Length, "The route edge count was inconsistent.");

        GalaxyRouteResult bounded = GalaxyRoutePlanner.Plan(
            knownGalaxy, knownGalaxy.CurrentSystemId, destination, GalaxyRoutePreference.TravelTime, 1);
        Equal(GalaxyRouteCode.CapacityExceeded, bounded.Code,
            "The route search ignored its expansion budget.");
    }
}
