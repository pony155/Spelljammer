using System.Collections.Immutable;
using Spelljammer.Simulation.Content;

namespace Spelljammer.Simulation.Characters;

/// <summary>Per-instance ranged weapon state, separate from immutable content rules.</summary>
public sealed record RangedWeaponState(
    RangedWeaponId WeaponId,
    int CurrentDurability,
    AmmunitionId? LoadedAmmunitionId,
    int CurrentAmmunition,
    int CurrentEnergy,
    int CurrentHeat)
{
    public static RangedWeaponState Create(
        RangedWeaponDefinition definition,
        AmmunitionDefinition? loadedAmmunition = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (loadedAmmunition is not null && definition.AmmunitionType != loadedAmmunition.AmmunitionType)
        {
            throw new InvalidOperationException("Loaded ammunition is incompatible with the ranged weapon.");
        }

        int loaded = loadedAmmunition is null ? 0 : Math.Max(1, definition.MagazineCapacity);
        return new RangedWeaponState(
            definition.RangedWeaponId,
            definition.MaximumDurability,
            loadedAmmunition?.AmmunitionId,
            loaded,
            definition.EnergyCapacity,
            0);
    }

    public void Validate(RangedWeaponDefinition definition, ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(catalog);
        int ammunitionCapacity = definition.AmmunitionType is null ? 0 : Math.Max(1, definition.MagazineCapacity);
        bool ammunitionInvalid = LoadedAmmunitionId is AmmunitionId ammunitionId
            ? !catalog.TryGetAmmunition(ammunitionId, out AmmunitionDefinition? ammunition) ||
              ammunition!.AmmunitionType != definition.AmmunitionType || CurrentAmmunition <= 0
            : CurrentAmmunition != 0;
        if (WeaponId != definition.RangedWeaponId || CurrentDurability < 0 ||
            CurrentDurability > definition.MaximumDurability || CurrentAmmunition < 0 ||
            CurrentAmmunition > ammunitionCapacity || CurrentEnergy < 0 ||
            CurrentEnergy > definition.EnergyCapacity || CurrentHeat < 0 ||
            CurrentHeat > definition.HeatCapacity || ammunitionInvalid)
        {
            throw new InvalidOperationException("Ranged weapon state is incompatible with its definition.");
        }
    }
}

public sealed record RangedTarget(
    CharacterId Id,
    bool IsPresent,
    bool IsLegal,
    int Distance,
    int Evasion,
    int Armor,
    int CoverPenalty);

public sealed record RangedAttackRequest(
    CharacterId ActorId,
    EquipmentId EquipmentId,
    RangedWeaponActionId ActionId,
    RangedTarget? Target,
    CharacterTurnState TurnState,
    RangedWeaponState WeaponState,
    int SituationalHitModifier,
    ulong RandomSeed,
    ulong RandomSequence);

public sealed record RangedAttackReservation(
    CharacterState OriginalActor,
    EquipmentDefinition Equipment,
    RangedWeaponDefinition Weapon,
    RangedWeaponActionDefinition Action,
    AmmunitionDefinition? Ammunition,
    RangedAttackRequest Request,
    int SkillValue,
    int ActionPointCost,
    int StaminaCost,
    int AmmunitionCost,
    int EnergyCost,
    int HeatGenerated,
    int RangePenalty,
    int DamageFalloffPercentage);

public sealed record RangedAttackEligibilityResult(
    RangedAttackReservation? Reservation,
    string RejectionCode,
    ContentId? RelatedId)
{
    public bool Accepted => Reservation is not null;
}

public sealed record RangedShotResolution(
    int ShotIndex,
    int Roll,
    int TotalAccuracy,
    bool Hit,
    int RolledDamage,
    int HealthDamage,
    int ArmorDamage);

public sealed record RangedAttackResolution(
    CharacterId ActorId,
    CharacterId TargetId,
    EquipmentId EquipmentId,
    RangedWeaponId WeaponId,
    RangedWeaponActionId ActionId,
    AmmunitionId? AmmunitionId,
    int TargetDefense,
    int ArmorPenetrationPercentage,
    int TotalHealthDamage,
    int TotalArmorDamage,
    ImmutableArray<RangedShotResolution> Shots,
    ImmutableArray<ContentId> AppliedEffectIds);

public sealed record RangedAttackResult(
    CharacterState Actor,
    CharacterTurnState TurnState,
    RangedWeaponState WeaponState,
    bool Accepted,
    bool Hit,
    string RejectionCode,
    RangedAttackResolution? Resolution);

public sealed record RangedReloadRequest(
    CharacterId ActorId,
    EquipmentId EquipmentId,
    RangedWeaponActionId ActionId,
    AmmunitionId AmmunitionId,
    int AvailableAmmunition,
    CharacterTurnState TurnState,
    RangedWeaponState WeaponState);

public sealed record RangedReloadResult(
    CharacterState? Actor,
    CharacterTurnState TurnState,
    RangedWeaponState WeaponState,
    int RemainingAmmunition,
    int LoadedAmount,
    bool Accepted,
    string RejectionCode);

/// <summary>Deterministic ranged attacks and transactional magazine reloads.</summary>
public static class RangedWeaponSystem
{
    private static readonly ResourceId StaminaId = new("resource.stamina");

    public static RangedAttackEligibilityResult CheckAttackEligibility(
        CharacterState? actor,
        RangedAttackRequest request,
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

        if (!TryResolveWeapon(actor, request.EquipmentId, request.WeaponState, catalog,
            out EquipmentDefinition? equipment, out RangedWeaponDefinition? weapon, out string rejection))
        {
            return Rejected(rejection, request.EquipmentId.Value);
        }

        if (!catalog.TryGetRangedWeaponAction(request.ActionId, out RangedWeaponActionDefinition? action) ||
            action!.Kind != RangedWeaponActionKind.Attack || !weapon!.ActionIds.Contains(request.ActionId))
        {
            return Rejected(ActionRejectionCodes.ActionUnknown, request.ActionId.Value);
        }

        if (request.Target is null || !request.Target.IsPresent)
        {
            return Rejected(ActionRejectionCodes.TargetMissing);
        }

        if (!request.Target.IsLegal || request.Target.Id == actor.Id || request.Target.Distance < 0 ||
            request.Target.Evasion < 0 || request.Target.Armor < 0 || request.Target.CoverPenalty < 0)
        {
            return Rejected(ActionRejectionCodes.TargetIllegal, request.Target.Id.Value);
        }

        AmmunitionDefinition? ammunition = null;
        int ammunitionCost = 0;
        if (weapon.AmmunitionType is not null)
        {
            if (request.WeaponState.LoadedAmmunitionId is not AmmunitionId ammunitionId ||
                !catalog.TryGetAmmunition(ammunitionId, out ammunition) ||
                ammunition!.AmmunitionType != weapon.AmmunitionType)
            {
                return Rejected(ActionRejectionCodes.AmmunitionRequired, weapon.RangedWeaponId.Value);
            }

            ammunitionCost = action.AmmunitionCost;
            if (ammunitionCost <= 0 || request.WeaponState.CurrentAmmunition < ammunitionCost)
            {
                return Rejected(ActionRejectionCodes.ResourceInsufficient, ammunitionId.Value);
            }
        }
        else if (action.AmmunitionCost != 0)
        {
            return Rejected(ActionRejectionCodes.ContentMismatch, action.RangedWeaponActionId.Value);
        }

        int energyCost = Math.Max(0, AddBounded(
            MultiplyBounded(weapon.EnergyPerShot, action.ShotCount), action.EnergyCostModifier));
        if (request.WeaponState.CurrentEnergy < energyCost)
        {
            return Rejected(ActionRejectionCodes.ResourceInsufficient, weapon.RangedWeaponId.Value);
        }

        int heatGenerated = Math.Max(0, AddBounded(
            MultiplyBounded(weapon.HeatPerShot, action.ShotCount), action.HeatModifier));
        if ((long)request.WeaponState.CurrentHeat + heatGenerated > weapon.HeatCapacity)
        {
            return Rejected(ActionRejectionCodes.EquipmentOverheated, weapon.RangedWeaponId.Value);
        }

        int rangeModifier = ammunition?.RangeModifier ?? 0;
        int optimalRange = Math.Max(0, AddBounded(weapon.OptimalRange, rangeModifier));
        int maximumRange = Math.Max(0, AddBounded(weapon.MaximumRange, rangeModifier));
        if (request.Target.Distance > maximumRange)
        {
            return Rejected(ActionRejectionCodes.TargetOutOfRange, request.Target.Id.Value);
        }

        int excessRange = Math.Max(0, request.Target.Distance - optimalRange);
        int rangePenalty = MultiplyBounded(excessRange, action.RangePenaltyPerUnit);
        int falloff = Math.Clamp(MultiplyBounded(excessRange, action.DamageFalloffPerUnitPercentage), 0, 100);
        if (!request.TurnState.CanSpendActionPoints(action.ActionPointCost))
        {
            return Rejected(ActionRejectionCodes.ResourceInsufficient);
        }

        int staminaCost = Math.Max(0, AddBounded(weapon.StaminaCost, action.StaminaCostModifier));
        if (!actor.CharacterResources.CanSpendResource(StaminaId, staminaCost))
        {
            return Rejected(ActionRejectionCodes.ResourceInsufficient, StaminaId.Value);
        }

        if (!actor.Capabilities.TryGetSkill(weapon.SkillId, catalog, out byte skill, out _))
        {
            return Rejected(ActionRejectionCodes.ContentMismatch, weapon.RangedWeaponId.Value);
        }

        return new RangedAttackEligibilityResult(
            new RangedAttackReservation(actor, equipment!, weapon, action, ammunition, request, skill,
                action.ActionPointCost, staminaCost, ammunitionCost, energyCost, heatGenerated,
                rangePenalty, falloff),
            ActionRejectionCodes.None,
            null);
    }

    public static RangedAttackResult ResolveAttack(
        RangedAttackReservation reservation,
        ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(catalog);
        if (reservation.OriginalActor.ContentFingerprint != catalog.Fingerprint ||
            !catalog.TryGetRangedWeapon(reservation.Weapon.RangedWeaponId, out RangedWeaponDefinition? weapon) ||
            weapon != reservation.Weapon ||
            !catalog.TryGetRangedWeaponAction(reservation.Action.RangedWeaponActionId, out RangedWeaponActionDefinition? action) ||
            action != reservation.Action ||
            (reservation.Ammunition is not null &&
             (!catalog.TryGetAmmunition(reservation.Ammunition.AmmunitionId, out AmmunitionDefinition? ammunition) ||
              ammunition != reservation.Ammunition)))
        {
            return RejectedResolution(reservation, ActionRejectionCodes.ContentMismatch);
        }

        int targetDefense = AddBounded(
            reservation.Request.Target!.Evasion,
            reservation.Request.Target.CoverPenalty);
        int penetration = Math.Clamp(AddBounded(
            reservation.Weapon.ArmorPenetrationPercentage,
            reservation.Ammunition?.ArmorPenetrationModifier ?? 0), 0, 100);
        int remainingArmor = reservation.Request.Target.Armor;
        int totalHealthDamage = 0;
        int totalArmorDamage = 0;
        ImmutableArray<RangedShotResolution>.Builder shots =
            ImmutableArray.CreateBuilder<RangedShotResolution>(reservation.Action.ShotCount);
        for (int shotIndex = 0; shotIndex < reservation.Action.ShotCount; shotIndex++)
        {
            ulong sequence = reservation.Request.RandomSequence + (ulong)(shotIndex * 2);
            int roll = DeterministicRange(reservation.Request.RandomSeed, sequence, 1, 100);
            int totalAccuracy = AddBounded(roll, reservation.SkillValue);
            totalAccuracy = AddBounded(totalAccuracy, reservation.Action.HitModifier);
            totalAccuracy = AddBounded(totalAccuracy, reservation.Request.SituationalHitModifier);
            totalAccuracy = AddBounded(totalAccuracy, -reservation.RangePenalty);
            bool hit = totalAccuracy >= targetDefense;
            int rolledDamage = 0;
            int healthDamage = 0;
            int armorDamage = 0;
            if (hit)
            {
                int baseDamage = DeterministicRange(
                    reservation.Request.RandomSeed,
                    sequence + 1,
                    reservation.Weapon.DamageMinimum,
                    reservation.Weapon.DamageMaximum);
                long modifiedDamage = (long)baseDamage * (reservation.Ammunition?.DamagePercentage ?? 100) / 100;
                modifiedDamage = modifiedDamage * reservation.Action.DamagePercentage / 100;
                modifiedDamage = modifiedDamage * (100 - reservation.DamageFalloffPercentage) / 100;
                rolledDamage = ClampNonnegative(modifiedDamage);
                int penetratingDamage = ClampNonnegative((long)rolledDamage * penetration / 100);
                int blockedDamage = Math.Min(remainingArmor, rolledDamage - penetratingDamage);
                healthDamage = rolledDamage - blockedDamage;
                long armorDamageValue = (long)rolledDamage * reservation.Weapon.ArmorDamagePercentage / 100;
                armorDamageValue = armorDamageValue * (reservation.Ammunition?.ArmorDamagePercentage ?? 100) / 100;
                armorDamage = Math.Min(remainingArmor, ClampNonnegative(armorDamageValue));
                remainingArmor -= armorDamage;
                totalHealthDamage = AddNonnegative(totalHealthDamage, healthDamage);
                totalArmorDamage = AddNonnegative(totalArmorDamage, armorDamage);
            }

            shots.Add(new RangedShotResolution(
                shotIndex, roll, totalAccuracy, hit, rolledDamage, healthDamage, armorDamage));
        }

        CharacterState committedActor = reservation.OriginalActor with
        {
            CharacterResources = reservation.OriginalActor.CharacterResources.SpendResource(
                StaminaId, reservation.StaminaCost),
        };
        RangedWeaponState committedWeapon = reservation.Request.WeaponState with
        {
            CurrentDurability = Math.Max(0,
                reservation.Request.WeaponState.CurrentDurability - reservation.Action.DurabilityCost),
            CurrentAmmunition = reservation.Request.WeaponState.CurrentAmmunition - reservation.AmmunitionCost,
            CurrentEnergy = reservation.Request.WeaponState.CurrentEnergy - reservation.EnergyCost,
            CurrentHeat = reservation.Request.WeaponState.CurrentHeat + reservation.HeatGenerated,
        };
        if (committedWeapon.CurrentAmmunition == 0)
        {
            committedWeapon = committedWeapon with { LoadedAmmunitionId = null };
        }

        CharacterTurnState committedTurn = reservation.Request.TurnState.SpendActionPoints(reservation.ActionPointCost);
        bool anyHit = shots.Any(value => value.Hit);
        RangedAttackResolution resolution = new(
            committedActor.Id,
            reservation.Request.Target.Id,
            reservation.Equipment.EquipmentId,
            reservation.Weapon.RangedWeaponId,
            reservation.Action.RangedWeaponActionId,
            reservation.Ammunition?.AmmunitionId,
            targetDefense,
            penetration,
            totalHealthDamage,
            totalArmorDamage,
            shots.MoveToImmutable(),
            anyHit ? reservation.Action.EffectIds : []);
        return new RangedAttackResult(
            committedActor, committedTurn, committedWeapon, true, anyHit, ActionRejectionCodes.None, resolution);
    }

    public static RangedReloadResult Reload(
        CharacterState? actor,
        RangedReloadRequest request,
        ICharacterContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        RangedReloadResult Reject(string code) => new(
            actor, request.TurnState, request.WeaponState, request.AvailableAmmunition, 0, false, code);

        if (actor is null || actor.Id != request.ActorId)
        {
            return Reject(ActionRejectionCodes.ActorMissing);
        }

        if (!actor.CanAct)
        {
            return Reject(ActionRejectionCodes.ActorCannotAct);
        }

        if (actor.ContentFingerprint != catalog.Fingerprint || actor.Capabilities.Fingerprint != catalog.Fingerprint)
        {
            return Reject(ActionRejectionCodes.ContentMismatch);
        }

        if (!TryResolveWeapon(actor, request.EquipmentId, request.WeaponState, catalog,
            out _, out RangedWeaponDefinition? weapon, out string rejection))
        {
            return Reject(rejection);
        }

        if (!catalog.TryGetRangedWeaponAction(request.ActionId, out RangedWeaponActionDefinition? action) ||
            action!.Kind != RangedWeaponActionKind.Reload || !weapon!.ActionIds.Contains(request.ActionId))
        {
            return Reject(ActionRejectionCodes.ActionUnknown);
        }

        if (weapon.AmmunitionType is null || !catalog.TryGetAmmunition(request.AmmunitionId, out AmmunitionDefinition? ammunition) ||
            ammunition!.AmmunitionType != weapon.AmmunitionType)
        {
            return Reject(ActionRejectionCodes.AmmunitionIncompatible);
        }

        if (request.WeaponState.LoadedAmmunitionId is AmmunitionId loadedId &&
            loadedId != request.AmmunitionId && request.WeaponState.CurrentAmmunition > 0)
        {
            return Reject(ActionRejectionCodes.AmmunitionIncompatible);
        }

        int capacity = Math.Max(1, weapon.MagazineCapacity);
        int space = capacity - request.WeaponState.CurrentAmmunition;
        if (space <= 0)
        {
            return Reject(ActionRejectionCodes.MagazineFull);
        }

        if (request.AvailableAmmunition <= 0 || !request.TurnState.CanSpendActionPoints(action.ActionPointCost))
        {
            return Reject(ActionRejectionCodes.ResourceInsufficient);
        }

        int staminaCost = Math.Max(0, AddBounded(weapon.StaminaCost, action.StaminaCostModifier));
        if (!actor.CharacterResources.CanSpendResource(StaminaId, staminaCost))
        {
            return Reject(ActionRejectionCodes.ResourceInsufficient);
        }

        int loadedAmount = Math.Min(space, Math.Min(action.ReloadAmount, request.AvailableAmmunition));
        CharacterState committedActor = actor with
        {
            CharacterResources = actor.CharacterResources.SpendResource(StaminaId, staminaCost),
        };
        RangedWeaponState committedWeapon = request.WeaponState with
        {
            LoadedAmmunitionId = request.AmmunitionId,
            CurrentAmmunition = request.WeaponState.CurrentAmmunition + loadedAmount,
        };
        return new RangedReloadResult(
            committedActor,
            request.TurnState.SpendActionPoints(action.ActionPointCost),
            committedWeapon,
            request.AvailableAmmunition - loadedAmount,
            loadedAmount,
            true,
            ActionRejectionCodes.None);
    }

    public static RangedWeaponState Cool(RangedWeaponState state, RangedWeaponDefinition definition, int amount)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        if (amount < 0 || state.WeaponId != definition.RangedWeaponId)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return state with { CurrentHeat = Math.Max(0, state.CurrentHeat - amount) };
    }

    private static bool TryResolveWeapon(
        CharacterState actor,
        EquipmentId equipmentId,
        RangedWeaponState state,
        ICharacterContentCatalog catalog,
        out EquipmentDefinition? equipment,
        out RangedWeaponDefinition? weapon,
        out string rejection)
    {
        equipment = null;
        weapon = null;
        if (!actor.EquipmentIds.Contains(equipmentId.Value) ||
            !catalog.TryGetEquipment(equipmentId, out equipment) ||
            equipment!.RangedWeaponId is not RangedWeaponId weaponId)
        {
            rejection = ActionRejectionCodes.EquipmentRequired;
            return false;
        }

        if (!catalog.TryGetRangedWeapon(weaponId, out weapon) || state.WeaponId != weaponId)
        {
            rejection = ActionRejectionCodes.ContentMismatch;
            return false;
        }

        try
        {
            state.Validate(weapon!, catalog);
        }
        catch (InvalidOperationException)
        {
            rejection = ActionRejectionCodes.ContentMismatch;
            return false;
        }

        if (state.CurrentDurability == 0)
        {
            rejection = ActionRejectionCodes.EquipmentBroken;
            return false;
        }

        rejection = ActionRejectionCodes.None;
        return true;
    }

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

    private static int MultiplyBounded(int left, int right) =>
        (int)Math.Clamp((long)left * right, int.MinValue, int.MaxValue);

    private static int AddNonnegative(int left, int right) =>
        (int)Math.Clamp((long)left + right, 0, int.MaxValue);

    private static int ClampNonnegative(long value) => (int)Math.Clamp(value, 0, int.MaxValue);

    private static RangedAttackEligibilityResult Rejected(string code, ContentId? relatedId = null) =>
        new(null, code, relatedId);

    private static RangedAttackResult RejectedResolution(RangedAttackReservation reservation, string code) =>
        new(reservation.OriginalActor, reservation.Request.TurnState, reservation.Request.WeaponState,
            false, false, code, null);
}
