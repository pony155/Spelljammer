# Data-driven gameplay implementation roadmap

## Status and purpose

This document sequences the transition from the compiled expedition prototype
to data-driven, mod-ready gameplay. Checked tasks describe implemented work;
unchecked tasks remain planned.

The current headless runtime has immutable expedition state, bounded content
loading, an eleven-character roster, character capabilities, a fixed-tick
`VoyageWorld`, modular ship and personal encounters, and content-locked campaign
saves. Those detailed systems and save controls are not yet connected to the
WPF shell, and local mod configuration, factions, and crises remain planned.

The transition is incremental. The existing 4 × 4 expedition remains buildable
until a replacement slice satisfies its contracts. New architecture is added
beside it instead of through one large rewrite.

## How to use this roadmap

Task IDs use `M<milestone>.<phase>.<task>`, for example `M2.3.2`. Within a
milestone, phases are completed in numeric order unless a task explicitly says
it can proceed independently. A task is intended to be one small, reviewable
change that keeps the solution buildable.

A task is complete only when:

- its source, project, content, and documentation changes are present;
- affected compile-time coverage exists;
- invalid and capacity behavior is represented where applicable;
- applicable formatting and `git diff --check` pass; and
- implemented/planned status text remains accurate.

Checkboxes track implementation, not document completion. They remain unchecked
until the corresponding source or content work exists. A phase may be split
further during implementation, but its stable task IDs should remain in commit
or issue descriptions so dependencies are traceable.

## Target dependency order

```text
Stable IDs and definition interfaces
  ↓
Manifest parsing, validation, and immutable content registry
  ↓
Base Attribute and Skill data
  ↓
Character creation, state, actions, and training
  ↓
Feat/Perk access, Spells, and Psychic abilities
  ↓
Ship modules, encounters, and Battle
  ↓
Content-locked saves and local Mods
  ↓
Galaxy generation and Factions
  ↓
Endgame crises
```

Save-oriented IDs and versions begin early, but public compatibility is not
declared until content locking and migrations exist.

## Planned source layout

```text
Source/
  Spelljammer.Simulation/
    ContentIds/
    Definitions/
    Characters/
    Actions/
    Training/
    Ships/
    Encounters/
    Events/
    Persistence/
  Spelljammer.Content/
    Manifests/
    SourceDocuments/
    Loading/
    Linking/
    Validation/
    Compilation/
    Diagnostics/
Tools/
  Spelljammer.Content.Compiler/
Tests/
  Spelljammer.Content.Tests/
  Spelljammer.Simulation.Tests/
Content/
  Packs/base/
```

Directories are created only with their first focused type. New projects are
added explicitly to `Spelljammer.slnx`.

## Milestone 0: freeze version 1 contracts

**Status: Complete.** The final audit is recorded in
[`Milestone0Review.md`](Milestone0Review.md).

### Phase M0.1: inventory existing identities

- [x] **M0.1.1** Extract every current Attribute, Skill, Feat, Perk, Access,
  Spell, psychic-technique, combat-context, ship-module, faction, crisis,
  authored travel-event, and event-choice ID from `Docs/DesignConcept` into the
  reviewed
  [`ContentIdInventory.md`](ContentIdInventory.md).
- [x] **M0.1.2** Mark each ID as base-owned, example-only, deferred, or already
  serialized by the prototype.
- [x] **M0.1.3** Find spelling variants and collisions without silently renaming
  any established ID.

### Phase M0.2: freeze syntax and version rules

- [x] **M0.2.1** Specify the stable-ID grammar, maximum UTF-8 byte length,
  reserved base namespaces, and third-party namespace form.
- [x] **M0.2.2** Specify pack, definition, schema, semantic, generator, formula,
  effect, and save version responsibilities.
- [x] **M0.2.3** Decide which changes are presentation-only, compatible semantic
  revisions, or migration-requiring breaks.
- [x] **M0.2.4** Record ordinal comparison and canonical serialization rules.

### Phase M0.3: freeze bounds and diagnostics

- [x] **M0.3.1** Propose initial bounds for packs, files, bytes, JSON depth,
  strings, definitions, references, graphs, and diagnostics.
- [x] **M0.3.2** Assign stable diagnostic codes for parse, dependency,
  namespace, reference, semantic, and capacity failures.
- [x] **M0.3.3** Define safe diagnostic arguments and rules for hiding absolute
  paths and protected gameplay information.

### Phase M0.4: freeze fixtures and fingerprints

- [x] **M0.4.1** Author the smallest valid base manifest and one valid document
  for Attribute, Skill, Feat, Perk, and Access references.
- [x] **M0.4.2** Author one focused invalid fixture for every initial diagnostic.
- [x] **M0.4.3** Define canonical semantic bytes and expected fixture hashes.
- [x] **M0.4.4** Review all version 1 decisions before loader code begins.

Deliverables:

- approve ID grammar, definition revision rules, manifest schema, UTF-8
  handling, pack ordering, duplicate policy, and initial limits;
- confirm exact integer ranges for base Attributes and Skills;
- assign stable diagnostic and command-rejection codes;
- define canonical semantic fingerprint serialization; and
- create minimal valid and invalid JSON fixtures.

Exit criteria:

- [`ContentPacks.md`](ContentPacks.md),
  [`CharacterCapabilities.md`](CharacterCapabilities.md), and
  [`Modding.md`](Modding.md) contain no unresolved decision needed by the first
  loader;
- IDs match current product documents; and
- fixtures cover an Attribute, Skill, Feat, Perk, and their references.

## Milestone 1: content foundation

Add `Spelljammer.Content` without changing expedition behavior.

### Phase M1.1: scaffold projects and ownership

- [x] **M1.1.1** Create `Source/Spelljammer.Content` targeting the repository's
  existing .NET baseline with nullable references and warnings as errors.
- [x] **M1.1.2** Reference Simulation from Content without adding the reverse
  reference.
- [x] **M1.1.3** Add `Tests/Spelljammer.Content.Tests` and the future compiler
  project to `Spelljammer.slnx` with minimal buildable entry points.
- [x] **M1.1.4** Document public namespaces and reject filesystem work inside
  Simulation.

### Phase M1.2: implement IDs and diagnostics

- [x] **M1.2.1** Implement a shared validated `ContentId` core and focused typed
  wrappers needed by the first schemas.
- [x] **M1.2.2** Make default IDs invalid and comparison ordinal.
- [x] **M1.2.3** Implement bounded structured diagnostics with stable codes,
  severity, pack, relative path, definition ID, and property path.
- [x] **M1.2.4** Add compile-only contracts for valid, invalid, default, maximum-
  length, and culture-independent IDs.

### Phase M1.3: parse and validate manifests

- [x] **M1.3.1** Implement strict UTF-8 reading with byte and JSON-depth limits.
- [x] **M1.3.2** Reject duplicate properties, unknown required schema versions,
  invalid numeric values, absolute paths, and traversal segments.
- [x] **M1.3.3** Parse the version 1 manifest into a temporary DTO.
- [x] **M1.3.4** Validate pack ID, semantic version, game range, roots,
  dependencies, `loadAfter`, and namespace claims.

### Phase M1.4: discover and order packs

- [x] **M1.4.1** Accept only explicit configured roots and resolve each once.
- [x] **M1.4.2** Read at most one bounded manifest from each root.
- [x] **M1.4.3** Build the bounded dependency graph and reject missing nodes,
  incompatible ranges, duplicate pack IDs, and cycles.
- [x] **M1.4.4** Produce deterministic topological order with ordinal pack-ID
  tie-breaking.

### Phase M1.5: build the candidate pipeline

- [x] **M1.5.1** Enumerate normalized relative definition paths in ordinal order.
- [x] **M1.5.2** Parse minimal generic source envelopes without exposing them to
  Simulation.
- [x] **M1.5.3** Validate definition-kind and namespace ownership and reject
  duplicate stable IDs.
- [x] **M1.5.4** Add explicit Link, Validate, and Compile stages with bounded
  work and stage-specific diagnostics.

### Phase M1.6: compile, fingerprint, and publish

- [x] **M1.6.1** Create an immutable minimal `GameContentSnapshot`.
- [x] **M1.6.2** Canonicalize semantic content and calculate its fingerprint.
- [x] **M1.6.3** Add a registry owner that publishes one complete snapshot only
  after successful validation.
- [x] **M1.6.4** Verify failed replacement preserves the prior snapshot and its
  fingerprint.

### Phase M1.7: expose offline validation

- [x] **M1.7.1** Add a noninteractive compiler command for one pack root.
- [x] **M1.7.2** Emit concise text diagnostics and a bounded machine-readable
  form.
- [x] **M1.7.3** Return stable nonzero exit status for invalid content.
- [x] **M1.7.4** Confirm the tool performs no network access or source rewrite.

Deliverables:

- strongly typed ID parsing;
- `ContentLimits`, manifest DTOs, strict bounded JSON, and diagnostics;
- explicit-root discovery and deterministic dependency ordering;
- candidate source ownership with no partial publication;
- immutable minimal `GameContentSnapshot`; and
- an offline compiler command that validates one pack.

Coverage includes malformed UTF-8, duplicate JSON properties, invalid IDs,
oversized values, traversal, missing dependencies, cycles, deterministic order,
and rollback after publication failure.

Exit criterion: repeated compilation produces equivalent canonical semantic
content and the same fingerprint.

## Milestone 2: base Attributes and Skills

Move only Attribute and Skill lists into `Content/Packs/base`.

### Phase M2.1: define Attribute documents

- [x] **M2.1.1** Create source DTO, immutable definition, validator, and
  diagnostic codes for Attributes.
- [x] **M2.1.2** Validate ID, revision, localization keys, minimum, maximum,
  default, and bounded tags.
- [x] **M2.1.3** Add the seven base Attribute JSON files with the IDs and 1–10
  range from `Attributes.md`.
- [x] **M2.1.4** Add focused valid, missing-field, invalid-range, duplicate-tag,
  and capacity fixtures.

### Phase M2.2: define Skill documents

- [x] **M2.2.1** Create source DTO, immutable definition, validator, and
  diagnostics for Skills.
- [x] **M2.2.2** Validate the 0–100 range, progression-curve reference,
  localization keys, and bounded action tags.
- [x] **M2.2.3** Add every Skill currently listed in `Skills.md` to the base
  pack without changing spelling or identity.
- [x] **M2.2.4** Add focused unknown-curve, invalid-range, duplicate-tag, and
  capacity fixtures.

### Phase M2.3: compile typed registries

- [x] **M2.3.1** Link Attribute and Skill references through typed IDs.
- [x] **M2.3.2** Sort stable IDs and assign fingerprint-scoped dense indices.
- [x] **M2.3.3** Expose immutable lookup by typed ID and deterministic iteration.
- [x] **M2.3.4** Reject use of a dense index with a different registry
  fingerprint.

### Phase M2.4: connect localization and inspection

- [x] **M2.4.1** Validate required default-locale keys without introducing a
  Simulation-to-Localization dependency.
- [x] **M2.4.2** Add missing base names and descriptions to the authored
  localization source.
- [x] **M2.4.3** Add an offline registry report listing IDs, provenance,
  revisions, indices, and counts.
- [x] **M2.4.4** Project registry entries into an inspection-only WPF view or a
  headless snapshot adapter.

### Phase M2.5: prove dynamic content

- [x] **M2.5.1** Create an additive fixture pack defining one namespaced Skill.
- [x] **M2.5.2** Compile base-only and base-plus-fixture snapshots repeatedly.
- [x] **M2.5.3** Verify the added Skill appears without changing Simulation
  enums, character properties, or UI layout code.
- [x] **M2.5.4** Verify disabling the fixture restores the exact base-only
  fingerprint and registry.

Deliverables:

- source schemas and compiled registries;
- seven base Attribute definitions with existing IDs;
- initial Skill definitions with existing IDs;
- deterministic dense indices scoped to the fingerprint;
- default-localization-key validation; and
- registry inspection through a diagnostic report or temporary view.

Do not remove prototype `SectorKind` or expedition constants here. Do not add a
switch over known Attribute or Skill IDs to the new implementation.

Exit criterion: a fixture mod can add a non-core Skill and generic registry,
iteration, diagnostics, and UI projection see it without a source change.

## Milestone 3: character capability slice

**Status: Complete.** The headless character slice is data-driven and ships
with an eleven-character first-voyage roster. WPF remains on the expedition
prototype; `RosterInspection` is the localization-ready inspection adapter.

### Phase M3.1: add character definition kinds

- [x] **M3.1.1** Add typed IDs and schemas for Character, Race, Heritage,
  Background, Perk, Feat, Access, Technique, and Training Project.
- [x] **M3.1.2** Implement compatibility and grant-reference linking.
- [x] **M3.1.3** Add minimal base definitions for one Race, Heritage,
  Background, and their Perk grants.
- [x] **M3.1.4** Reject incompatible Heritage, missing grant, and grant cycles.

### Phase M3.2: implement generic capability storage

- [x] **M3.2.1** Add bounded immutable Attribute and Skill value storage indexed
  by the compiled registry.
- [x] **M3.2.2** Add bounded Feat, Perk, Technique, and grant-source sets.
- [x] **M3.2.3** Implement typed lookup that returns explicit missing-definition
  failures rather than default values.
- [x] **M3.2.4** Produce a read-only character capability snapshot.

### Phase M3.3: implement deterministic character creation

- [x] **M3.3.1** Define a typed creation request containing content fingerprint,
  scenario, definition IDs, and explicit seed.
- [x] **M3.3.2** Allocate Attribute and Skill values through bounded authored
  rules and owned random streams.
- [x] **M3.3.3** Grant Race and Heritage Perks with provenance.
- [x] **M3.3.4** Validate the complete character before assigning its stable ID
  and publishing state.

### Phase M3.4: implement action eligibility

- [x] **M3.4.1** Define minimal Action, Requirement, Cost, Target, and stable
  Rejection contracts.
- [x] **M3.4.2** Check actor, definition, grant, technique, Skill, Attribute,
  equipment, context, and resource requirements in a fixed order.
- [x] **M3.4.3** Reserve costs without mutating published state.
- [x] **M3.4.4** Prove rejection consumes nothing and exposes no protected
  information.

### Phase M3.5: implement resolution and explanation

- [x] **M3.5.1** Implement one allowlisted standard check formula using integer
  or fixed-point arithmetic.
- [x] **M3.5.2** Feed it an explicitly owned deterministic random stream.
- [x] **M3.5.3** Commit result, costs, grants, and events atomically.
- [x] **M3.5.4** Record every contributing ID, modifier, roll, and failure reason
  required for replay and UI explanation.

### Phase M3.6: implement Skill advancement and roster UI

- [x] **M3.6.1** Add bounded practice awards and anti-trivial-repetition rules.
- [x] **M3.6.2** Add deterministic advancement threshold processing.
- [x] **M3.6.3** Expose a roster snapshot that iterates dynamic Attribute and
  Skill registries.
- [x] **M3.6.4** Render localized values and disabled-action reasons without
  direct mutation or fixed-property binding.

### Phase M3.7: author and prove the base race roster

- [x] **M3.7.1** Author all eleven base Race definitions and their Race Perk
  grants, including Eidolon Soul Anchor and Tharun Trail Sense.
- [x] **M3.7.2** Author one compatible first-slice Heritage and Heritage Perk
  for each Race; defer additional Heritage rows until their Perk IDs are
  explicitly inventoried.
- [x] **M3.7.3** Author one deterministic first-slice character per Race with
  attributes, skills, background, position, language, script, and equipment.
- [x] **M3.7.4** Validate mixed-race quarters, equipment, care, nutrition or
  reserve, rest, and environmental compatibility before roster publication.
- [x] **M3.7.5** Prove Soul Anchor recovery requires its anchor and resources,
  and prove Trail Sense consumes only observed evidence without revealing
  hidden state.

Deliverables:

- character, Race, Heritage, Background, Attribute, Skill, Feat, Perk,
  Access, and Technique ID types;
- immutable definition and character-state snapshots;
- data-authored Race, Heritage, Background, Perk, and training definitions;
- deterministic creation with validated grants;
- generic capability lookup and action eligibility;
- bounded Skill practice and training projects;
- stable events explaining grants and advancement; and
- an eleven-character base roster with one representative of every planned
  Race and one first-slice Heritage per Race.

WPF may initially show an inspection-only roster. It enumerates registry data
and resolves localization separately instead of binding fixed Strength or
Engineering properties.

Exit criteria: the same seed and fingerprint create the same eleven-character
roster, an invalid grant prevents publication, new Skills need no state-schema
change, Soul Anchor recovery cannot bypass its costs, and Trail Sense cannot
reveal unobserved information.

## Milestone 4: supernatural access slice

### Phase M4.1: implement access definitions and grants

- [x] **M4.1.1** Add `access.magic` and `access.psionics` base definitions.
- [x] **M4.1.2** Add `feat.access.magic` and `feat.access.psionics` definitions
  with training-project references.
- [x] **M4.1.3** Derive effective access from bounded grant sources rather than
  storing independent booleans.
- [x] **M4.1.4** Verify multiple sources coexist and source removal recomputes
  access correctly.

### Phase M4.2: implement training projects

- [x] **M4.2.1** Add training prerequisites, work units, facility, cost, safety,
  progress cap, and completion grants.
- [x] **M4.2.2** Add start, contribute, cancel, and complete commands.
- [x] **M4.2.3** Keep partial progress from granting partial access.
- [x] **M4.2.4** Commit completion Feat and access provenance atomically.

### Phase M4.3: implement innate access

- [x] **M4.3.1** Add Elf Aether Sense and Somnari Mindwake Perk definitions.
- [x] **M4.3.2** Grant innate access during deterministic character creation.
- [x] **M4.3.3** Grant only named racial abilities, never free Skill ranks or an
  entire technique catalog.
- [x] **M4.3.4** Expose trained versus innate provenance to inspection UI.

### Phase M4.4: implement known-content collections

- [x] **M4.4.1** Add bounded Spell and psychic-technique definition registries.
- [x] **M4.4.2** Add character-known ID sets and learning-project state.
- [x] **M4.4.3** Validate required access, Skill, target, resource, consent, and
  resistance references.
- [x] **M4.4.4** Reject a known technique whose definition is missing from the
  active content fingerprint.

### Phase M4.5: implement one Spell

- [x] **M4.5.1** Author one first-playable Spell, Magic Missile, with complete
  costs and failure behavior.
- [x] **M4.5.2** Implement declare, preview, reserve, prepare, resolve, commit,
  and recover phases.
- [x] **M4.5.3** Persist observable evidence and bounded active effects.
- [x] **M4.5.4** Cover no-access, unknown-Spell, insufficient-resource,
  interruption, success, and deterministic replay cases.

### Phase M4.6: implement one psychic technique

- [x] **M4.6.1** Author Mindlink with access, knowledge, consent, range, strain,
  sustain, and information-scope rules.
- [x] **M4.6.2** Implement invitation, acceptance or rejection, reservation,
  commit, sustain, revocation, and release.
- [x] **M4.6.3** Ensure failed or rejected contact reveals no protected data.
- [x] **M4.6.4** Cover trained and innate users, Psychic Strain, resistance,
  evidence, and replay.

Deliverables:

- `feat.access.magic` grants `access.magic` only after completed training;
- `feat.access.psionics` grants `access.psionics` only after completed training;
- Elf Aether Sense supplies innate magical access;
- Somnari Mindwake supplies innate psychic access and named abilities;
- bounded known Spell and psychic-technique collections;
- commands reject active use without both access and knowledge; and
- one Spell and psychic technique reserve, resolve, publish evidence, and roll
  back atomically.

Exit criterion: trained and innate access produce the same permission through
different inspectable sources, grant no free Skill rank, and replay
deterministically.

## Milestone 5: dual-tempo fixed-tick encounter slice

**Status: Complete (headless vertical slice).** The WPF expedition shell is
unchanged; immutable snapshots and command logs provide its future integration
boundary.

Introduce `VoyageWorld` and a bounded command queue while keeping the expedition
layer as a navigation shell. Ship engagements are real-time with tactical
pause on a continuous 2D coordinate map; personal encounters use a hex board,
individual Turn Meters, and Action Points without global rounds.

### Phase M5.1: establish world time and ownership

- [x] **M5.1.1** Define fixed tick units, maximum catch-up work, ship tactical
  pause, personal Ready pauses, and allowed command-submission boundaries.
- [x] **M5.1.2** Create `VoyageWorld` with seed, content fingerprint, tick, and
  explicitly owned random streams.
- [x] **M5.1.3** Add a bounded typed command queue with stable ordering and
  rejection for overflow or stale targets.
- [x] **M5.1.4** Publish read-only snapshots only after a complete tick commit.

### Phase M5.2: add actors, zones, and placement

- [x] **M5.2.1** Add stable Actor, Team, Space Object, Personal Board, Cell,
  Zone, Link, Encounter, and Objective IDs.
- [x] **M5.2.2** Compile bounded personal-combat hex boards and zone graphs with
  capacity, occupancy, access, visibility, cover, atmosphere, gravity, and
  hazard tags.
- [x] **M5.2.3** Validate legal initial placement, connected required
  objectives, and retreat or one-way rules.
- [x] **M5.2.4** Add deterministic movement and bounded path search.
- [x] **M5.2.5** Add data-authored Equipment definitions and bounded personal
  loadouts: Main hand, Off hand, Body, Utility, and Relic slots; each item uses
  the Ready, Depleted, or Damaged state model from
  [`../DesignConcept/Equipments.md`](../DesignConcept/Equipments.md).

### Phase M5.3: add scheduled actions and effects

- [x] **M5.3.1** Implement declare, validate, reserve, prepare, commit, and
  recover timing in ticks.
- [x] **M5.3.2** Add bounded Turn Meters, Ready queues, AP budgets, action
  schedules, active effects, reactions, and stable equal-tick priority rules.
- [x] **M5.3.3** Implement allowlisted movement, resource, damage, condition,
  detection, and objective primitives.
- [x] **M5.3.4** Prove interruption and failure cannot partially publish or
  duplicate resources.

### Phase M5.4: add ship and module state

- [x] **M5.4.1** Add stable Ship, Frame, Compartment, Module, Ship Weapon
  Configuration, Network, Station, and Resource IDs; revise the content schema
  contract before loading the new weapon-configuration kind. Freeze the small
  stat budget: Hull, Armor, Shield, energy, cargo, slots, and each module's
  single primary effect.
- [x] **M5.4.2** Compile one minimal Arcane and one minimal Industrial energy
  package plus common armor, Energy Shield, prow, and configurable
  cannon definitions. Author an Aether Energy Cannon and Diesel Shell Cannon
  for the first slice, then defer the Atomic Shell Cannon; ship cannons have no
  Gunner position or Gunnery Skill requirement.
- [x] **M5.4.3** Implement a bounded priority order for Power or Aether,
  including the shield's fixed Energy Consumption Rate while raised.
- [x] **M5.4.4** Resolve finite current Shield Value, Armor Value mitigation,
  damage overflow, Hull damage, and selected-module condition in one atomic
  commit.
- [x] **M5.4.5** Validate and transactionally commit one pre-voyage loadout with
  available slots, Armor Value, Energy Shield energy feed, power, propulsion,
  prow effect, cannon resource type, and cargo displacement.

### Phase M5.5: implement a ship engagement

- [x] **M5.5.1** Add real-time-with-pause ship timing, bounded deterministic
  fixed-point 2D coordinates, heading, velocity, collision shapes, derived
  range labels, contact knowledge, firing solutions, cannon damage, rate of
  fire, effective and maximum range, reload time, damage type and area, armor
  penetration, Aether charge or physical ammunition, weapon readiness, maximum
  and current Shield Value, Recharge Rate, Energy Consumption Rate, raised
  state, and disengagement state.
- [x] **M5.5.2** Implement bounded queued scan, course, thrust, turn, brake,
  intercept, fire, ram, raise or lower shield, defend, damage-control, signal,
  and retreat orders with explicit cancellation rules;
  fire orders use ship targeting state without an assigned Gunner.
- [x] **M5.5.3** Add one opponent plan with bounded candidates and a documented
  safe fallback.
- [x] **M5.5.4** Persist module damage, spent resources, knowledge, witnesses,
  and escape results into the voyage state.

### Phase M5.6: implement boarding or ruin combat

- [x] **M5.6.1** Author a six-zone derelict or ruin with one hazard, hostile
  group, ancient defense, non-combat solution, and extraction objective.
- [x] **M5.6.2** Add a bounded hex board and four active crew with individual
  Turn Meters, AP budgets, movement, melee, ranged, defense, Spell or psychic,
  Engineering, and Medicine actions.
- [x] **M5.6.3** Add injury, incapacitation, stabilization, surrender, prisoner,
  retreat, and cleanup rules.
- [x] **M5.6.4** Preserve exploration changes and desired objects damaged during
  combat.

### Phase M5.7: connect presentation and replay

- [x] **M5.7.1** Project safe world, action, objective, and explanation data to
  immutable UI snapshots.
- [x] **M5.7.2** Add tactical-pause ship ordering and Ready-actor personal
  planning without placing WPF objects or wall-clock time in simulation state.
- [x] **M5.7.3** Record a bounded command/event stream sufficient to reproduce
  the slice.
- [x] **M5.7.4** Compare results across different rendering cadence, UI timing,
  and worker completion schedules.

Deliverables:

- actor, ship, space-object, module, personal-board, cell, zone, encounter,
  action, effect, and event IDs;
- explicit ticks, deterministic command ordering, Turn Meters, and AP budgets;
- real-time-with-pause ship commands and non-round-based personal activations;
- continuous ship positions and movement with no ship-combat grid;
- a transactional player loadout covering armor, Energy Shield, power,
  propulsion, prow fitting, cannon configuration, and supporting modules;
- one ship engagement and connected boarding or ruin encounter from
  [`../DesignConcept/Battle.md`](../DesignConcept/Battle.md);
- persistent equipment, injuries, module damage, resources, knowledge, and
  retreat results; and
- read-only presentation snapshots after complete tick commits.

Exit criterion: render cadence, pause duration, UI order, and thread completion
do not change authoritative results; the same personal command stream produces
the same Ready order, AP expenditure, and action timing, while the same ship
orders reproduce identical fixed-point trajectories.

## Milestone 6: content-locked saves

### Phase M6.1: define the save envelope

- [x] **M6.1.1** Specify magic bytes or document discriminator, save schema,
  game build, generator versions, content lock, payload length, and checksum.
- [x] **M6.1.2** Define maximum save bytes, collection counts, nesting, strings,
  and retained history.
- [x] **M6.1.3** Add stable diagnostics for corrupt, oversized, unsupported, and
  truncated saves.
- [x] **M6.1.4** Keep localization text, WPF state, native pointers, and runtime
  dense indices out of the contract.

### Phase M6.2: serialize a minimal campaign

- [x] **M6.2.1** Serialize voyage header, seed, tick, content lock, ship,
  characters, and current location through stable IDs.
- [x] **M6.2.2** Serialize known definitions, training, resources, injuries,
  active effects, and queued work within bounds.
- [x] **M6.2.3** Reconstruct runtime indices only after selecting the compatible
  content snapshot.
- [x] **M6.2.4** Verify stable canonical output for identical authoritative
  state.

### Phase M6.3: implement content preflight

- [x] **M6.3.1** Compare exact pack IDs, versions, semantic fingerprints,
  generator, formula, effect, and save versions.
- [x] **M6.3.2** Return Exact, Compatible, Migratable, Missing, or Incompatible
  before decoding full authoritative state.
- [x] **M6.3.3** List missing packs and definition IDs without guessing from
  display names.
- [x] **M6.3.4** Prevent preflight from altering the active registry or campaign.

### Phase M6.4: validate and publish loads

- [x] **M6.4.1** Decode into a temporary bounded representation.
- [x] **M6.4.2** Resolve every stable reference and validate invariants, values,
  grants, graphs, schedules, and ownership.
- [x] **M6.4.3** Reject partial or unknown state and retain the active campaign.
- [x] **M6.4.4** Publish one complete reconstructed campaign only after all
  validation succeeds.

### Phase M6.5: implement safe file replacement

- [x] **M6.5.1** Write a new save to a same-directory temporary artifact.
- [x] **M6.5.2** Flush, validate, and replace only the exact target save.
- [x] **M6.5.3** Preserve a bounded recovery artifact and define cleanup rules.
- [x] **M6.5.4** Cover interrupted write, disk error, invalid replacement, and
  recovery without broad filesystem deletion.

### Phase M6.6: implement one migration

- [x] **M6.6.1** Define a migration ID, exact source fingerprint, destination,
  and deterministic transformation contract.
- [x] **M6.6.2** Transform a temporary copy and validate against destination
  content.
- [x] **M6.6.3** Write a new migrated save without overwriting the source.
- [x] **M6.6.4** Cover success, missing path, wrong source, failed transform,
  failed validation, and rollback.

Deliverables:

- versioned bounded campaign envelope;
- exact `CampaignContentLock` and fingerprint;
- stable-ID serialization and index reconstruction;
- complete validation before publication;
- atomic save replacement with recovery artifact; and
- Exact, Compatible, Migratable, Missing, and Incompatible preflight results.

Exit criterion: exact content round-trips deterministically, missing definitions
never disappear silently, and failed load or migration preserves active and
original data.

## Milestone 7: local additive mods

### Phase M7.1: configure enabled packs

- [ ] **M7.1.1** Define a versioned local configuration containing explicit
  enabled pack roots or installed pack IDs.
- [ ] **M7.1.2** Separate installation state from per-campaign content locks.
- [ ] **M7.1.3** Add base-only fallback when configuration contains no mods.
- [ ] **M7.1.4** Present invalid or missing configured roots without scanning
  unrelated directories.

### Phase M7.2: enforce the filesystem trust boundary

- [ ] **M7.2.1** Resolve pack root and every relative path with escape checks.
- [ ] **M7.2.2** Reject absolute paths, traversal, unsupported links, reserved
  device names, and excess path length.
- [ ] **M7.2.3** Bound file count, bytes, archives, assets, and diagnostic output.
- [ ] **M7.2.4** Confirm mod load performs no code execution, process launch, or
  network access.

### Phase M7.3: enforce namespace and additive behavior

- [ ] **M7.3.1** Validate manifest namespace claims.
- [ ] **M7.3.2** Require third-party stable IDs to use an owned namespace.
- [ ] **M7.3.3** Reject duplicate and base-replacement definitions in version 1.
- [ ] **M7.3.4** Record pack and file provenance on each compiled definition and
  diagnostic.

### Phase M7.4: merge localization and assets

- [ ] **M7.4.1** Validate mod localization namespaces and default-locale keys.
- [ ] **M7.4.2** Compile localization in the same deterministic pack order while
  retaining independent publication.
- [ ] **M7.4.3** Load only declared bounded presentation assets through safe
  decoders.
- [ ] **M7.4.4** Fail the whole pack contribution if required localization or
  assets are invalid.

### Phase M7.5: extend author tooling

- [ ] **M7.5.1** Add `validate-set`, `fingerprint`, and `explain-id` operations.
- [ ] **M7.5.2** Emit pack counts, dependencies, namespaces, provenance,
  references, limits, localization gaps, and fingerprint.
- [ ] **M7.5.3** Add machine-readable diagnostics for editor integration.
- [ ] **M7.5.4** Ensure validation never reformats or writes source implicitly.

### Phase M7.6: complete the additive test pack

- [ ] **M7.6.1** Add a test Race, Heritage, Perk, trained Feat, Spell, item,
  encounter, and localization entries under one namespace.
- [ ] **M7.6.2** Start a deterministic campaign with base plus test pack.
- [ ] **M7.6.3** Exercise at least one granted ability and persist its state.
- [ ] **M7.6.4** Verify exact reload succeeds and missing-pack reload fails
  without changing the save.

Deliverables:

- enabled-pack configuration;
- safe local discovery and dependency graph;
- namespace ownership and additive policy;
- merged gameplay and localization validation;
- offline reports and fingerprints; and
- clear startup and load diagnostics.

Exit criterion: the test pack in [`Modding.md`](Modding.md) works through new
campaign, action, save, and exact reload, then fails safely when absent.

Base-definition patching is separate and cannot use last-file-wins semantics.

## Milestone 8: galaxy and faction expansion

Replace the prototype grid only after smaller character and encounter contracts
are stable. Deliverables follow
[`../DesignConcept/GalaxyMap.md`](../DesignConcept/GalaxyMap.md) and
[`../DesignConcept/Factions.md`](../DesignConcept/Factions.md), with travel-event
behavior from [`../DesignConcept/Events.md`](../DesignConcept/Events.md): seeded
graph generation, knowledge separation, bounded voyage events, faction
instances, reports, agreements, territory, markets, and a complete return
voyage.

### Phase M8.1: define galaxy content and settings

Campaign-setting ownership and persistence follow
[`../DesignConcept/GameSettings.md`](../DesignConcept/GameSettings.md).

- [ ] **M8.1.1** Add versioned Galaxy Settings, Shape, Region, System Archetype,
  Site, Starway, Hazard, Landmark, Travel Event, and Event Choice definitions.
- [ ] **M8.1.2** Validate placement constraints, counts, compatibility, route
  costs, and required services.
- [ ] **M8.1.3** Add the base 16-system slice settings and authored site tables.
- [ ] **M8.1.4** Store generator and definition revisions in new-campaign state.

### Phase M8.2: generate the immutable graph

- [ ] **M8.2.1** Split named random streams for topology, systems, sites,
  factions, hazards, travel-event selection and outcomes, and names.
- [ ] **M8.2.2** Place systems, guarantee connectivity, then add bounded optional
  edges in a deterministic order.
- [ ] **M8.2.3** Validate starting routes, chokepoint alternatives, required
  services, and unreachable nodes before publication.
- [ ] **M8.2.4** Prove repeated generation from seed, settings, and fingerprint
  creates identical graph state.

### Phase M8.3: implement map knowledge and travel

- [ ] **M8.3.1** Separate authoritative topology from Unknown, Detected,
  Surveyed, and Charted player knowledge.
- [ ] **M8.3.2** Add observations with source, confidence, and tick.
- [ ] **M8.3.3** Implement bounded route search by time, fuel or aether, and
  known danger.
- [ ] **M8.3.4** Preserve changed sites and routes rather than regenerating on
  revisit.
- [ ] **M8.3.5** Create bounded event opportunities from committed travel-leg
  progress rather than render frames or wall-clock time.
- [ ] **M8.3.6** Filter and ordinally weight eligible events by route, ship,
  crew, knowledge, faction, history, cooldown, and remaining event budget.
- [ ] **M8.3.7** Implement Scheduled, Revealed, AwaitingDecision, Resolving,
  ActiveEncounter, Resolved, and Expired event-instance states.
- [ ] **M8.3.8** Persist event seeds, choices, recurrence, cooldowns, follow-ups,
  and a no-event outcome without rerolling after load.

### Phase M8.4: define and place factions

- [ ] **M8.4.1** Add Faction definition, instance, policy, goal, agreement,
  market, territory, and report IDs and schemas.
- [ ] **M8.4.2** Place three slice factions through bounded compatible origin and
  territory rules.
- [ ] **M8.4.3** Separate Race and Heritage from membership and citizenship.
- [ ] **M8.4.4** Validate starting services, conflicts, frontiers, and neutral
  access.

### Phase M8.5: implement knowledge and delayed response

- [ ] **M8.5.1** Give each faction its own observations rather than global
  omniscience.
- [ ] **M8.5.2** Add bounded reports with dispatch, delivery, interception, and
  credibility.
- [ ] **M8.5.3** Select actions from known state, goals, resources, and stable
  tie-breaking.
- [ ] **M8.5.4** Record why every faction offer and response occurred.

### Phase M8.6: implement agreements, markets, and territory

- [ ] **M8.6.1** Add one salvage agreement with obligations, witnesses,
  fulfillment, breach, expiry, and remedies.
- [ ] **M8.6.2** Add Regard, Trust, Alarm, and one cargo Debt with bounded rules.
- [ ] **M8.6.3** Add one neutral market whose stock and price respond to
  committed logistics state.
- [ ] **M8.6.4** Distinguish claim, control, presence, jurisdiction, and access on
  the Starway graph.

### Phase M8.7: replace the navigation shell

- [ ] **M8.7.1** Present the generated graph and limited knowledge in WPF.
- [ ] **M8.7.2** Complete departure, route choice, seeded travel event, site
  encounter, consequence, and return through the new world.
- [ ] **M8.7.3** Migrate or retire prototype-only UI without deleting the
  independently buildable expedition code prematurely.
- [ ] **M8.7.4** Declare replacement only after deterministic save/load and
  replay criteria pass.

Exit criterion: the same seed, fingerprint, and commands reproduce galaxy,
travel events, and politics, and failed content or save migration cannot
partially replace them.

## Milestone 9: endgame crises

Endgame begins only after campaign saves, faction autonomy, strategic fronts,
and bounded distant simulation exist. Implement one family from omens through
aftermath before expanding the catalog in
[`../DesignConcept/Endgame_Crisis.md`](../DesignConcept/Endgame_Crisis.md).

### Phase M9.1: define crisis content

- [ ] **M9.1.1** Add Crisis, Phase, Omen, Front, Objective, Resolution, and
  Aftermath definitions with stable IDs.
- [ ] **M9.1.2** Validate required sites, technology paths, factions, skills,
  warnings, resolutions, and simulation bounds.
- [ ] **M9.1.3** Author one Story-intensity family and exclude other families
  from runtime selection initially.
- [ ] **M9.1.4** Add configuration for Off or One, intensity, warning horizon,
  eligible families, and continuation.

### Phase M9.2: select and persist the dormant crisis

- [ ] **M9.2.1** Derive an owned `stream.crisis` from campaign creation state.
- [ ] **M9.2.2** Select family, variants, anchors, and bounded roles once.
- [ ] **M9.2.3** Store selection privately with definition and generator
  revisions.
- [ ] **M9.2.4** Verify save/load, exploration order, and worker timing cannot
  reroll or leak the selection.

### Phase M9.3: implement maturity, omens, and confirmation

- [ ] **M9.3.1** Calculate bounded maturity from documented galaxy, faction,
  player, instability, discovery, and campaign-age inputs.
- [ ] **M9.3.2** Schedule omens as observations and encounters without asserting
  hidden truth to the UI.
- [ ] **M9.3.3** Propagate evidence through ordinary faction reports and player
  knowledge.
- [ ] **M9.3.4** Enforce the configured minimum warning horizon after confirmed
  threat.

### Phase M9.4: implement one bounded front

- [ ] **M9.4.1** Add front pressure, resistance, capacity, location, next tick,
  observed state, and authoritative state.
- [ ] **M9.4.2** Spend bounded pressure on authored actions through legal
  Starways, signals, cargo paths, or crisis movement.
- [ ] **M9.4.3** Add investigation, rescue, containment, combat, and retreat
  contributions.
- [ ] **M9.4.4** Cap active fronts, dependencies, spawned actors, searches,
  reports, and retained history.

### Phase M9.5: integrate factions and player objectives

- [ ] **M9.5.1** Add bounded faction response goals based only on possessed
  knowledge and available resources.
- [ ] **M9.5.2** Add a temporary accord with contributions, authority, sharing,
  exit, and breach rules.
- [ ] **M9.5.3** Connect one crisis objective to shared Battle rules without a
  separate combat engine.
- [ ] **M9.5.4** Preserve player choice to join, broker, oppose, exploit,
  retreat, or remain outside the accord.

### Phase M9.6: implement confrontation and alternatives

- [ ] **M9.6.1** Unlock final projects only from inspectable prerequisites.
- [ ] **M9.6.2** Implement at least three viable approaches, including one that
  is not fleet destruction.
- [ ] **M9.6.3** Track contributions across research, logistics, diplomacy,
  rescue, sabotage, and combat.
- [ ] **M9.6.4** Commit the selected resolution, costs, losses, and remaining
  threats atomically.

### Phase M9.7: implement aftermath and expansion gate

- [ ] **M9.7.1** Persist changed Starways, settlements, factions, refugees,
  technologies, contamination, memorials, and remnants.
- [ ] **M9.7.2** Support continuation, ask, or conclude settings and all declared
  outcome grades.
- [ ] **M9.7.3** Verify deterministic replay from dormant selection through
  aftermath and continued save/load.
- [ ] **M9.7.4** Add another family or higher intensity only after the first
  family's phase, bound, and migration coverage is complete.

Exit criterion: crisis selection cannot reroll on load, faction reactions use
known information, several resolutions remain valid, and aftermath persists.

## Build and verification

For every milestone:

- update `Spelljammer.slnx` and project references explicitly;
- keep Simulation independent from WPF, SpriteForge, localized rendering,
  filesystem discovery, and wall-clock time;
- compile the solution and affected test targets with warnings as errors;
- leave executable unit-test runs to CI or the user, per repository policy;
- run applicable formatting and `git diff --check`; and
- inspect for generated files, absolute paths, unbounded inputs, and inaccurate
  implementation claims.

Report engine, game, content-tool, and CI-owned test status separately.

## Risk controls

| Risk | Control |
| --- | --- |
| Generic content becomes an untyped string map | Strong ID wrappers, linked definitions, and compiled indices |
| Mod order changes outcomes | Dependency graph, ordinal tie-breaks, and semantic fingerprint |
| Content executes unsafe behavior | Declarative data and allowlisted formula/effect primitives |
| Saves lose objects after changes | Exact lock, reference scan, and explicit migrations |
| Dynamic Attributes break UI | Registry iteration, generic stat blocks, phased public support |
| Loader failure publishes partial state | Candidate registry and validate-before-publish transaction |
| Scope delays the playable voyage | Move one content family at a time and retain the prototype |

The near-term target is Milestone 7, not unrestricted mod support. Milestone 6
supplies the versioned content-locked save boundary, stable-ID reconstruction,
transactional publication, durable replacement and recovery, and explicit
migrations.
