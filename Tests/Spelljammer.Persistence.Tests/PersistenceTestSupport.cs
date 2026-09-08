using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Spelljammer.Content.Compilation;
using Spelljammer.Content.Manifests;
using Spelljammer.Content.Sources;
using Spelljammer.Persistence;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Ships;
using Spelljammer.Simulation.World;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

public sealed partial class PersistenceContracts
{
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
                content.Characters.SelectMany(value => value.StartingItemDefinitionIds).ToImmutableHashSet()));
        True(roster.Succeeded, roster.Failure.ToString());
        CharacterState protagonist = roster.Roster!.Characters[0];
        RecruitmentResult activeResult = CrewRecruitmentSystem.Create(protagonist, content);
        True(activeResult.Succeeded, activeResult.Failure.ToString());
        CrewRoster activeRoster = activeResult.Roster!;
        ScenarioDefinition scenario = content.Scenarios.Single(value => value.ScenarioId == protagonist.ScenarioId);
        foreach (CharacterState npc in roster.Roster.Characters.Skip(1).Take(scenario.MaximumRosterSize - 1))
        {
            activeResult = CrewRecruitmentSystem.Recruit(activeRoster, npc, content);
            True(activeResult.Succeeded, activeResult.Failure.ToString());
            activeRoster = activeResult.Roster!;
        }

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
        CharacterState crew = activeRoster.Members[0];
        AmmunitionDefinition ammunition = content.Ammunition.Single();
        InventoryContainer inventory = crew.Items.InventoryContainers.Single();
        InventoryEntryId ammunitionEntryId = new(Guid.Parse("88888888-8888-8888-8888-888888888888"));
        ItemSystemResult stocked = ItemSystem.AddStack(
            crew.Items, inventory.ContainerId, ammunitionEntryId, ammunition.Id, 12, content);
        True(stocked.Accepted, stocked.RejectionCode);
        crew = crew with { Items = stocked.State };
        StatusDefinition savedStatus = content.Statuses.Single(value => value.StatusId == new StatusId("status.confused"));
        StatusInstanceId savedStatusId = new(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        crew = crew with
        {
            Statuses = new StatusState([new StatusInstance(
                savedStatusId, savedStatus.StatusId, savedStatus.Revision, crew.Id.Value, crew.Id.Value, 2, 1, 1)]),
        };
        ImmutableArray<CharacterState> campaignCharacters =
            [.. activeRoster.Members.Select(value => value.Id == crew.Id ? crew : value)];
        True(content.TryGetCharacterResourceProfile(new CharacterResourceProfileId("character-resources.standard"),
            out CharacterResourceProfileDefinition? resourceProfile), "Character resource profile is missing.");
        ActorId actorId = new("actor.first-voyage.saved-crew");
        CellId cellId = new("cell.ruin.entry");
        PersonalActorState actor = new(
            actorId, new TeamId("team.player"), crew.Id, cellId,
            CharacterTurnState.Create(resourceProfile!.TurnRules) with { CurrentTurnMeter = 50, CurrentActionPoints = 2 },
            crew.CharacterResources.WithCurrentValue(CharacterResourceIds.Health, 7), true, false, false,
            crew.Items,
            [new InjuryState(new ContentId("injury.ruin.arc-burn"), InjurySeverity.Serious, true)])
        {
            Statuses = new StatusState([new StatusInstance(
                savedStatusId, savedStatus.StatusId, savedStatus.Revision, actorId.Value, actorId.Value, 2, 1, 1)]),
        };
        PersonalEncounterState encounter = new(
            encounterDefinition.EncounterId,
            board.Board!.Place(actorId, cellId),
            ImmutableDictionary<ActorId, PersonalActorState>.Empty.Add(actorId, actor),
            boardDefinition.RequiredObjectiveIds.ToImmutableDictionary(value => value, _ => ObjectiveState.Active),
            ImmutableHashSet.Create(new ContentId("exploration.ruin.console-restored")),
            ImmutableHashSet.Create(new ContentId("object.ruin.ancient-defense")),
            false,
            false);
        World world = World.Create(
            0x5eedUL,
            content.Fingerprint,
            content.WorldTimes.Single(),
            content.Calendars.Single(),
            content.TimeScales.Single(),
            new TeamId("team.player"),
            [loadout.Ship!],
            encounter);
        WorldCommand queued = new(
            new ContentId("command.saved.course"), WorldCommandKind.Course, 1, 10, loadout.Ship!.Id.Value,
            loadout.Ship.Id.Value, new FixedVector2(FixedScalar.FromInt(1), FixedScalar.FromInt(0)), 0, null, 1);
        world = world.Enqueue(queued).World;
        world = world with
        {
            Clock = world.Clock with { ElapsedWorldSeconds = 123_456, FractionRemainder = 7 },
        };
        return new CampaignState(
            "0.1.0-dev",
            CampaignContentLock.Create(content),
            new ContentId("location.anchorage.home"),
            world,
            activeRoster.ProtagonistId,
            campaignCharacters);
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
        public void EnsureDirectory(string path)
        {
        }

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
