using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.World;

namespace Spelljammer.Simulation.Ships;

/// <summary>
/// Validates and applies module installation changes to a ship loadout.
/// </summary>
/// <remarks>
/// Code flow: A requested module and slot are resolved through the content catalog, compatibility and capacity rules are checked, and an accepted request returns a replacement ship.
/// </remarks>
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
