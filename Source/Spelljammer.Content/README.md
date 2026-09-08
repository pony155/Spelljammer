# Spelljammer.Content

This project owns bounded filesystem discovery, strict JSON parsing, pack
ordering, source validation and linking, immutable snapshot compilation,
semantic fingerprinting, diagnostics, and transactional publication. It
references `Spelljammer.Simulation`, which owns stable gameplay ID wrappers and
immutable runtime definition types. Simulation never references this project
and performs no filesystem work.

Milestone 2 additionally provides typed Ability and Skill registries whose
dense indices are scoped to one fingerprint, required `en-US` key validation,
and a headless inspection snapshot used by the offline `report` command.
Milestone 3 registers Character, Background, Heritage, and Feat schemas, links
bounded grant graphs, and exposes typed registries through the
simulation-owned character catalog interface. Character creation and action
rules remain in `Spelljammer.Simulation`; `RosterInspection` is a read-only,
localization-ready presentation projection.
Milestone 4 adds active Feats with strict spell and psionic execution rules,
expanded training-project contracts, and validation for supernatural access,
knowledge, targets, resources, consent, resistance, and bounded effects.
Milestone 5 adds Equipment, Board Cell, Zone Link, Personal Board, Encounter,
Ship Frame, Ship Module, and Ship Weapon Configuration schemas. Their linked,
fingerprint-scoped registries feed the headless encounter runtime without
adding filesystem access to Simulation.
Character resource profiles define Health, Stamina, Mana, Resolve, Strain,
Turn Meter, Action Points, stamina speed bands, and stable personal-action AP
costs. These values are compiled and fingerprinted with the rest of gameplay
content rather than supplied by simulation fallbacks.
World-time definitions similarly own the fixed ticks-per-second cadence and
the bounded catch-up limit used by each authoritative `World`.
Time-scale definitions provide deterministic rational conversion from ticks to
campaign seconds, while calendar definitions provide ordered localized months
and all date-unit sizes. The compiled snapshot exposes all three through
`IWorldContentCatalog`.

Public namespaces are:

- `Spelljammer.Content` for versioned limits;
- `Spelljammer.Content.Compilation` for compilation results, snapshots,
  registry publication, fingerprints, and roster inspection;
- `Spelljammer.Content.Diagnostics` for bounded structured failures;
- `Spelljammer.Content.Manifests` for semantic versions and manifest contracts;
  and
- `Spelljammer.Content.Sources` for explicitly configured pack roots.

The related `Spelljammer.Simulation.Content` namespace contains the validated
stable-ID wrappers and immutable definitions that may cross into authoritative
gameplay. It has no loader or filesystem dependency.

`GameContentCompiler` is the public compilation facade. Its partial
implementations are separated by pipeline responsibility: localization,
pack ordering, source processing, linking, validation dispatch, definition
validation, definition compilation, and snapshot construction. It builds an
entire candidate before returning a snapshot.
`GameContentRegistry` publishes only successful candidates, so callers can
retain the prior snapshot after any content or I/O failure.
