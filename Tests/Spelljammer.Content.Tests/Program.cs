using System.Text;
using System.Text.Json;
using System.Collections.Immutable;
using Spelljammer.Content;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Diagnostics;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;

return ContentContracts.Run();

internal static class ContentContracts
{
    private static readonly SemanticVersion GameVersion = new(0, 1, 0);
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Milestone0");
    private static readonly string Milestone2Root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Milestone2");

    public static int Run()
    {
        StableIdsAreValidatedAndOrdinal();
        ValidFixtureIsCanonicalAndDeterministic();
        EveryFrozenDiagnosticCaseIsRecognized();
        FailedReplacementPreservesPublishedSnapshot();
        BaseAbilitysAndSkillsAreTypedAndIndexed();
        LevelProgressionTablesAreDataDriven();
        CharacterResourcesAreBoundedAndDirectional();
        Milestone2InvalidCasesAreRecognized();
        AdditiveSkillIsDynamicAndReversible();
        CharacterDefinitionsRejectInvalidGraphs();
        BaseRosterIsDeterministicAndDynamic();
        RecruitmentHonorsScenarioRosterLimit();
        EligibilityAndResolutionAreAtomic();
        TrainingGrantsAccessOnlyAtCompletion();
        AccessSourcesCoexistAndRecompute();
        SupernaturalDefinitionsAndExecutionAreBounded();
        MindlinkRequiresKnowledgeConsentAndStrain();
        RaceCapabilitiesRespectTheirBoundaries();
        Milestone5EncounterAndShipContentIsLinked();
        MeleeWeaponsAreDataDrivenAndAtomic();
        RangedWeaponsUseAmmunitionAndReloadAtomically();
        Console.WriteLine("Content and character capability contracts passed.");
        return 0;
    }

    private static void StableIdsAreValidatedAndOrdinal()
    {
        True(ContentId.TryParse("ability.strength", out ContentId valid), "A valid ID was rejected.");
        False(ContentId.TryParse("Ability.Strength", out _), "A culture-sensitive ID was accepted.");
        False(default(ContentId).IsValid, "The default ID became valid.");
        string maximum = "domain." + new string('a', 120);
        Equal(ContentId.MaximumLength, maximum.Length, "Maximum-length fixture is wrong.");
        True(ContentId.TryParse(maximum, out _), "A maximum-length ID was rejected.");
        False(ContentId.TryParse(maximum + "a", out _), "An oversized ID was accepted.");
        True(valid.CompareTo(new ContentId("ability.toughness")) < 0, "ID comparison was not ordinal.");
    }

    private static void Milestone5EncounterAndShipContentIsLinked()
    {
        ContentCompilationResult compiled = CompileDirectory(Path.Combine(Milestone2Root, "base"));
        True(compiled.Succeeded, Primary(compiled));
        GameContentSnapshot snapshot = compiled.Snapshot!;
        Equal(21, snapshot.ItemRegistry.Count, "The unified item catalog is incomplete.");
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
        Equal(first.Resolution!, second.Resolution!, "Melee resolution was not deterministic.");
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

        MeleeAttackRequest tooFar = request with
        {
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
        Equal(first.Resolution! with { Shots = [] }, second.Resolution! with { Shots = [] },
            "Ranged resolution summary was not deterministic.");
        True(first.Resolution.Shots.SequenceEqual(second.Resolution.Shots),
            "Per-shot ranged resolution was not deterministic.");
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

    private static void ValidFixtureIsCanonicalAndDeterministic()
    {
        string baseRoot = Path.Combine(FixtureRoot, "valid", "base");
        GameContentCompiler compiler = new();
        ContentCompilationResult first = compiler.Compile([new DirectoryContentPackSource(baseRoot)], GameVersion);
        ContentCompilationResult second = compiler.Compile([new DirectoryContentPackSource(baseRoot)], GameVersion);
        True(first.Succeeded, Primary(first));
        True(second.Succeeded, Primary(second));

        using JsonDocument expectedFingerprint = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureRoot, "expected", "fingerprints.json")));
        string expectedHash = expectedFingerprint.RootElement.GetProperty("sha256").GetString()!;
        Equal(expectedHash, first.Snapshot!.Fingerprint.ToString(), "Canonical fingerprint changed.");
        Equal(first.Snapshot.Fingerprint, second.Snapshot!.Fingerprint, "Repeated compilation was not deterministic.");
        byte[] expectedCanonical = File.ReadAllBytes(Path.Combine(FixtureRoot, "expected", "canonical-semantic.json"));
        True(expectedCanonical.AsSpan().SequenceEqual(first.Snapshot.CanonicalSemanticContent.AsSpan()), "Canonical semantic bytes changed.");
    }

    private static void EveryFrozenDiagnosticCaseIsRecognized()
    {
        using JsonDocument casesDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureRoot, "invalid", "cases.json")));
        Dictionary<string, byte[]> baseFiles = ReadFiles(Path.Combine(FixtureRoot, "valid", "base"));
        foreach (JsonElement testCase in casesDocument.RootElement.GetProperty("cases").EnumerateArray())
        {
            Dictionary<string, byte[]> files = baseFiles.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
            List<string> duplicateEntries = [];
            ApplyMutations(testCase, files, duplicateEntries);
            List<IContentPackSource> sources = [new MemoryPackSource(files, duplicateEntries)];
            if (testCase.TryGetProperty("additionalPacks", out JsonElement additionalPacks))
            {
                foreach (JsonElement pack in additionalPacks.EnumerateArray())
                {
                    Dictionary<string, byte[]> packFiles = [];
                    foreach (JsonElement file in pack.GetProperty("files").EnumerateArray())
                    {
                        packFiles.Add(file.GetProperty("path").GetString()!, Encoding.UTF8.GetBytes(file.GetProperty("text").GetString()!));
                    }

                    sources.Add(new MemoryPackSource(packFiles, []));
                }
            }

            SemanticVersion gameVersion = testCase.TryGetProperty("gameVersion", out JsonElement versionElement)
                ? ParseVersion(versionElement.GetString()!)
                : GameVersion;
            ContentLimits limits = testCase.TryGetProperty("limitOverrides", out JsonElement overrides)
                ? ContentLimits.Version1 with { ManifestBytes = overrides.GetProperty("manifestBytes").GetInt32() }
                : ContentLimits.Version1;
            ContentCompilationResult result = new GameContentCompiler(limits).Compile(sources, gameVersion);
            string expected = testCase.GetProperty("expectedPrimaryDiagnostic").GetString()!;
            Equal(expected, result.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
                $"Wrong primary diagnostic for '{testCase.GetProperty("id").GetString()}'.");
        }
    }

    private static void FailedReplacementPreservesPublishedSnapshot()
    {
        GameContentRegistry registry = new();
        string baseRoot = Path.Combine(FixtureRoot, "valid", "base");
        ContentCompilationResult valid = registry.CompileAndPublish([new DirectoryContentPackSource(baseRoot)], GameVersion);
        True(valid.Succeeded, Primary(valid));
        GameContentSnapshot published = registry.Current!;
        ContentCompilationResult invalid = registry.CompileAndPublish([new MemoryPackSource([], [])], GameVersion);
        False(invalid.Succeeded, "An invalid replacement was published.");
        True(ReferenceEquals(published, registry.Current), "Failed replacement changed the registry owner.");
    }

    private static void BaseAbilitysAndSkillsAreTypedAndIndexed()
    {
        ContentCompilationResult result = CompileDirectory(Path.Combine(Milestone2Root, "base"));
        True(result.Succeeded, Primary(result));
        GameContentSnapshot snapshot = result.Snapshot!;
        using JsonDocument expectedFingerprints = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(Milestone2Root, "expected", "fingerprints.json")));
        Equal(
            expectedFingerprints.RootElement.GetProperty("baseSha256").GetString()!,
            snapshot.Fingerprint.ToString(),
            "The base Ability and Skill fingerprint changed.");
        Equal(6, snapshot.AbilityRegistry.Count, "The base Ability roster is incomplete.");
        Equal(30, snapshot.SkillRegistry.Count, "The base Skill roster is incomplete.");
        Equal(5, snapshot.StatusRegistry.Count, "The base Status roster is incomplete.");
        Equal(11, snapshot.EffectRegistry.Count, "The base Effect roster is incomplete.");
        True(snapshot.TryGetStatus(new StatusId("status.charmed"), out StatusDefinition? charmed),
            "Charmed was not published through the Status registry.");
        Equal(StatusStackPolicy.Refresh, charmed!.StackPolicy, "Charmed stack policy changed.");
        True(snapshot.TryGetEffect(new EffectId("effect.status.apply-burning"), out EffectDefinition? ignite) &&
            ignite!.Payload is ApplyStatusEffectPayload { StatusId: var statusId } &&
            statusId == new StatusId("status.burning"),
            "Apply Burning did not resolve its Status reference.");
        True(snapshot.TryGetEffect(CombatEffectIds.PhysicalDamage, out EffectDefinition? physicalDamage) &&
            physicalDamage!.Payload is DamageEffectPayload { Type: EffectType.PhysicalDamage },
            "Combat physical damage was not compiled as a typed damage payload.");
        True(snapshot.TryGetEffect(new EffectId("effect.equipment.focus"), out EffectDefinition? focus) &&
            focus!.Payload is EmitEventEffectPayload { EventId: var eventId } &&
            eventId == new ContentId("event.equipment.focus"),
            "Equipment focus was not compiled as a typed event payload.");
        True(snapshot.TryGetStatus(new StatusId("status.mindlinked"), out StatusDefinition? mindlinked) &&
            mindlinked!.DurationType == StatusDurationType.UntilRemoved,
            "Mindlinked was not published as an until-removed Status.");
        string[] expectedAbilitys =
        [
            "ability.agility", "ability.intelligence", "ability.perception",
            "ability.strength", "ability.toughness", "ability.willpower",
        ];
        string[] expectedSkills =
        [
            "skill.acrobatics", "skill.alchemy", "skill.ancient-lore", "skill.archery", "skill.astrogation",
            "skill.athletics", "skill.command", "skill.cooking", "skill.crafting", "skill.deception",
            "skill.defense", "skill.enchantment", "skill.engineering", "skill.eva", "skill.firearms", "skill.gunnery",
            "skill.insight", "skill.language-literacy", "skill.magic", "skill.medicine", "skill.melee",
            "skill.merchant", "skill.negotiation", "skill.piloting", "skill.psionics", "skill.rigging",
            "skill.salvage", "skill.sensors", "skill.stealth", "skill.xenology",
        ];
        Equal(string.Join('|', expectedAbilitys), string.Join('|', snapshot.Abilities.Select(value => value.Id)),
            "Ability iteration is incomplete or nondeterministic.");
        Equal(string.Join('|', expectedSkills), string.Join('|', snapshot.Skills.Select(value => value.Id)),
            "Skill iteration is incomplete or nondeterministic.");

        SkillId engineeringId = new("skill.engineering");
        True(snapshot.SkillRegistry.TryGet(engineeringId, out var engineering), "Typed Skill lookup failed.");
        Equal(engineeringId, engineering!.SkillId, "Typed Skill lookup returned the wrong definition.");
        True(snapshot.SkillRegistry.TryGetIndex(engineeringId, out ScopedContentIndex<SkillId> index),
            "Dense Skill index lookup failed.");
        Equal(engineeringId, snapshot.SkillRegistry.Resolve(index).SkillId, "Dense Skill index resolved incorrectly.");
    }

    private static void Milestone2InvalidCasesAreRecognized()
    {
        using JsonDocument casesDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Milestone2Root, "invalid", "cases.json")));
        Dictionary<string, byte[]> baseFiles = ReadFiles(Path.Combine(Milestone2Root, "base"));
        foreach (JsonElement testCase in casesDocument.RootElement.GetProperty("cases").EnumerateArray())
        {
            Dictionary<string, byte[]> files = baseFiles.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
            ApplyMutations(testCase, files, []);
            ContentLimits limits = ApplyLimitOverrides(testCase, ContentLimits.Version1);
            ContentCompilationResult result = new GameContentCompiler(limits).Compile([new MemoryPackSource(files, [])], GameVersion);
            string expected = testCase.GetProperty("expectedPrimaryDiagnostic").GetString()!;
            Equal(expected, result.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
                $"Wrong M2 diagnostic for '{testCase.GetProperty("id").GetString()}'.");
        }
    }

    private static void LevelProgressionTablesAreDataDriven()
    {
        Dictionary<string, byte[]> files = ReadFiles(Path.Combine(Milestone2Root, "base"));
        ContentCompilationResult result = new GameContentCompiler().Compile([new MemoryPackSource(files, [])], GameVersion);
        True(result.Succeeded, Primary(result));
        GameContentSnapshot snapshot = result.Snapshot!;
        Equal(1, snapshot.LevelProgressionTableRegistry.Count, "The progression table was not published.");
        True(snapshot.TryGetLevelProgressionTable(new LevelProgressionTableId("level-progression.character.standard"),
            out LevelProgressionTableDefinition? table), "Typed progression lookup failed.");
        Equal(20, table!.MaximumLevel, "Maximum level was not derived from authored rows.");
        Equal(100, table.Levels[1].RequiredExperience, "The authored XP threshold was replaced by a code default.");
        Equal(3, table.Levels[1].MaximumHealthIncrease, "The authored Health reward was replaced by a code default.");
        Equal(1, table.Levels[3].AbilityPoints, "The authored Ability Point reward was replaced by a code default.");

        Dictionary<string, byte[]> invalid = Clone(files);
        ReplaceText(invalid, "Definitions/LevelProgressionTables/character-standard.json",
            "{\"level\":2,\"requiredExperience\":100",
            "{\"level\":2,\"requiredExperience\":0");
        ContentCompilationResult rejected = new GameContentCompiler().Compile([new MemoryPackSource(invalid, [])], GameVersion);
        Equal(ContentDiagnosticCodes.SemanticInvalid, rejected.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
            "A non-increasing XP threshold was accepted.");
    }

    private static void CharacterResourcesAreBoundedAndDirectional()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        CharacterState character = roster.Characters[0];
        True(snapshot.TryGetCharacterResourceProfile(new CharacterResourceProfileId("character-resources.standard"),
            out CharacterResourceProfileDefinition? profile), "The character resource profile was not published.");
        CharacterResourceRule staminaRule = profile!.Resources.Single(value => value.ResourceId == CharacterResourceIds.Stamina);
        Equal(5, staminaRule.BaseRecoveryRate, "Stamina recovery did not come from JSON.");
        Equal("50|75|100", string.Join('|', profile.Resources.Single(value =>
            value.ResourceId == CharacterResourceIds.Strain).ThresholdPercentages), "Strain thresholds did not come from JSON.");
        Equal(100, profile.TurnRules.TurnMeterThreshold, "Turn Meter threshold did not come from JSON.");
        Equal(10, profile.TurnRules.BaseActionPoints, "Base AP did not come from JSON.");
        CharacterTurnState turn = CharacterTurnState.Create(profile.TurnRules).AddTurnMeter(20);
        Equal(8, turn.CurrentTurnMeter, "Low Stamina did not apply the authored Turn Meter penalty.");
        Equal(6, turn.GetActionPointCost(new ContentId("action.personal.spell")),
            "The spell AP cost did not come from JSON.");

        CharacterResourceSet spent = character.CharacterResources.SpendResource(CharacterResourceIds.Stamina, 20);
        Equal(80, spent.GetCurrentValue(CharacterResourceIds.Stamina), "Stamina spending produced the wrong value.");
        Equal(85, spent.RecoverOneTick().GetCurrentValue(CharacterResourceIds.Stamina), "Stamina recovery ignored its profile.");
        CharacterResourceSet strained = character.CharacterResources.GenerateStrain(150);
        Equal(100, strained.GetCurrentValue(CharacterResourceIds.Strain), "Strain was not clamped to its authored maximum.");
        True(strained.CheckPsionicOverload(), "Maximum Strain did not report overload risk.");
        Equal(97, strained.RecoverOneTick().GetCurrentValue(CharacterResourceIds.Strain), "Strain decay moved in the wrong direction.");
    }

    private static void AdditiveSkillIsDynamicAndReversible()
    {
        string baseRoot = Path.Combine(Milestone2Root, "base");
        string modRoot = Path.Combine(Milestone2Root, "additive", "starwrights");
        GameContentCompiler compiler = new();
        ContentCompilationResult baseOnly = CompileDirectory(baseRoot);
        ContentCompilationResult withMod = compiler.Compile(
            [new DirectoryContentPackSource(modRoot), new DirectoryContentPackSource(baseRoot)], GameVersion);
        ContentCompilationResult repeated = compiler.Compile(
            [new DirectoryContentPackSource(baseRoot), new DirectoryContentPackSource(modRoot)], GameVersion);
        True(baseOnly.Succeeded, Primary(baseOnly));
        True(withMod.Succeeded, Primary(withMod));
        True(repeated.Succeeded, Primary(repeated));
        using JsonDocument expectedFingerprints = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(Milestone2Root, "expected", "fingerprints.json")));
        Equal(
            expectedFingerprints.RootElement.GetProperty("basePlusStarwrightsSha256").GetString()!,
            withMod.Snapshot!.Fingerprint.ToString(),
            "The additive fixture fingerprint changed.");
        Equal(31, withMod.Snapshot.SkillRegistry.Count, "The additive Skill did not enter the generic registry.");
        True(withMod.Snapshot.SkillRegistry.TryGet(new SkillId("skill.mod.starwrights.gravimetry"), out _),
            "The namespaced Skill was not available through typed lookup.");
        Equal(withMod.Snapshot.Fingerprint, repeated.Snapshot!.Fingerprint,
            "Pack input order changed the resolved semantic fingerprint.");
        True(withMod.Snapshot.Inspect().Entries.Any(entry => entry.Id == "skill.mod.starwrights.gravimetry"),
            "The headless inspection projection omitted dynamic content.");
        RosterCreationResult dynamicRoster = CharacterCreator.CreateRoster(
            withMod.Snapshot.Fingerprint,
            new ScenarioId("scenario.first-voyage"),
            41,
            withMod.Snapshot,
            FullSupport(withMod.Snapshot));
        True(dynamicRoster.Succeeded, dynamicRoster.Failure.ToString());
        Equal(31, dynamicRoster.Roster!.Characters[0].Capabilities.Snapshot(withMod.Snapshot).Skills.Length,
            "Character capability storage did not expand for an additive Skill.");

        True(baseOnly.Snapshot!.SkillRegistry.TryGetIndex(new SkillId("skill.engineering"), out ScopedContentIndex<SkillId> baseIndex),
            "Base dense index was unavailable.");
        Throws<ContentIndexFingerprintMismatchException>(
            () => withMod.Snapshot.SkillRegistry.Resolve(baseIndex),
            "A dense index crossed registry fingerprints.");

        ContentCompilationResult restored = CompileDirectory(baseRoot);
        Equal(baseOnly.Snapshot.Fingerprint, restored.Snapshot!.Fingerprint,
            "Disabling the additive pack did not restore the base fingerprint.");
    }

    private static void CharacterDefinitionsRejectInvalidGraphs()
    {
        Dictionary<string, byte[]> baseFiles = ReadFiles(Path.Combine(Milestone2Root, "base"));
        Dictionary<string, byte[]> missingGrant = Clone(baseFiles);
        ReplaceText(missingGrant, "Definitions/Races/human.json", "feat.race.human.versatility", "feat.race.human.missing");
        ContentCompilationResult missing = new GameContentCompiler().Compile([new MemoryPackSource(missingGrant, [])], GameVersion);
        False(missing.Succeeded, "A missing racial grant was published.");
        Equal("CONTENT_REFERENCE_UNKNOWN", missing.Diagnostics[0].Code, "Missing grants did not fail during linking.");
        GameContentRegistry registry = new();
        ContentCompilationResult published = registry.CompileAndPublish(
            [new MemoryPackSource(Clone(baseFiles), [])], GameVersion);
        True(published.Succeeded, Primary(published));
        GameContentSnapshot previous = registry.Current!;
        registry.CompileAndPublish([new MemoryPackSource(missingGrant, [])], GameVersion);
        True(ReferenceEquals(previous, registry.Current), "An invalid grant replaced the published registry.");

        Dictionary<string, byte[]> incompatible = Clone(baseFiles);
        ReplaceText(incompatible, "Definitions/Characters/human.json", "heritage.human.hearthworld", "heritage.elf.dawnweave");
        ContentCompilationResult wrongHeritage = new GameContentCompiler().Compile([new MemoryPackSource(incompatible, [])], GameVersion);
        False(wrongHeritage.Succeeded, "An incompatible Heritage was published.");
        Equal("CONTENT_SEMANTIC_INVALID", wrongHeritage.Diagnostics[0].Code, "Incompatible Heritage used the wrong diagnostic.");

        Dictionary<string, byte[]> cycle = Clone(baseFiles);
        string path = "Definitions/Feats/race-human.json";
        string text = Encoding.UTF8.GetString(cycle[path]);
        cycle[path] = Encoding.UTF8.GetBytes(text.Replace("}\n", ",\"grantedFeatIds\":[\"feat.race.human.versatility\"]}\n", StringComparison.Ordinal));
        ContentCompilationResult cyclic = new GameContentCompiler().Compile([new MemoryPackSource(cycle, [])], GameVersion);
        False(cyclic.Succeeded, "A capability grant cycle was published.");
        Equal("CONTENT_SEMANTIC_INVALID", cyclic.Diagnostics[0].Code, "Grant cycles used the wrong diagnostic.");
    }

    private static void BaseRosterIsDeterministicAndDynamic()
    {
        ContentCompilationResult compiled = CompileDirectory(Path.Combine(Milestone2Root, "base"));
        True(compiled.Succeeded, Primary(compiled));
        GameContentSnapshot snapshot = compiled.Snapshot!;
        CrewSupportProfile support = FullSupport(snapshot);
        ScenarioId scenario = new("scenario.first-voyage");
        RosterCreationResult first = CharacterCreator.CreateRoster(snapshot.Fingerprint, scenario, 0x5eedUL, snapshot, support);
        RosterCreationResult second = CharacterCreator.CreateRoster(snapshot.Fingerprint, scenario, 0x5eedUL, snapshot, support);
        True(first.Succeeded, first.Failure.ToString());
        True(second.Succeeded, second.Failure.ToString());
        Equal(11, first.Roster!.Characters.Length, "The first-voyage roster does not cover all base races.");
        Equal(
            Describe(first.Roster, snapshot),
            Describe(second.Roster!, snapshot),
            "An identical content fingerprint and seed produced a different roster.");
        Equal(snapshot.Abilities.Length, first.Roster.AbilityColumns.Length, "Roster Ability columns are not registry-driven.");
        Equal(snapshot.Skills.Length, first.Roster.SkillColumns.Length, "Roster Skill columns are not registry-driven.");
        CharacterRosterDisplay display = RosterInspection.Project(
            first.Roster,
            snapshot,
            key => "[" + key + "]",
            ActionRejectionCodes.EquipmentRequired);
        Equal(snapshot.Abilities.Length, display.Characters[0].Abilities.Length, "Roster display fixed its Ability columns.");
        Equal("[command.equipment-required]", display.DisabledReason!, "Disabled reason did not pass through localization.");

        RosterCreationResult unsupported = CharacterCreator.CreateRoster(
            snapshot.Fingerprint,
            scenario,
            0x5eedUL,
            snapshot,
            new CrewSupportProfile(ImmutableHashSet<ContentId>.Empty, support.AvailableItemDefinitionIds));
        False(unsupported.Succeeded, "An unsupported mixed-race roster was published.");
        Equal(CharacterCreationFailure.SupportUnavailable, unsupported.Failure, "Missing quarters/care support was not explicit.");
    }

    private static void EligibilityAndResolutionAreAtomic()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        CharacterState eidolon = roster.Characters.Single(value => value.RaceId == new RaceId("race.eidolon"));
        ActionDefinition action = RaceCapabilities.CreateSoulAnchorRecoveryAction(eidolon, snapshot)!;
        ActionRequest missingContext = new(
            eidolon.Id,
            action.Id,
            new ActionTarget(new ContentId("target.self"), true, true),
            ImmutableHashSet<ContentId>.Empty,
            17,
            0);
        int before = eidolon.Resources[new ResourceId("resource.resonance")];
        ActionEligibilityResult rejected = CharacterActionSystem.CheckEligibility(eidolon, action, missingContext, snapshot);
        False(rejected.Accepted, "An action missing its safe recovery context was accepted.");
        Equal(ActionRejectionCodes.ContextRequired, rejected.RejectionCode, "Eligibility order returned the wrong reason.");
        Equal(before, eidolon.Resources[new ResourceId("resource.resonance")], "A rejection consumed a resource.");

        ActionRequest valid = missingContext with
        {
            ContextIds = ImmutableHashSet.Create(new ContentId("context.recovery.safe-anchor")),
        };
        ActionEligibilityResult eligible = CharacterActionSystem.CheckEligibility(eidolon, action, valid, snapshot);
        True(eligible.Accepted, eligible.RejectionCode);
        Equal(before, eidolon.Resources[new ResourceId("resource.resonance")], "Reservation mutated published state.");
        ActionExecutionResult first = CharacterActionSystem.Resolve(eligible.Reservation!, snapshot);
        ActionExecutionResult repeated = CharacterActionSystem.Resolve(eligible.Reservation!, snapshot);
        Equal(first.Resolution!.Roll, repeated.Resolution!.Roll, "Owned action randomness was not reproducible.");
        Equal(before - 2, first.State.Resources[new ResourceId("resource.resonance")], "Committed Soul Anchor cost was wrong.");
        True(first.Resolution.AbilityId.IsValid && first.Resolution.SkillId.IsValid, "Resolution explanation omitted contributors.");
    }

    private static void RecruitmentHonorsScenarioRosterLimit()
    {
        Dictionary<string, byte[]> invalidFiles = ReadFiles(Path.Combine(Milestone2Root, "base"));
        ReplaceText(
            invalidFiles,
            "Definitions/Scenarios/first-voyage.json",
            "\"maximumRosterSize\": 8",
            "\"maximumRosterSize\": 0");
        ContentCompilationResult invalid = new GameContentCompiler().Compile(
            [new MemoryPackSource(invalidFiles, [])], GameVersion);
        False(invalid.Succeeded, "A scenario with no roster capacity was published.");
        Equal("CONTENT_VALUE_OUT_OF_RANGE", invalid.Diagnostics[0].Code, "Roster capacity used the wrong diagnostic.");

        (GameContentSnapshot snapshot, RosterSnapshot candidates) = BaseRoster();
        ScenarioId scenarioId = new("scenario.first-voyage");
        True(snapshot.TryGetScenario(scenarioId, out ScenarioDefinition? scenario), "The recruitment scenario was missing.");
        Equal(8, scenario!.MaximumRosterSize, "The first-voyage roster limit did not come from scenario content.");

        CharacterState protagonist = candidates.Characters.Single(value => value.RaceId == new RaceId("race.human"));
        RecruitmentResult created = CrewRecruitmentSystem.Create(protagonist, snapshot);
        True(created.Succeeded, created.Failure.ToString());
        CrewRoster active = created.Roster!;
        CharacterState[] npcs = [.. candidates.Characters.Where(value => value.Id != protagonist.Id)];
        foreach (CharacterState npc in npcs.Take(scenario.MaximumRosterSize - 1))
        {
            RecruitmentResult recruited = CrewRecruitmentSystem.Recruit(active, npc, snapshot);
            True(recruited.Succeeded, recruited.Failure.ToString());
            active = recruited.Roster!;
        }

        Equal(scenario.MaximumRosterSize, active.Members.Length, "Recruitment did not fill the authored roster capacity.");
        RecruitmentResult full = CrewRecruitmentSystem.Recruit(active, npcs[scenario.MaximumRosterSize - 1], snapshot);
        False(full.Succeeded, "Recruitment exceeded the authored roster capacity.");
        Equal(RecruitmentFailure.RosterFull, full.Failure, "A full roster returned the wrong failure.");
    }

    private static void TrainingGrantsAccessOnlyAtCompletion()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        CharacterState human = roster.Characters.Single(value => value.RaceId == new RaceId("race.human"));
        AccessId magic = new("access.magic");
        False(human.Capabilities.Access.Contains(magic), "The human began with unexplained magical access.");
        TrainingProjectId project = new("training.magic.spellcasting");
        True(snapshot.TryGetTrainingProject(project, out TrainingProjectDefinition? found), "Training definition was missing.");
        TrainingProjectDefinition definition = found!;
        TrainingContext context = TrainingContextFor(definition);
        TrainingCommandResult started = CharacterTrainingSystem.Start(human, project, context, snapshot);
        True(started.Accepted, started.RejectionCode);
        TrainingCommandResult partial = CharacterTrainingSystem.Contribute(started.State, project, 40, snapshot);
        True(partial.Accepted, partial.RejectionCode);
        False(partial.State.Capabilities.Access.Contains(magic), "Partial training granted partial access.");
        TrainingCommandResult ready = CharacterTrainingSystem.Contribute(partial.State, project, 60, snapshot);
        False(ready.State.Capabilities.Access.Contains(magic), "Ready training granted access before completion.");
        int supplies = ready.State.Resources[definition.ResourceId];
        TrainingCommandResult completed = CharacterTrainingSystem.Complete(ready.State, project, snapshot);
        True(completed.Accepted, completed.RejectionCode);
        True(completed.State.Capabilities.Access.Contains(magic), "Completed training did not atomically grant access.");
        True(completed.Completion!.GrantedFeatIds.Contains(new FeatId("feat.access.magic")), "Training event omitted the Feat grant.");
        Equal(supplies - definition.ResourceCost, completed.State.Resources[definition.ResourceId], "Training cost was not committed atomically.");

        TrainingCommandResult restarted = CharacterTrainingSystem.Start(completed.State, project, context, snapshot);
        TrainingCommandResult cancelled = CharacterTrainingSystem.Cancel(restarted.State, project, snapshot);
        True(cancelled.Accepted, cancelled.RejectionCode);
        False(cancelled.State.TrainingProgress.ContainsKey(project), "Cancelled training retained project state.");
        Equal(completed.State.Resources[definition.ResourceId], cancelled.State.Resources[definition.ResourceId], "Cancellation consumed resources.");
    }

    private static void AccessSourcesCoexistAndRecompute()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        CharacterState elf = roster.Characters.Single(value => value.RaceId == new RaceId("race.elf"));
        AccessId magic = new("access.magic");
        True(elf.Capabilities.Access.Contains(magic), "Aether Sense did not grant innate magic access.");
        True(elf.Capabilities.GrantSources.Any(value => value.CapabilityId == magic.Value && value.SourceKind == GrantSourceKind.Feat),
            "Innate magic access lost its provenance.");

        CharacterState trained = CompleteTraining(elf, new TrainingProjectId("training.magic.spellcasting"), snapshot);
        Equal(2, trained.Capabilities.GrantSources.Count(value => value.CapabilityId == magic.Value),
            "Innate and trained access sources did not coexist.");
        CharacterCapabilities withoutInnate = trained.Capabilities.WithoutGrantSource(new FeatId("feat.race.elf.aether-sense").Value);
        True(withoutInnate.Access.Contains(magic), "Removing the innate source removed a surviving trained source.");
        CharacterCapabilities withoutEither = withoutInnate.WithoutGrantSource(new TrainingProjectId("training.magic.spellcasting").Value);
        False(withoutEither.Access.Contains(magic), "Effective access did not recompute after all sources were removed.");
    }

    private static void SupernaturalDefinitionsAndExecutionAreBounded()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        Equal(1, snapshot.Feats.Count(value => value.SpellRules is not null), "The first-playable Spell Feat is incomplete.");
        Equal(1, snapshot.Feats.Count(value => value.PsionicRules is not null), "The first-playable psionic Feat is incomplete.");
        FeatId spellId = new("feat.active.spell.spirit.magic-missile");
        CharacterState human = roster.Characters.Single(value => value.RaceId == new RaceId("race.human"));
        CharacterState target = roster.Characters.Single(value => value.RaceId == new RaceId("race.orc"));
        SupernaturalTarget spellTarget = new(target.Id, true, true, true, ImmutableHashSet.Create("character"));

        SpellActionResult noAccess = SpellActionSystem.Declare(human, spellId, spellTarget, 42, 3, 7, snapshot);
        Equal(ActionRejectionCodes.AccessRequired, noAccess.RejectionCode, "A Spell bypassed access.");
        CharacterState withAccess = CompleteTraining(human, new TrainingProjectId("training.magic.spellcasting"), snapshot);
        SpellActionResult unknown = SpellActionSystem.Declare(withAccess, spellId, spellTarget, 42, 3, 7, snapshot);
        Equal(ActionRejectionCodes.FeatUnknown, unknown.RejectionCode, "A Spell bypassed active-Feat knowledge.");
        CharacterState caster = CompleteTraining(withAccess, new TrainingProjectId("training.magic.magic-missile"), snapshot);
        True(caster.Capabilities.Feats.Contains(spellId), "Completed study did not add the bounded active Spell Feat.");

        CharacterState mismatched = caster with { ContentFingerprint = new ContentFingerprint(new string('0', 64)) };
        SpellActionResult wrongFingerprint = SpellActionSystem.Declare(mismatched, spellId, spellTarget, 42, 3, 7, snapshot);
        Equal(ActionRejectionCodes.ContentMismatch, wrongFingerprint.RejectionCode,
            "Known Spell state crossed its active content fingerprint.");

        CharacterState lowMana = caster with
        {
            CharacterResources = caster.CharacterResources.WithCurrentValue(CharacterResourceIds.Mana, 1),
        };
        SpellActionResult lowDeclared = SpellActionSystem.Declare(lowMana, spellId, spellTarget, 42, 3, 7, snapshot);
        SpellActionResult lowPreviewed = SpellActionSystem.Preview(lowDeclared.Action!);
        SpellActionResult insufficient = SpellActionSystem.Reserve(lowPreviewed.Action!);
        Equal(ActionRejectionCodes.ResourceInsufficient, insufficient.RejectionCode, "A Spell bypassed its Mana cost.");
        True(ReferenceEquals(lowMana, insufficient.Actor), "Failed Spell reservation changed the actor state.");

        SpellActionResult declared = SpellActionSystem.Declare(caster, spellId, spellTarget, 42, 3, 7, snapshot);
        True(declared.Accepted, declared.RejectionCode);
        SpellActionResult previewed = SpellActionSystem.Preview(declared.Action!);
        SpellActionResult reserved = SpellActionSystem.Reserve(previewed.Action!);
        Equal(caster.CharacterResources.GetCurrentValue(CharacterResourceIds.Mana),
            reserved.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Mana),
            "Spell reservation mutated published Mana.");
        SpellActionResult prepared = SpellActionSystem.Prepare(reserved.Action!);
        SpellActionResult resolved = SpellActionSystem.Resolve(prepared.Action!, snapshot);
        SpellActionResult replay = SpellActionSystem.Resolve(prepared.Action!, snapshot);
        Equal(resolved.Action!.Roll, replay.Action!.Roll, "Spell replay changed its owned random result.");
        SpellActionResult committed = SpellActionSystem.Commit(resolved.Action);
        True(committed.Accepted, committed.RejectionCode);
        Equal(caster.CharacterResources.GetCurrentValue(CharacterResourceIds.Mana) - 2,
            committed.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Mana),
            "Spell commit charged the wrong Mana cost.");
        True(committed.Actor.Evidence.Any(value => value.SourceId == spellId.Value), "Spell commit omitted observable evidence.");
        True(committed.Effects.Any(value => value.EffectId == new EffectId("effect.spirit.magic-missile-impact") &&
            value.SourceId == caster.Id.Value), "Spell commit omitted its actor-sourced Effect request.");
        SpellActionResult recovered = SpellActionSystem.Recover(committed.Actor, committed.Action!);
        Equal(SpellActionPhase.Recovered, recovered.Action!.Phase, "Spell recovery did not close the phase sequence.");
        SpellActionResult interruption = SpellActionSystem.Interrupt(reserved.Action!);
        False(interruption.Accepted, "An instant Spell was interruptible as a channeled action.");
        True(ReferenceEquals(caster, interruption.Actor), "Rejected interruption changed the actor state.");
    }

    private static void MindlinkRequiresKnowledgeConsentAndStrain()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        FeatId mindlinkId = new("feat.active.psionics.contact.mindlink");
        CharacterState somnari = roster.Characters.Single(value => value.RaceId == new RaceId("race.somnari"));
        CharacterState human = roster.Characters.Single(value => value.RaceId == new RaceId("race.human"));
        True(somnari.Capabilities.Access.Contains(new AccessId("access.psionics")), "Mindwake omitted innate psionic access.");
        True(somnari.Capabilities.Feats.Contains(mindlinkId), "Mindwake omitted its innate active Mindlink Feat.");
        MindlinkResult noAccess = MindlinkSystem.Invite(human, somnari, mindlinkId, true, 10, snapshot);
        Equal(ActionRejectionCodes.AccessRequired, noAccess.RejectionCode, "Mindlink bypassed psionic access.");

        MindlinkResult invited = MindlinkSystem.Invite(somnari, human, mindlinkId, true, 11, snapshot);
        True(invited.Accepted, invited.RejectionCode);
        MindlinkResult declined = MindlinkSystem.Respond(invited.Link!, human.Id, false);
        Equal(MindlinkPhase.Rejected, declined.Link!.Phase, "Mindlink rejection was not explicit.");
        Equal(0, declined.Actor.Evidence.Length, "Rejected Mindlink leaked protected evidence.");
        MindlinkResult accepted = MindlinkSystem.Respond(invited.Link!, human.Id, true);
        MindlinkResult reserved = MindlinkSystem.Reserve(accepted.Link!);
        Equal(0, reserved.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain), "Mindlink reservation mutated published Strain.");
        MindlinkResult active = MindlinkSystem.Commit(reserved.Link!);
        MindlinkResult replay = MindlinkSystem.Commit(reserved.Link!);
        Equal(active.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain), replay.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain),
            "Mindlink replay changed deterministic strain publication.");
        Equal(4, active.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain), "Mindlink charged the wrong initial Strain.");
        True(active.Actor.Evidence.Any(value => value.SourceId == mindlinkId.Value), "Mindlink commit omitted observable evidence.");
        True(active.Effects.Any(value => value.EffectId == new EffectId("effect.psionics.shared-channel")),
            "Mindlink omitted its shared-channel Effect request.");
        MindlinkResult sustained = MindlinkSystem.Sustain(active.Actor, active.Link!, 12);
        Equal(5, sustained.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain), "Mindlink sustain charged the wrong Strain.");
        MindlinkResult revoked = MindlinkSystem.Revoke(sustained.Actor, sustained.Link!, human.Id);
        True(revoked.Effects.Any(value => value.EffectId == new EffectId("effect.psionics.remove-shared-channel")),
            "Revoked Mindlink omitted its shared-channel removal request.");

        CharacterState awakened = CompleteTraining(human, new TrainingProjectId("training.psionics.awakening"), snapshot);
        MindlinkResult unknown = MindlinkSystem.Invite(awakened, somnari, mindlinkId, true, 13, snapshot);
        Equal(ActionRejectionCodes.FeatUnknown, unknown.RejectionCode, "Mindlink bypassed active-Feat knowledge.");
        CharacterState trained = CompleteTraining(awakened, new TrainingProjectId("training.psionics.mindlink"), snapshot);
        True(trained.Capabilities.Feats.Contains(mindlinkId), "Trained Mindlink Feat was not published.");
        True(trained.Capabilities.GrantSources.Any(value => value.CapabilityId == mindlinkId.Value && value.SourceKind == GrantSourceKind.TrainingProject),
            "Trained Mindlink knowledge lost its provenance.");
        True(snapshot.TryGetFeat(mindlinkId, out FeatDefinition? definition) && definition!.PsionicRules is not null &&
            snapshot.TryGetSkill(definition.PsionicRules.ResistanceSkillId, out _, out _),
            "Mindlink's reviewed resistance reference was not linked to the active fingerprint.");
        True(MindlinkSystem.Invite(trained, somnari, mindlinkId, true, 14, snapshot).Accepted,
            "A trained Mindlink user could not declare the same action as an innate user.");
        MindlinkResult released = MindlinkSystem.Release(replay.Actor, replay.Link!);
        Equal(MindlinkPhase.Released, released.Link!.Phase, "Mindlink release did not close the active channel.");
    }

    private static void RaceCapabilitiesRespectTheirBoundaries()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        CharacterState eidolon = roster.Characters.Single(value => value.RaceId == new RaceId("race.eidolon"));
        CharacterState withoutAnchor = RemoveItem(eidolon, new ContentId("equipment.soul-anchor.portable"));
        ActionDefinition soulRecovery = RaceCapabilities.CreateSoulAnchorRecoveryAction(withoutAnchor, snapshot)!;
        ActionRequest request = new(
            withoutAnchor.Id,
            soulRecovery.Id,
            new ActionTarget(new ContentId("target.self"), true, true),
            ImmutableHashSet.Create(new ContentId("context.recovery.safe-anchor")),
            9,
            0);
        ActionEligibilityResult noAnchor = CharacterActionSystem.CheckEligibility(withoutAnchor, soulRecovery, request, snapshot);
        Equal(ActionRejectionCodes.EquipmentRequired, noAnchor.RejectionCode, "Soul Anchor recovery bypassed the anchor requirement.");

        CharacterState tharun = roster.Characters.Single(value => value.RaceId == new RaceId("race.tharun"));
        ObservedRouteEvidence evidence = new(new ContentId("route.red-wake"), new ContentId("evidence.engine-trace"), 72);
        ImmutableArray<TrailInterpretation> interpretations = RaceCapabilities.InterpretObservedTrails(tharun, snapshot, [evidence]);
        Equal(1, interpretations.Length, "Trail Sense did not interpret observed evidence.");
        Equal(evidence.RouteId, interpretations[0].RouteId, "Trail Sense produced an unobserved route.");
        True(interpretations[0].EvidenceIds.All(id => id == evidence.EvidenceId), "Trail Sense exposed evidence it was not given.");
    }

    private static (GameContentSnapshot Snapshot, RosterSnapshot Roster) BaseRoster()
    {
        ContentCompilationResult compiled = CompileDirectory(Path.Combine(Milestone2Root, "base"));
        True(compiled.Succeeded, Primary(compiled));
        GameContentSnapshot snapshot = compiled.Snapshot!;
        RosterCreationResult roster = CharacterCreator.CreateRoster(
            snapshot.Fingerprint,
            new ScenarioId("scenario.first-voyage"),
            0x5eedUL,
            snapshot,
            FullSupport(snapshot));
        True(roster.Succeeded, roster.Failure.ToString());
        return (snapshot, roster.Roster!);
    }

    private static CharacterState CompleteTraining(
        CharacterState character,
        TrainingProjectId projectId,
        GameContentSnapshot snapshot)
    {
        True(snapshot.TryGetTrainingProject(projectId, out TrainingProjectDefinition? found), "Training definition was missing.");
        TrainingProjectDefinition definition = found!;
        TrainingCommandResult started = CharacterTrainingSystem.Start(character, projectId, TrainingContextFor(definition), snapshot);
        True(started.Accepted, started.RejectionCode);
        TrainingCommandResult contributed = CharacterTrainingSystem.Contribute(started.State, projectId, definition.WorkUnits, snapshot);
        True(contributed.Accepted, contributed.RejectionCode);
        TrainingCommandResult completed = CharacterTrainingSystem.Complete(contributed.State, projectId, snapshot);
        True(completed.Accepted, completed.RejectionCode);
        return completed.State;
    }

    private static TrainingContext TrainingContextFor(TrainingProjectDefinition definition) => new(
        ImmutableHashSet.Create(definition.FacilityId),
        ImmutableHashSet.Create(definition.SafetyId));

    private static CrewSupportProfile FullSupport(GameContentSnapshot snapshot) => new(
        snapshot.Races.SelectMany(value => value.RequiredSupportIds).ToImmutableHashSet(),
        snapshot.Characters.SelectMany(value => value.StartingItemDefinitionIds).ToImmutableHashSet());

    private static (CharacterState Character, ItemInstanceId ItemId) AddEquippedWeapon(
        CharacterState character,
        WeaponDefinition definition,
        MeleeWeaponState? meleeState,
        RangedWeaponState? rangedState,
        ICharacterContentCatalog catalog)
    {
        InventoryContainer container = character.Items.InventoryContainers.Single();
        ItemInstanceId instanceId = new(definition is MeleeWeaponDefinition
            ? Guid.Parse("11111111-1111-1111-1111-111111111111")
            : Guid.Parse("22222222-2222-2222-2222-222222222222"));
        ItemInstance item = new(
            instanceId,
            definition.Id,
            container.ContainerId,
            meleeState?.CurrentDurability ?? rangedState?.CurrentDurability,
            null,
            null,
            1,
            meleeState,
            rangedState);
        ItemSystemState candidate = character.Items with
        {
            ItemInstances = character.Items.ItemInstances.Add(item),
            InventoryContainers = character.Items.InventoryContainers.Select(value => value.ContainerId == container.ContainerId
                ? value with { ItemInstanceIds = value.ItemInstanceIds.Add(instanceId) }
                : value).ToImmutableArray(),
        };
        ItemSystemResult created = ItemSystem.Create(candidate, catalog);
        True(created.Accepted, created.RejectionCode);
        ItemSystemResult equipped = ItemSystem.Equip(created.State, character.Id.Value, container.ContainerId, instanceId, catalog);
        True(equipped.Accepted, equipped.RejectionCode);
        return (character with { Items = equipped.State }, instanceId);
    }

    private static CharacterState RemoveItem(CharacterState character, ContentId definitionId)
    {
        ItemInstance item = character.Items.ItemInstances.Single(value => value.DefinitionId == definitionId);
        return character with
        {
            Items = character.Items with
            {
                ItemInstances = character.Items.ItemInstances.Remove(item),
                InventoryContainers = character.Items.InventoryContainers.Select(value => value with
                {
                    ItemInstanceIds = value.ItemInstanceIds.Remove(item.InstanceId),
                }).ToImmutableArray(),
                EquipmentLoadouts = character.Items.EquipmentLoadouts.Select(value => value with
                {
                    SlotAssignments = value.SlotAssignments
                        .Where(assignment => assignment.ItemInstanceId != item.InstanceId).ToImmutableArray(),
                }).ToImmutableArray(),
            },
        };
    }

    private static string Describe(RosterSnapshot roster, GameContentSnapshot snapshot) => string.Join(
        ';',
        roster.Characters.Select(character =>
            character.Id + ":" + string.Join(',', character.Capabilities.Snapshot(snapshot).Abilities.Select(value => value.Value))));

    private static Dictionary<string, byte[]> Clone(Dictionary<string, byte[]> source) =>
        source.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);

    private static void ReplaceText(Dictionary<string, byte[]> files, string path, string oldValue, string newValue)
    {
        string text = Encoding.UTF8.GetString(files[path]);
        files[path] = Encoding.UTF8.GetBytes(text.Replace(oldValue, newValue, StringComparison.Ordinal));
    }

    private static void ApplyMutations(JsonElement testCase, Dictionary<string, byte[]> files, List<string> duplicateEntries)
    {
        if (testCase.TryGetProperty("removeFiles", out JsonElement removals))
        {
            foreach (JsonElement removal in removals.EnumerateArray())
            {
                files.Remove(removal.GetString()!);
            }
        }

        if (testCase.TryGetProperty("writeText", out JsonElement writes))
        {
            foreach (JsonElement write in writes.EnumerateArray())
            {
                files[write.GetProperty("path").GetString()!] = Encoding.UTF8.GetBytes(write.GetProperty("text").GetString()!);
            }
        }

        if (testCase.TryGetProperty("writeHex", out JsonElement hexWrites))
        {
            foreach (JsonElement write in hexWrites.EnumerateArray())
            {
                files[write.GetProperty("path").GetString()!] = Convert.FromHexString(write.GetProperty("bytes").GetString()!);
            }
        }

        if (testCase.TryGetProperty("duplicateFiles", out JsonElement duplicates))
        {
            duplicateEntries.AddRange(duplicates.EnumerateArray().Select(item => item.GetString()!));
        }
    }

    private static Dictionary<string, byte[]> ReadFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);

    private static SemanticVersion ParseVersion(string value)
    {
        True(SemanticVersion.TryParse(value, out SemanticVersion version), "Fixture game version is invalid.");
        return version;
    }

    private static ContentCompilationResult CompileDirectory(string root) =>
        new GameContentCompiler().Compile([new DirectoryContentPackSource(root)], GameVersion);

    private static ContentLimits ApplyLimitOverrides(JsonElement testCase, ContentLimits defaults)
    {
        if (!testCase.TryGetProperty("limitOverrides", out JsonElement overrides))
        {
            return defaults;
        }

        ContentLimits result = defaults;
        if (overrides.TryGetProperty("manifestBytes", out JsonElement manifestBytes))
        {
            result = result with { ManifestBytes = manifestBytes.GetInt32() };
        }

        if (overrides.TryGetProperty("tagsPerDefinition", out JsonElement tags))
        {
            result = result with { TagsPerDefinition = tags.GetInt32() };
        }

        if (overrides.TryGetProperty("referencesPerDefinition", out JsonElement references))
        {
            result = result with { ReferencesPerDefinition = references.GetInt32() };
        }

        return result;
    }

    private static string Primary(ContentCompilationResult result) =>
        result.Diagnostics.FirstOrDefault()?.Code ?? result.IoFailure?.Kind.ToString() ?? "Compilation failed without a diagnostic.";

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
        }
    }

    private static void Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class MemoryPackSource(Dictionary<string, byte[]> files, IReadOnlyList<string> duplicates) : IContentPackSource
    {
        public IReadOnlyList<string> EnumerateFiles() => [.. files.Keys, .. duplicates];

        public byte[] ReadFile(string relativePath, int maximumBytes)
        {
            byte[] bytes = files[relativePath];
            if (bytes.Length > maximumBytes)
            {
                throw new ContentSourceLimitException(relativePath);
            }

            return bytes.ToArray();
        }
    }
}
