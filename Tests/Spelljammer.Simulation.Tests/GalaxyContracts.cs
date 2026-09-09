using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Galaxy;
using Spelljammer.Simulation.Ships;
using Spelljammer.Simulation.World;

public sealed partial class SimulationContracts
{
    private static void GalaxyGenerationAndRoutingAreDeterministic()
    {
        GalaxyGenerationResult firstResult = GalaxyGenerator.Generate(0x5eedUL);
        GalaxyGenerationResult secondResult = GalaxyGenerator.Generate(0x5eedUL);
        True(firstResult.Succeeded, $"First galaxy generation failed: {firstResult.Code}.");
        True(secondResult.Succeeded, $"Second galaxy generation failed: {secondResult.Code}.");

        GalaxyState first = firstResult.Galaxy!;
        GalaxyState second = secondResult.Galaxy!;
        VoyageNavigationState firstNavigation = firstResult.Navigation!;
        Equal(16, first.Topology.Systems.Count, "The voyage galaxy did not contain sixteen systems.");
        True(GalaxyValidator.Validate(first).Accepted, "The generated galaxy failed validation.");
        True(first.Topology.Systems.Values.OrderBy(value => value.Id)
                .SequenceEqual(second.Topology.Systems.Values.OrderBy(value => value.Id)),
            "The same seed produced different systems.");
        True(first.Topology.Starways.Values.OrderBy(value => value.Id)
                .SequenceEqual(second.Topology.Starways.Values.OrderBy(value => value.Id)),
            "The same seed produced different Starways.");
        True(first.StarwaysFrom(firstNavigation.CurrentSystemId).Count() >= 2,
            "The starting anchorage did not have two outward Starways.");

        StarSystemId destination = first.Topology.Systems.Values.OrderBy(value => value.Ordinal).Last().Id;
        GalaxyRouteResult unknown = GalaxyRoutePlanner.Plan(
            first, firstNavigation.CurrentSystemId, destination, GalaxyRoutePreference.TravelTime);
        Equal(GalaxyRouteCode.DestinationUnknown, unknown.Code,
            "Route planning exposed an unknown destination.");

        ImmutableDictionary<StarSystemId, GalaxyKnowledgeLevel> charted = first.Topology.Systems.Keys
            .ToImmutableDictionary(id => id, _ => GalaxyKnowledgeLevel.Charted);
        GalaxyState knownGalaxy = first with { Knowledge = new GalaxyKnowledgeState(charted) };
        GalaxyRouteResult route = GalaxyRoutePlanner.Plan(
            knownGalaxy, firstNavigation.CurrentSystemId, destination, GalaxyRoutePreference.TravelTime);
        True(route.Succeeded, $"A connected charted galaxy did not produce a route: {route.Code}.");
        Equal(firstNavigation.CurrentSystemId, route.Systems[0], "The route began at the wrong system.");
        Equal(destination, route.Systems[^1], "The route ended at the wrong system.");
        Equal(route.Systems.Length - 1, route.Starways.Length, "The route edge count was inconsistent.");

        GalaxyRouteResult bounded = GalaxyRoutePlanner.Plan(
            knownGalaxy, firstNavigation.CurrentSystemId, destination, GalaxyRoutePreference.TravelTime, 1);
        Equal(GalaxyRouteCode.CapacityExceeded, bounded.Code,
            "The route search ignored its expansion budget.");

        ImmutableDictionary<StarwayId, StarwayDynamicState> blockedDepartures = knownGalaxy
            .StarwaysFrom(firstNavigation.CurrentSystemId)
            .ToImmutableDictionary(value => value.Id, _ => new StarwayDynamicState(true, 0, null));
        GalaxyState blockedGalaxy = knownGalaxy with
        {
            Dynamic = new GalaxyDynamicState(blockedDepartures, ImmutableHashSet<ContentId>.Empty),
        };
        True(GalaxyValidator.Validate(blockedGalaxy).Accepted,
            "A valid dynamic Starway closure failed validation.");
        Equal(GalaxyRouteCode.NoRoute, GalaxyRoutePlanner.Plan(
                blockedGalaxy,
                firstNavigation.CurrentSystemId,
                destination,
                GalaxyRoutePreference.TravelTime).Code,
            "Route planning traversed a dynamically blocked Starway.");

        World world = World.Create(
            0x5eedUL,
            new ContentFingerprint(new string('a', 64)),
            TestWorldTime,
            TestCalendar,
            TestTimeScale,
            new TeamId("team.player"),
            [CreateShip(new ShipId("ship.galaxy.player"), new TeamId("team.player"), "ship.path.arcane")],
            galaxy: first,
            voyageNavigation: firstNavigation);
        Equal(firstNavigation, world.Snapshot().VoyageNavigation!,
            "World snapshot did not preserve voyage navigation.");
    }
}
