# Battle Map Implementation Roadmap

## 1. Purpose

This document turns the target design in [BattleMap.md](BattleMap.md) into an
incremental implementation plan. The target experience is a continuous-looking
map backed by a deterministic hidden square grid, with real-time exploration
and turn-based personal combat sharing the same persistent environment.

This is a roadmap, not a statement that every described feature already
exists. Each phase must leave the repository buildable and preserve the
simulation/presentation boundary.

## 2. Current Baseline

The repository already has a useful headless foundation:

- authored `PersonalBoardDefinition`, `BoardCellDefinition`, and
  `ZoneLinkDefinition` content;
- an immutable `TacticalBoard` with bounded cells and links;
- deterministic connectivity checks and breadth-first pathfinding;
- bounded occupancy and per-cell capacity;
- authored cover, visibility, atmosphere, gravity, and hazard tags;
- `BattleUnitState` units with Turn Meter, Action Points, resources, equipment,
  injuries, and Status instances;
- fixed-tick Ready unit processing and typed personal commands;
- melee, ranged, spell, psionic, item, Effect, and Status subsystems; and
- campaign save schema 13 persistence for encounter units and board state.

The current board is closer to a small linked zone/hex encounter graph than the
dense hidden square grid described by `BattleMap.md`. Movement currently uses
unweighted links, a move command reaches its destination in one commit, and
the map does not yet derive line of sight, attack cover, procedural geometry,
visual chunks, or exploration/combat transitions.

The WPF host also does not yet present the personal tactical board.

## 3. Architecture Gate: One Personal-Map Topology

Before adding more map mechanics, the project must settle on one authoritative
personal-map topology.

The target selected by `BattleMap.md` is:

```text
Hidden square logic grid
        -> deterministic gameplay queries
        -> continuous presentation coordinates
```

The migration should preserve stable `CellId` identity while replacing the
hex-oriented `Q`/`R` meaning with an explicit square-grid coordinate contract.
Authored links remain useful for doors, ladders, hatches, ducts, one-way
connections, and other exceptional traversal. Ordinary neighboring floor
cells should not require hand-authored links.

Phase 0 must also reconcile `Docs/Concept/Battle.md`, which currently describes
personal combat as hex based. New gameplay work must not support square and hex
personal maps simultaneously unless a later concrete requirement justifies a
versioned topology abstraction.

## 4. Delivery Principles

- Implement an authored-map vertical slice before procedural generation.
- Keep authoritative state in `Spelljammer.Simulation`; WPF and SpriteForge
  only present snapshots and submit commands.
- Use integer or fixed-point values for path costs, world conversion, and
  combat queries. Rendering interpolation must not affect results.
- Put movement costs, cell scale, cover values, and encounter tuning in
  versioned content definitions. Code may enforce bounded safety ceilings but
  must not contain balance values that belong in JSON.
- Separate immutable cell definitions from mutable cell state such as open
  doors, destroyed cover, fire, pressure, and discovered information.
- Validate replacement maps completely before publishing them.
- Make all searches and per-tick work bounded, deterministic, and ordered by
  stable IDs when costs tie.
- Preserve one environment across exploration and combat; mode changes should
  not recreate the map.

## 5. Roadmap Overview

| Phase | Deliverable | Depends on | Exit condition |
| --- | --- | --- | --- |
| 0 | Topology and contract alignment | Current baseline | Square-grid contract is documented and conflicting hex claims are removed |
| 1 | Square-grid simulation kernel | Phase 0 | Bounded map creation, occupancy, neighbors, and deterministic weighted paths work headlessly |
| 2 | Authored battle-map vertical slice | Phase 1 | One content-authored map loads, validates, deploys units, and supports objectives and exits |
| 3 | Movement planning and commit | Phase 2 | Reachability, path preview, AP cost, interruption, and atomic movement use the same rules |
| 4 | LOS, cover, and combat integration | Phase 3 | Attack legality and protection are derived from map state and Effects update encounter units atomically |
| 5 | Tactical presentation | Phases 2-4 | The player can inspect and complete the authored encounter through the visible game host |
| 6 | Exploration/combat continuity | Phase 5 | One map transitions between real-time exploration and tactical combat without state loss |
| 7 | Procedural map generation | Phases 1-6 | Seeded generation produces validated, replayable maps from authored modules |
| 8 | Dynamic environment | Phases 4 and 7 | Doors, damage, hazards, atmosphere, and local rebuilds persist and affect gameplay |
| 9 | AI and content expansion | Phases 4 and 8 | AI uses the same bounded queries and multiple encounter themes are playable |
| 10 | Persistence, diagnostics, and optimization | All prior phases | Save migration, profiling budgets, debugging tools, and failure diagnostics are production ready |

## 6. Phase 0 - Align the Contracts

### Work

- Record square grid as the only planned personal-map topology.
- Define whether ordinary movement permits four-way or eight-way adjacency.
- Define the deterministic integer movement metric, including diagonal rules if
  diagonals are allowed.
- Define grid-to-world origin, axis direction, cell scale, and rotation without
  placing floating-point world coordinates in authoritative state.
- Decide how existing `Q`/`R` content migrates to `X`/`Y` or a
  `GridCoordinate` value.
- Keep authored graph links only for exceptional traversal and stateful edges.
- Update `Battle.md`, `BattleMap.md`, content schemas, and naming together.

### Exit criteria

- One coordinate and adjacency model is authoritative.
- Equal-cost path tie-breaking is documented.
- Save and content-fingerprint consequences are documented before schema
  changes are committed.
- No public type claims to support both square and hex maps accidentally.

## 7. Phase 1 - Build the Square-Grid Kernel

### Static map definition

Introduce or evolve definitions for:

- map width, height, cell scale, and valid/void mask;
- stable cell identity and square coordinate;
- base terrain and movement cost;
- walkability, elevation, capacity, cover, and visibility;
- room or zone identity;
- atmosphere, gravity, and initial hazard references; and
- exceptional edges such as doors, ladders, hatches, and one-way traversal.

### Runtime map state

Keep mutable state separate:

- occupants and multi-cell reservations;
- blocked or temporarily reserved cells;
- door and barrier state;
- damaged or destroyed cover;
- active hazards and environmental values; and
- player-team discovery and visibility state.

### Queries

Implement bounded deterministic queries for:

- coordinate and `CellId` lookup;
- ordinary and exceptional neighbors;
- placement, removal, and atomic movement;
- weighted shortest path;
- reachable cells within an AP or movement budget; and
- connected-component validation for required deployment, objective, and exit
  areas.

Pathfinding should use a deterministic priority queue. Equal-cost candidates
must resolve by documented coordinate order and then stable `CellId`, never by
hash-map enumeration order.

### Exit criteria

- Invalid dimensions, duplicate coordinates, illegal edges, unreachable
  required areas, and capacity overflow are rejected before publication.
- Path results are identical for the same map and request.
- Rejected placement and movement return the original immutable state.
- Search work stops at explicit limits with an observable rejection code.

## 8. Phase 2 - Deliver One Authored Vertical Slice

Convert the existing ruin encounter into the first dense square-grid map before
adding a generator.

The slice should include:

- a deployment area for each participating team;
- at least one objective and one legal retreat or extraction route;
- rooms connected by a stateful doorway;
- open floor, blocked cells, half cover, and full cover;
- one visible but initially non-damaging hazard;
- enough alternate paths to demonstrate a tactical route choice; and
- stable localization keys for player-visible map and objective text.

The content compiler should validate map dimensions, cell counts, coordinate
uniqueness, referenced terrain and environment definitions, spawn capacity,
objective reachability, and safety ceilings. The map must participate in the
semantic content fingerprint.

### Exit criteria

- Content validation publishes the complete map transactionally.
- Deployment cannot overlap or exceed cell capacity.
- Every required objective has a legal initial route from the appropriate
  deployment area.
- The encounter can be completed or abandoned entirely through headless
  commands.

## 9. Phase 3 - Movement Planning and Commit

Add a movement request and preview that share one rules implementation.

The preview should report:

- reachable cells;
- selected path;
- total AP and movement cost;
- dangerous or conditionally traversable cells;
- the first currently invalid step; and
- whether the route depends on a door, special access, or traversal action.

The commit boundary should:

1. validate the actor, activation, current origin, and requested destination;
2. recompute or validate the path against the current map revision;
3. reserve AP and any explicit traversal resources;
4. process steps in deterministic order;
5. allow declared reactions or hazards at cell-entry boundaries; and
6. atomically publish the final legal state and event history.

Visual interpolation follows the committed cell path and cannot move the actor
in simulation state.

### Exit criteria

- Preview and commit calculate the same cost from the same snapshot.
- A stale or newly blocked route cannot move an actor through an illegal cell.
- Movement AP comes from data-driven profiles and terrain/edge definitions.
- Occupancy, AP, map revision, and events commit together or not at all.

## 10. Phase 4 - LOS, Cover, and Combat Integration

Use one deterministic grid query for simulation LOS. World-space raycasts may
refine presentation, but they must not decide authoritative hit legality.

Implement:

- supercover or equivalent square-grid line traversal;
- opaque cell and edge blocking;
- elevation and doorway participation;
- directional cover from the attacker-target line;
- visible, obscured, and blocked target states;
- attack preview with range, LOS, cover, and known modifiers; and
- bounded area and cone cell selection for later Effect delivery.

Replace the simple `WorldCommand.Amount` personal-damage path with the existing
melee, ranged, spell, psionic, and Effect systems. Encounter commit should pass
map-derived range and cover into action eligibility, resolve the emitted Effect
batch, and publish the returned Health, Armor, Shield, and Status state back to
the target actor atomically.

### Exit criteria

- The map, not UI input, determines range, LOS, and cover.
- Preview exposes only information currently known to the acting team.
- Identical attack inputs produce identical Effect invocation IDs and results.
- Failed Effect resolution does not spend costs or partially mutate either
  actor.
- Incapacitation, surrender, reactions, objectives, and cleanup consume the
  committed result rather than a parallel damage implementation.

## 11. Phase 5 - Tactical Presentation

Add a game-owned tactical presentation over immutable simulation snapshots.
Reusable rendering, input, camera, picking, and chunk APIs belong in
SpriteForge when a concrete engine requirement exists.

The first presentation slice needs:

- continuous floor, wall, doorway, obstacle, and actor placement;
- camera pan, zoom, bounds, and deterministic cell picking;
- current actor and target selection;
- reachable-area, path, destination, AP-cost, cover, and hazard overlays;
- attack previews and rejection reasons;
- turn order or readiness, resources, actions, and end-activation controls;
- interpolation between committed cells; and
- an optional debug grid and cell-inspection overlay excluded from gameplay
  state.

Permanent grid lines remain hidden during normal play. Tactical overlays appear
only when they communicate a decision.

### Exit criteria

- The authored vertical slice is playable from deployment through cleanup.
- Resizing, frame rate, animation speed, and camera movement do not change
  simulation results.
- The renderer can rebuild only dirty visual chunks after a local map change.
- All player-visible text comes from localization catalogs.

## 12. Phase 6 - Exploration and Combat Continuity

Introduce an explicit mode state over one persistent map:

```text
Exploration -> detection/hostility -> tactical combat -> cleanup -> exploration
```

Exploration movement may look continuous, but authoritative position still
resolves against the logical map. Entering combat should freeze at a completed
simulation boundary, determine participants and initial readiness, preserve
doors and hazards, and reveal only justified information. Leaving combat should
remove tactical UI without reconstructing the environment.

### Exit criteria

- Opening a door, moving an object, or damaging cover before combat produces
  the same state when combat begins.
- Combat damage and environmental changes remain after exploration resumes.
- Repeated transition attempts are idempotent and cannot duplicate units,
  objectives, loot, or Effects.
- Save/load works in either mode and at the transition boundary.

## 13. Phase 7 - Seeded Procedural Generation

Build generation from reviewed authored modules rather than unconstrained cell
noise.

Recommended pipeline:

```text
seed and encounter context
  -> mission topology
  -> room/zone modules
  -> square-grid placement
  -> corridors and exceptional edges
  -> connectivity and objective validation
  -> cover, hazards, and deployment
  -> faction visual theme
  -> visual reconstruction inputs
```

Separate random streams by generation stage so adding decoration cannot change
topology, objectives, or combat placement. Failed candidates must retry through
a bounded, deterministic policy and eventually return an explicit generation
failure.

### Exit criteria

- The same seed, content fingerprint, and request produce identical logical
  maps and placements.
- Required objectives and exits are reachable before decoration runs.
- Generator retries and total work are bounded.
- Faction themes change presentation and authored module selection without
  silently changing universal movement rules.

## 14. Phase 8 - Dynamic Environment

Add mutable environmental systems in small vertical slices:

1. doors and locks;
2. destructible obstacles and cover;
3. fire or another spreading hazard;
4. atmosphere and pressure regions;
5. hull breaches and decompression; and
6. gravity, lighting, and visibility changes.

Each system should use commands, Events, Effects, or Statuses rather than direct
cross-system mutation. Propagation runs on fixed ticks with explicit cell and
work budgets. A changed cell invalidates only affected path, LOS, and visual
chunks.

### Exit criteria

- Environmental state is deterministic, bounded, saved, and replayable.
- A hazard cannot spread through an invalid or sealed edge.
- Dynamic blockers invalidate stale paths before movement commits.
- Gameplay collision and cover come only from promoted gameplay objects;
  decoration remains non-authoritative.

## 15. Phase 9 - AI and Content Expansion

AI must consume the same legal-action, reachability, LOS, cover, hazard, and
Effect previews available to players. It receives an immutable observation
snapshot and a bounded planning budget.

Expand content only after the authored ruin slice is stable:

- boarding and ship interiors;
- EVA and hull exteriors;
- stations or settlements;
- planetary surfaces; and
- large units with explicit multi-cell footprints.

### Exit criteria

- AI never bypasses movement, visibility, resource, or action rules.
- Equal observations and seeds produce the same selected plan.
- Planning exhaustion has a safe fallback such as defend, wait, or retreat.
- At least two visual themes can reconstruct the same logical layout without
  changing its fingerprinted gameplay rules.

## 16. Phase 10 - Persistence, Diagnostics, and Optimization

Finalize the public contract only after the vertical slices establish the
required state.

Work includes:

- versioned DTOs and migrations for map coordinates and mutable cell state;
- required-definition preflight for maps, terrain, hazards, and objectives;
- replay diagnostics for paths, LOS, cover, Effects, and generation streams;
- debug views for connectivity, occupancy, movement cost, visibility, room
  identity, atmosphere, and dirty chunks;
- measured budgets for pathfinding, LOS, AI candidates, hazard propagation,
  snapshot size, and render rebuilds; and
- corruption, bounds, rollback, determinism, and migration coverage.

Do not optimize by moving gameplay authority into rendering or by retaining
mutable native pointers in simulation or save state.

### Exit criteria

- Supported old saves either migrate deterministically or fail during preflight
  with an actionable diagnostic.
- Large supported maps stay within documented simulation and rendering budgets.
- Debug tooling can explain why a destination, target, or generated map was
  rejected.
- Full replay from the same initial save and command stream reproduces the same
  authoritative map state.

## 17. First Playable Battle-Map Milestone

The first milestone ends after Phase 5. It intentionally contains:

- one authored square-grid ruin;
- one player team and one hostile team;
- movement, melee, ranged attacks, cover, a door, an objective, and retreat;
- individual Turn Meter activations and data-driven AP;
- Effect/Status-based consequences;
- hidden-grid continuous presentation and tactical overlays; and
- save/load of the active encounter.

It intentionally postpones procedural generation, large creatures, complex
verticality, spreading hazards, decompression, and multiple biome families.
Those features should build on a proven map and combat loop rather than delay
the first playable result.

## 18. Definition of Done for Every Phase

A phase is complete only when:

- the authoritative state and ownership boundary are explicit;
- authored values are validated and fingerprinted;
- success, rejection, capacity, rollback, and deterministic replay contracts
  are represented in test source;
- save and migration impact is documented;
- relevant architecture and design documents describe implemented behavior
  separately from planned behavior;
- the full solution builds with warnings treated as errors; and
- the final diff contains no generated clutter, developer-specific paths, or
  unintended changes outside the phase.
