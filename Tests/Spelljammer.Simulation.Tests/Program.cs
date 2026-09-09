using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed partial class SimulationContracts
{
    [TestMethod]
    public void FixedTickCadenceAndOrderingAreDeterministicContract() =>
        FixedTickCadenceAndOrderingAreDeterministic();

    [TestMethod]
    public void CampaignClockAndCalendarAreDeterministicContract() =>
        CampaignClockAndCalendarAreDeterministic();

    [TestMethod]
    public void TacticalBoardAndEncounterCleanupAreBoundedContract() =>
        TacticalBoardAndEncounterCleanupAreBounded();

    [TestMethod]
    public void ShipLoadoutPowerAndDamageAreAtomicContract() =>
        ShipLoadoutPowerAndDamageAreAtomic();

    [TestMethod]
    public void CombatSystemRoutesAndRejectsAtomicallyContract() =>
        CombatSystemRoutesAndRejectsAtomically();

    [TestMethod]
    public void PersonalCombatResolutionIsCommittedAtomicallyContract() =>
        PersonalCombatResolutionIsCommittedAtomically();

    [TestMethod]
    public void ItemInventoryAndEquipmentLoadoutAreAtomicContract() =>
        ItemInventoryAndEquipmentLoadoutAreAtomic();

    [TestMethod]
    public void StatusLifecycleAndConflictsAreAtomicContract() =>
        StatusLifecycleAndConflictsAreAtomic();

    [TestMethod]
    public void GalaxyGenerationAndRoutingAreDeterministicContract() =>
        GalaxyGenerationAndRoutingAreDeterministic();
}
