using System.Collections.Immutable;
using Spelljammer.Simulation.Characters;
using Spelljammer.Simulation.Content;
using Spelljammer.Simulation.Encounters;
using Spelljammer.Simulation.Items;
using Spelljammer.Simulation.Effects;

internal static partial class SimulationContracts
{
    private static void ItemInventoryAndEquipmentLoadoutAreAtomic()
    {
        ContentId owner = new("character.test.item-owner");
        ContentId otherOwner = new("character.test.item-recipient");
        ContentId mainHand = new("equipment-slot.main-hand");
        ContentId offHand = new("equipment-slot.off-hand");
        ContentId head = new("equipment-slot.head");
        InventoryContainerId sourceId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        InventoryContainerId destinationId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        ItemInstanceId bladeId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        ItemInstanceId helmId = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        ItemInstanceId competingBladeId = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        InventoryEntryId ammunitionEntryId = new(Guid.Parse("66666666-6666-6666-6666-666666666666"));
        InventoryEntryId splitEntryId = new(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        InventoryEntryId transferredEntryId = new(Guid.Parse("88888888-8888-8888-8888-888888888888"));
        GearDefinition blade = new(
            new ContentId("item.test.boarding-blade"), 1, 1, "item.test.blade.name", "item.test.blade.description",
            125, 80, [], [mainHand, offHand], [new ContentId("action.test.slash")], []);
        ArmorDefinition helm = new(
            new ContentId("item.test.helm"), 1, 1, "item.test.helm.name", "item.test.helm.description",
            80, 50, [], [head], 2, 100, [], ["head"], []);
        AmmunitionDefinition ammunition = new(
            new AmmunitionId("ammunition.test.pistol"), 1, 1, "ammunition.test.name", "ammunition.test.description",
            3, 2, 20, ["standard"], AmmunitionType.PistolRound, RangedWeaponTechnology.Ballistic,
            100, 100, 0, 0);
        ItemDefinitionCatalog catalog = new([blade, helm, ammunition], [mainHand, offHand, head]);
        ItemSystemState initial = new(
        [
            new ItemInstance(bladeId, blade.Id, sourceId, 100, null, null, 1),
            new ItemInstance(helmId, helm.Id, sourceId, 100, null, null, 1),
            new ItemInstance(competingBladeId, blade.Id, sourceId, 100, null, null, 1),
        ],
        [
            new InventoryContainer(sourceId, owner, 500, 4, [bladeId, helmId, competingBladeId]),
            new InventoryContainer(destinationId, otherOwner, 500, 4, []),
        ],
        [
            new EquipmentLoadout(owner, []),
            new EquipmentLoadout(otherOwner, []),
        ]);

        ItemSystemResult created = ItemSystem.Create(initial, catalog);
        True(created.Accepted, created.RejectionCode);

        ItemSystemResult equipped = ItemSystem.Equip(created.State, owner, sourceId, bladeId, catalog);
        True(equipped.Accepted, equipped.RejectionCode);
        Equal(2, equipped.State.EquipmentLoadouts.Single(value => value.OwnerId == owner).SlotAssignments.Length,
            "Two-handed equipment did not claim both required slots.");

        ItemSystemResult duplicateEquip = ItemSystem.Equip(equipped.State, owner, sourceId, helmId, catalog);
        True(duplicateEquip.Accepted, duplicateEquip.RejectionCode);
        ItemSystemResult conflictingEquip = ItemSystem.Equip(duplicateEquip.State, owner, sourceId, competingBladeId, catalog);
        False(conflictingEquip.Accepted, "Conflicting two-handed equipment occupied the same slots.");
        Equal(ItemRejectionCodes.SlotConflict, conflictingEquip.RejectionCode, "Slot-conflict rejection was not stable.");
        True(ReferenceEquals(duplicateEquip.State, conflictingEquip.State), "A rejected equip replaced authoritative state.");

        ItemSystemResult transferred = ItemSystem.Transfer(duplicateEquip.State, bladeId, destinationId, catalog);
        True(transferred.Accepted, transferred.RejectionCode);
        Equal(destinationId, transferred.State.ItemInstances.Single(value => value.InstanceId == bladeId).OwnerContainerId,
            "Transfer did not change the item's authoritative owner.");
        False(transferred.State.EquipmentLoadouts.Single(value => value.OwnerId == owner).SlotAssignments
            .Any(value => value.ItemInstanceId == bladeId),
            "Transfer left the transferred item equipped in the source owner's loadout.");

        ItemSystemResult wrongOwnerEquip = ItemSystem.Equip(transferred.State, owner, sourceId, bladeId, catalog);
        False(wrongOwnerEquip.Accepted, "An item in another container was equipped by the source owner.");
        Equal(ItemRejectionCodes.OwnershipMismatch, wrongOwnerEquip.RejectionCode, "Ownership rejection was not stable.");
        True(ReferenceEquals(transferred.State, wrongOwnerEquip.State), "A rejected item command replaced authoritative state.");

        ItemSystemResult added = ItemSystem.AddStack(
            transferred.State, sourceId, ammunitionEntryId, ammunition.Id, 10, catalog);
        True(added.Accepted, added.RejectionCode);
        Equal(10, added.State.InventoryEntries.Single().Stack.Quantity, "AddStack did not publish its quantity.");

        ItemSystemResult split = ItemSystem.SplitStack(added.State, ammunitionEntryId, splitEntryId, 4, catalog);
        True(split.Accepted, split.RejectionCode);
        Equal(6, split.State.InventoryEntries.Single(value => value.EntryId == ammunitionEntryId).Stack.Quantity,
            "SplitStack charged the wrong source quantity.");

        ItemSystemResult merged = ItemSystem.MergeStack(split.State, splitEntryId, ammunitionEntryId, catalog);
        True(merged.Accepted, merged.RejectionCode);
        Equal(10, merged.State.InventoryEntries.Single().Stack.Quantity, "MergeStack lost ammunition.");

        ItemSystemResult partialTransfer = ItemSystem.TransferStack(
            merged.State, ammunitionEntryId, destinationId, 4, transferredEntryId, catalog);
        True(partialTransfer.Accepted, partialTransfer.RejectionCode);
        Equal(6, partialTransfer.State.InventoryEntries.Single(value => value.EntryId == ammunitionEntryId).Stack.Quantity,
            "TransferStack charged the wrong source quantity.");
        Equal(destinationId, partialTransfer.State.InventoryEntries.Single(value => value.EntryId == transferredEntryId).OwnerContainerId,
            "TransferStack did not publish destination ownership.");

        ItemSystemResult consumed = ItemSystem.Consume(partialTransfer.State, transferredEntryId, 4, catalog);
        True(consumed.Accepted, consumed.RejectionCode);
        False(consumed.State.InventoryEntries.Any(value => value.EntryId == transferredEntryId),
            "Consume retained an exhausted stack.");

        ItemSystemResult overLimit = ItemSystem.AddStack(
            consumed.State, destinationId, transferredEntryId, ammunition.Id, ammunition.MaximumStackSize + 1, catalog);
        False(overLimit.Accepted, "AddStack accepted more than maximumStackSize.");
        Equal(ItemRejectionCodes.StackLimitExceeded, overLimit.RejectionCode, "Stack-limit rejection was unstable.");
        True(ReferenceEquals(consumed.State, overLimit.State), "A rejected stack command replaced authoritative state.");
    }
}
