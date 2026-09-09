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

        int[] supportedSizes = [16, 64, 128, 256, 512, 1_024];
        foreach (GalaxyShape shape in Enum.GetValues<GalaxyShape>())
        {
            foreach (int systemCount in supportedSizes)
            {
                GalaxyGenerationResult shapedResult = GalaxyGenerator.Generate(
                    0x5eedUL,
                    new GalaxyGenerationSettings(systemCount, shape));
                True(shapedResult.Succeeded,
                    $"{shape} galaxy generation with {systemCount} systems failed: {shapedResult.Code}.");
                Equal(systemCount, shapedResult.Galaxy!.Topology.Systems.Count,
                    $"{shape} galaxy generation returned the wrong system count.");
                True(GalaxyValidator.Validate(shapedResult.Galaxy).Accepted,
                    $"{shape} galaxy generation failed validation.");
                True(shapedResult.Galaxy.StarwaysFrom(shapedResult.Navigation!.CurrentSystemId).Count() >= 2,
                    $"{shape} galaxy generation left the starting anchorage with fewer than two routes.");
            }
        }

        GalaxyGenerationResult spiral = GalaxyGenerator.Generate(
            0x5eedUL, new GalaxyGenerationSettings(16, GalaxyShape.Spiral));
        GalaxyGenerationResult ring = GalaxyGenerator.Generate(
            0x5eedUL, new GalaxyGenerationSettings(16, GalaxyShape.Ring));
        True(!spiral.Galaxy!.Topology.Systems.Values.OrderBy(value => value.Ordinal)
                .SequenceEqual(ring.Galaxy!.Topology.Systems.Values.OrderBy(value => value.Ordinal)),
            "Different galaxy shapes produced the same system layout.");

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
        GalaxyState blockedGalaxy = knownGalaxy with {
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

        world = world with { Galaxy = knownGalaxy, ShipPaused = false };
        ShipState voyageShip = world.Ships.Values.Single();
        WorldCommand plan = new(
            new ContentId("command.galaxy.plan"),
            WorldCommandKind.PlanRoute,
            world.Tick,
            0,
            voyageShip.Id.Value,
            destination.Value,
            FixedVector2.Zero,
            0,
            new ContentId("route-preference.travel-time"),
            1);
        WorldCommandResult planned = world.Enqueue(plan);
        True(planned.Accepted, planned.RejectionCode);
        world = planned.World.Advance(2).World;
        True(!world.VoyageNavigation!.PlannedRoute.IsEmpty, "PlanRoute did not commit a route.");

        StarwayId firstLegId = world.VoyageNavigation.PlannedRoute[0];
        StarwayState firstLeg = world.Galaxy!.Topology.Starways[firstLegId];
        StarSystemId nextSystem = firstLeg.FirstSystemId == world.VoyageNavigation.CurrentSystemId
            ? firstLeg.SecondSystemId
            : firstLeg.FirstSystemId;
        VoyageLegQuote quote = ShipVoyageSystem.Quote(voyageShip, firstLeg).Quote!;
        int resourceBefore = world.Ships[voyageShip.Id].Resources[quote.ResourceId];
        ShipState emptyShip = world.Ships[voyageShip.Id] with {
            Resources = world.Ships[voyageShip.Id].Resources.SetItem(quote.ResourceId, 0),
        };
        World insufficient = world with { Ships = world.Ships.SetItem(emptyShip.Id, emptyShip) };
        WorldCommand insufficientBegin = new(
            new ContentId("command.galaxy.begin-insufficient"), WorldCommandKind.BeginVoyage, insufficient.Tick, 0,
            emptyShip.Id.Value, nextSystem.Value, FixedVector2.Zero, 0, null, 2);
        insufficient = insufficient.Enqueue(insufficientBegin).World.Advance(2).World;
        True(insufficient.VoyageNavigation!.ActiveStarwayId is null,
            "A rejected departure changed the voyage state.");
        Equal(0, insufficient.Ships[emptyShip.Id].Resources[quote.ResourceId],
            "A rejected departure changed the propulsion resource.");
        True(insufficient.Events.Any(value => value.ResultCode == "command.resource-insufficient"),
            "An insufficient-resource departure did not publish its rejection.");

        WorldCommand begin = new(
            new ContentId("command.galaxy.begin"),
            WorldCommandKind.BeginVoyage,
            world.Tick,
            0,
            voyageShip.Id.Value,
            nextSystem.Value,
            FixedVector2.Zero,
            0,
            null,
            2);
        WorldCommandResult begun = world.Enqueue(begin);
        True(begun.Accepted, begun.RejectionCode);
        world = begun.World.Advance(2).World;
        Equal(firstLegId, world.VoyageNavigation!.ActiveStarwayId!.Value,
            "BeginVoyage did not activate the first Starway.");
        Equal(resourceBefore - quote.ResourceCost, world.Ships[voyageShip.Id].Resources[quote.ResourceId],
            "BeginVoyage did not commit the propulsion cost exactly once.");

        for (int index = 0; index < 256 && world.VoyageNavigation!.ActiveStarwayId is not null; index++)
        {
            world = world.Advance(8).World;
        }

        Equal(nextSystem, world.VoyageNavigation!.CurrentSystemId, "Arrival did not update the current system.");
        True(world.VoyageNavigation.ActiveStarwayId is null, "Arrival left the voyage in transit.");
        True(world.Galaxy!.KnowledgeOf(nextSystem) >= GalaxyKnowledgeLevel.Detected,
            "Arrival did not reveal the destination system.");
        True(world.Events.Any(value => value.ResultCode == "voyage.arrived"),
            "Arrival did not publish an authoritative event.");
    }
}
