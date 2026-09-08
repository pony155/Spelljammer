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

internal static partial class ContentContracts
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
        WorldTimeIsDataDriven();
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


}
