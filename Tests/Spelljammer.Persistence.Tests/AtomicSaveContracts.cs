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
using Spelljammer.Simulation.World;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

public sealed partial class PersistenceContracts
{
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
}
