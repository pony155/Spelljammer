using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;
using Spelljammer.Simulation.Items;

namespace Spelljammer.Simulation.Combat;

/// <summary>Definitions consumed by character action, weapon, status, and effect resolution.</summary>
public interface ICombatContentCatalog : ICharacterStateCatalog
{
    ImmutableArray<EquipmentDefinition> Equipment { get; }
    ImmutableArray<ItemDefinition> Items { get; }
    ImmutableArray<MeleeWeaponDefinition> MeleeWeapons { get; }
    ImmutableArray<MeleeWeaponActionDefinition> MeleeWeaponActions { get; }
    ImmutableArray<RangedWeaponDefinition> RangedWeapons { get; }
    ImmutableArray<AmmunitionDefinition> Ammunition { get; }
    ImmutableArray<RangedWeaponActionDefinition> RangedWeaponActions { get; }
    ImmutableArray<EffectDefinition> Effects { get; }
    ImmutableArray<StatusDefinition> Statuses { get; }

    bool TryGetMeleeWeapon(MeleeWeaponId id, out MeleeWeaponDefinition? definition);
    bool TryGetMeleeWeaponAction(MeleeWeaponActionId id, out MeleeWeaponActionDefinition? definition);
    bool TryGetRangedWeapon(RangedWeaponId id, out RangedWeaponDefinition? definition);
    bool TryGetAmmunition(AmmunitionId id, out AmmunitionDefinition? definition);
    bool TryGetRangedWeaponAction(RangedWeaponActionId id, out RangedWeaponActionDefinition? definition);
}
