using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.World;

namespace Spelljammer.Simulation.Ships;

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
    ImmutableHashSet<BattleUnitId> Witnesses);

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
