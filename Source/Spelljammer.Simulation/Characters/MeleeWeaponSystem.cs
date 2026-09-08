using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

/// <summary>Mutable-per-instance condition for a weapon whose rules remain content-owned.</summary>
public sealed record MeleeWeaponState(
    MeleeWeaponId WeaponId,
    int CurrentDurability,
    int CurrentEnergy)
{
    public static MeleeWeaponState Create(MeleeWeaponDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new MeleeWeaponState(
            definition.MeleeWeaponId,
            definition.MaximumDurability,
            definition.EnergyCapacity);
    }

    public void Validate(MeleeWeaponDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (WeaponId != definition.MeleeWeaponId || CurrentDurability < 0 ||
            CurrentDurability > definition.MaximumDurability || CurrentEnergy < 0 ||
            CurrentEnergy > definition.EnergyCapacity)
        {
            throw new InvalidOperationException("Melee weapon state is incompatible with its definition.");
        }
    }
}

public sealed record MeleeTarget(
    CharacterId Id,
    bool IsPresent,
    bool IsLegal,
    int Distance,
    int Evasion,
    int Armor);

public sealed record MeleeAttackRequest(
    CharacterId ActorId,
    EquipmentId EquipmentId,
    MeleeWeaponActionId ActionId,
    MeleeTarget? Target,
    CharacterTurnState TurnState,
    MeleeWeaponState WeaponState,
    ulong RandomSeed,
    ulong RandomSequence);

public sealed record MeleeAttackReservation(
    CharacterState OriginalActor,
    EquipmentDefinition Equipment,
    MeleeWeaponDefinition Weapon,
    MeleeWeaponActionDefinition Action,
    MeleeAttackRequest Request,
    int AbilityValue,
    int SkillValue,
    int ActionPointCost,
    int StaminaCost,
    int EnergyCost,
    bool UsesUnpoweredMode);

public sealed record MeleeAttackEligibilityResult(
    MeleeAttackReservation? Reservation,
    string RejectionCode,
    ContentId? RelatedId)
{
    public bool Accepted => Reservation is not null;
}

public sealed record MeleeAttackResolution(
    CharacterId ActorId,
    CharacterId TargetId,
    EquipmentId EquipmentId,
    MeleeWeaponId WeaponId,
    MeleeWeaponActionId ActionId,
    int Roll,
    int TotalAccuracy,
    int TargetEvasion,
    bool Hit,
    bool UsedUnpoweredMode,
    int RolledDamage,
    int HealthDamage,
    int ArmorDamage,
    int ArmorPenetrationPercentage,
    ImmutableArray<ContentId> AppliedEffectIds);

public sealed record MeleeAttackResult(
    CharacterState Actor,
    CharacterTurnState TurnState,
    MeleeWeaponState WeaponState,
    bool Accepted,
    bool Hit,
    string RejectionCode,
    MeleeAttackResolution? Resolution);

/// <summary>Deterministic, side-effect-free melee validation and resolution.</summary>
public static class MeleeWeaponSystem
{
    public static MeleeAttackEligibilityResult CheckEligibility(
        CharacterState? actor,
        MeleeAttackRequest request,
        ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        if (actor is null || actor.Id != request.ActorId)
        {
            return Rejected(ActionRejectionCodes.ActorMissing, request.ActorId.Value);
        }

        if (!actor.CanAct)
        {
            return Rejected(ActionRejectionCodes.ActorCannotAct, actor.Id.Value);
        }

        if (actor.ContentFingerprint != catalog.Fingerprint || actor.Capabilities.Fingerprint != catalog.Fingerprint)
        {
            return Rejected(ActionRejectionCodes.ContentMismatch);
        }

        if (!actor.EquipmentIds.Contains(request.EquipmentId.Value) ||
            !catalog.TryGetEquipment(request.EquipmentId, out EquipmentDefinition? equipment) ||
            equipment!.MeleeWeaponId is not MeleeWeaponId weaponId)
        {
            return Rejected(ActionRejectionCodes.EquipmentRequired, request.EquipmentId.Value);
        }

        if (!catalog.TryGetMeleeWeapon(weaponId, out MeleeWeaponDefinition? weapon) ||
            request.WeaponState.WeaponId != weaponId)
        {
            return Rejected(ActionRejectionCodes.ContentMismatch, weaponId.Value);
        }

        if (!catalog.TryGetMeleeWeaponAction(request.ActionId, out MeleeWeaponActionDefinition? action) ||
            !weapon!.ActionIds.Contains(request.ActionId))
        {
            return Rejected(ActionRejectionCodes.ActionUnknown, request.ActionId.Value);
        }

        try
        {
            request.WeaponState.Validate(weapon);
        }
        catch (InvalidOperationException)
        {
            return Rejected(ActionRejectionCodes.ContentMismatch, weaponId.Value);
        }

        if (request.WeaponState.CurrentDurability == 0)
        {
            return Rejected(ActionRejectionCodes.EquipmentBroken, request.EquipmentId.Value);
        }

        if (request.Target is null || !request.Target.IsPresent)
        {
            return Rejected(ActionRejectionCodes.TargetMissing);
        }

        if (!request.Target.IsLegal || request.Target.Id == actor.Id || request.Target.Distance < 0 ||
            request.Target.Evasion < 0 || request.Target.Armor < 0)
        {
            return Rejected(ActionRejectionCodes.TargetIllegal, request.Target.Id.Value);
        }

        int effectiveRange = Math.Max(0, AddBounded(weapon.Range, action!.RangeModifier));
        if (request.Target.Distance > effectiveRange)
        {
            return Rejected(ActionRejectionCodes.TargetOutOfRange, request.Target.Id.Value);
        }

        if (!request.TurnState.CanSpendActionPoints(action.ActionPointCost))
        {
            return Rejected(ActionRejectionCodes.ResourceInsufficient);
        }

        int staminaCost = Math.Max(0, AddBounded(weapon.StaminaCost, action.StaminaCostModifier));
        ResourceId staminaId = new("resource.stamina");
        if (!actor.CharacterResources.CanSpendResource(staminaId, staminaCost))
        {
            return Rejected(ActionRejectionCodes.ResourceInsufficient, staminaId.Value);
        }

        int energyCost = Math.Max(0, AddBounded(weapon.EnergyPerAttack, action.EnergyCostModifier));
        bool unpowered = request.WeaponState.CurrentEnergy < energyCost;
        if (unpowered && weapon.UnpoweredDamagePercentage == 0)
        {
            return Rejected(ActionRejectionCodes.ResourceInsufficient, weaponId.Value);
        }

        if (!actor.Capabilities.TryGetSkill(weapon.SkillId, catalog, out byte skill, out _) ||
            !actor.Capabilities.TryGetAbility(weapon.AbilityId, catalog, out short ability, out _))
        {
            return Rejected(ActionRejectionCodes.ContentMismatch, weaponId.Value);
        }

        return new MeleeAttackEligibilityResult(
            new MeleeAttackReservation(actor, equipment, weapon, action, request, ability, skill,
                action.ActionPointCost, staminaCost, unpowered ? 0 : energyCost, unpowered),
            ActionRejectionCodes.None,
            null);
    }

    public static MeleeAttackResult Resolve(MeleeAttackReservation reservation, ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(catalog);
        if (reservation.OriginalActor.ContentFingerprint != catalog.Fingerprint ||
            !catalog.TryGetMeleeWeapon(reservation.Weapon.MeleeWeaponId, out MeleeWeaponDefinition? currentWeapon) ||
            currentWeapon != reservation.Weapon ||
            !catalog.TryGetMeleeWeaponAction(reservation.Action.MeleeWeaponActionId, out MeleeWeaponActionDefinition? currentAction) ||
            currentAction != reservation.Action)
        {
            return RejectedResolution(reservation, ActionRejectionCodes.ContentMismatch);
        }

        int attackRoll = DeterministicRoll(reservation.Request.RandomSeed, reservation.Request.RandomSequence);
        int totalAccuracy = AddBounded(attackRoll, AddBounded(reservation.SkillValue, reservation.Action.HitModifier));
        bool hit = totalAccuracy >= reservation.Request.Target!.Evasion;
        int rolledDamage = 0;
        int healthDamage = 0;
        int armorDamage = 0;
        int penetration = 0;
        if (hit)
        {
            int damageRoll = DeterministicRange(
                reservation.Request.RandomSeed,
                reservation.Request.RandomSequence + 1,
                reservation.Weapon.DamageMinimum,
                reservation.Weapon.DamageMaximum);
            long strengthBonus = (long)reservation.AbilityValue * reservation.Weapon.StrengthDamageScale / 100;
            long poweredDamage = Math.Max(0, (long)damageRoll + strengthBonus);
            long actionDamage = poweredDamage * reservation.Action.DamagePercentage / 100;
            int modeDamagePercentage = reservation.UsesUnpoweredMode
                ? reservation.Weapon.UnpoweredDamagePercentage
                : 100;
            rolledDamage = ClampNonnegative(actionDamage * modeDamagePercentage / 100);
            int basePenetration = reservation.UsesUnpoweredMode
                ? reservation.Weapon.UnpoweredArmorPenetrationPercentage
                : reservation.Weapon.ArmorPenetrationPercentage;
            penetration = Math.Clamp(AddBounded(basePenetration, reservation.Action.ArmorPenetrationModifier), 0, 100);
            int penetratingDamage = ClampNonnegative((long)rolledDamage * penetration / 100);
            int blockableDamage = rolledDamage - penetratingDamage;
            int blockedDamage = Math.Min(reservation.Request.Target.Armor, blockableDamage);
            healthDamage = rolledDamage - blockedDamage;
            int armorDamagePercentage = Math.Max(0, AddBounded(
                reservation.Weapon.ArmorDamagePercentage,
                reservation.Action.ArmorDamagePercentage));
            armorDamage = Math.Min(
                reservation.Request.Target.Armor,
                ClampNonnegative((long)rolledDamage * armorDamagePercentage / 100));
        }

        CharacterState committedActor = reservation.OriginalActor with
        {
            CharacterResources = reservation.OriginalActor.CharacterResources.SpendResource(
                new ResourceId("resource.stamina"), reservation.StaminaCost),
        };
        CharacterTurnState committedTurn = reservation.Request.TurnState.SpendActionPoints(reservation.ActionPointCost);
        MeleeWeaponState committedWeapon = reservation.Request.WeaponState with
        {
            CurrentDurability = Math.Max(0,
                reservation.Request.WeaponState.CurrentDurability - reservation.Action.DurabilityCost),
            CurrentEnergy = reservation.Request.WeaponState.CurrentEnergy - reservation.EnergyCost,
        };
        ImmutableArray<ContentId> effects = hit ? reservation.Action.EffectIds : [];
        MeleeAttackResolution resolution = new(
            committedActor.Id,
            reservation.Request.Target.Id,
            reservation.Equipment.EquipmentId,
            reservation.Weapon.MeleeWeaponId,
            reservation.Action.MeleeWeaponActionId,
            attackRoll,
            totalAccuracy,
            reservation.Request.Target.Evasion,
            hit,
            reservation.UsesUnpoweredMode,
            rolledDamage,
            healthDamage,
            armorDamage,
            penetration,
            effects);
        return new MeleeAttackResult(
            committedActor,
            committedTurn,
            committedWeapon,
            true,
            hit,
            ActionRejectionCodes.None,
            resolution);
    }

    private static int DeterministicRoll(ulong seed, ulong sequence) =>
        DeterministicRange(seed, sequence, 1, 100);

    private static int DeterministicRange(ulong seed, ulong sequence, int minimum, int maximum)
    {
        ulong value = seed + (sequence + 1) * 0x9e3779b97f4a7c15UL;
        value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
        value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
        value ^= value >> 31;
        ulong width = (ulong)((long)maximum - minimum + 1);
        return minimum + (int)(value % width);
    }

    private static int AddBounded(int left, int right) =>
        (int)Math.Clamp((long)left + right, int.MinValue, int.MaxValue);

    private static int ClampNonnegative(long value) => (int)Math.Clamp(value, 0, int.MaxValue);

    private static MeleeAttackEligibilityResult Rejected(string code, ContentId? relatedId = null) =>
        new(null, code, relatedId);

    private static MeleeAttackResult RejectedResolution(MeleeAttackReservation reservation, string code) =>
        new(reservation.OriginalActor, reservation.Request.TurnState, reservation.Request.WeaponState,
            false, false, code, null);
}
