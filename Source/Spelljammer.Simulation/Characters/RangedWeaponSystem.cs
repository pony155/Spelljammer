using System.Collections.Immutable;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

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

    public void Validate(RangedWeaponDefinition definition, ICombatContentCatalog catalog)
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
    ItemInstanceId WeaponItemInstanceId,
    RangedWeaponActionId ActionId,
    RangedTarget? Target,
    CharacterTurnState TurnState,
    int SituationalHitModifier,
    ulong RandomSeed,
    ulong RandomSequence);

public sealed record RangedAttackReservation(
    CharacterState OriginalActor,
    ItemInstance Item,
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
    ItemInstanceId WeaponItemInstanceId,
    RangedWeaponId WeaponId,
    RangedWeaponActionId ActionId,
    AmmunitionId? AmmunitionId,
    int TargetDefense,
    int ArmorPenetrationPercentage,
    int TotalHealthDamage,
    int TotalArmorDamage,
    ImmutableArray<RangedShotResolution> Shots,
    ImmutableArray<EffectRequest> Effects);

public sealed record RangedAttackResult(
    CharacterState Actor,
    CharacterTurnState TurnState,
    bool Accepted,
    bool Hit,
    string RejectionCode,
    RangedAttackResolution? Resolution);

public sealed record RangedReloadRequest(
    CharacterId ActorId,
    ItemInstanceId WeaponItemInstanceId,
    RangedWeaponActionId ActionId,
    InventoryEntryId AmmunitionEntryId,
    CharacterTurnState TurnState);

public sealed record RangedReloadResult(
    CharacterState? Actor,
    CharacterTurnState TurnState,
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

        if (!TryResolveWeapon(actor, request.WeaponItemInstanceId, catalog,
            out ItemInstance? item, out RangedWeaponDefinition? weapon,
            out RangedWeaponState? weaponState, out string rejection))
        {
            return Rejected(rejection, item?.DefinitionId);
        }

        if (!catalog.TryGetRangedWeaponAction(request.ActionId, out RangedWeaponActionDefinition? action) ||
            action!.Kind != RangedWeaponActionKind.Attack || !weapon!.ActionIds.Contains(request.ActionId.Value))
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

        if (!StatusQueries.CanAttack(actor.Statuses, request.Target.Id.Value, catalog))
        {
            return Rejected(ActionRejectionCodes.ActorCannotAct, actor.Id.Value);
        }

        AmmunitionDefinition? ammunition = null;
        int ammunitionCost = 0;
        if (weapon.AmmunitionType is not null)
        {
            if (weaponState!.LoadedAmmunitionId is not AmmunitionId ammunitionId ||
                !catalog.TryGetAmmunition(ammunitionId, out ammunition) ||
                ammunition!.AmmunitionType != weapon.AmmunitionType)
            {
                return Rejected(ActionRejectionCodes.AmmunitionRequired, weapon.RangedWeaponId.Value);
            }

            ammunitionCost = action.AmmunitionCost;
            if (ammunitionCost <= 0 || weaponState.CurrentAmmunition < ammunitionCost)
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
        if (weaponState!.CurrentEnergy < energyCost)
        {
            return Rejected(ActionRejectionCodes.ResourceInsufficient, weapon.RangedWeaponId.Value);
        }

        int heatGenerated = Math.Max(0, AddBounded(
            MultiplyBounded(weapon.HeatPerShot, action.ShotCount), action.HeatModifier));
        if ((long)weaponState.CurrentHeat + heatGenerated > weapon.HeatCapacity)
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
            new RangedAttackReservation(actor, item!, weapon, action, ammunition, request, skill,
                action.ActionPointCost, staminaCost, ammunitionCost, energyCost, heatGenerated,
                rangePenalty, falloff),
            ActionRejectionCodes.None,
            null);
    }

    public static RangedAttackResult ResolveAttack(
        RangedAttackReservation reservation,
        ICombatContentCatalog catalog)
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
              ammunition != reservation.Ammunition)) ||
            !reservation.OriginalActor.TryGetItem(reservation.Item.InstanceId, out ItemInstance? currentItem) ||
            currentItem != reservation.Item)
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
            int roll = CombatResolutionUtilities.DeterministicRange(
                reservation.Request.RandomSeed,
                sequence,
                1,
                100);
            int totalAccuracy = AddBounded(roll, reservation.SkillValue);
            totalAccuracy = AddBounded(totalAccuracy, reservation.Action.HitModifier);
            totalAccuracy = AddBounded(totalAccuracy, reservation.Request.SituationalHitModifier);
            totalAccuracy = AddBounded(totalAccuracy, StatusQueries.GetModifier(
                reservation.OriginalActor.Statuses, StatusModifierType.ModifyAccuracy, catalog));
            totalAccuracy = AddBounded(totalAccuracy, -reservation.RangePenalty);
            bool hit = totalAccuracy >= targetDefense;
            int rolledDamage = 0;
            int healthDamage = 0;
            int armorDamage = 0;
            if (hit)
            {
                int baseDamage = CombatResolutionUtilities.DeterministicRange(
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

        RangedWeaponState originalWeaponState = reservation.Item.RangedWeaponState!;
        RangedWeaponState committedWeapon = originalWeaponState with
        {
            CurrentDurability = Math.Max(0,
                originalWeaponState.CurrentDurability - reservation.Action.DurabilityCost),
            CurrentAmmunition = originalWeaponState.CurrentAmmunition - reservation.AmmunitionCost,
            CurrentEnergy = originalWeaponState.CurrentEnergy - reservation.EnergyCost,
            CurrentHeat = originalWeaponState.CurrentHeat + reservation.HeatGenerated,
        };
        if (committedWeapon.CurrentAmmunition == 0)
        {
            committedWeapon = committedWeapon with { LoadedAmmunitionId = null };
        }

        ItemInstance committedItem = reservation.Item with
        {
            CurrentDurability = committedWeapon.CurrentDurability,
            RangedWeaponState = committedWeapon,
        };
        CharacterState committedActor = reservation.OriginalActor.ReplaceItem(committedItem) with
        {
            CharacterResources = reservation.OriginalActor.CharacterResources.SpendResource(
                StaminaId, reservation.StaminaCost),
        };
        CharacterTurnState committedTurn = reservation.Request.TurnState.SpendActionPoints(reservation.ActionPointCost);
        bool anyHit = shots.Any(value => value.Hit);
        RangedAttackResolution resolution = new(
            committedActor.Id,
            reservation.Request.Target.Id,
            reservation.Item.InstanceId,
            reservation.Weapon.RangedWeaponId,
            reservation.Action.RangedWeaponActionId,
            reservation.Ammunition?.AmmunitionId,
            targetDefense,
            penetration,
            totalHealthDamage,
            totalArmorDamage,
            shots.MoveToImmutable(),
            anyHit ? BuildEffects(reservation, totalHealthDamage, totalArmorDamage) : []);
        return new RangedAttackResult(
            committedActor, committedTurn, true, anyHit, ActionRejectionCodes.None, resolution);
    }

    public static EffectResolution ResolveEffects(
        RangedAttackResolution resolution,
        EffectTargetState target,
        ICombatContentCatalog catalog,
        StatusSystemLimits limits)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return EffectSystem.Resolve(target, resolution.Effects, catalog, limits);
    }

    private static ImmutableArray<EffectRequest> BuildEffects(
        RangedAttackReservation reservation,
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

    public static RangedReloadResult Reload(
        CharacterState? actor,
        RangedReloadRequest request,
        ICombatContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        int RemainingAmmunition() => actor is not null &&
            actor.TryGetInventoryEntry(request.AmmunitionEntryId, out InventoryEntry? currentEntry)
                ? currentEntry!.Stack.Quantity
                : 0;
        RangedReloadResult Reject(string code) => new(
            actor, request.TurnState, RemainingAmmunition(), 0, false, code);

        if (actor is null || actor.Id != request.ActorId)
        {
            return Reject(ActionRejectionCodes.ActorMissing);
        }

        if (!actor.CanAct || !StatusQueries.CanAct(actor.Statuses, catalog))
        {
            return Reject(ActionRejectionCodes.ActorCannotAct);
        }

        if (actor.ContentFingerprint != catalog.Fingerprint || actor.Capabilities.Fingerprint != catalog.Fingerprint)
        {
            return Reject(ActionRejectionCodes.ContentMismatch);
        }

        if (!TryResolveWeapon(actor, request.WeaponItemInstanceId, catalog,
            out ItemInstance? item, out RangedWeaponDefinition? weapon,
            out RangedWeaponState? weaponState, out string rejection))
        {
            return Reject(rejection);
        }

        if (!catalog.TryGetRangedWeaponAction(request.ActionId, out RangedWeaponActionDefinition? action) ||
            action!.Kind != RangedWeaponActionKind.Reload || !weapon!.ActionIds.Contains(request.ActionId.Value))
        {
            return Reject(ActionRejectionCodes.ActionUnknown);
        }

        if (!actor.TryGetInventoryEntry(request.AmmunitionEntryId, out InventoryEntry? ammunitionEntry))
        {
            return Reject(ActionRejectionCodes.AmmunitionRequired);
        }

        InventoryEntry resolvedAmmunitionEntry = ammunitionEntry!;
        InventoryContainer? ammunitionContainer = actor.Items.InventoryContainers.SingleOrDefault(
            value => value.ContainerId == resolvedAmmunitionEntry.OwnerContainerId);
        if (ammunitionContainer?.OwnerId != actor.Id.Value ||
            !catalog.TryGetItem(resolvedAmmunitionEntry.Stack.DefinitionId, out ItemDefinition? ammunitionItem) ||
            ammunitionItem is not AmmunitionDefinition ammunition ||
            weapon.AmmunitionType is null || ammunition.AmmunitionType != weapon.AmmunitionType)
        {
            return Reject(ActionRejectionCodes.AmmunitionIncompatible);
        }

        if (weaponState!.LoadedAmmunitionId is AmmunitionId loadedId &&
            loadedId != ammunition.AmmunitionId && weaponState.CurrentAmmunition > 0)
        {
            return Reject(ActionRejectionCodes.AmmunitionIncompatible);
        }

        int capacity = Math.Max(1, weapon.MagazineCapacity);
        int space = capacity - weaponState.CurrentAmmunition;
        if (space <= 0)
        {
            return Reject(ActionRejectionCodes.MagazineFull);
        }

        if (!request.TurnState.CanSpendActionPoints(action.ActionPointCost))
        {
            return Reject(ActionRejectionCodes.ResourceInsufficient);
        }

        int staminaCost = Math.Max(0, AddBounded(weapon.StaminaCost, action.StaminaCostModifier));
        if (!actor.CharacterResources.CanSpendResource(StaminaId, staminaCost))
        {
            return Reject(ActionRejectionCodes.ResourceInsufficient);
        }

        int loadedAmount = Math.Min(space, Math.Min(action.ReloadAmount, resolvedAmmunitionEntry.Stack.Quantity));
        RangedWeaponState committedWeapon = weaponState with
        {
            LoadedAmmunitionId = ammunition.AmmunitionId,
            CurrentAmmunition = weaponState.CurrentAmmunition + loadedAmount,
        };
        ItemInstance committedItem = item! with
        {
            CurrentDurability = committedWeapon.CurrentDurability,
            RangedWeaponState = committedWeapon,
        };
        ItemSystemState itemCandidate = actor.Items with
        {
            ItemInstances = actor.Items.ItemInstances.Select(value => value.InstanceId == committedItem.InstanceId
                ? committedItem
                : value).ToImmutableArray(),
        };
        ItemSystemResult consumed = ItemSystem.Consume(itemCandidate, request.AmmunitionEntryId, loadedAmount, catalog);
        if (!consumed.Accepted)
        {
            return Reject(ActionRejectionCodes.ResourceInsufficient);
        }

        CharacterState committedActor = actor with
        {
            Items = consumed.State,
            CharacterResources = actor.CharacterResources.SpendResource(StaminaId, staminaCost),
        };
        int remainingAmmunition = consumed.State.InventoryEntries
            .SingleOrDefault(value => value.EntryId == request.AmmunitionEntryId)?.Stack.Quantity ?? 0;
        return new RangedReloadResult(
            committedActor,
            request.TurnState.SpendActionPoints(action.ActionPointCost),
            remainingAmmunition,
            loadedAmount,
            true,
            ActionRejectionCodes.None);
    }

    public static CharacterState Cool(
        CharacterState actor,
        ItemInstanceId weaponItemInstanceId,
        int amount,
        ICombatContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(catalog);
        if (amount < 0 || !TryResolveWeapon(actor, weaponItemInstanceId, catalog,
                out ItemInstance? item, out _, out RangedWeaponState? state, out _))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        RangedWeaponState cooled = state! with { CurrentHeat = Math.Max(0, state.CurrentHeat - amount) };
        return actor.ReplaceItem(item! with { RangedWeaponState = cooled });
    }

    private static bool TryResolveWeapon(
        CharacterState actor,
        ItemInstanceId itemInstanceId,
        ICombatContentCatalog catalog,
        out ItemInstance? item,
        out RangedWeaponDefinition? weapon,
        out RangedWeaponState? state,
        out string rejection)
    {
        item = null;
        weapon = null;
        state = null;
        if (!actor.TryGetItem(itemInstanceId, out item) || !actor.IsEquipped(itemInstanceId) ||
            !catalog.TryGetItem(item!.DefinitionId, out ItemDefinition? definition) ||
            definition is not RangedWeaponDefinition resolvedWeapon ||
            item.RangedWeaponState is not RangedWeaponState resolvedState)
        {
            rejection = ActionRejectionCodes.EquipmentRequired;
            return false;
        }

        weapon = resolvedWeapon;
        state = resolvedState;
        if (state.WeaponId != weapon.RangedWeaponId)
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
        new(reservation.OriginalActor, reservation.Request.TurnState, false, false, code, null);
}
