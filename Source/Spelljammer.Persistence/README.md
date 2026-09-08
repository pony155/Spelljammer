# Spelljammer.Persistence

This headless project owns the versioned, content-locked campaign save format,
bounded preflight and reconstruction, transactional campaign publication,
durable same-directory replacement, recovery artifacts, and explicit
migrations. See
[`../../Docs/Architecture/CampaignSaves.md`](../../Docs/Architecture/CampaignSaves.md)
for the implemented contract.

It deliberately has no WPF, localization, SpriteForge, or native dependency.
Callers supply the immutable `GameContentSnapshot` selected for a load; saved
stable IDs are resolved and runtime indices are reconstructed only inside that
validated boundary.

Save schema 11 preserves campaign and encounter character resources, turn state,
and complete item-instance ownership: containers, equipped slots, durability,
melee energy, ranged ammunition, energy, and heat, and stackable inventory
entries. It also persists authored Status instances, including source, target,
duration, stacks, potency, and definition revision. Superseded character and
encounter `activeEffects` records from schemas 7 through 10 remain readable but
are discarded during deterministic migration; ongoing mechanics use Statuses.
