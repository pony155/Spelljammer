# Campaign save architecture

## Implemented boundary

`Source/Spelljammer.Persistence` owns the Milestone 6 campaign-save boundary.
It references headless content and simulation state only; localization strings,
WPF objects, SpriteForge handles, native pointers, and runtime dense indices are
not persisted. The WPF host does not yet expose save/load controls.

A campaign is encoded from immutable authoritative state and reconstructed only
after a compatible `GameContentSnapshot` has been selected. Publication is a
separate transaction: `CampaignRegistry` retains its previous campaign unless
the complete candidate decodes, resolves, and validates successfully.

## Envelope version 1

All integer fields in the fixed header are little-endian.

| Offset | Bytes | Value |
| ---: | ---: | --- |
| 0 | 8 | ASCII magic `SJSAVE01` |
| 8 | 2 | Envelope version |
| 10 | 2 | Save-schema version |
| 12 | 4 | Preflight JSON byte length |
| 16 | 4 | Authoritative payload JSON byte length |
| 20 | 32 | SHA-256 of the preflight and payload bytes |
| 52 | variable | UTF-8 preflight JSON followed by UTF-8 payload JSON |

The preflight document contains the document discriminator, game build, exact
content lock, and sorted required-definition IDs. The content lock records the
base revision, ordered pack IDs/semantic versions/revisions, manifest-lock
digest, semantic and effective fingerprints, generator/formula/effect/save
versions, and applied migration IDs.

The payload stores the voyage seed and tick, current location, ships, roster,
capabilities, training, resources, injuries, effects, encounter state, queued
commands, scheduled actions, and bounded histories through stable IDs. JSON
property order and all unordered collections are canonicalized so encoding the
same authoritative state produces the same bytes.

## Bounds and diagnostics

The current limits are 8 MiB per save, 256 KiB of preflight metadata, 1,024
UTF-8 bytes per string, nesting depth 32, 4,096 entries in a general collection,
64 characters, 32 ships, 8,192 required-definition IDs, and 512 retained
commands or events. A physical file's length is checked before it is read into
memory.

Stable diagnostics distinguish corrupt, oversized, unsupported, truncated,
checksum-mismatched, missing-content, incompatible-content, invalid-state,
I/O, and migration failures. Untrusted JSON rejects duplicate properties,
comments, trailing commas, excessive tokens, strings, and nesting.

## Preflight and reconstruction

Preflight verifies the bounded envelope and metadata, then compares exact pack
identities and versions, manifest and semantic fingerprints, and all behavior
versions. It reports one of `Exact`, `Compatible`, `Migratable`, `Missing`, or
`Incompatible` before the authoritative payload is decoded. Missing results
list stable pack and definition IDs, never display names.

`Compatible` requires an explicit source-to-destination fingerprint rule.
During reconstruction, definitions are looked up by stable ID and current
dense indices are rebuilt from the selected snapshot. A compatible destination
may add Attributes or Skills; only those new entries receive their authored
default or minimum. Unknown saved definitions remain fatal.

The temporary candidate is checked for content identity, collection bounds,
duplicate identity, command order, character templates and grants, ship
loadouts and resources, encounter graphs and occupancy, equipment, objectives,
effects, schedules, and ownership before publication.

## Durable replacement and recovery

`CampaignSaveStore` writes a uniquely named temporary file in the target
directory with write-through and an explicit durable flush. It reads and
validates that exact artifact before replacing the exact target. Replacing an
existing save retains one `<target>.recovery` artifact; first writes use an
atomic same-directory move. Cleanup removes only that exact recovery path.
Write, validation, and replacement failures leave the current target intact.

Recovery validates the bounded recovery envelope, stages it in the same
directory, and replaces only the requested target. No operation scans or
deletes a broader directory.

## Migration contract

Each migration declares a stable migration ID and exact source and destination
fingerprints. `CampaignMigrationRegistry` chooses a deterministic bounded path.
Transforms run against temporary immutable campaign values, rebind definitions
to each destination snapshot, and validate after every step. Milestone 6 ships
a deterministic location-ID rename migration. Successful migration creates new
save bytes and `WriteMigrated` refuses to use the source path as its destination;
failure leaves both the original bytes and active campaign unchanged.

Compile-only contracts live in `Tests/Spelljammer.Persistence.Tests`. Execution
is owned by CI or the user, consistent with the repository test policy.
