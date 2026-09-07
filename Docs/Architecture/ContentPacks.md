# Data-driven content packs

## Status

This document specifies the gameplay content pipeline. The Milestone 1
foundation is implemented: explicit-root discovery, strict source validation,
deterministic ordering, immutable candidate compilation, semantic
fingerprinting, transactional publication, and offline validation are
available. Milestone 2 adds the production base Attribute and Skill catalogs,
typed dense registries, default-locale validation, and registry inspection.
Milestone 3 adds strict Character, Background, Heritage, and Technique kinds,
grant-graph validation, the base race roster, and the simulation catalog
boundary. Milestone 4 adds Spell and psychic-technique kinds, expanded training
projects, and their fingerprint-scoped registries. The current WPF expedition
behavior remains unchanged. Milestone 5 adds linked equipment, personal-board,
encounter, ship-frame, ship-module, and ship-weapon-configuration kinds plus
the authored six-zone ruin and two technology packages.

The implementation moves character definitions into a built-in base pack
without changing the expedition UI. Later systems reuse the pipeline for
spells, items, ships, encounters, factions, and crises.

## Decisions

- Hard-code the definition machinery and safe rule primitives, not lists such
  as `attribute.strength` or `skill.engineering`.
- Treat the base game as the first content pack. Mods use the same schemas and
  validation path as first-party data.
- Author strict UTF-8 JSON in versioned schemas. Arbitrary executable scripts
  are outside the initial contract.
- Use stable lowercase ASCII IDs for references and saves. Localized text is
  never gameplay identity.
- Parse into temporary objects, link and validate the complete candidate set,
  compile an immutable registry, then publish once.
- Sort every input explicitly. Filesystem order, locale, threads, and wall-clock
  time cannot affect compiled content.
- Store the pack set and semantic content fingerprint with every campaign.

## Project boundaries

The intended dependency direction is:

```text
Spelljammer.App
  ├─ Spelljammer.Content ──> Spelljammer.Simulation
  ├─ Spelljammer.Localization
  └─ SpriteForge native ABI

Tools/Spelljammer.Content.Compiler
  └─ Spelljammer.Content ──> Spelljammer.Simulation
```

`Spelljammer.Simulation` owns gameplay ID types, immutable runtime definitions,
state, commands, and generic rule execution. It does not discover files, parse
JSON, display localized strings, or depend on WPF.

The implemented `Spelljammer.Content` project owns manifests, source DTOs, JSON
parsing, pack ordering, cross-reference resolution, validation, compilation,
fingerprinting, and diagnostics. It may reference Simulation contracts;
Simulation must not reference the loader.

The application selects configured content roots and requests a complete
`GameContentSnapshot`. That snapshot is passed into world creation and cannot
be replaced for a running campaign without an explicit validated migration.

The offline compiler uses the same loader and validators. A later
binary artifact must preserve the same stable IDs, schema versions, bounds, and
semantic fingerprint as source JSON.

## Repository layout

```text
Content/
  Packs/
    base/
      manifest.json
      Definitions/
        Attributes/
        Access/
        Backgrounds/
        Characters/
        Skills/
        Feats/
        Perks/
        PsychicTechniques/
        Races/
        Spells/
        Heritages/
        Techniques/
        TrainingProjects/
        Equipment/
        BoardCells/
        ZoneLinks/
        PersonalBoards/
        Encounters/
        ShipFrames/
        ShipModules/
        ShipWeaponConfigurations/
        Items/                 # planned
        Ships/                 # planned
        Events/
        Factions/
        Crises/
      Localization/
        en-US/
```

Existing localization catalogs remain supported while the base pack is
introduced. Migration must not create two authoritative owners for the same
localization key.

User content is discovered only from application-configured roots. A typical
development root is `Mods/<pack-id>/`; it need not be committed and no project
file may contain a developer-specific absolute path.

## Pack manifest

Every pack contains exactly one root `manifest.json` using the version 1 schema:

```json
{
  "schemaVersion": 1,
  "id": "spelljammer.base",
  "version": "0.1.0",
  "displayNameKey": "content-pack.spelljammer.base.name",
  "gameVersionRange": ">=0.1.0 <0.2.0",
  "dependencies": [],
  "loadAfter": [],
  "definitionRoots": ["Definitions"],
  "localizationRoots": ["Localization"],
  "contentRevision": 1
}
```

Manifest paths are relative, normalized, and required to stay inside the pack
root. Dependency IDs and versions determine validity and topological order;
`loadAfter` adds optional ordering edges but cannot make a missing dependency
valid. Cycles, duplicate pack IDs, incompatible versions, invalid paths, and
ambiguous ownership are errors.

## Stable ID contract

Established core IDs remain valid:

```text
attribute.strength
skill.engineering
feat.access.magic
perk.race.elf.aether-sense
access.psionics
spell.spirit.magic-missile
combat.context.ruin
```

The reviewed design-document snapshot is maintained in
[`ContentIdInventory.md`](ContentIdInventory.md).

The exact grammar, 127-byte maximum, comparison rules, base namespaces, pack
namespace form, version responsibilities, and canonical fingerprint bytes are
frozen in [`ContentContractsV1.md`](ContentContractsV1.md). Case folding,
Unicode normalization, display names, and paths never change identity.

The base pack reserves the short IDs documented under `Docs/DesignConcept`. A
third-party pack has an ID such as `mod.starwrights` and uses that namespace in
definitions, for example `skill.mod.starwrights.gravimetry`. It cannot define
IDs owned by another pack.

Renaming an ID requires a versioned persistence migration. Display-name
similarity is never used to guess replacements.

## Definition lifecycle

The loader performs bounded phases:

1. **Discover:** resolve only configured roots and read one manifest per pack.
2. **Order:** validate versions and calculate a deterministic dependency order.
3. **Enumerate:** normalize and sort paths using UTF-8 ordinal order.
4. **Parse:** decode strict JSON while enforcing byte, depth, number, string,
   and collection limits.
5. **Claim:** validate kinds, stable IDs, namespaces, and duplicates.
6. **Link:** resolve all cross-definition references.
7. **Validate:** run schema, range, semantic, determinism, capacity, cycle, and
   subsystem checks over the whole candidate set.
8. **Compile:** assign deterministic dense indices and create immutable arrays
   and lookup tables.
9. **Fingerprint:** hash the canonical semantic projection of ordered
   manifests and definitions, excluding release-only metadata, irrelevant
   whitespace, and filesystem metadata.
10. **Publish:** replace the previous menu-time registry only after all phases
    succeed.

Failure returns structured diagnostics and leaves the previous working
registry active. Partial definitions never reach simulation code.

## Additions and replacement

Initial mod support is additive. Two packs defining the same stable ID fail to
load, even if their JSON is identical. This keeps ownership and saves
unambiguous.

Base-definition replacement is deferred until an explicit patch schema defines
target ID, expected source pack and revision, allowed fields, operation order,
conflict behavior, and fingerprint impact. File order and "last mod wins" are
not acceptable gameplay contracts.

## Runtime registry

The conceptual runtime boundary is:

```csharp
public sealed class GameContentSnapshot
{
    public required ContentFingerprint Fingerprint { get; init; }
    public required ImmutableArray<AttributeDefinition> Attributes { get; init; }
    public required ImmutableArray<SkillDefinition> Skills { get; init; }
    public required ImmutableArray<FeatDefinition> Feats { get; init; }
    public required ImmutableArray<PerkDefinition> Perks { get; init; }
}
```

Production types may use private constructors and indexed registries. Required
properties are:

- definitions are immutable after publication;
- lookup uses strongly typed stable IDs;
- iteration order is deterministic;
- unresolved strings do not reach rule execution;
- runtime indices are valid only for one fingerprint; and
- saves and command logs store stable IDs, never dense indices.

Dense indices allow bounded arrays in hot simulation paths without hard-coding
enum members. Compilation sorts stable IDs before assigning indices.

## Localization boundary

Definitions contain keys such as `nameKey` and `descriptionKey`, not displayed
prose. Validation checks key syntax and rejects missing default-locale entries,
while Simulation stores and compares gameplay IDs only.

Each pack owns its localization namespace. Gameplay and localization catalogs
publish independently but use the same validated pack order and transactional
failure policy. Changing language does not change the gameplay fingerprint.

## Limits and diagnostics

The exact version 1 `ContentLimits` values and diagnostic registry are frozen
in
[`ContentLimitsAndDiagnostics.md`](ContentLimitsAndDiagnostics.md). Values may
change in a later contract through profiling, never by silently removing
bounds.

The parser rejects duplicate JSON properties, invalid UTF-8, non-finite
numbers, unknown required schema versions, and values that cannot round-trip
through their declared numeric type.

Every diagnostic contains the stable fields defined by that registry. Normal
UI diagnostics do not expose arbitrary absolute user paths or unbounded source
content.

## Verification contract

Compile-time and CI-owned tests cover:

- identical fingerprints and indices across repeated loads;
- input files presented in different orders;
- missing, duplicate, malformed, oversized, cyclic, and incompatible content;
- path traversal and namespace violations;
- dependency ordering and additive cross-pack references;
- failed publication preserving the previous snapshot;
- localization keys remaining separate from gameplay identity; and
- saves using stable IDs instead of runtime indices.

Executable mod scripts, live authoritative hot reload, automatic dependency
downloads, and silent conflict resolution are outside the first implementation.
