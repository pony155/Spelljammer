using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

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
    ItemInstanceId WeaponItemInstanceId,
    MeleeWeaponActionId ActionId,
    MeleeTarget? Target,
    CharacterTurnState TurnState,
    ulong RandomSeed,
    ulong RandomSequence);

public sealed record MeleeAttackReservation(
    CharacterState OriginalActor,
    ItemInstance Item,
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
    ItemInstanceId WeaponItemInstanceId,
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
    ImmutableArray<EffectRequest> Effects);

public sealed record MeleeAttackResult(
    CharacterState Actor,
    CharacterTurnState TurnState,
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
        ICombatContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        if (actor is null || actor.Id != request.ActorId)
        {
            return Rejected(ActionRejectionCodes.ActorMissing, request.ActorId.Value);
        }

        if (!actor.CanAct || !StatusQueries.CanAct(actor.Statuses, catalog))
        {
            return Rejected(ActionRejectionCodes.ActorCannotAct, actor.Id.Value);
        }

        if (actor.ContentFingerprint != catalog.Fingerprint || actor.Capabilities.Fingerprint != catalog.Fingerprint)
        {
            return Rejected(ActionRejectionCodes.ContentMismatch);
        }

        if (!actor.TryGetItem(request.WeaponItemInstanceId, out ItemInstance? item) ||
            !actor.IsEquipped(request.WeaponItemInstanceId) ||
            !catalog.TryGetItem(item!.DefinitionId, out ItemDefinition? definition) ||
            definition is not MeleeWeaponDefinition weapon || item.MeleeWeaponState is not MeleeWeaponState weaponState)
        {
            return Rejected(ActionRejectionCodes.EquipmentRequired, item?.DefinitionId);
        }

        MeleeWeaponId weaponId = weapon.MeleeWeaponId;
        if (weaponState.WeaponId != weaponId)
        {
            return Rejected(ActionRejectionCodes.ContentMismatch, weaponId.Value);
        }

        if (!catalog.TryGetMeleeWeaponAction(request.ActionId, out MeleeWeaponActionDefinition? action) ||
            !weapon!.ActionIds.Contains(request.ActionId.Value))
        {
            return Rejected(ActionRejectionCodes.ActionUnknown, request.ActionId.Value);
        }

        try
        {
            weaponState.Validate(weapon);
        }
        catch (InvalidOperationException)
        {
            return Rejected(ActionRejectionCodes.ContentMismatch, weaponId.Value);
        }

        if (weaponState.CurrentDurability == 0)
        {
            return Rejected(ActionRejectionCodes.EquipmentBroken, item.DefinitionId);
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

        if (!StatusQueries.CanAttack(actor.Statuses, request.Target.Id.Value, catalog))
        {
            return Rejected(ActionRejectionCodes.ActorCannotAct, actor.Id.Value);
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
        bool unpowered = weaponState.CurrentEnergy < energyCost;
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
            new MeleeAttackReservation(actor, item, weapon, action, request, ability, skill,
                action.ActionPointCost, staminaCost, unpowered ? 0 : energyCost, unpowered),
            ActionRejectionCodes.None,
            null);
    }

    public static MeleeAttackResult Resolve(MeleeAttackReservation reservation, ICombatContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(catalog);
        if (reservation.OriginalActor.ContentFingerprint != catalog.Fingerprint ||
            !catalog.TryGetMeleeWeapon(reservation.Weapon.MeleeWeaponId, out MeleeWeaponDefinition? currentWeapon) ||
            currentWeapon != reservation.Weapon ||
            !catalog.TryGetMeleeWeaponAction(reservation.Action.MeleeWeaponActionId, out MeleeWeaponActionDefinition? currentAction) ||
            currentAction != reservation.Action ||
            !reservation.OriginalActor.TryGetItem(reservation.Item.InstanceId, out ItemInstance? currentItem) ||
            currentItem != reservation.Item)
        {
            return RejectedResolution(reservation, ActionRejectionCodes.ContentMismatch);
        }

        int attackRoll = CombatResolutionUtilities.DeterministicRoll(
            reservation.Request.RandomSeed,
            reservation.Request.RandomSequence);
        int statusAccuracy = StatusQueries.GetModifier(
            reservation.OriginalActor.Statuses, StatusModifierType.ModifyAccuracy, catalog);
        int totalAccuracy = AddBounded(
            attackRoll, AddBounded(reservation.SkillValue, AddBounded(reservation.Action.HitModifier, statusAccuracy)));
        bool hit = totalAccuracy >= reservation.Request.Target!.Evasion;
        int rolledDamage = 0;
        int healthDamage = 0;
        int armorDamage = 0;
        int penetration = 0;
        if (hit)
        {
            int damageRoll = CombatResolutionUtilities.DeterministicRange(
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

        MeleeWeaponState originalWeaponState = reservation.Item.MeleeWeaponState!;
        MeleeWeaponState committedWeapon = originalWeaponState with
        {
            CurrentDurability = Math.Max(0,
                originalWeaponState.CurrentDurability - reservation.Action.DurabilityCost),
            CurrentEnergy = originalWeaponState.CurrentEnergy - reservation.EnergyCost,
        };
        ItemInstance committedItem = reservation.Item with
        {
            CurrentDurability = committedWeapon.CurrentDurability,
            MeleeWeaponState = committedWeapon,
        };
        CharacterState committedActor = reservation.OriginalActor.ReplaceItem(committedItem) with
        {
            CharacterResources = reservation.OriginalActor.CharacterResources.SpendResource(
                new ResourceId("resource.stamina"), reservation.StaminaCost),
        };
        CharacterTurnState committedTurn = reservation.Request.TurnState.SpendActionPoints(reservation.ActionPointCost);
        ImmutableArray<EffectRequest> effects = hit
            ? BuildEffects(reservation, healthDamage, armorDamage)
            : [];
        MeleeAttackResolution resolution = new(
            committedActor.Id,
            reservation.Request.Target.Id,
            reservation.Item.InstanceId,
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
            true,
            hit,
            ActionRejectionCodes.None,
            resolution);
    }

    public static EffectResolution ResolveEffects(
        MeleeAttackResolution resolution,
        EffectTargetState target,
        ICombatContentCatalog catalog,
        StatusSystemLimits limits)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return EffectSystem.Resolve(target, resolution.Effects, catalog, limits);
    }

    private static ImmutableArray<EffectRequest> BuildEffects(
        MeleeAttackReservation reservation,
        int healthDamage,
        int armorDamage)
    {
        return CombatResolutionUtilities.BuildEffectRequests(
            [
                new ImmediateEffectAmount(CombatEffectIds.ArmorDamage, armorDamage),
                new ImmediateEffectAmount(CombatEffectIds.PhysicalDamage, healthDamage),
            ],
            reservation.Action.Effects,
            reservation.OriginalActor.Id.Value,
            reservation.Request.Target!.Id.Value,
            reservation.Request.RandomSeed,
            reservation.Request.RandomSequence);
    }

    private static int AddBounded(int left, int right) =>
        (int)Math.Clamp((long)left + right, int.MinValue, int.MaxValue);

    private static int ClampNonnegative(long value) => (int)Math.Clamp(value, 0, int.MaxValue);

    private static MeleeAttackEligibilityResult Rejected(string code, ContentId? relatedId = null) =>
        new(null, code, relatedId);

    private static MeleeAttackResult RejectedResolution(MeleeAttackReservation reservation, string code) =>
        new(reservation.OriginalActor, reservation.Request.TurnState, false, false, code, null);
}
