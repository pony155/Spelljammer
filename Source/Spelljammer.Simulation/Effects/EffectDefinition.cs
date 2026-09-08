using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Effects;

/// <summary>
/// Defines effect kinds, typed payloads, target selection, timing, and authored effect applications.
/// </summary>
/// <remarks>
/// Code flow: Content links each application to an immutable effect definition, action systems select targets and timing, and the effect system dispatches the typed payload.
/// </remarks>
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
    EmitEvent,
}

public enum EffectTargetSelector : byte
{
    DeclaredTarget,
    Source,
}

public enum EffectTiming : byte
{
    Instant,
    Delayed,
    ForTurns,
    UntilRemoved,
    WhileEquipped,
    WhileCondition,
}

public abstract record EffectPayload(EffectType Type);

public sealed record ResourceEffectPayload(EffectType Type, ResourceId ResourceId, int Amount)
    : EffectPayload(Type);

public sealed record DamageEffectPayload(EffectType Type, int Amount)
    : EffectPayload(Type);

public sealed record ApplyStatusEffectPayload(StatusId StatusId, int? Duration, int Stacks, int Potency)
    : EffectPayload(EffectType.ApplyStatus);

public sealed record RemoveStatusEffectPayload(StatusId StatusId)
    : EffectPayload(EffectType.RemoveStatus);

public sealed record ModifierEffectPayload(EffectType Type, int Amount)
    : EffectPayload(Type);

public sealed record GrantShieldEffectPayload(int Amount)
    : EffectPayload(EffectType.GrantShield);

public sealed record EmitEventEffectPayload(ContentId EventId)
    : EffectPayload(EffectType.EmitEvent);

/// <summary>One authored gameplay operation with strongly typed parameters.</summary>
public sealed record EffectDefinition(
    EffectId EffectId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    EffectPayload Payload)
    : ContentDefinition(EffectId.Value, SchemaVersion, Revision, NameKey, DescriptionKey)
{
    public EffectType Type => Payload.Type;
}

/// <summary>Describes how an authored Effect is delivered by an action, item, Feat, or Status.</summary>
public sealed record EffectApplicationDefinition(
    EffectId EffectId,
    EffectTargetSelector TargetSelector,
    EffectTiming Timing,
    int Delay,
    int Duration,
    int Interval,
    int Probability,
    int Power,
    ImmutableArray<string> Tags)
{
    public static EffectApplicationDefinition InstantTarget(EffectId effectId) => new(
        effectId,
        EffectTargetSelector.DeclaredTarget,
        EffectTiming.Instant,
        0,
        0,
        0,
        100,
        0,
        []);
}

public static class CombatEffectIds
{
    public static readonly EffectId PhysicalDamage = new("effect.combat.physical-damage");
    public static readonly EffectId ArmorDamage = new("effect.combat.armor-damage");
}
