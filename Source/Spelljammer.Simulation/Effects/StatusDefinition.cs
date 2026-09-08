using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Effects;

/// <summary>
/// Defines data-driven status categories, durations, stacking, modifiers, restrictions, and AI rules.
/// </summary>
/// <remarks>
/// Code flow: Content compilation validates authored status data, catalogs index definitions by stable ID, and status systems resolve the rules when applying or querying instances.
/// </remarks>
public enum StatusCategory : byte
{
    Mental,
    Emotion,
    Control,
    Disruption,
    DamageOverTime,
    Defensive,
    Resource,
    Physical,
    Magical,
}

public enum StatusDurationType : byte
{
    Timed,
    Permanent,
    UntilRemoved,
    Conditional,
}

public enum StatusStackPolicy : byte
{
    Refresh,
    Extend,
    IntensityStack,
    StrongerWins,
    Independent,
    Reject,
}

public enum StatusModifierType : byte
{
    ModifyDamage,
    ModifyDefense,
    ModifyAccuracy,
    ModifyMovement,
    ModifyResistance,
    ModifyResolve,
    GrantShield,
}

public enum StatusRestrictionType : byte
{
    CannotAct,
    CannotAttack,
    CannotMove,
    DiscourageAction,
}

public enum StatusAiRuleType : byte
{
    TreatAsAlly,
    PreferTarget,
    PreferAction,
    DiscourageAction,
    ReduceDecisionReliability,
    UnstableTargetSelection,
}

public enum StatusTargetRule : byte
{
    None,
    StatusSource,
    NearestEnemy,
    SourceEnemies,
}

public sealed record StatusModifierDefinition(StatusModifierType Type, int Amount);

public sealed record StatusRestrictionDefinition(
    StatusRestrictionType Type,
    StatusTargetRule TargetRule,
    ContentId? ActionTag);

public sealed record StatusAiRuleDefinition(
    StatusAiRuleType Type,
    StatusTargetRule TargetRule,
    ContentId? ActionTag,
    int Amount);

public sealed record StatusDefinition(
    StatusId StatusId,
    int SchemaVersion,
    int Revision,
    string NameKey,
    string DescriptionKey,
    StatusCategory Category,
    ImmutableArray<string> Tags,
    int DefaultDuration,
    StatusDurationType DurationType,
    StatusStackPolicy StackPolicy,
    int MaximumStacks,
    ContentId? ExclusiveGroupId,
    int Priority,
    ImmutableArray<StatusModifierDefinition> Modifiers,
    ImmutableArray<StatusRestrictionDefinition> Restrictions,
    ImmutableArray<StatusAiRuleDefinition> AiRules,
    ImmutableArray<EffectId> OnApplyEffectIds,
    ImmutableArray<EffectId> OnTickEffectIds,
    ImmutableArray<EffectId> OnExpireEffectIds)
    : ContentDefinition(StatusId.Value, SchemaVersion, Revision, NameKey, DescriptionKey);
