using System.Collections.Immutable;
using Spelljammer.Content.Compilation;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;

namespace Spelljammer.Persistence;

public interface ICampaignMigration
{
    ContentId Id { get; }
    ContentFingerprint SourceFingerprint { get; }
    ContentFingerprint DestinationFingerprint { get; }
    CampaignState Transform(CampaignState source);
}

public sealed record LocationRenameMigration(
    ContentId Id,
    ContentFingerprint SourceFingerprint,
    ContentFingerprint DestinationFingerprint,
    ContentId SourceLocationId,
    ContentId DestinationLocationId) : ICampaignMigration
{
    public CampaignState Transform(CampaignState source)
    {
        if (source.ContentLock.EffectiveFingerprint != SourceFingerprint || source.CurrentLocationId != SourceLocationId)
        {
            throw new InvalidOperationException("Migration source contract does not match the campaign.");
        }

        return source with { CurrentLocationId = DestinationLocationId };
    }
}

public sealed class CampaignMigrationRegistry
{
    private const int MaximumMigrations = 256;
    private const int MaximumPathLength = 32;
    private readonly ImmutableArray<ICampaignMigration> migrations;

    public CampaignMigrationRegistry(IEnumerable<ICampaignMigration> values)
    {
        migrations = [.. values.OrderBy(value => value.Id)];
        if (migrations.Length > MaximumMigrations || migrations.Select(value => value.Id).Distinct().Count() != migrations.Length)
        {
            throw new ArgumentException("Migration registry exceeds capacity or contains duplicate IDs.", nameof(values));
        }
    }

    public ImmutableArray<ContentId> FindPath(ContentFingerprint source, ContentFingerprint destination) =>
        [.. FindMigrations(source, destination).Select(value => value.Id)];

    public ImmutableArray<ICampaignMigration> FindMigrations(ContentFingerprint source, ContentFingerprint destination)
    {
        if (source == destination)
        {
            return [];
        }

        Queue<(ContentFingerprint Fingerprint, ImmutableArray<ICampaignMigration> Path)> pending = new();
        HashSet<ContentFingerprint> visited = [source];
        pending.Enqueue((source, []));
        while (pending.Count > 0)
        {
            (ContentFingerprint current, ImmutableArray<ICampaignMigration> path) = pending.Dequeue();
            if (path.Length >= MaximumPathLength)
            {
                continue;
            }

            foreach (ICampaignMigration migration in migrations.Where(value => value.SourceFingerprint == current).OrderBy(value => value.Id))
            {
                ImmutableArray<ICampaignMigration> nextPath = path.Add(migration);
                if (migration.DestinationFingerprint == destination)
                {
                    return nextPath;
                }

                if (visited.Add(migration.DestinationFingerprint))
                {
                    pending.Enqueue((migration.DestinationFingerprint, nextPath));
                }
            }
        }

        return [];
    }
}

public sealed record CampaignMigrationResult(
    CampaignState? Campaign,
    byte[]? SaveBytes,
    SaveDiagnosticCode Diagnostic,
    ImmutableArray<ContentId> AppliedMigrationIds)
{
    public bool Succeeded => Campaign is not null && SaveBytes is not null;
}

public static class CampaignMigrationService
{
    public static CampaignMigrationResult Migrate(
        ReadOnlyMemory<byte> sourceBytes,
        GameContentSnapshot destination,
        IReadOnlyDictionary<ContentFingerprint, GameContentSnapshot> availableContent,
        CampaignMigrationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(availableContent);
        ArgumentNullException.ThrowIfNull(registry);
        CampaignContentLock? sourceLock = availableContent.Keys
            .Select(fingerprint => CampaignSaveCodec.Preflight(sourceBytes, availableContent[fingerprint], migrations: registry))
            .Select(result => result.ContentLock)
            .FirstOrDefault(value => value is not null);
        if (sourceLock is null || !availableContent.TryGetValue(sourceLock.EffectiveFingerprint, out GameContentSnapshot? sourceContent))
        {
            return Failed(SaveDiagnosticCode.MigrationSourceMismatch);
        }

        CampaignReadResult decoded = CampaignSaveCodec.Decode(sourceBytes, sourceContent);
        if (!decoded.Succeeded)
        {
            return Failed(decoded.Diagnostic);
        }

        ImmutableArray<ICampaignMigration> path = registry.FindMigrations(sourceLock.EffectiveFingerprint, destination.Fingerprint);
        if (path.IsEmpty)
        {
            return Failed(SaveDiagnosticCode.MigrationUnavailable);
        }

        CampaignState temporary = decoded.Campaign!;
        GameContentSnapshot currentContent = sourceContent;
        ImmutableArray<ContentId>.Builder applied = ImmutableArray.CreateBuilder<ContentId>();
        try
        {
            foreach (ICampaignMigration migration in path)
            {
                if (!availableContent.TryGetValue(migration.DestinationFingerprint, out GameContentSnapshot? nextContent))
                {
                    return Failed(SaveDiagnosticCode.MigrationUnavailable);
                }

                temporary = migration.Transform(temporary);
                temporary = Rebind(temporary, currentContent, nextContent, migration.Id);
                if (!CampaignValidator.TryValidate(temporary, nextContent, out _))
                {
                    return Failed(SaveDiagnosticCode.MigrationFailed);
                }

                currentContent = nextContent;
                applied.Add(migration.Id);
            }

            byte[] bytes = CampaignSaveCodec.Encode(temporary, destination);
            return new CampaignMigrationResult(temporary, bytes, SaveDiagnosticCode.None, applied.MoveToImmutable());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Failed(SaveDiagnosticCode.MigrationFailed);
        }

        static CampaignMigrationResult Failed(SaveDiagnosticCode diagnostic) => new(null, null, diagnostic, []);
    }

    public static SaveWriteResult WriteMigrated(
        string sourcePath,
        string destinationPath,
        CampaignMigrationResult migration,
        CampaignSaveStore store)
    {
        if (!migration.Succeeded)
        {
            return new SaveWriteResult(false, migration.Diagnostic, Path.GetFullPath(destinationPath), null);
        }

        if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath)))
        {
            return new SaveWriteResult(false, SaveDiagnosticCode.MigrationFailed, Path.GetFullPath(destinationPath), null);
        }

        return store.Save(destinationPath, migration.SaveBytes!);
    }

    private static CampaignState Rebind(
        CampaignState source,
        GameContentSnapshot oldContent,
        GameContentSnapshot newContent,
        ContentId migrationId)
    {
        ImmutableArray<CharacterState> characters = [.. source.Characters.Select(character =>
        {
            CharacterCapabilitySnapshot old = character.Capabilities.Snapshot(oldContent);
            CharacterCapabilitySnapshot rebound = old with
            {
                Fingerprint = newContent.Fingerprint,
                Attributes = [.. newContent.Attributes.Select(definition => old.Attributes
                    .FirstOrDefault(value => value.Id == definition.AttributeId) ??
                    new AttributeValueSnapshot(definition.AttributeId, (short)definition.DefaultValue))],
                Skills = [.. newContent.Skills.Select(definition => old.Skills
                    .FirstOrDefault(value => value.Id == definition.SkillId) ??
                    new SkillValueSnapshot(definition.SkillId, (byte)definition.Minimum, 0))],
            };
            CharacterCapabilities capabilities = CharacterCapabilities.Restore(rebound, newContent);
            return character with { ContentFingerprint = newContent.Fingerprint, Capabilities = capabilities };
        })];
        ImmutableDictionary<ShipId, ShipState> ships = source.Voyage.Ships.Values.Select(ship =>
        {
            if (!newContent.TryGetShipFrame(ship.Frame.ShipFrameId, out ShipFrameDefinition? frame))
            {
                throw new InvalidOperationException("Destination frame is missing.");
            }

            ImmutableArray<InstalledModuleState> modules = [.. ship.Modules.Select(module =>
            {
                if (!newContent.TryGetShipModule(module.Definition.ModuleId, out ShipModuleDefinition? definition))
                {
                    throw new InvalidOperationException("Destination module is missing.");
                }

                ShipWeaponConfigurationDefinition? weapon = null;
                if (module.Weapon is not null && !newContent.TryGetShipWeaponConfiguration(
                        module.Weapon.ShipWeaponConfigurationId, out weapon))
                {
                    throw new InvalidOperationException("Destination weapon is missing.");
                }

                return module with { Definition = definition!, Weapon = weapon };
            })];
            return ship with { Frame = frame!, Modules = modules };
        }).ToImmutableDictionary(ship => ship.Id);
        PersonalEncounterState? encounter = source.Voyage.PersonalEncounter is null
            ? null
            : RebindEncounter(source.Voyage.PersonalEncounter, newContent);
        CampaignContentLock contentLock = CampaignContentLock.Create(
            newContent, source.ContentLock.AppliedMigrationIds.Append(migrationId));
        return source with
        {
            ContentLock = contentLock,
            Characters = characters,
            Voyage = source.Voyage with
            {
                ContentFingerprint = newContent.Fingerprint,
                Ships = ships,
                PersonalEncounter = encounter,
            },
        };
    }

    private static PersonalEncounterState RebindEncounter(PersonalEncounterState source, GameContentSnapshot content)
    {
        if (!content.TryGetEncounter(source.Id, out EncounterDefinition? encounter) ||
            !content.TryGetPersonalBoard(encounter!.PersonalBoardId, out PersonalBoardDefinition? board))
        {
            throw new InvalidOperationException("Destination encounter is missing.");
        }

        BoardValidationResult rebuilt = TacticalBoard.Create(
            board!, board!.CellIds.Select(id => content.TryGetBoardCell(id, out BoardCellDefinition? cell) ? cell! : throw new InvalidOperationException()),
            board.LinkIds.Select(id => content.TryGetZoneLink(id, out ZoneLinkDefinition? link) ? link! : throw new InvalidOperationException()));
        if (!rebuilt.Accepted)
        {
            throw new InvalidOperationException(rebuilt.RejectionCode);
        }

        TacticalBoard tacticalBoard = rebuilt.Board!;
        foreach (PersonalActorState actor in source.Actors.Values.OrderBy(value => value.Id))
        {
            tacticalBoard = tacticalBoard.Place(actor.Id, actor.CellId);
        }

        return source with { Board = tacticalBoard };
    }
}
