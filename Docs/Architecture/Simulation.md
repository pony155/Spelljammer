# Space-expedition simulation

## Implemented boundary

`Source/Spelljammer.Simulation` is a headless .NET project that owns the first
authoritative gameplay state. It does not reference WPF, SpriteForge,
localization, wall-clock APIs, or mutable presentation objects.

`ExpeditionSimulation.Create(seed)` creates a run. `Apply(state, command)` is
the only state-transition boundary. Accepted commands return a new immutable
state; rejected commands return the original state and a stable
`CommandRejection` value. The current 4 × 4 chart is bounded by a 16-bit visit
mask and a 16-bit salvage mask, so growth cannot create unbounded work.

Sector properties are derived from the run seed and stable row-major sector ID
with an integer mixing function. The same seed and command stream therefore
produce the same hazards, rewards, and state on every host. Presentation maps
the resulting enums and numbers to player-facing text.

## Prototype rules

- Travel costs one fuel and exposes the hull to the destination's danger.
- Every third accepted action consumes one supply.
- Each non-anchorage sector can be salvaged once.
- Salvage produces cargo and may contain a fuel cache.
- Repair consumes two cargo and restores up to three hull.
- A run succeeds by returning to the anchorage with at least eight cargo.
- Zero hull or zero supplies ends the run.

These constants remain compiled prototype rules. The implemented campaign-save
boundary persists the detailed `VoyageWorld`; replacing this legacy expedition
layer with content-driven rules remains planned before saves are exposed in the
public WPF flow.

## Detailed voyage and saves

The headless fixed-tick `VoyageWorld` now supplies stable ship, crew, module,
encounter, command, schedule, and event identities. The Milestone 6 persistence
layer serializes that state through stable IDs and reconstructs it only after
content preflight. See [`CampaignSaves.md`](CampaignSaves.md). Connecting this
detailed world and its save controls to the current expedition WPF shell is
still planned.

The staged plan is defined in
[`ImplementationRoadmap.md`](ImplementationRoadmap.md). Gameplay content and
character capability contracts are defined in
[`ContentPacks.md`](ContentPacks.md) and
[`CharacterCapabilities.md`](CharacterCapabilities.md). The planned untrusted
mod boundary is defined in [`Modding.md`](Modding.md).
