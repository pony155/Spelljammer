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
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

return PersistenceContracts.Run();

internal static partial class PersistenceContracts
{
    private static readonly SemanticVersion GameVersion = new(0, 1, 0);
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static int Run()
    {
        ExactCampaignRoundTripsCanonically();
        SchemaSevenEquipmentMigratesToItemInstances();
        CorruptionAndMissingContentFailPreflight();
        CompatibilityIsExplicitAndLoadable();
        FailedLoadPreservesActiveCampaign();
        AtomicReplacementPreservesRecovery();
        LocationMigrationIsDeterministicAndNonDestructive();
        MigrationFailuresPreserveTheSource();
        Console.WriteLine("Campaign persistence contracts passed.");
        return 0;
    }


}
