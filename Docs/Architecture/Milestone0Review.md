# Milestone 0 contract review

## Result

Milestone 0 is complete. Version 1 identity, source, ordering, versioning,
compatibility, bounds, diagnostics, canonical serialization, and fixture
contracts are frozen. This milestone intentionally adds no runtime loader; M1
implements these reviewed contracts.

## Reviewed artifacts

| Concern | Authoritative artifact | Frozen result |
| --- | --- | --- |
| Existing gameplay identities | [`ContentIdInventory.md`](ContentIdInventory.md) | 152 unique explicit IDs |
| Ownership, delivery, spelling, collisions | [`ContentIdReview.md`](ContentIdReview.md) | Base, example, deferred, and prototype status assigned |
| Stable IDs and namespaces | [`ContentContractsV1.md`](ContentContractsV1.md) | ASCII grammar, 127-byte maximum, base and mod ownership |
| Source documents and ordering | [`ContentContractsV1.md`](ContentContractsV1.md) | Strict UTF-8 JSON, exact manifest, directories, deterministic pack order |
| Revisions and compatibility | [`ContentContractsV1.md`](ContentContractsV1.md) | Explicit responsibility and migration rules |
| Gameplay fingerprint | [`ContentContractsV1.md`](ContentContractsV1.md) | `spelljammer-semantic-v1`, canonical JSON, SHA-256 |
| Bounds and safe errors | [`ContentLimitsAndDiagnostics.md`](ContentLimitsAndDiagnostics.md) | Exact initial limits and 24 stable diagnostic codes |
| Contract examples | [`../../Tests/Spelljammer.Content.Tests/Fixtures/Milestone0/README.md`](../../Tests/Spelljammer.Content.Tests/Fixtures/Milestone0/README.md) | One valid graph and one focused case per diagnostic |

## Exit-criteria audit

- The base pack is `spelljammer.base`; third-party packs use
  `mod.<namespace>` and definitions use `<domain>.mod.<namespace>.<local>`.
- Stable IDs are compared and sorted ordinally. No existing ID requires a
  rename and no current ID is serialized by the prototype.
- Base Attributes have the exact range 1 through 10; base Skills have the
  exact range 0 through 100.
- Manifest discovery, definition directory mapping, dependency edge
  direction, optional `loadAfter` behavior, cycle handling, and ordinal
  topological tie-breaking are specified.
- Unknown properties, duplicate JSON properties, invalid UTF-8, path escape,
  duplicate ownership, unresolved references, invalid semantics, and every
  named capacity have deterministic failure contracts.
- Diagnostic records retain only bounded safe identifiers, normalized relative
  paths, typed values, and safe tokens. Normal UI output excludes absolute
  paths, source documents, secrets, stack traces, and unobserved simulation
  information.
- The valid fixture resolves Attribute, Skill, Access, training, Feat, Race,
  and Perk references. Its canonical file is 1,187 bytes, contains no BOM or
  CR, and ends with exactly one LF.
- The valid fixture SHA-256 is
  `bdf1737ce9b11c9b4bc9440918ad3247f4b800bcd9eecaef2d9547b00fe8f202`.
- The invalid fixture contains 24 cases and its expected-primary-code set is
  identical to the 24-code initial diagnostic registry.

## Explicitly out of version 1 scope

Base-definition patching, arbitrary executable scripts, live campaign reload,
automatic dependency downloads, adding arbitrary Attributes from public mods,
archive installation, and public save migration execution remain later work.
Their absence is an explicit rejection or product-scope decision, not an
unresolved requirement for the M1 loader.

Future content kinds beyond Attribute, Skill, Access, Feat, Perk, Race, and
Training Project require a reviewed schema-contract revision before being
accepted. M1 begins with the common Stable ID and diagnostic primitives, then
implements manifest parsing and the transactional immutable registry against
the checked-in fixtures.
