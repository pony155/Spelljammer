using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

return SimulationContracts.Run();

internal static partial class SimulationContracts
{
    public static int Run()
    {
        FixedTickCadenceAndOrderingAreDeterministic();
        TacticalBoardAndEncounterCleanupAreBounded();
        ShipLoadoutPowerAndDamageAreAtomic();
        CombatSystemRoutesAndRejectsAtomically();
        PersonalCombatResolutionIsCommittedAtomically();
        ItemInventoryAndEquipmentLoadoutAreAtomic();
        StatusLifecycleAndConflictsAreAtomic();
        Console.WriteLine("Spelljammer simulation contracts passed.");
        return 0;
    }


}
