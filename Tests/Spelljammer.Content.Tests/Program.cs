using System.Text;
using System.Text.Json;
using System.Collections.Immutable;
using Spelljammer.Content;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

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
        BaseAttributesAndSkillsAreTypedAndIndexed();
        Milestone2InvalidCasesAreRecognized();
        AdditiveSkillIsDynamicAndReversible();
        CharacterDefinitionsRejectInvalidGraphs();
        BaseRosterIsDeterministicAndDynamic();
        EligibilityAndResolutionAreAtomic();
        TrainingGrantsAccessOnlyAtCompletion();
        AccessSourcesCoexistAndRecompute();
        SupernaturalDefinitionsAndExecutionAreBounded();
        MindlinkRequiresKnowledgeConsentAndStrain();
        RaceCapabilitiesRespectTheirBoundaries();
        Milestone5EncounterAndShipContentIsLinked();
        Console.WriteLine("Content and character capability contracts passed.");
        return 0;
    }

    private static void StableIdsAreValidatedAndOrdinal()
    {
        True(ContentId.TryParse("attribute.strength", out ContentId valid), "A valid ID was rejected.");
        False(ContentId.TryParse("Attribute.Strength", out _), "A culture-sensitive ID was accepted.");
        False(default(ContentId).IsValid, "The default ID became valid.");
        string maximum = "domain." + new string('a', 120);
        Equal(ContentId.MaximumLength, maximum.Length, "Maximum-length fixture is wrong.");
        True(ContentId.TryParse(maximum, out _), "A maximum-length ID was rejected.");
        False(ContentId.TryParse(maximum + "a", out _), "An oversized ID was accepted.");
        True(valid.CompareTo(new ContentId("attribute.toughness")) < 0, "ID comparison was not ordinal.");
    }

    private static void Milestone5EncounterAndShipContentIsLinked()
    {
        ContentCompilationResult compiled = CompileDirectory(Path.Combine(Milestone2Root, "base"));
        True(compiled.Succeeded, Primary(compiled));
        GameContentSnapshot snapshot = compiled.Snapshot!;
        Equal(5, snapshot.EquipmentRegistry.Count, "The first encounter equipment set is incomplete.");
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

    private static void BaseAttributesAndSkillsAreTypedAndIndexed()
    {
        ContentCompilationResult result = CompileDirectory(Path.Combine(Milestone2Root, "base"));
        True(result.Succeeded, Primary(result));
        GameContentSnapshot snapshot = result.Snapshot!;
        using JsonDocument expectedFingerprints = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(Milestone2Root, "expected", "fingerprints.json")));
        Equal(
            expectedFingerprints.RootElement.GetProperty("baseSha256").GetString()!,
            snapshot.Fingerprint.ToString(),
            "The base Attribute and Skill fingerprint changed.");
        Equal(7, snapshot.AttributeRegistry.Count, "The base Attribute roster is incomplete.");
        Equal(29, snapshot.SkillRegistry.Count, "The base Skill roster is incomplete.");
        string[] expectedAttributes =
        [
            "attribute.agility", "attribute.charisma", "attribute.intelligence", "attribute.luck",
            "attribute.strength", "attribute.toughness", "attribute.willpower",
        ];
        string[] expectedSkills =
        [
            "skill.acrobatics", "skill.alchemy", "skill.ancient-lore", "skill.archery", "skill.astrogation",
            "skill.athletics", "skill.command", "skill.cooking", "skill.crafting", "skill.deception",
            "skill.defense", "skill.enchantment", "skill.engineering", "skill.eva", "skill.gunnery",
            "skill.insight", "skill.language-literacy", "skill.magic", "skill.medicine", "skill.melee",
            "skill.merchant", "skill.negotiation", "skill.piloting", "skill.psionics", "skill.rigging",
            "skill.salvage", "skill.sensors", "skill.stealth", "skill.xenology",
        ];
        Equal(string.Join('|', expectedAttributes), string.Join('|', snapshot.Attributes.Select(value => value.Id)),
            "Attribute iteration is incomplete or nondeterministic.");
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
        Equal(30, withMod.Snapshot.SkillRegistry.Count, "The additive Skill did not enter the generic registry.");
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
        Equal(30, dynamicRoster.Roster!.Characters[0].Capabilities.Snapshot(withMod.Snapshot).Skills.Length,
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
        ReplaceText(missingGrant, "Definitions/Races/human.json", "perk.race.human.versatility", "perk.race.human.missing");
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
        string path = "Definitions/Perks/race-human.json";
        string text = Encoding.UTF8.GetString(cycle[path]);
        cycle[path] = Encoding.UTF8.GetBytes(text.Replace("}\n", ",\"grantedPerkIds\":[\"perk.race.human.versatility\"]}\n", StringComparison.Ordinal));
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
        Equal(snapshot.Attributes.Length, first.Roster.AttributeColumns.Length, "Roster Attribute columns are not registry-driven.");
        Equal(snapshot.Skills.Length, first.Roster.SkillColumns.Length, "Roster Skill columns are not registry-driven.");
        CharacterRosterDisplay display = RosterInspection.Project(
            first.Roster,
            snapshot,
            key => "[" + key + "]",
            ActionRejectionCodes.EquipmentRequired);
        Equal(snapshot.Attributes.Length, display.Characters[0].Attributes.Length, "Roster display fixed its Attribute columns.");
        Equal("[command.equipment-required]", display.DisabledReason!, "Disabled reason did not pass through localization.");

        RosterCreationResult unsupported = CharacterCreator.CreateRoster(
            snapshot.Fingerprint,
            scenario,
            0x5eedUL,
            snapshot,
            new CrewSupportProfile(ImmutableHashSet<ContentId>.Empty, support.AvailableEquipmentIds));
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
            new ContentId("practice.soul-anchor.first"),
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
        True(first.Resolution.AttributeId.IsValid && first.Resolution.SkillId.IsValid, "Resolution explanation omitted contributors.");
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
        True(elf.Capabilities.GrantSources.Any(value => value.CapabilityId == magic.Value && value.SourceKind == GrantSourceKind.Perk),
            "Innate magic access lost its provenance.");

        CharacterState trained = CompleteTraining(elf, new TrainingProjectId("training.magic.spellcasting"), snapshot);
        Equal(2, trained.Capabilities.GrantSources.Count(value => value.CapabilityId == magic.Value),
            "Innate and trained access sources did not coexist.");
        CharacterCapabilities withoutInnate = trained.Capabilities.WithoutGrantSource(new PerkId("perk.race.elf.aether-sense").Value);
        True(withoutInnate.Access.Contains(magic), "Removing the innate source removed a surviving trained source.");
        CharacterCapabilities withoutEither = withoutInnate.WithoutGrantSource(new TrainingProjectId("training.magic.spellcasting").Value);
        False(withoutEither.Access.Contains(magic), "Effective access did not recompute after all sources were removed.");
    }

    private static void SupernaturalDefinitionsAndExecutionAreBounded()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        Equal(1, snapshot.SpellRegistry.Count, "The first-playable Spell registry is incomplete.");
        Equal(1, snapshot.PsychicTechniqueRegistry.Count, "The first-playable psychic registry is incomplete.");
        SpellId spellId = new("spell.spirit.magic-missile");
        CharacterState human = roster.Characters.Single(value => value.RaceId == new RaceId("race.human"));
        CharacterState target = roster.Characters.Single(value => value.RaceId == new RaceId("race.orc"));
        SupernaturalTarget spellTarget = new(target.Id, true, true, true, ImmutableHashSet.Create("character"));

        SpellActionResult noAccess = SpellActionSystem.Declare(human, spellId, spellTarget, 42, 3, 7, snapshot);
        Equal(ActionRejectionCodes.AccessRequired, noAccess.RejectionCode, "A Spell bypassed access.");
        CharacterState withAccess = CompleteTraining(human, new TrainingProjectId("training.magic.spellcasting"), snapshot);
        SpellActionResult unknown = SpellActionSystem.Declare(withAccess, spellId, spellTarget, 42, 3, 7, snapshot);
        Equal(ActionRejectionCodes.TechniqueUnknown, unknown.RejectionCode, "A Spell bypassed knowledge.");
        CharacterState caster = CompleteTraining(withAccess, new TrainingProjectId("training.magic.magic-missile"), snapshot);
        True(caster.Capabilities.KnownSpellIds.Contains(spellId), "Completed study did not add the bounded known Spell ID.");

        CharacterState mismatched = caster with { ContentFingerprint = new ContentFingerprint(new string('0', 64)) };
        SpellActionResult wrongFingerprint = SpellActionSystem.Declare(mismatched, spellId, spellTarget, 42, 3, 7, snapshot);
        Equal(ActionRejectionCodes.ContentMismatch, wrongFingerprint.RejectionCode,
            "Known Spell state crossed its active content fingerprint.");

        CharacterState lowFocus = caster with
        {
            Resources = caster.Resources.SetItem(new ResourceId("resource.focus"), 1),
        };
        SpellActionResult lowDeclared = SpellActionSystem.Declare(lowFocus, spellId, spellTarget, 42, 3, 7, snapshot);
        SpellActionResult lowPreviewed = SpellActionSystem.Preview(lowDeclared.Action!);
        SpellActionResult insufficient = SpellActionSystem.Reserve(lowPreviewed.Action!);
        Equal(ActionRejectionCodes.ResourceInsufficient, insufficient.RejectionCode, "A Spell bypassed its Focus cost.");
        True(ReferenceEquals(lowFocus, insufficient.Actor), "Failed Spell reservation changed the actor state.");

        SpellActionResult declared = SpellActionSystem.Declare(caster, spellId, spellTarget, 42, 3, 7, snapshot);
        True(declared.Accepted, declared.RejectionCode);
        SpellActionResult previewed = SpellActionSystem.Preview(declared.Action!);
        SpellActionResult reserved = SpellActionSystem.Reserve(previewed.Action!);
        Equal(caster.Resources[new ResourceId("resource.focus")], reserved.Actor.Resources[new ResourceId("resource.focus")],
            "Spell reservation mutated published Focus.");
        SpellActionResult prepared = SpellActionSystem.Prepare(reserved.Action!);
        SpellActionResult resolved = SpellActionSystem.Resolve(prepared.Action!, snapshot);
        SpellActionResult replay = SpellActionSystem.Resolve(prepared.Action!, snapshot);
        Equal(resolved.Action!.Roll, replay.Action!.Roll, "Spell replay changed its owned random result.");
        SpellActionResult committed = SpellActionSystem.Commit(resolved.Action);
        True(committed.Accepted, committed.RejectionCode);
        Equal(caster.Resources[new ResourceId("resource.focus")] - 2, committed.Actor.Resources[new ResourceId("resource.focus")],
            "Spell commit charged the wrong Focus cost.");
        True(committed.Actor.Evidence.Any(value => value.SourceId == spellId.Value), "Spell commit omitted observable evidence.");
        True(committed.Actor.ActiveEffects.Any(value => value.SourceId == spellId.Value), "Spell commit omitted its bounded active effect.");
        SpellActionResult recovered = SpellActionSystem.Recover(committed.Actor, committed.Action!);
        Equal(SpellActionPhase.Recovered, recovered.Action!.Phase, "Spell recovery did not close the phase sequence.");
        SpellActionResult interruption = SpellActionSystem.Interrupt(reserved.Action!);
        False(interruption.Accepted, "An instant Spell was interruptible as a channeled action.");
        True(ReferenceEquals(caster, interruption.Actor), "Rejected interruption changed the actor state.");
    }

    private static void MindlinkRequiresKnowledgeConsentAndStrain()
    {
        (GameContentSnapshot snapshot, RosterSnapshot roster) = BaseRoster();
        PsychicTechniqueId mindlinkId = new("psychic.contact.mindlink");
        CharacterState somnari = roster.Characters.Single(value => value.RaceId == new RaceId("race.somnari"));
        CharacterState human = roster.Characters.Single(value => value.RaceId == new RaceId("race.human"));
        True(somnari.Capabilities.Access.Contains(new AccessId("access.psionics")), "Mindwake omitted innate psionic access.");
        True(somnari.Capabilities.KnownPsychicTechniqueIds.Contains(mindlinkId), "Mindwake omitted innate Mindlink knowledge.");
        MindlinkResult noAccess = MindlinkSystem.Invite(human, somnari, mindlinkId, true, 10, snapshot);
        Equal(ActionRejectionCodes.AccessRequired, noAccess.RejectionCode, "Mindlink bypassed psionic access.");

        MindlinkResult invited = MindlinkSystem.Invite(somnari, human, mindlinkId, true, 11, snapshot);
        True(invited.Accepted, invited.RejectionCode);
        MindlinkResult declined = MindlinkSystem.Respond(invited.Link!, human.Id, false);
        Equal(MindlinkPhase.Rejected, declined.Link!.Phase, "Mindlink rejection was not explicit.");
        Equal(0, declined.Actor.Evidence.Length, "Rejected Mindlink leaked protected evidence.");
        MindlinkResult accepted = MindlinkSystem.Respond(invited.Link!, human.Id, true);
        MindlinkResult reserved = MindlinkSystem.Reserve(accepted.Link!);
        Equal(0, reserved.Actor.Resources[new ResourceId("resource.psychic-strain")], "Mindlink reservation mutated published Strain.");
        MindlinkResult active = MindlinkSystem.Commit(reserved.Link!);
        MindlinkResult replay = MindlinkSystem.Commit(reserved.Link!);
        Equal(active.Actor.Resources[new ResourceId("resource.psychic-strain")], replay.Actor.Resources[new ResourceId("resource.psychic-strain")],
            "Mindlink replay changed deterministic strain publication.");
        Equal(4, active.Actor.Resources[new ResourceId("resource.psychic-strain")], "Mindlink charged the wrong initial Strain.");
        True(active.Actor.Evidence.Any(value => value.SourceId == mindlinkId.Value), "Mindlink commit omitted observable evidence.");
        True(active.Actor.ActiveEffects.Any(value => value.ScopeId == new ContentId("psychic.scope.deliberate-message")),
            "Mindlink exposed a broader information scope.");
        MindlinkResult sustained = MindlinkSystem.Sustain(active.Actor, active.Link!, 12);
        Equal(5, sustained.Actor.Resources[new ResourceId("resource.psychic-strain")], "Mindlink sustain charged the wrong Strain.");
        MindlinkResult revoked = MindlinkSystem.Revoke(sustained.Actor, sustained.Link!, human.Id);
        False(revoked.Actor.ActiveEffects.Any(value => value.SourceId == mindlinkId.Value), "Revoked Mindlink retained its active channel.");

        CharacterState awakened = CompleteTraining(human, new TrainingProjectId("training.psionics.awakening"), snapshot);
        MindlinkResult unknown = MindlinkSystem.Invite(awakened, somnari, mindlinkId, true, 13, snapshot);
        Equal(ActionRejectionCodes.TechniqueUnknown, unknown.RejectionCode, "Mindlink bypassed technique knowledge.");
        CharacterState trained = CompleteTraining(awakened, new TrainingProjectId("training.psionics.mindlink"), snapshot);
        True(trained.Capabilities.KnownPsychicTechniqueIds.Contains(mindlinkId), "Trained Mindlink knowledge was not published.");
        True(trained.Capabilities.GrantSources.Any(value => value.CapabilityId == mindlinkId.Value && value.SourceKind == GrantSourceKind.TrainingProject),
            "Trained Mindlink knowledge lost its provenance.");
        True(snapshot.TryGetPsychicTechnique(mindlinkId, out PsychicTechniqueDefinition? definition) &&
            snapshot.TryGetSkill(definition!.ResistanceSkillId, out _, out _),
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
        CharacterState withoutAnchor = eidolon with { EquipmentIds = eidolon.EquipmentIds.Remove(new ContentId("equipment.soul-anchor.portable")) };
        ActionDefinition soulRecovery = RaceCapabilities.CreateSoulAnchorRecoveryAction(withoutAnchor, snapshot)!;
        ActionRequest request = new(
            withoutAnchor.Id,
            soulRecovery.Id,
            new ActionTarget(new ContentId("target.self"), true, true),
            ImmutableHashSet.Create(new ContentId("context.recovery.safe-anchor")),
            new ContentId("practice.soul-anchor.boundary"),
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
        snapshot.Characters.SelectMany(value => value.EquipmentIds).ToImmutableHashSet());

    private static string Describe(RosterSnapshot roster, GameContentSnapshot snapshot) => string.Join(
        ';',
        roster.Characters.Select(character =>
            character.Id + ":" + string.Join(',', character.Capabilities.Snapshot(snapshot).Attributes.Select(value => value.Value))));

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
