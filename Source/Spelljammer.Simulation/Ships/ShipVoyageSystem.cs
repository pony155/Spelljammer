using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Galaxy;

namespace Spelljammer.Simulation.Ships;

public sealed record VoyageLegQuote(
    StarwayId StarwayId,
    ResourceId ResourceId,
    int ResourceCost,
    int TravelTicks);

public sealed record VoyageLegQuoteResult(VoyageLegQuote? Quote, string RejectionCode)
{
    public bool Accepted => Quote is not null;
}

public static class ShipVoyageSystem
{
    public static VoyageLegQuoteResult Quote(ShipState ship, StarwayState starway)
    {
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentNullException.ThrowIfNull(starway);
        InstalledModuleState[] propulsion = [.. ship.Modules
            .Where(value => value.Definition.Propulsion is not null && value.IsOn &&
                value.Condition != ModuleCondition.Disabled && value.Integrity > 0)
            .OrderBy(value => value.InstanceId)];
        if (propulsion.Length != 1)
        {
            return new(null, "voyage.propulsion-unavailable");
        }

        PropulsionCostDefinition contract = propulsion[0].Definition.Propulsion!;
        int cost;
        try
        {
            cost = contract.Calculate(starway.FuelCost);
        }
        catch (OverflowException)
        {
            return new(null, "voyage.cost-overflow");
        }

        return new(new VoyageLegQuote(starway.Id, contract.ResourceId, cost, starway.TravelTime), string.Empty);
    }
}
