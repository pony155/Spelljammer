using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Galaxy;
using Spelljammer.Simulation.Ships;

namespace Spelljammer.Simulation.World;

/// <summary>Plans, begins, advances, and completes authoritative voyage legs.</summary>
public sealed partial record World
{
    private World CommitVoyage(WorldCommand command, ResourceId? reservedResource, int reservedAmount) =>
        command.Kind switch
        {
            WorldCommandKind.PlanRoute => CommitRoutePlan(command),
            WorldCommandKind.BeginVoyage => CommitVoyageDeparture(command, reservedResource, reservedAmount),
            _ => AddEvent(command, false, 0, "command.action-unknown"),
        };

    private World CommitRoutePlan(WorldCommand command)
    {
        if (Galaxy is null || VoyageNavigation is null ||
            !TryShip(command.IssuerId, out ShipState? ship) || ship!.TeamId != PlayerTeamId ||
            VoyageNavigation.ActiveStarwayId is not null ||
            !TrySystemId(command.TargetId, out StarSystemId destination) ||
            !TryRoutePreference(command.OptionId, out GalaxyRoutePreference preference))
        {
            return AddEvent(command, false, 0, "voyage.plan-invalid");
        }

        GalaxyRouteResult result = GalaxyRoutePlanner.Plan(
            Galaxy, VoyageNavigation.CurrentSystemId, destination, preference);
        if (!result.Succeeded)
        {
            return AddEvent(command, false, 0, result.Code switch
            {
                GalaxyRouteCode.DestinationUnknown => "voyage.destination-unknown",
                GalaxyRouteCode.CapacityExceeded => "voyage.search-capacity",
                GalaxyRouteCode.NoRoute => "voyage.route-unavailable",
                _ => "voyage.plan-invalid",
            });
        }

        VoyageNavigationState navigation = VoyageNavigation with
        {
            PlannedRoute = result.Starways,
            RouteProgress = 0,
            DepartureTick = Tick,
            ArrivalTick = Tick,
        };
        return (this with { VoyageNavigation = navigation })
            .AddEvent(command, true, result.TotalCost, "voyage.route-planned");
    }

    private World CommitVoyageDeparture(
        WorldCommand command,
        ResourceId? reservedResource,
        int reservedAmount)
    {
        if (!TryResolveNextLeg(command, out ShipState? ship, out _, out VoyageLegQuote? quote, out string rejection) ||
            reservedResource != quote!.ResourceId || reservedAmount != quote.ResourceCost)
        {
            return AddEvent(command, false, 0, string.IsNullOrEmpty(rejection) ? "voyage.route-stale" : rejection);
        }

        ship!.Resources.TryGetValue(quote.ResourceId, out int available);
        if (available < quote.ResourceCost)
        {
            return AddEvent(command, false, 0, "command.resource-insufficient");
        }

        long arrivalTick;
        try
        {
            arrivalTick = checked(Tick + quote.TravelTicks);
        }
        catch (OverflowException)
        {
            return AddEvent(command, false, 0, "voyage.time-overflow");
        }

        ShipState committedShip = ship with
        {
            Resources = ship.Resources.SetItem(quote.ResourceId, available - quote.ResourceCost),
        };
        VoyageNavigationState navigation = VoyageNavigation! with
        {
            ActiveStarwayId = quote.StarwayId,
            RouteProgress = 0,
            DepartureTick = Tick,
            ArrivalTick = arrivalTick,
        };
        return (this with
        {
            Ships = Ships.SetItem(committedShip.Id, committedShip),
            VoyageNavigation = navigation,
        }).AddEvent(command, true, quote.ResourceCost, "voyage.departed");
    }

    private World UpdateVoyage()
    {
        if (Galaxy is null || VoyageNavigation?.ActiveStarwayId is not StarwayId activeId ||
            !Galaxy.Topology.Starways.TryGetValue(activeId, out StarwayState? starway))
        {
            return this;
        }

        if (Tick < VoyageNavigation.ArrivalTick)
        {
            long duration = VoyageNavigation.ArrivalTick - VoyageNavigation.DepartureTick;
            int progress = duration <= 0
                ? 10_000
                : (int)Math.Clamp(
                    ((Tick - VoyageNavigation.DepartureTick) * 10_000L) / duration,
                    0,
                    9_999);
            return this with { VoyageNavigation = VoyageNavigation with { RouteProgress = progress } };
        }

        StarSystemId destination = starway.FirstSystemId == VoyageNavigation.CurrentSystemId
            ? starway.SecondSystemId
            : starway.FirstSystemId;
        VoyageNavigationState arrived = VoyageNavigation with
        {
            CurrentSystemId = destination,
            ActiveStarwayId = null,
            PlannedRoute = VoyageNavigation.PlannedRoute.IsEmpty ? [] : VoyageNavigation.PlannedRoute.RemoveAt(0),
            RouteProgress = 0,
            DepartureTick = Tick,
            ArrivalTick = Tick,
        };
        GalaxyState discovered = Galaxy.WithKnowledge(destination, GalaxyKnowledgeLevel.Detected);
        ShipState? playerShip = Ships.Values.Where(value => value.TeamId == PlayerTeamId).OrderBy(value => value.Id).FirstOrDefault();
        World world = this with { Galaxy = discovered, VoyageNavigation = arrived };
        if (playerShip is null)
        {
            return world;
        }

        WorldCommand arrival = new(
            new ContentId($"command.voyage.arrival-{RandomSequence % 1_000_000}"),
            WorldCommandKind.BeginVoyage,
            Tick,
            0,
            playerShip.Id.Value,
            destination.Value,
            FixedVector2.Zero,
            0,
            null,
            RandomSequence);
        return world.AddEvent(arrival, true, 0, "voyage.arrived");
    }

    private bool TryResolveNextLeg(
        WorldCommand command,
        out ShipState? ship,
        out StarwayState? starway,
        out VoyageLegQuote? quote,
        out string rejection)
    {
        ship = null;
        starway = null;
        quote = null;
        rejection = "voyage.route-unavailable";
        if (Galaxy is null || VoyageNavigation is null || VoyageNavigation.ActiveStarwayId is not null ||
            VoyageNavigation.PlannedRoute.IsEmpty || !TryShip(command.IssuerId, out ship) || ship!.TeamId != PlayerTeamId)
        {
            return false;
        }

        StarwayId routeId = VoyageNavigation.PlannedRoute[0];
        if (!Galaxy.Topology.Starways.TryGetValue(routeId, out starway) || Galaxy.Dynamic.StateOf(routeId).IsBlocked)
        {
            rejection = "voyage.route-blocked";
            return false;
        }

        StarSystemId next;
        if (starway.FirstSystemId == VoyageNavigation.CurrentSystemId)
        {
            next = starway.SecondSystemId;
        }
        else if (starway.SecondSystemId == VoyageNavigation.CurrentSystemId)
        {
            next = starway.FirstSystemId;
        }
        else
        {
            rejection = "voyage.route-stale";
            return false;
        }

        if (command.TargetId != next.Value)
        {
            rejection = "voyage.destination-mismatch";
            return false;
        }

        VoyageLegQuoteResult result = ShipVoyageSystem.Quote(ship, starway);
        if (!result.Accepted)
        {
            rejection = result.RejectionCode;
            return false;
        }

        quote = result.Quote;
        rejection = string.Empty;
        return true;
    }

    private static bool TrySystemId(ContentId id, out StarSystemId systemId)
    {
        try
        {
            systemId = new StarSystemId(id);
            return true;
        }
        catch (ArgumentException)
        {
            systemId = default;
            return false;
        }
    }

    private static bool TryRoutePreference(ContentId? id, out GalaxyRoutePreference preference)
    {
        preference = id?.ToString() switch
        {
            "route-preference.travel-time" => GalaxyRoutePreference.TravelTime,
            "route-preference.fuel" => GalaxyRoutePreference.Fuel,
            "route-preference.known-danger" => GalaxyRoutePreference.KnownDanger,
            _ => (GalaxyRoutePreference)byte.MaxValue,
        };
        return Enum.IsDefined(preference);
    }

    private static bool IsVoyage(WorldCommandKind kind) =>
        kind is WorldCommandKind.PlanRoute or WorldCommandKind.BeginVoyage;
}
