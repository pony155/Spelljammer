using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Encounters;

public enum ModuleCondition : byte
{
    Intact,
    Damaged,
    Disabled,
}

public enum WeaponReadiness : byte
{
    Ready,
    Reloading,
    Depleted,
    Damaged,
}

public sealed record InstalledModuleState(
    ContentId InstanceId,
    ShipModuleDefinition Definition,
    ModuleCondition Condition,
    int Integrity,
    bool IsOn,
    bool IsPowered,
    bool ShieldRaised,
    int CurrentShield,
    ShipWeaponConfigurationDefinition? Weapon,
    WeaponReadiness WeaponReadiness,
    long ReadyTick);

public sealed record ShipContactState(
    ShipId ShipId,
    ContentId KnowledgeId,
    long LastObservedTick,
    bool HasFiringSolution,
    ImmutableHashSet<ActorId> Witnesses);

public sealed record ShipState(
    ShipId Id,
    TeamId TeamId,
    ShipFrameDefinition Frame,
    ContentId PathId,
    int Hull,
    int Armor,
    int Cargo,
    FixedVector2 Position,
    FixedVector2 Velocity,
    int HeadingMilliDegrees,
    int CollisionRadius,
    ImmutableArray<InstalledModuleState> Modules,
    ImmutableDictionary<ResourceId, int> Resources,
    ImmutableDictionary<ShipId, ShipContactState> Contacts,
    ImmutableHashSet<ContentId> PersistentEvidence,
    bool Disengaged,
    bool Defending)
{
    public int MaximumShield => Modules.Where(value => value.Definition.ShieldValue > 0).Sum(value => value.Definition.ShieldValue);
    public int CurrentShield => Modules.Sum(value => value.CurrentShield);
}

public sealed record ShipLoadoutResult(ShipState? Ship, string RejectionCode)
{
    public bool Accepted => Ship is not null;
}

public static class ShipLoadoutSystem
{
    public const int MaximumModules = 32;

    public static ShipLoadoutResult Create(
        ShipId shipId,
        TeamId teamId,
        ShipFrameDefinition frame,
        ContentId pathId,
        IEnumerable<ShipModuleDefinition> modules,
        ShipWeaponConfigurationDefinition weapon,
        ImmutableDictionary<ResourceId, int> resources)
    {
        ShipModuleDefinition[] ordered = [.. modules.OrderBy(value => value.ModuleId)];
        int slotCost = ordered.Sum(value => value.SlotCost);
        int displacement = ordered.Sum(value => value.CargoDisplacement);
        if (ordered.Length is 0 or > MaximumModules || slotCost > frame.MaximumSlots ||
            displacement > frame.CargoCapacity || ordered.Select(value => value.MountId).Distinct().Count() != ordered.Length ||
            ordered.Any(value => !value.CompatiblePathIds.Contains(pathId)))
        {
            return new ShipLoadoutResult(null, "ship.loadout-invalid");
        }

        ShipModuleDefinition? battery = ordered.SingleOrDefault(value => value.MountId == new ContentId("mount.weapon"));
        if (battery is null || battery.NetworkId != weapon.NetworkId || !resources.ContainsKey(weapon.ResourceId))
        {
            return new ShipLoadoutResult(null, "ship.weapon-incompatible");
        }

        ImmutableArray<InstalledModuleState>.Builder installed = ImmutableArray.CreateBuilder<InstalledModuleState>(ordered.Length);
        for (int index = 0; index < ordered.Length; index++)
        {
            ShipModuleDefinition definition = ordered[index];
            bool isBattery = definition.ModuleId == battery.ModuleId;
            installed.Add(new InstalledModuleState(
                new ContentId($"module-instance.first-voyage.slot-{index}"),
                definition,
                ModuleCondition.Intact,
                definition.MaximumIntegrity,
                true,
                false,
                false,
                definition.ShieldValue,
                isBattery ? weapon : null,
                isBattery ? WeaponReadiness.Ready : WeaponReadiness.Depleted,
                0));
        }

        ShipState candidate = new(
            shipId,
            teamId,
            frame,
            pathId,
            frame.MaximumHull,
            frame.BaseArmor + ordered.Sum(value => value.ArmorValue),
            0,
            FixedVector2.Zero,
            FixedVector2.Zero,
            0,
            2_000,
            installed.MoveToImmutable(),
            resources,
            ImmutableDictionary<ShipId, ShipContactState>.Empty,
            ImmutableHashSet<ContentId>.Empty,
            false,
            false);
        return new ShipLoadoutResult(candidate, string.Empty);
    }
}

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

        return new PowerAllocationResult(ship with { Modules = [.. modules.OrderBy(value => value.InstanceId)] }, unpowered.MoveToImmutable());
    }
}

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

public enum ShipRange : byte
{
    Contact,
    Near,
    Far,
    Beyond,
}

public static class ShipGeometry
{
    public static ShipRange Range(FixedVector2 left, FixedVector2 right)
    {
        long x = Math.Abs(left.X.Raw - right.X.Raw);
        long y = Math.Abs(left.Y.Raw - right.Y.Raw);
        long distance = Math.Max(x, y);
        return distance switch
        {
            <= 2_000 => ShipRange.Contact,
            <= 10_000 => ShipRange.Near,
            <= 30_000 => ShipRange.Far,
            _ => ShipRange.Beyond,
        };
    }
}
