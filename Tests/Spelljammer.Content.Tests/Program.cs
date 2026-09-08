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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MeleeWeaponDefinition = Spelljammer.Simulation.Items.MeleeWeaponDefinition;
using RangedWeaponDefinition = Spelljammer.Simulation.Items.RangedWeaponDefinition;

[TestClass]
[DoNotParallelize]
public sealed partial class ContentContracts
{
    private static readonly SemanticVersion GameVersion = new(0, 1, 0);
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Milestone0");
    private static readonly string Milestone2Root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Milestone2");

    [TestMethod]
    public void StableIdsAreValidatedAndOrdinalContract() => StableIdsAreValidatedAndOrdinal();

    [TestMethod]
    public void ValidFixtureIsCanonicalAndDeterministicContract() => ValidFixtureIsCanonicalAndDeterministic();

    [TestMethod]
    public void EveryFrozenDiagnosticCaseIsRecognizedContract() => EveryFrozenDiagnosticCaseIsRecognized();

    [TestMethod]
    public void FailedReplacementPreservesPublishedSnapshotContract() => FailedReplacementPreservesPublishedSnapshot();

    [TestMethod]
    public void BaseAbilitysAndSkillsAreTypedAndIndexedContract() => BaseAbilitysAndSkillsAreTypedAndIndexed();

    [TestMethod]
    public void WorldTimeIsDataDrivenContract() => WorldTimeIsDataDriven();

    [TestMethod]
    public void LevelProgressionTablesAreDataDrivenContract() => LevelProgressionTablesAreDataDriven();

    [TestMethod]
    public void CharacterResourcesAreBoundedAndDirectionalContract() => CharacterResourcesAreBoundedAndDirectional();

    [TestMethod]
    public void Milestone2InvalidCasesAreRecognizedContract() => Milestone2InvalidCasesAreRecognized();

    [TestMethod]
    public void AdditiveSkillIsDynamicAndReversibleContract() => AdditiveSkillIsDynamicAndReversible();

    [TestMethod]
    public void CharacterDefinitionsRejectInvalidGraphsContract() => CharacterDefinitionsRejectInvalidGraphs();

    [TestMethod]
    public void BaseRosterIsDeterministicAndDynamicContract() => BaseRosterIsDeterministicAndDynamic();

    [TestMethod]
    public void RecruitmentHonorsScenarioRosterLimitContract() => RecruitmentHonorsScenarioRosterLimit();

    [TestMethod]
    public void EligibilityAndResolutionAreAtomicContract() => EligibilityAndResolutionAreAtomic();

    [TestMethod]
    public void TrainingGrantsAccessOnlyAtCompletionContract() => TrainingGrantsAccessOnlyAtCompletion();

    [TestMethod]
    public void AccessSourcesCoexistAndRecomputeContract() => AccessSourcesCoexistAndRecompute();

    [TestMethod]
    public void SupernaturalDefinitionsAndExecutionAreBoundedContract() => SupernaturalDefinitionsAndExecutionAreBounded();

    [TestMethod]
    public void MindlinkRequiresKnowledgeConsentAndStrainContract() => MindlinkRequiresKnowledgeConsentAndStrain();

    [TestMethod]
    public void RaceCapabilitiesRespectTheirBoundariesContract() => RaceCapabilitiesRespectTheirBoundaries();

    [TestMethod]
    public void Milestone5EncounterAndShipContentIsLinkedContract() => Milestone5EncounterAndShipContentIsLinked();

    [TestMethod]
    public void MeleeWeaponsAreDataDrivenAndAtomicContract() => MeleeWeaponsAreDataDrivenAndAtomic();

    [TestMethod]
    public void RangedWeaponsUseAmmunitionAndReloadAtomicallyContract() => RangedWeaponsUseAmmunitionAndReloadAtomically();


}
