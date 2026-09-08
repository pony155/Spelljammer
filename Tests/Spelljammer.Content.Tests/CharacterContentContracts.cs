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
using Spelljammer.Simulation.Effects;
using Spelljammer.Simulation.World;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;

internal static partial class ContentContracts
{
    private static void WorldTimeIsDataDriven()
    {
        Dictionary<string, byte[]> files = ReadFiles(Path.Combine(Milestone2Root, "base"));
        ContentCompilationResult result = new GameContentCompiler().Compile(
            [new MemoryPackSource(files, [])], GameVersion);
        True(result.Succeeded, Primary(result));
        GameContentSnapshot snapshot = result.Snapshot!;
        Equal(1, snapshot.WorldTimeRegistry.Count, "The world-time definition was not published.");
        True(snapshot.TryGetWorldTime(new WorldTimeId("world-time.standard"), out WorldTimeDefinition? time),
            "Typed world-time lookup failed.");
        Equal(20, time!.TicksPerSecond, "The fixed-tick cadence did not come from JSON.");
        Equal(8, time.MaximumCatchUpTicks, "The catch-up limit did not come from JSON.");
        Equal(1, snapshot.CalendarRegistry.Count, "The campaign calendar was not published.");
        True(snapshot.TryGetCalendar(new CalendarId("calendar.voidfarer-standard"), out CalendarDefinition? calendar),
            "Typed calendar lookup failed.");
        Equal(7421L, calendar!.StartingYear, "The starting campaign year did not come from JSON.");
        Equal(1, calendar.StartingMonth, "The starting campaign month did not come from JSON.");
        Equal(1, calendar.StartingDay, "The starting campaign day did not come from JSON.");
        Equal(0, calendar.StartingDayOfWeekIndex, "The starting weekday did not come from JSON.");
        Equal(0, calendar.StartingHour, "The starting EAT hour did not come from JSON.");
        Equal(0, calendar.StartingMinute, "The starting EAT minute did not come from JSON.");
        Equal(0, calendar.StartingSecond, "The starting EAT second did not come from JSON.");
        Equal(12, calendar.Months.Length, "The authored calendar months were not preserved.");
        Equal(new CalendarMonthId("calendar.month.first-light"), calendar.Months[0].CalendarMonthId,
            "Calendar month order changed during compilation.");
        Equal(1, snapshot.TimeScaleRegistry.Count, "The tactical time scale was not published.");
        True(snapshot.TryGetTimeScale(new TimeScaleId("time-scale.tactical"), out TimeScaleDefinition? timeScale),
            "Typed time-scale lookup failed.");
        Equal(1, timeScale!.WorldSecondsNumerator, "The world-time numerator did not come from JSON.");
        Equal(20, timeScale.SimulationTicksDenominator, "The simulation-tick denominator did not come from JSON.");

        Dictionary<string, byte[]> invalid = Clone(files);
        ReplaceText(invalid, "Definitions/WorldTimes/standard.json",
            "\"ticksPerSecond\": 20",
            "\"ticksPerSecond\": 0");
        ContentCompilationResult rejected = new GameContentCompiler().Compile(
            [new MemoryPackSource(invalid, [])], GameVersion);
        Equal(ContentDiagnosticCodes.ValueOutOfRange, rejected.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
            "An invalid fixed-tick cadence was accepted.");

        Dictionary<string, byte[]> invalidCalendar = Clone(files);
        ReplaceText(invalidCalendar, "Definitions/Calendars/voidfarer-standard.json",
            "\"days\": 30",
            "\"days\": 0");
        ContentCompilationResult rejectedCalendar = new GameContentCompiler().Compile(
            [new MemoryPackSource(invalidCalendar, [])], GameVersion);
        Equal(ContentDiagnosticCodes.ValueOutOfRange,
            rejectedCalendar.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
            "A calendar with zero-day months was accepted.");

        Dictionary<string, byte[]> invalidTimeScale = Clone(files);
        ReplaceText(invalidTimeScale, "Definitions/TimeScales/tactical.json",
            "\"simulationTicksDenominator\": 20",
            "\"simulationTicksDenominator\": 0");
        ContentCompilationResult rejectedTimeScale = new GameContentCompiler().Compile(
            [new MemoryPackSource(invalidTimeScale, [])], GameVersion);
        Equal(ContentDiagnosticCodes.ValueOutOfRange,
            rejectedTimeScale.Diagnostics.FirstOrDefault()?.Code ?? "<none>",
            "A zero-denominator time scale was accepted.");
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
        MindlinkResult noAccess = PsionicActionSystem.Invite(human, somnari, mindlinkId, true, 10, snapshot);
        Equal(ActionRejectionCodes.AccessRequired, noAccess.RejectionCode, "Mindlink bypassed psionic access.");

        MindlinkResult invited = PsionicActionSystem.Invite(somnari, human, mindlinkId, true, 11, snapshot);
        True(invited.Accepted, invited.RejectionCode);
        MindlinkResult declined = PsionicActionSystem.Respond(invited.Link!, human.Id, false);
        Equal(MindlinkPhase.Rejected, declined.Link!.Phase, "Mindlink rejection was not explicit.");
        Equal(0, declined.Actor.Evidence.Length, "Rejected Mindlink leaked protected evidence.");
        MindlinkResult accepted = PsionicActionSystem.Respond(invited.Link!, human.Id, true);
        MindlinkResult reserved = PsionicActionSystem.Reserve(accepted.Link!);
        Equal(0, reserved.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain), "Mindlink reservation mutated published Strain.");
        MindlinkResult active = PsionicActionSystem.Commit(reserved.Link!);
        MindlinkResult replay = PsionicActionSystem.Commit(reserved.Link!);
        Equal(active.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain), replay.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain),
            "Mindlink replay changed deterministic strain publication.");
        Equal(4, active.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain), "Mindlink charged the wrong initial Strain.");
        True(active.Actor.Evidence.Any(value => value.SourceId == mindlinkId.Value), "Mindlink commit omitted observable evidence.");
        True(active.Effects.Any(value => value.EffectId == new EffectId("effect.psionics.shared-channel")),
            "Mindlink omitted its shared-channel Effect request.");
        MindlinkResult sustained = PsionicActionSystem.Sustain(active.Actor, active.Link!, 12);
        Equal(5, sustained.Actor.CharacterResources.GetCurrentValue(CharacterResourceIds.Strain), "Mindlink sustain charged the wrong Strain.");
        MindlinkResult revoked = PsionicActionSystem.Revoke(sustained.Actor, sustained.Link!, human.Id);
        True(revoked.Effects.Any(value => value.EffectId == new EffectId("effect.psionics.remove-shared-channel")),
            "Revoked Mindlink omitted its shared-channel removal request.");

        CharacterState awakened = CompleteTraining(human, new TrainingProjectId("training.psionics.awakening"), snapshot);
        MindlinkResult unknown = PsionicActionSystem.Invite(awakened, somnari, mindlinkId, true, 13, snapshot);
        Equal(ActionRejectionCodes.FeatUnknown, unknown.RejectionCode, "Mindlink bypassed active-Feat knowledge.");
        CharacterState trained = CompleteTraining(awakened, new TrainingProjectId("training.psionics.mindlink"), snapshot);
        True(trained.Capabilities.Feats.Contains(mindlinkId), "Trained Mindlink Feat was not published.");
        True(trained.Capabilities.GrantSources.Any(value => value.CapabilityId == mindlinkId.Value && value.SourceKind == GrantSourceKind.TrainingProject),
            "Trained Mindlink knowledge lost its provenance.");
        True(snapshot.TryGetFeat(mindlinkId, out FeatDefinition? definition) && definition!.PsionicRules is not null &&
            snapshot.TryGetSkill(definition.PsionicRules.ResistanceSkillId, out _, out _),
            "Mindlink's reviewed resistance reference was not linked to the active fingerprint.");
        True(PsionicActionSystem.Invite(trained, somnari, mindlinkId, true, 14, snapshot).Accepted,
            "A trained Mindlink user could not declare the same action as an innate user.");
        MindlinkResult released = PsionicActionSystem.Release(replay.Actor, replay.Link!);
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
        IGameContentCatalog catalog)
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
}
