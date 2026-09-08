using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Ships;

public sealed record PowerAllocationResult(ShipState Ship, ImmutableArray<ContentId> UnpoweredModuleIds);

public static class ShipPowerSystem
{
    public static PowerAllocationResult Allocate(ShipState ship, NetworkId networkId, ImmutableArray<ContentId> priority)
    {
        if (priority.Length > ShipLoadoutSystem.MaximumModules || priority.Distinct().Count() != priority.Length)
        {
            throw new InvalidOperationException("Power priority is invalid or exceeds capacity.");
        }

        int available = ship.Modules
            .Where(value => value.IsOn && value.Condition != ModuleCondition.Disabled && value.Definition.NetworkId == networkId)
            .Sum(value => value.Definition.EnergyGeneration);
        Dictionary<ContentId, int> rank = priority.Select((id, index) => (id, index)).ToDictionary(value => value.id, value => value.index);
        ImmutableArray<ContentId>.Builder unpowered = ImmutableArray.CreateBuilder<ContentId>();
        ImmutableArray<InstalledModuleState>.Builder modules = ImmutableArray.CreateBuilder<InstalledModuleState>(ship.Modules.Length);
        foreach (InstalledModuleState module in ship.Modules
                     .OrderBy(value => rank.GetValueOrDefault(value.InstanceId, int.MaxValue))
                     .ThenBy(value => value.InstanceId))
        {
            int demand = module.Definition.NetworkId == networkId && module.IsOn
                ? module.Definition.EnergyConsumption + (module.ShieldRaised ? module.Definition.ShieldEnergyConsumptionRate : 0)
                : 0;
            bool powered = module.Condition != ModuleCondition.Disabled && demand <= available;
            if (powered)
            {
                available -= demand;
            }
            else if (demand > 0)
            {
                unpowered.Add(module.InstanceId);
            }

            int shield = module.CurrentShield;
            if (powered && module.ShieldRaised && module.Definition.ShieldValue > 0)
            {
                shield = Math.Min(module.Definition.ShieldValue, shield + module.Definition.ShieldRechargeRate);
            }

            modules.Add(module with { IsPowered = powered, CurrentShield = shield });
        }

        return new PowerAllocationResult(ship with { Modules = [.. modules.OrderBy(value => value.InstanceId)] }, unpowered.ToImmutable());
    }
}
