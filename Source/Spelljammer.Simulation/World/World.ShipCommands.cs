using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Ships;

namespace Spelljammer.Simulation.World;

/// <summary>
/// Implements ship-command commitment for the authoritative world state.
/// </summary>
/// <remarks>
/// Code flow: A queued ship command resolves its actor and target, reserves required resources, delegates movement or combat rules, and appends the resulting world event.
/// </remarks>
public sealed partial record World
{
    private World CommitShip(WorldCommand command, ResourceId? reservedResource, int reservedAmount)
    {
        if (!TryShip(command.IssuerId, out ShipState? actor))
        {
            return AddEvent(command, false, 0, "command.actor-missing");
        }

        ShipState ship = actor!;
        switch (command.Kind)
        {
            case WorldCommandKind.Scan:
                if (!TryShip(command.TargetId, out ShipState? scanned))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                ShipContactState contact = new(
                    scanned!.Id,
                    new ContentId("knowledge.contact.scanned"),
                    Tick,
                    ShipGeometry.Range(ship.Position, scanned.Position) != ShipRange.Beyond,
                    command.OptionId is ContentId witnessId && BattleUnitIdFrom(witnessId, out BattleUnitId witness)
                        ? ImmutableHashSet.Create(witness)
                        : ImmutableHashSet<BattleUnitId>.Empty);
                ship = ship with { Contacts = ship.Contacts.SetItem(scanned.Id, contact) };
                break;
            case WorldCommandKind.Course:
            case WorldCommandKind.Thrust:
                ship = ship with { Velocity = command.Vector };
                break;
            case WorldCommandKind.Turn:
                ship = ship with { HeadingMilliDegrees = NormalizeHeading(ship.HeadingMilliDegrees + command.Amount) };
                break;
            case WorldCommandKind.Brake:
                ship = ship with { Velocity = FixedVector2.Zero };
                break;
            case WorldCommandKind.Intercept:
                if (!TryShip(command.TargetId, out ShipState? intercepted))
                {
                    return AddEvent(command, false, 0, "command.target-stale");
                }

                ship = ship with { Velocity = Direction(ship.Position, intercepted!.Position) };
                break;
            case WorldCommandKind.Fire:
                return CommitFire(command, ship, reservedResource, reservedAmount);
            case WorldCommandKind.Ram:
                return CommitRam(command, ship);
            case WorldCommandKind.RaiseShield:
            case WorldCommandKind.LowerShield:
                bool raised = command.Kind == WorldCommandKind.RaiseShield;
                ship = ship with {
                    Modules = [.. ship.Modules.Select(value => value.Definition.ShieldValue > 0 ? value with { ShieldRaised = raised } : value)],
                };
                break;
            case WorldCommandKind.Defend:
                ship = ship with { Defending = true };
                break;
            case WorldCommandKind.DamageControl:
                ship = RepairModule(ship, command.OptionId);
                break;
            case WorldCommandKind.Signal:
                ship = ship with { PersistentEvidence = ship.PersistentEvidence.Add(new ContentId("evidence.ship.signal")) };
                break;
            case WorldCommandKind.Retreat:
                ship = ship with { Disengaged = true, PersistentEvidence = ship.PersistentEvidence.Add(new ContentId("evidence.ship.escape")) };
                break;
            default:
                return AddEvent(command, false, 0, "command.action-unknown");
        }

        return (this with { Ships = Ships.SetItem(ship.Id, ship) }).AddEvent(command, true, command.Amount, string.Empty);
    }

    private World CommitFire(WorldCommand command, ShipState attacker, ResourceId? resourceId, int resourceCost)
    {
        if (!TryShip(command.TargetId, out ShipState? target) || resourceId is null ||
            !attacker.Contacts.TryGetValue(target!.Id, out ShipContactState? contact) || !contact.HasFiringSolution)
        {
            return AddEvent(command, false, 0, "command.firing-solution-required");
        }

        int batteryIndex = IndexOf(attacker.Modules, value => value.Weapon is not null);
        InstalledModuleState battery = attacker.Modules[batteryIndex];
        ShipWeaponConfigurationDefinition weapon = battery.Weapon!;
        long distance = Math.Max(
            Math.Abs(attacker.Position.X.Raw - target.Position.X.Raw),
            Math.Abs(attacker.Position.Y.Raw - target.Position.Y.Raw));
        if (distance > weapon.MaximumRange)
        {
            return AddEvent(command, false, 0, "command.target-out-of-range");
        }

        attacker.Resources.TryGetValue(resourceId.Value, out int available);
        if (available < resourceCost)
        {
            return AddEvent(command, false, 0, "command.resource-insufficient");
        }

        ContentId moduleTarget = target.Modules.OrderBy(value => value.InstanceId).First().InstanceId;
        ShipDamageResult damage = ShipDamageSystem.Apply(target, weapon.Damage, weapon.ArmorPenetration, moduleTarget);
        attacker = attacker with {
            Resources = attacker.Resources.SetItem(resourceId.Value, available - resourceCost),
            Modules = attacker.Modules.SetItem(batteryIndex, battery with {
                WeaponReadiness = WeaponReadiness.Reloading,
                ReadyTick = Tick + Math.Max(weapon.ReloadTicks, weapon.RateOfFireTicks),
            }),
            PersistentEvidence = attacker.PersistentEvidence.Add(new ContentId("evidence.ship.weapon-fired")),
        };
        return (this with {
            Ships = Ships.SetItem(attacker.Id, attacker).SetItem(target.Id, damage.Ship),
        }).AddEvent(command, true, damage.Event.HullDamage, string.Empty);
    }

    private World CommitRam(WorldCommand command, ShipState attacker)
    {
        if (!TryShip(command.TargetId, out ShipState? target) || ShipGeometry.Range(attacker.Position, target!.Position) != ShipRange.Contact)
        {
            return AddEvent(command, false, 0, "command.target-out-of-range");
        }

        ShipDamageResult targetDamage = ShipDamageSystem.Apply(target, Math.Max(1, command.Amount), 2, target.Modules[0].InstanceId);
        ShipDamageResult selfDamage = ShipDamageSystem.Apply(attacker, Math.Max(1, command.Amount / 2), 0, attacker.Modules[0].InstanceId);
        return (this with {
            Ships = Ships.SetItem(attacker.Id, selfDamage.Ship).SetItem(target.Id, targetDamage.Ship),
        }).AddEvent(command, true, targetDamage.Event.HullDamage, string.Empty);
    }

    private World UpdateShips()
    {
        ImmutableDictionary<ShipId, ShipState>.Builder ships = Ships.ToBuilder();
        foreach (ShipState original in Ships.Values.OrderBy(value => value.Id))
        {
            ShipState ship = original with { Position = original.Position + original.Velocity };
            foreach (NetworkId network in ship.Modules.Select(value => value.Definition.NetworkId).Distinct().Order())
            {
                ship = ShipPowerSystem.Allocate(ship, network, [.. ship.Modules.Select(value => value.InstanceId)]).Ship;
            }

            ship = ship with {
                Modules = [.. ship.Modules.Select(value => value.Weapon is not null && value.WeaponReadiness == WeaponReadiness.Reloading && value.ReadyTick <= Tick
                    ? value with { WeaponReadiness = WeaponReadiness.Ready }
                    : value)],
            };
            ships[ship.Id] = ship;
        }

        return this with { Ships = ships.ToImmutable() };
    }

    private ShipState RepairModule(ShipState ship, ContentId? instanceId)
    {
        if (instanceId is not ContentId id)
        {
            return ship;
        }

        int index = IndexOf(ship.Modules, value => value.InstanceId == id);
        ResourceId parts = new("resource.spare-parts");
        ship.Resources.TryGetValue(parts, out int available);
        if (index < 0 || available <= 0)
        {
            return ship;
        }

        InstalledModuleState module = ship.Modules[index];
        int integrity = Math.Min(module.Definition.MaximumIntegrity, module.Integrity + 3);
        return ship with {
            Resources = ship.Resources.SetItem(parts, available - 1),
            Modules = ship.Modules.SetItem(index, module with {
                Integrity = integrity,
                Condition = integrity == module.Definition.MaximumIntegrity ? ModuleCondition.Intact : ModuleCondition.Damaged,
                WeaponReadiness = module.Weapon is null ? module.WeaponReadiness : WeaponReadiness.Ready,
            }),
        };
    }

    private bool TryShip(ContentId id, out ShipState? ship)
    {
        foreach ((ShipId shipId, ShipState value) in Ships)
        {
            if (shipId.Value == id)
            {
                ship = value;
                return true;
            }
        }

        ship = null;
        return false;
    }

    private static int NormalizeHeading(int value)
    {
        int result = value % 360_000;
        return result < 0 ? result + 360_000 : result;
    }

    private static FixedVector2 Direction(FixedVector2 from, FixedVector2 to) => new(
        new FixedScalar(Math.Sign(to.X.Raw - from.X.Raw) * FixedScalar.Scale),
        new FixedScalar(Math.Sign(to.Y.Raw - from.Y.Raw) * FixedScalar.Scale));
}
