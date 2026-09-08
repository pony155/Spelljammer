using System.Text;
using System.Text.Json;
using System.Collections.Immutable;
using Spelljammer.Content;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Combat;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Ships;
using Spelljammer.Simulation.Effects;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;

internal static partial class ContentContracts
{
    private static void Milestone5EncounterAndShipContentIsLinked()
    {
        ContentCompilationResult compiled = CompileDirectory(Path.Combine(Milestone2Root, "base"));
        True(compiled.Succeeded, Primary(compiled));
        GameContentSnapshot snapshot = compiled.Snapshot!;
        Equal(20, snapshot.ItemRegistry.Count, "The unified item catalog is incomplete.");
        Equal(6, snapshot.BoardCellRegistry.Count, "The authored ruin does not contain six cells.");
        Equal(5, snapshot.ZoneLinkRegistry.Count, "The authored ruin link graph is incomplete.");
        Equal(1, snapshot.PersonalBoardRegistry.Count, "The first personal board was not published.");
        Equal(1, snapshot.EncounterRegistry.Count, "The first encounter was not published.");
        Equal(1, snapshot.ShipFrameRegistry.Count, "The Wayfarer frame was not published.");
        Equal(11, snapshot.ShipModuleRegistry.Count, "The two ship technology packages are incomplete.");
        Equal(2, snapshot.ShipWeaponConfigurationRegistry.Count, "The ship weapon configurations are incomplete.");

        PersonalBoardDefinition boardDefinition = snapshot.PersonalBoards.Single();
        BoardValidationResult board = TacticalBoard.Create(
            boardDefinition,
            snapshot.BoardCells.Where(value => boardDefinition.CellIds.Contains(value.CellId)),
            snapshot.ZoneLinks.Where(value => boardDefinition.LinkIds.Contains(value.LinkId)));
        True(board.Accepted, board.RejectionCode);
        True(board.Board!.FindPath(new CellId("cell.ruin.entry"), new CellId("cell.ruin.extraction"), TacticalBoard.MaximumCells).Length == 6,
            "The six-zone ruin does not have a bounded entry-to-extraction route.");

        ShipFrameDefinition frame = snapshot.ShipFrames.Single();
        ShipState arcane = CreateContentShip(snapshot, frame, "arcane", new ShipId("ship.first-voyage.arcane"));
        ShipState industrial = CreateContentShip(snapshot, frame, "industrial", new ShipId("ship.first-voyage.industrial"));
        True(arcane.Modules.Any(value => value.Definition.ModuleId == new ModuleId("module.power.aether-dynamo")),
            "The arcane package lost its aether generator.");
        True(industrial.Modules.Any(value => value.Definition.ModuleId == new ModuleId("module.power.diesel-generator")),
            "The industrial package lost its diesel generator.");
        Equal(frame.MaximumHull, arcane.Hull, "A valid loadout did not publish the frame's hull budget.");
    }

    private static void MeleeWeaponsAreDataDrivenAndAtomic()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        Equal(1, snapshot.MeleeWeaponRegistry.Count, "The base melee weapon was not published.");
        Equal(3, snapshot.MeleeWeaponActionRegistry.Count, "The base melee action set is incomplete.");

        True(snapshot.TryGetMeleeWeapon(new MeleeWeaponId("melee-weapon.boarding-blade"), out MeleeWeaponDefinition? weapon),
            "Boarding blade melee rules are missing.");
        True(snapshot.TryGetMeleeWeaponAction(new MeleeWeaponActionId("melee-action.slash"), out _),
            "Slash action is missing.");
        Equal(MeleeWeaponFamily.Blade, weapon!.Family, "Weapon family was not compiled from JSON.");
        Equal(MeleeWeaponTechnology.Conventional, weapon.Technology, "Weapon technology was not compiled from JSON.");
        True(weapon is Spelljammer.Simulation.Items.WeaponDefinition,
            "The melee weapon did not inherit the shared item/equipment definition chain.");
        Equal(265, weapon.WeightHundredthsOfPound, "Melee weight was not compiled in hundredths of a pound.");
        True(weapon.OccupiedSlotIds.Contains(new ContentId("equipment-slot.main-hand")),
            "The melee weapon lost its inherited equipment slot.");

        MeleeWeaponState weaponState = MeleeWeaponState.Create(weapon);
        (CharacterState actor, ItemInstanceId weaponItemInstanceId) = AddEquippedWeapon(
            roster.Characters.First(), weapon, weaponState, null, snapshot);
        CharacterState target = roster.Characters.First(value => value.Id != actor.Id);
        ScenarioDefinition scenario = snapshot.Scenarios.Single(value => value.ScenarioId == actor.ScenarioId);
        True(snapshot.TryGetCharacterResourceProfile(scenario.CharacterResourceProfileId!.Value,
            out CharacterResourceProfileDefinition? profile), "Character resource profile is missing.");
        CharacterTurnState turn = CharacterTurnState.Create(profile!.TurnRules).RestoreActionPoints(profile.TurnRules.BaseActionPoints);
        MeleeAttackRequest request = new(
            actor.Id,
            weaponItemInstanceId,
            new MeleeWeaponActionId("melee-action.slash"),
            new MeleeTarget(target.Id, true, true, 1, 0, 5),
            turn,
            0x5eedUL,
            12);

        MeleeAttackEligibilityResult eligible = MeleeWeaponSystem.CheckEligibility(actor, request, snapshot);
        True(eligible.Accepted, eligible.RejectionCode);
        MeleeAttackResult first = MeleeWeaponSystem.Resolve(eligible.Reservation!, snapshot);
        MeleeAttackResult second = MeleeWeaponSystem.Resolve(eligible.Reservation!, snapshot);
        True(first.Accepted && first.Hit, first.RejectionCode);
        Equal(first.Resolution! with { Effects = [] }, second.Resolution! with { Effects = [] },
            "Melee resolution was not deterministic.");
        True(first.Resolution.Effects.SequenceEqual(second.Resolution.Effects),
            "Melee effect requests were not deterministic.");
        Equal(turn.CurrentActionPoints - eligible.Reservation!.ActionPointCost, first.TurnState.CurrentActionPoints,
            "The action-owned AP cost was not committed.");
        Equal(actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Stamina) - eligible.Reservation.StaminaCost,
            first.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Stamina),
            "The weapon and action stamina cost was not committed.");
        True(first.Actor.TryGetItem(weaponItemInstanceId, out ItemInstance? committedMeleeItem),
            "Committed melee item is missing.");
        Equal(weaponState.CurrentDurability - eligible.Reservation.Action.DurabilityCost,
            committedMeleeItem!.MeleeWeaponState!.CurrentDurability, "Weapon durability was not committed.");
        True(first.Resolution!.Effects.Any(value => value.EffectId == CombatEffectIds.PhysicalDamage),
            "Melee hit did not emit a physical-damage Effect request.");
        EffectResolution meleeEffects = MeleeWeaponSystem.ResolveEffects(
            first.Resolution,
            new EffectTargetState(target.Id.Value, target.CharacterResources, 5, 0, target.Statuses),
            snapshot,
            new StatusSystemLimits(CharacterState.MaximumStatuses, 1_000_000, CharacterState.MaximumStatuses));
        True(meleeEffects.Accepted, meleeEffects.RejectionCode);
        Equal(
            Math.Max(0, target.CharacterResources.GetCurrentValue(CharacterResourceIds.Health) - first.Resolution.HealthDamage),
            meleeEffects.State.Resources.GetCurrentValue(CharacterResourceIds.Health),
            "Melee Effect resolution did not apply its computed Health damage.");

        MeleeAttackRequest tooFar = request with {
            Target = request.Target! with { Distance = weapon.Range + 1 },
        };
        MeleeAttackEligibilityResult rejected = MeleeWeaponSystem.CheckEligibility(actor, tooFar, snapshot);
        False(rejected.Accepted, "An out-of-range melee attack was accepted.");
        Equal(ActionRejectionCodes.TargetOutOfRange, rejected.RejectionCode, "Out-of-range rejection was unstable.");
        Equal(actor, eligible.Reservation.OriginalActor, "Eligibility mutated the actor before commit.");
        Equal(turn, request.TurnState, "Eligibility mutated Action Points before commit.");
        Equal(weaponState, actor.Items.ItemInstances.Single(value => value.InstanceId == weaponItemInstanceId).MeleeWeaponState!,
            "Eligibility mutated weapon state before commit.");
    }

    private static void RangedWeaponsUseAmmunitionAndReloadAtomically()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        Equal(1, snapshot.RangedWeaponRegistry.Count, "The base ranged weapon was not published.");
        Equal(1, snapshot.AmmunitionRegistry.Count, "The base ammunition definition was not published.");
        Equal(3, snapshot.RangedWeaponActionRegistry.Count, "The base ranged action set is incomplete.");

        True(snapshot.TryGetRangedWeapon(new RangedWeaponId("ranged-weapon.service-pistol"), out RangedWeaponDefinition? weapon),
            "Service pistol ranged rules are missing.");
        True(snapshot.TryGetAmmunition(new AmmunitionId("ammunition.pistol.standard"),
            out AmmunitionDefinition? ammunition), "Standard pistol ammunition is missing.");
        True(ammunition is ItemDefinition && snapshot.TryGetItem(ammunition!.Id, out ItemDefinition? ammunitionItem) &&
            ReferenceEquals(ammunition, ammunitionItem), "Ammunition is not published through the unified item catalog.");
        Equal(50, ammunition!.MaximumStackSize, "Ammunition maximumStackSize was not compiled.");
        Equal(3, ammunition.WeightHundredthsOfPound, "Ammunition weight is not stored in hundredths of a pound.");
        Equal(RangedWeaponFamily.Pistol, weapon!.Family, "Ranged family was not compiled from JSON.");
        Equal(RangedWeaponTechnology.Ballistic, weapon.Technology, "Ranged technology was not compiled from JSON.");
        True(weapon is Spelljammer.Simulation.Items.WeaponDefinition,
            "The ranged weapon did not inherit the shared item/equipment definition chain.");
        Equal(209, weapon.WeightHundredthsOfPound, "Ranged weight was not compiled in hundredths of a pound.");
        True(weapon.OccupiedSlotIds.Contains(new ContentId("equipment-slot.off-hand")),
            "The ranged weapon lost its inherited equipment slot.");

        RangedWeaponState weaponState = RangedWeaponState.Create(weapon, ammunition);
        (CharacterState actor, ItemInstanceId weaponItemInstanceId) = AddEquippedWeapon(
            roster.Characters.First(), weapon, null, weaponState, snapshot);
        CharacterState target = roster.Characters.First(value => value.Id != actor.Id);
        ScenarioDefinition scenario = snapshot.Scenarios.Single(value => value.ScenarioId == actor.ScenarioId);
        True(snapshot.TryGetCharacterResourceProfile(scenario.CharacterResourceProfileId!.Value,
            out CharacterResourceProfileDefinition? profile), "Character resource profile is missing.");
        CharacterTurnState turn = CharacterTurnState.Create(profile!.TurnRules).RestoreActionPoints(profile.TurnRules.BaseActionPoints);
        RangedAttackRequest request = new(
            actor.Id,
            weaponItemInstanceId,
            new RangedWeaponActionId("ranged-action.standard-shot"),
            new RangedTarget(target.Id, true, true, 6, 0, 5, 0),
            turn,
            100,
            0x51deUL,
            4);

        RangedAttackEligibilityResult eligible = RangedWeaponSystem.CheckAttackEligibility(actor, request, snapshot);
        True(eligible.Accepted, eligible.RejectionCode);
        RangedAttackResult first = RangedWeaponSystem.ResolveAttack(eligible.Reservation!, snapshot);
        RangedAttackResult second = RangedWeaponSystem.ResolveAttack(eligible.Reservation!, snapshot);
        True(first.Accepted && first.Hit, first.RejectionCode);
        Equal(first.Resolution! with { Shots = [], Effects = [] }, second.Resolution! with { Shots = [], Effects = [] },
            "Ranged resolution summary was not deterministic.");
        True(first.Resolution.Shots.SequenceEqual(second.Resolution.Shots),
            "Per-shot ranged resolution was not deterministic.");
        True(first.Resolution.Effects.SequenceEqual(second.Resolution.Effects),
            "Ranged effect requests were not deterministic.");
        True(first.Actor.TryGetItem(weaponItemInstanceId, out ItemInstance? committedRangedItem),
            "Committed ranged item is missing.");
        Equal(weaponState.CurrentAmmunition - 1, committedRangedItem!.RangedWeaponState!.CurrentAmmunition,
            "Accepted fire did not consume authored ammunition.");
        Equal(turn.CurrentActionPoints - eligible.Reservation!.ActionPointCost, first.TurnState.CurrentActionPoints,
            "The ranged action's AP cost was not committed.");
        True(first.Resolution!.TotalHealthDamage > 0, "A successful ranged attack produced no Health damage.");
        True(first.Resolution.Effects.Any(value => value.EffectId == CombatEffectIds.PhysicalDamage),
            "Ranged hit did not emit a physical-damage Effect request.");
        EffectResolution rangedEffects = RangedWeaponSystem.ResolveEffects(
            first.Resolution,
            new EffectTargetState(target.Id.Value, target.CharacterResources, 5, 0, target.Statuses),
            snapshot,
            new StatusSystemLimits(CharacterState.MaximumStatuses, 1_000_000, CharacterState.MaximumStatuses));
        True(rangedEffects.Accepted, rangedEffects.RejectionCode);
        Equal(
            Math.Max(0, target.CharacterResources.GetCurrentValue(CharacterResourceIds.Health) - first.Resolution.TotalHealthDamage),
            rangedEffects.State.Resources.GetCurrentValue(CharacterResourceIds.Health),
            "Ranged Effect resolution did not apply its computed Health damage.");

        InventoryEntryId ammunitionEntryId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        InventoryContainer actorContainer = actor.Items.InventoryContainers.Single();
        ItemSystemResult stocked = ItemSystem.AddStack(
            actor.Items, actorContainer.ContainerId, ammunitionEntryId, ammunition.Id, 10, snapshot);
        True(stocked.Accepted, stocked.RejectionCode);
        CharacterState stockedActor = actor with { Items = stocked.State };
        RangedWeaponState empty = weaponState with { LoadedAmmunitionId = null, CurrentAmmunition = 0 };
        ItemInstance rangedItem = stockedActor.Items.ItemInstances.Single(value => value.InstanceId == weaponItemInstanceId);
        CharacterState unloadedActor = stockedActor.ReplaceItem(rangedItem with { RangedWeaponState = empty });
        RangedAttackEligibilityResult unloaded = RangedWeaponSystem.CheckAttackEligibility(unloadedActor, request, snapshot);
        False(unloaded.Accepted, "An unloaded ranged weapon was fired.");
        Equal(ActionRejectionCodes.AmmunitionRequired, unloaded.RejectionCode,
            "An unloaded weapon returned the wrong rejection.");
        Equal(actor, eligible.Reservation.OriginalActor, "Attack eligibility mutated the actor before commit.");
        Equal(turn, request.TurnState, "Attack eligibility mutated AP before commit.");
        Equal(weaponState, actor.Items.ItemInstances.Single(value => value.InstanceId == weaponItemInstanceId).RangedWeaponState!,
            "Attack eligibility mutated weapon state before commit.");

        RangedReloadRequest reloadRequest = new(
            actor.Id,
            weaponItemInstanceId,
            new RangedWeaponActionId("ranged-action.reload-magazine"),
            ammunitionEntryId,
            turn);
        RangedReloadResult reloaded = RangedWeaponSystem.Reload(unloadedActor, reloadRequest, snapshot);
        True(reloaded.Accepted, reloaded.RejectionCode);
        Equal(weapon.MagazineCapacity, reloaded.LoadedAmount, "Reload ignored magazine capacity.");
        Equal(10 - weapon.MagazineCapacity, reloaded.RemainingAmmunition,
            "Reload did not return the remaining inventory ammunition.");
        True(reloaded.Actor!.TryGetItem(weaponItemInstanceId, out ItemInstance? reloadedItem), "Reloaded item is missing.");
        Equal(ammunition.AmmunitionId, reloadedItem!.RangedWeaponState!.LoadedAmmunitionId!.Value,
            "Reload did not publish the loaded ammunition identity.");
        Equal(10 - weapon.MagazineCapacity,
            reloaded.Actor.Items.InventoryEntries.Single(value => value.EntryId == ammunitionEntryId).Stack.Quantity,
            "Reload did not atomically consume the inventory stack.");

        RangedReloadResult full = RangedWeaponSystem.Reload(stockedActor, reloadRequest, snapshot);
        False(full.Accepted, "A full magazine accepted more ammunition.");
        Equal(ActionRejectionCodes.MagazineFull, full.RejectionCode, "Full-magazine rejection was unstable.");
        Equal(stockedActor, full.Actor!, "Rejected reload mutated weapon or inventory state.");
        Equal(turn, full.TurnState, "Rejected reload spent AP.");

        Dictionary<string, byte[]> invalidFiles = ReadFiles(Path.Combine(Milestone2Root, "base"));
        ReplaceText(invalidFiles, "Definitions/RangedWeapons/service-pistol.json",
            "\"technology\": \"ballistic\"", "\"technology\": \"arcane\"");
        ContentCompilationResult invalidCombination = new GameContentCompiler().Compile(
            [new MemoryPackSource(invalidFiles, [])], GameVersion);
        Equal(ContentDiagnosticCodes.ValueOutOfRange,
            invalidCombination.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
            "An invalid Pistol/Arcane family-technology combination was published.");
    }

    private static ShipState CreateContentShip(GameContentSnapshot snapshot, ShipFrameDefinition frame, string path, ShipId shipId)
    {
        ContentId pathId = new($"ship.path.{path}");
        ShipModuleDefinition[] modules = [.. snapshot.ShipModules.Where(value => value.CompatiblePathIds.Contains(pathId))
            .GroupBy(value => value.MountId)
            .Select(group => group.OrderBy(value => value.ModuleId).First())];
        ShipWeaponConfigurationDefinition weapon = snapshot.ShipWeaponConfigurations.Single(value =>
            value.NetworkId == new NetworkId(path == "arcane" ? "network.aether" : "network.power"));
        ShipLoadoutResult result = ShipLoadoutSystem.Create(
            shipId,
            new TeamId("team.player"),
            frame,
            pathId,
            modules,
            weapon,
            ImmutableDictionary<ResourceId, int>.Empty
                .Add(weapon.ResourceId, 12)
                .Add(new ResourceId("resource.spare-parts"), 4));
        True(result.Accepted, result.RejectionCode);
        return result.Ship!;
    }
}
