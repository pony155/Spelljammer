using System.Collections.Immutable;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Persistence;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

return PersistenceContracts.Run();

internal static class PersistenceContracts
{
    private static readonly SemanticVersion GameVersion = new(0, 1, 0);
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static int Run()
    {
        ExactCampaignRoundTripsCanonically();
        CorruptionAndMissingContentFailPreflight();
        CompatibilityIsExplicitAndLoadable();
        FailedLoadPreservesActiveCampaign();
        AtomicReplacementPreservesRecovery();
        LocationMigrationIsDeterministicAndNonDestructive();
        MigrationFailuresPreserveTheSource();
        Console.WriteLine("Campaign persistence contracts passed.");
        return 0;
    }

    private static void ExactCampaignRoundTripsCanonically()
    {
        GameContentSnapshot content = Compile(false);
        CampaignState campaign = CreateCampaign(content);
        byte[] first = CampaignSaveCodec.Encode(campaign, content);
        byte[] second = CampaignSaveCodec.Encode(campaign, content);
        True(first.AsSpan().SequenceEqual(second), "Identical authoritative state produced different save bytes.");
        True(first.Length <= CampaignSaveLimits.MaximumSaveBytes, "A minimal campaign exceeded the save bound.");

        ContentPreflightResult preflight = CampaignSaveCodec.Preflight(first, content);
        Equal(ContentPreflightKind.Exact, preflight.Kind, "Exact content did not pass preflight.");
        CampaignReadResult loaded = CampaignSaveCodec.Decode(first, content);
        True(loaded.Succeeded, loaded.Diagnostic.ToString());
        Equal(campaign.Voyage.Seed, loaded.Campaign!.Voyage.Seed, "Voyage seed did not round-trip.");
        Equal(campaign.Voyage.Tick, loaded.Campaign.Voyage.Tick, "Voyage tick did not round-trip.");
        Equal(campaign.CurrentLocationId, loaded.Campaign.CurrentLocationId, "Current location did not round-trip.");
        Equal(campaign.Characters.Length, loaded.Campaign.Characters.Length, "Roster did not round-trip.");
        Equal(campaign.Voyage.Ships.Values.Single().Modules.Length,
            loaded.Campaign.Voyage.Ships.Values.Single().Modules.Length, "Ship modules did not round-trip.");
        True(loaded.Campaign.Voyage.PersonalEncounter!.Actors.Values.Single().Injuries.Single().Stabilized,
            "A stabilized injury did not round-trip.");
        True(loaded.Campaign.Voyage.Commands.Length == 1 && loaded.Campaign.Voyage.CommandHistory.Length == 1,
            "Queued work and retained history did not round-trip.");
    }

    private static void CorruptionAndMissingContentFailPreflight()
    {
        GameContentSnapshot baseContent = Compile(false);
        byte[] valid = CampaignSaveCodec.Encode(CreateCampaign(baseContent), baseContent);
        byte[] truncated = valid[..^1];
        Equal(SaveDiagnosticCode.Truncated, CampaignSaveCodec.ValidateEnvelope(truncated), "Truncation diagnostic changed.");
        byte[] corrupt = valid.ToArray();
        corrupt[^1] ^= 0xff;
        Equal(SaveDiagnosticCode.ChecksumMismatch, CampaignSaveCodec.ValidateEnvelope(corrupt), "Checksum diagnostic changed.");
        byte[] oversized = new byte[CampaignSaveLimits.MaximumSaveBytes + 1];
        Equal(SaveDiagnosticCode.Oversized, CampaignSaveCodec.ValidateEnvelope(oversized), "Oversize diagnostic changed.");
        byte[] unsupported = valid.ToArray();
        unsupported[10] = 0xff;
        Equal(SaveDiagnosticCode.Unsupported, CampaignSaveCodec.ValidateEnvelope(unsupported), "Unsupported schema diagnostic changed.");

        GameContentSnapshot additive = Compile(true);
        byte[] additiveSave = CampaignSaveCodec.Encode(CreateCampaign(additive), additive);
        ContentPreflightResult missing = CampaignSaveCodec.Preflight(additiveSave, baseContent);
        Equal(ContentPreflightKind.Missing, missing.Kind, "Missing pack did not stop at preflight.");
        True(missing.MissingPackIds.Contains(new ContentId("mod.starwrights")), "Missing pack ID was not reported.");
        True(missing.MissingDefinitionIds.Contains(new ContentId("skill.mod.starwrights.gravimetry")),
            "Missing definition ID was not reported.");
    }

    private static void CompatibilityIsExplicitAndLoadable()
    {
        GameContentSnapshot source = Compile(false);
        GameContentSnapshot destination = Compile(true);
        byte[] bytes = CampaignSaveCodec.Encode(CreateCampaign(source), source);
        ContentCompatibilityRule rule = new(source.Fingerprint, destination.Fingerprint);
        Equal(ContentPreflightKind.Incompatible, CampaignSaveCodec.Preflight(bytes, destination).Kind,
            "Changed content was accepted without an explicit rule.");
        ContentPreflightResult compatible = CampaignSaveCodec.Preflight(bytes, destination, [rule]);
        Equal(ContentPreflightKind.Compatible, compatible.Kind, "Explicit compatibility was not recognized.");
        CampaignReadResult loaded = CampaignSaveCodec.Decode(bytes, destination, [rule]);
        True(loaded.Succeeded, loaded.Diagnostic.ToString());
        Equal(destination.Skills.Length, loaded.Campaign!.Characters[0].Capabilities.Snapshot(destination).Skills.Length,
            "Compatible load did not reconstruct the destination Skill registry.");
    }

    private static void FailedLoadPreservesActiveCampaign()
    {
        GameContentSnapshot content = Compile(false);
        CampaignState initial = CreateCampaign(content);
        CampaignRegistry registry = new(initial);
        byte[] corrupt = CampaignSaveCodec.Encode(initial, content);
        corrupt[^1] ^= 1;
        CampaignPublicationResult result = registry.Load(corrupt, content);
        False(result.Published, "A corrupt campaign was published.");
        True(ReferenceEquals(initial, registry.Active), "Failed load replaced the active campaign.");
    }

    private static void AtomicReplacementPreservesRecovery()
    {
        GameContentSnapshot content = Compile(false);
        byte[] bytes = CampaignSaveCodec.Encode(CreateCampaign(content), content);
        byte[] oldBytes = CampaignSaveCodec.Encode(CreateCampaign(content) with { GameBuild = "old-build" }, content);
        MemoryFileSystem files = new();
        string target = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "campaign.sjsave"));
        files.Seed(target, oldBytes);
        CampaignSaveStore store = new(files);
        SaveWriteResult replaced = store.Save(target, bytes);
        True(replaced.Succeeded, replaced.Diagnostic.ToString());
        True(replaced.RecoveryPath is not null && files.Exists(replaced.RecoveryPath), "Replacement omitted its recovery artifact.");
        True(oldBytes.AsSpan().SequenceEqual(files.ReadAllBytes(replaced.RecoveryPath!)), "Recovery does not contain the replaced save.");

        byte[] afterReplacement = files.ReadAllBytes(target);
        SaveWriteResult invalid = store.Save(target, new byte[] { 1, 2, 3 });
        False(invalid.Succeeded, "Invalid staged bytes were published.");
        True(afterReplacement.AsSpan().SequenceEqual(files.ReadAllBytes(target)), "Invalid replacement altered the target.");

        files.FailNextWrite = true;
        SaveWriteResult interrupted = store.Save(target, bytes);
        Equal(SaveDiagnosticCode.IoFailure, interrupted.Diagnostic, "Interrupted durable write diagnostic changed.");
        True(afterReplacement.AsSpan().SequenceEqual(files.ReadAllBytes(target)), "Interrupted write altered the target.");

        files.FailNextReplace = true;
        byte[] before = files.ReadAllBytes(target);
        SaveWriteResult failed = store.Save(target, bytes);
        False(failed.Succeeded, "Injected replacement failure was reported as success.");
        True(before.AsSpan().SequenceEqual(files.ReadAllBytes(target)), "Failed replacement altered the target.");

        SaveWriteResult recovered = store.Recover(target);
        True(recovered.Succeeded, recovered.Diagnostic.ToString());
        True(oldBytes.AsSpan().SequenceEqual(files.ReadAllBytes(target)), "Recovery did not restore the bounded artifact.");
        True(store.CleanupRecovery(target), "Recovery cleanup failed.");
        False(files.Exists(target + ".recovery"), "Recovery cleanup retained its exact artifact.");

        string oversizedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "oversized.sjsave"));
        files.Seed(oversizedPath, new byte[CampaignSaveLimits.MaximumSaveBytes + 1]);
        Equal(SaveDiagnosticCode.Oversized, store.Read(oversizedPath, content).Diagnostic,
            "File size was not rejected before decode.");
    }

    private static void LocationMigrationIsDeterministicAndNonDestructive()
    {
        GameContentSnapshot sourceContent = Compile(false);
        GameContentSnapshot destinationContent = Compile(true);
        CampaignState source = CreateCampaign(sourceContent);
        byte[] sourceBytes = CampaignSaveCodec.Encode(source, sourceContent);
        LocationRenameMigration migration = new(
            new ContentId("migration.m6.anchorage-rename"),
            sourceContent.Fingerprint,
            destinationContent.Fingerprint,
            source.CurrentLocationId,
            new ContentId("location.anchorage.restored"));
        CampaignMigrationRegistry registry = new([migration]);
        Dictionary<ContentFingerprint, GameContentSnapshot> snapshots = new()
        {
            [sourceContent.Fingerprint] = sourceContent,
            [destinationContent.Fingerprint] = destinationContent,
        };
        ContentPreflightResult preflight = CampaignSaveCodec.Preflight(sourceBytes, destinationContent, migrations: registry);
        Equal(ContentPreflightKind.Migratable, preflight.Kind, "Migration path was not reported during preflight.");
        CampaignMigrationResult first = CampaignMigrationService.Migrate(sourceBytes, destinationContent, snapshots, registry);
        CampaignMigrationResult second = CampaignMigrationService.Migrate(sourceBytes, destinationContent, snapshots, registry);
        True(first.Succeeded, first.Diagnostic.ToString());
        True(first.SaveBytes!.AsSpan().SequenceEqual(second.SaveBytes), "Migration output was not deterministic.");
        Equal(new ContentId("location.anchorage.restored"), first.Campaign!.CurrentLocationId, "Migration transform was not applied.");
        True(sourceBytes.AsSpan().SequenceEqual(CampaignSaveCodec.Encode(source, sourceContent)), "Migration altered the source campaign.");
    }

    private static void MigrationFailuresPreserveTheSource()
    {
        GameContentSnapshot sourceContent = Compile(false);
        GameContentSnapshot destinationContent = Compile(true);
        CampaignState source = CreateCampaign(sourceContent);
        byte[] sourceBytes = CampaignSaveCodec.Encode(source, sourceContent);
        Dictionary<ContentFingerprint, GameContentSnapshot> snapshots = new()
        {
            [sourceContent.Fingerprint] = sourceContent,
            [destinationContent.Fingerprint] = destinationContent,
        };

        CampaignMigrationResult missing = CampaignMigrationService.Migrate(
            sourceBytes, destinationContent, snapshots, new CampaignMigrationRegistry([]));
        Equal(SaveDiagnosticCode.MigrationUnavailable, missing.Diagnostic, "Missing migration path diagnostic changed.");

        Dictionary<ContentFingerprint, GameContentSnapshot> wrongSource = new()
        {
            [destinationContent.Fingerprint] = destinationContent,
        };
        CampaignMigrationResult mismatch = CampaignMigrationService.Migrate(
            sourceBytes, destinationContent, wrongSource, new CampaignMigrationRegistry([]));
        Equal(SaveDiagnosticCode.MigrationSourceMismatch, mismatch.Diagnostic, "Wrong source diagnostic changed.");

        TestMigration throwing = new(
            new ContentId("migration.m6.throw"), sourceContent.Fingerprint, destinationContent.Fingerprint,
            _ => throw new InvalidOperationException("Injected transform failure."));
        CampaignMigrationResult failedTransform = CampaignMigrationService.Migrate(
            sourceBytes, destinationContent, snapshots, new CampaignMigrationRegistry([throwing]));
        Equal(SaveDiagnosticCode.MigrationFailed, failedTransform.Diagnostic, "Failed transform diagnostic changed.");

        TestMigration invalid = new(
            new ContentId("migration.m6.invalid"), sourceContent.Fingerprint, destinationContent.Fingerprint,
            campaign => campaign with { GameBuild = string.Empty });
        CampaignMigrationResult failedValidation = CampaignMigrationService.Migrate(
            sourceBytes, destinationContent, snapshots, new CampaignMigrationRegistry([invalid]));
        Equal(SaveDiagnosticCode.MigrationFailed, failedValidation.Diagnostic, "Failed migration validation diagnostic changed.");
        True(sourceBytes.AsSpan().SequenceEqual(CampaignSaveCodec.Encode(source, sourceContent)),
            "Failed migration changed the source bytes.");
    }

    private static GameContentSnapshot Compile(bool additive)
    {
        List<IContentPackSource> sources = [new DirectoryContentPackSource(Path.Combine(FixtureRoot, "base"))];
        if (additive)
        {
            sources.Add(new DirectoryContentPackSource(Path.Combine(FixtureRoot, "starwrights")));
        }

        ContentCompilationResult result = new GameContentCompiler().Compile(sources, GameVersion);
        True(result.Succeeded, result.Diagnostics.FirstOrDefault()?.Code ?? "Content compilation failed.");
        return result.Snapshot!;
    }

    private static CampaignState CreateCampaign(GameContentSnapshot content)
    {
        RosterCreationResult roster = CharacterCreator.CreateRoster(
            content.Fingerprint,
            new ScenarioId("scenario.first-voyage"),
            0x5eedUL,
            content,
            new CrewSupportProfile(
                content.Races.SelectMany(value => value.RequiredSupportIds).ToImmutableHashSet(),
                content.Characters.SelectMany(value => value.EquipmentIds).ToImmutableHashSet()));
        True(roster.Succeeded, roster.Failure.ToString());

        ShipFrameDefinition frame = content.ShipFrames.Single();
        ContentId path = new("ship.path.arcane");
        ShipModuleDefinition[] modules = [.. content.ShipModules.Where(value => value.CompatiblePathIds.Contains(path))
            .GroupBy(value => value.MountId).Select(group => group.OrderBy(value => value.ModuleId).First())];
        ShipWeaponConfigurationDefinition weapon = content.ShipWeaponConfigurations.Single(value =>
            value.ShipWeaponConfigurationId == new ShipWeaponConfigurationId("ship.weapon.arcane.aether-cannon"));
        ShipLoadoutResult loadout = ShipLoadoutSystem.Create(
            new ShipId("ship.first-voyage.player"), new TeamId("team.player"), frame, path, modules, weapon,
            ImmutableDictionary<ResourceId, int>.Empty
                .Add(weapon.ResourceId, 12).Add(new ResourceId("resource.spare-parts"), 4));
        True(loadout.Accepted, loadout.RejectionCode);

        EncounterDefinition encounterDefinition = content.Encounters.Single();
        PersonalBoardDefinition boardDefinition = content.PersonalBoards.Single();
        BoardValidationResult board = TacticalBoard.Create(
            boardDefinition,
            boardDefinition.CellIds.Select(id => content.BoardCells.Single(value => value.CellId == id)),
            boardDefinition.LinkIds.Select(id => content.ZoneLinks.Single(value => value.LinkId == id)));
        True(board.Accepted, board.RejectionCode);
        CharacterState crew = roster.Roster!.Characters[0];
        ActorId actorId = new("actor.first-voyage.saved-crew");
        CellId cellId = new("cell.ruin.entry");
        PersonalActorState actor = new(
            actorId, new TeamId("team.player"), crew.Id, cellId, 500, 100, 2, 7, true, false, false,
            PersonalLoadout.Create(content.Equipment),
            [new InjuryState(new ContentId("injury.ruin.arc-burn"), InjurySeverity.Serious, true)]);
        PersonalEncounterState encounter = new(
            encounterDefinition.EncounterId,
            board.Board!.Place(actorId, cellId),
            ImmutableDictionary<ActorId, PersonalActorState>.Empty.Add(actorId, actor),
            boardDefinition.RequiredObjectiveIds.ToImmutableDictionary(value => value, _ => ObjectiveState.Active),
            ImmutableHashSet.Create(new ContentId("exploration.ruin.console-restored")),
            ImmutableHashSet.Create(new ContentId("object.ruin.ancient-defense")),
            false,
            false)
        {
            ActiveEffects = [new ActiveEffectState(
                new EffectId("effect.personal.defending"), new ContentId("command.saved.defend"), actorId, 20, 1)],
        };
        VoyageWorld world = VoyageWorld.Create(0x5eedUL, content.Fingerprint, new TeamId("team.player"), [loadout.Ship!], encounter);
        VoyageCommand queued = new(
            new ContentId("command.saved.course"), VoyageCommandKind.Course, 1, 10, loadout.Ship!.Id.Value,
            loadout.Ship.Id.Value, new FixedVector2(FixedScalar.FromInt(1), FixedScalar.FromInt(0)), 0, null, 1);
        world = world.Enqueue(queued).World;
        return new CampaignState(
            "0.1.0-dev",
            CampaignContentLock.Create(content),
            new ContentId("location.anchorage.home"),
            world,
            roster.Roster.Characters);
    }

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

    private sealed class MemoryFileSystem : ICampaignSaveFileSystem
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.OrdinalIgnoreCase);
        public bool FailNextReplace { get; set; }
        public bool FailNextWrite { get; set; }
        public bool Exists(string path) => files.ContainsKey(path);
        public long GetLength(string path) => files[path].LongLength;
        public byte[] ReadAllBytes(string path) => files[path].ToArray();
        public void Seed(string path, byte[] bytes) => files[path] = bytes.ToArray();
        public void WriteDurable(string path, ReadOnlySpan<byte> bytes)
        {
            if (FailNextWrite)
            {
                FailNextWrite = false;
                throw new IOException("Injected durable write failure.");
            }

            files.Add(path, bytes.ToArray());
        }
        public void Move(string source, string destination)
        {
            files.Add(destination, files[source]);
            files.Remove(source);
        }

        public void Replace(string source, string destination, string? recoveryPath)
        {
            if (FailNextReplace)
            {
                FailNextReplace = false;
                throw new IOException("Injected replacement failure.");
            }

            if (recoveryPath is not null)
            {
                files[recoveryPath] = files[destination].ToArray();
            }

            files[destination] = files[source];
            files.Remove(source);
        }

        public void Delete(string path) => files.Remove(path);
    }

    private sealed record TestMigration(
        ContentId Id,
        ContentFingerprint SourceFingerprint,
        ContentFingerprint DestinationFingerprint,
        Func<CampaignState, CampaignState> TransformFunction) : ICampaignMigration
    {
        public CampaignState Transform(CampaignState source) => TransformFunction(source);
    }
}
