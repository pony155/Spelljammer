using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Statuses;

public enum EffectType : byte
{
    HealHealth,
    RestoreMana,
    RestoreStamina,
    RestoreResolve,
    ReduceStrain,
    PhysicalDamage,
    ThermalDamage,
    ShockDamage,
    ArcaneDamage,
    ArmorDamage,
    ApplyStatus,
    RemoveStatus,
    ModifyDamage,
    ModifyDefense,
    ModifyAccuracy,
    ModifyMovement,
    ModifyResistance,
    GrantShield,
}

/// <summary>One authored gameplay result. Source and target belong to the runtime request.</summary>
public sealed record EffectDefinition(
    EffectId EffectId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    EffectType Type,
    int Amount,
    StatusId? StatusId,
    int Duration,
    int Stacks,
    int Potency)
    : ContentDefinition(EffectId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);
