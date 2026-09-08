using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Effects;

namespace Spelljammer.Simulation.Combat;

public enum MeleeWeaponFamily : byte
{
    Blade,
    Dagger,
    Axe,
    Blunt,
    Polearm,
    Fist,
}

public enum MeleeWeaponTechnology : byte
{
    Conventional,
    Chain,
    Shock,
    Powered,
    Arcane,
}

public enum MeleeWeaponHands : byte
{
    OneHanded,
    TwoHanded,
}

public sealed record MeleeWeaponActionDefinition(
    MeleeWeaponActionId MeleeWeaponActionId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    int ActionPointCost,
    int StaminaCostModifier,
    int HitModifier,
    int DamagePercentage,
    int ArmorDamagePercentage,
    int ArmorPenetrationModifier,
    int RangeModifier,
    int EnergyCostModifier,
    int DurabilityCost,
    ImmutableArray<EffectApplicationDefinition> Effects)
    : ContentDefinition(MeleeWeaponActionId.Value, SchemaVersion, Revision, NameKey, DescriptionKey)
{
    public ImmutableArray<EffectId> EffectIds => [.. Effects.Select(value => value.EffectId)];
}

public enum RangedWeaponFamily : byte
{
    Bow,
    Crossbow,
    Pistol,
    Rifle,
    Shotgun,
    Heavy,
    Launcher,
    Special,
}

public enum RangedWeaponTechnology : byte
{
    Conventional,
    Ballistic,
    Laser,
    Plasma,
    Arcane,
}

public enum RangedWeaponHands : byte
{
    OneHanded,
    TwoHanded,
}

public enum AmmunitionType : byte
{
    Arrow,
    CrossbowBolt,
    PistolRound,
    RifleRound,
    ShotgunShell,
    Grenade,
    Rocket,
    MiniNuke,
    FlamethrowerFuel,
    LaserCell,
    PlasmaCell,
}

public enum RangedWeaponActionKind : byte
{
    Attack,
    Reload,
}

public sealed record RangedWeaponActionDefinition(
    RangedWeaponActionId RangedWeaponActionId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    RangedWeaponActionKind Kind,
    int ActionPointCost,
    int StaminaCostModifier,
    int HitModifier,
    int DamagePercentage,
    int AmmunitionCost,
    int ShotCount,
    int EnergyCostModifier,
    int HeatModifier,
    int DurabilityCost,
    int RangePenaltyPerUnit,
    int DamageFalloffPerUnitPercentage,
    int ReloadAmount,
    ImmutableArray<EffectApplicationDefinition> Effects)
    : ContentDefinition(RangedWeaponActionId.Value, SchemaVersion, Revision, NameKey, DescriptionKey)
{
    public ImmutableArray<EffectId> EffectIds => [.. Effects.Select(value => value.EffectId)];
}
