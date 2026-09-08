using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Ships;

public sealed record ShipDamageEvent(
    ShipId TargetId,
    int Incoming,
    int ShieldAbsorbed,
    int ArmorMitigated,
    int HullDamage,
    ContentId? ModuleInstanceId,
    ModuleCondition? ModuleCondition);

public sealed record ShipDamageResult(ShipState Ship, ShipDamageEvent Event);

public static class ShipDamageSystem
{
    public static ShipDamageResult Apply(
        ShipState ship,
        int incoming,
        int armorPenetration,
        ContentId? selectedModuleInstanceId)
    {
        if (incoming <= 0 || armorPenetration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(incoming));
        }

        int remaining = incoming;
        int shieldAbsorbed = 0;
        ImmutableArray<InstalledModuleState>.Builder modules = ship.Modules.ToBuilder();
        for (int index = 0; index < modules.Count && remaining > 0; index++)
        {
            InstalledModuleState module = modules[index];
            if (!module.ShieldRaised || !module.IsPowered || module.CurrentShield <= 0)
            {
                continue;
            }

            int absorbed = Math.Min(remaining, module.CurrentShield);
            remaining -= absorbed;
            shieldAbsorbed += absorbed;
            modules[index] = module with { CurrentShield = module.CurrentShield - absorbed };
        }

        int armorMitigated = Math.Min(remaining, Math.Max(0, ship.Armor - armorPenetration));
        remaining -= armorMitigated;
        int hullDamage = Math.Min(ship.Hull, remaining);
        ModuleCondition? resultingCondition = null;
        if (selectedModuleInstanceId is ContentId selected && hullDamage > 0)
        {
            int index = -1;
            for (int candidate = 0; candidate < modules.Count; candidate++)
            {
                if (modules[candidate].InstanceId == selected)
                {
                    index = candidate;
                    break;
                }
            }

            if (index >= 0)
            {
                InstalledModuleState module = modules[index];
                int integrity = Math.Max(0, module.Integrity - hullDamage);
                resultingCondition = integrity == 0 ? ModuleCondition.Disabled : ModuleCondition.Damaged;
                modules[index] = module with
                {
                    Integrity = integrity,
                    Condition = resultingCondition.Value,
                    WeaponReadiness = module.Weapon is null ? module.WeaponReadiness : WeaponReadiness.Damaged,
                };
            }
        }

        ShipState committed = ship with
        {
            Hull = ship.Hull - hullDamage,
            Modules = modules.MoveToImmutable(),
            PersistentEvidence = ship.PersistentEvidence.Add(new ContentId("evidence.ship.damage")),
        };
        return new ShipDamageResult(committed, new ShipDamageEvent(
            ship.Id,
            incoming,
            shieldAbsorbed,
            armorMitigated,
            hullDamage,
            selectedModuleInstanceId,
            resultingCondition));
    }
}
