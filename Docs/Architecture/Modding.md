# Modding architecture

## Status

This document specifies the planned declarative mod boundary. The content-locked
save contract and explicit migration boundary are implemented, but no gameplay
mod directory, package manager, executable-script host, or user-facing mod
loader exists. Existing localization source files are build inputs, not
evidence that runtime gameplay mods already work.

The content pipeline is defined in [`ContentPacks.md`](ContentPacks.md).
Character definitions and access grants are defined in
[`CharacterCapabilities.md`](CharacterCapabilities.md). The implemented save
contract is defined in [`CampaignSaves.md`](CampaignSaves.md).

## Initial scope

The first mod release supports local declarative packs that add validated:

- Races, Heritage, Perks, learned Feats, and Backgrounds;
- Skills after every consumer supports a dynamic Skill registry;
- spells, psychic abilities, combat techniques, recipes, and enchantments;
- items, weapons, armor, ship frames, and ship modules;
- travel events, encounters, ruin rooms, galaxy sites, factions, and crises;
  and
- localization entries and presentation assets within declared limits.

Adding or removing Attributes, patching base definitions, executable behavior,
custom native modules, custom WPF controls, network downloads, and live
mid-campaign reload are later capabilities. Version 1 rejects them explicitly.

## Trust boundary

Mods are untrusted input. Loading a pack permits reading files inside its
configured root and interpreting supported data. It does not permit executing
code, accessing the network, enumerating arbitrary directories, launching
processes, or writing outside designated cache and save locations.

Definitions invoke only allowlisted formula, condition, target, and effect IDs
implemented by Spelljammer. Parameters are range-checked before publication.
Type names, reflection targets, DLL paths, commands, URLs, and arbitrary
expressions are not accepted as callbacks.

Asset paths are pack-relative and normalized once. Traversal, absolute paths,
and links that escape the resolved pack root are rejected before decoding.

## Discovery

The application receives an explicit ordered list of enabled pack roots. It
does not scan entire drives or fetch missing dependencies at startup.

Discovery:

1. resolves every configured root to a normalized absolute path;
2. confirms the manifest remains within that root;
3. reads bounded manifest data;
4. validates ID, version, compatibility, and namespaces;
5. calculates dependencies and deterministic order; and
6. reports all blocking diagnostics before definition loading.

The base pack is first and cannot be disabled for a normal campaign. Command-
line and UI configuration eventually produce the same pack-set request.

## Identity and namespaces

A pack has a globally unique lowercase ID such as `mod.starwrights`.
Third-party definitions use that owned namespace:

```text
skill.mod.starwrights.gravimetry
feat.mod.starwrights.void-savant
spell.mod.starwrights.anchor-pulse
```

Display name and author are not identity. A manifest ID change creates a new
pack unless an explicit migration maps it. Packs cannot impersonate the base
namespace or another enabled pack.

## Dependencies and conflicts

Required dependencies use exact pack IDs and semantic version ranges. Missing
or incompatible requirements fail. `loadAfter` supplies optional ordering edges
without changing validity.

The loader builds a bounded directed graph, rejects cycles, and applies ordinal
pack-ID tie-breaking to otherwise equal nodes. Directory order and install time
never decide behavior.

Version 1 is additive, so duplicate definition IDs are errors. A future patch
document may resemble:

```json
{
  "schemaVersion": 1,
  "id": "patch.mod.example.brace-ward-balance",
  "targetId": "spell.spirit.magic-missile",
  "targetPackId": "spelljammer.base",
  "expectedRevision": 1,
  "operations": []
}
```

Patch support cannot ship until operation allowlists, order, field ownership,
conflict diagnostics, and fingerprint effects are specified. There is no
"last file wins" fallback.

## Campaign content lock

Starting a campaign creates a `CampaignContentLock` containing:

- base content revision;
- ordered pack IDs and versions;
- manifest and semantic-content fingerprints;
- effective gameplay fingerprint;
- generator, formula, effect, and save-schema versions; and
- migration IDs already applied.

A campaign owns one immutable `GameContentSnapshot`. Changing enabled packs
affects menus and new campaigns only until a load or migration validates the
whole replacement. Developer hot reload cannot mutate authoritative live state.

## Save compatibility

Preflight returns one result:

| Result | Meaning |
| --- | --- |
| Exact | Pack set and semantic fingerprint match |
| Compatible | Differences are covered by a verified compatible-revision rule |
| Migratable | A complete ordered migration path exists |
| Missing content | Required packs or definitions are unavailable |
| Incompatible | Versions, schemas, IDs, or semantics cannot be reconciled |

Missing content never silently deletes Skills, Perks, Feats, items, ships,
sites, factions, active effects, or other state. Diagnostics name the missing
pack and definition IDs; the original save remains unchanged.

A migration:

1. reads old data into a bounded temporary representation;
2. verifies the expected source fingerprint and prerequisites;
3. applies deterministic ID and state transforms;
4. validates the whole result against the candidate content snapshot;
5. writes a new artifact without overwriting the original; and
6. publishes only after a successful reload validation.

Mod authors own migrations for retired IDs and semantic breaks. The base game
cannot infer replacements from display names.

## Removal policy

A pack can be removed from a campaign only when no persistent state references
it and derived generation remains valid. The reference scan covers characters,
training, techniques, items, cargo, ships, modules, sites, encounters, factions,
agreements, galaxy generation, active effects, and crisis state.

The UI explains blockers. Destructive cleanup or substitution requires a
separate explicit migration; uninstall never performs it automatically.

## Author tooling

The planned offline compiler is the primary validation tool. Intended
operations are:

```text
validate <pack-root>
validate-set <base-root> <mod-root>...
fingerprint <pack-root>
explain-id <stable-id>
```

These command names and positional arguments are the version 1 author-tooling
contract. Commands are offline, noninteractive, deterministic, and return zero
only when validation succeeds. Machine-readable diagnostics use the records
defined in
[`ContentLimitsAndDiagnostics.md`](ContentLimitsAndDiagnostics.md) and
accompany concise console output.

A pack report lists definition counts, namespaces, dependencies, references,
unused localization keys, consumed limits, and semantic fingerprint. Validation
does not rewrite authored JSON unless a separate format command is invoked.

## Distribution boundary

A mod package is an archive with one root manifest and declared folders.
Installation validates every archive path and extracts into one exact new
destination. An update is staged and validated before replacing a prior
version.

Online discovery, ratings, payment, accounts, cloud synchronization, workshop
integration, and automatic downloads are outside initial scope. They can sit
above the local pack contract later.

## Limits and failure behavior

Mod loading applies shared `ContentLimits` plus limits on roots, dependency
depth, namespaces, assets, archive entries, extracted bytes, migrations, and
diagnostics.

A failed pack contributes no definitions or localization. Menu-time replacement
failure preserves the previous registry. Startup without a valid registry
reports errors and does not begin a campaign with partial content.

## Verification contract

CI-owned tests cover:

- deterministic order across installation and enumeration order;
- missing dependencies, incompatible versions, and cycles;
- namespace ownership and duplicate rejection;
- traversal, escaping links, oversized JSON, archives, and assets;
- unsupported scripts and callbacks being rejected;
- exact and incompatible content locks;
- missing-mod diagnostics preserving the original save;
- transactional migration and rollback; and
- a base-only campaign remaining identical with no mods enabled.

The first public milestone is complete when one additive test pack adds a Race,
Perk, Feat, Spell, item, encounter, and localization; starts and saves a
deterministic campaign; reloads with the exact pack set; and fails safely when
the pack is absent.
