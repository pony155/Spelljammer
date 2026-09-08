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
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[DoNotParallelize]
public sealed partial class PersistenceContracts
{
    private static readonly SemanticVersion GameVersion = new(0, 1, 0);
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [TestMethod]
    public void ExactCampaignRoundTripsCanonicallyContract() => ExactCampaignRoundTripsCanonically();

    [TestMethod]
    public void CorruptionAndMissingContentFailPreflightContract() => CorruptionAndMissingContentFailPreflight();

    [TestMethod]
    public void CompatibilityIsExplicitAndLoadableContract() => CompatibilityIsExplicitAndLoadable();

    [TestMethod]
    public void FailedLoadPreservesActiveCampaignContract() => FailedLoadPreservesActiveCampaign();

    [TestMethod]
    public void AtomicReplacementPreservesRecoveryContract() => AtomicReplacementPreservesRecovery();

    [TestMethod]
    public void LocationMigrationIsDeterministicAndNonDestructiveContract() =>
        LocationMigrationIsDeterministicAndNonDestructive();

    [TestMethod]
    public void MigrationFailuresPreserveTheSourceContract() => MigrationFailuresPreserveTheSource();

    [TestMethod]
    public void BattleUnitProjectionRoundTripsCharacterStateContract() => BattleUnitProjectionRoundTripsCharacterState();

    [TestMethod]
    public void Schema12ActorSavesMigrateToBattleUnitsContract() => Schema12ActorSavesMigrateToBattleUnits();


}
