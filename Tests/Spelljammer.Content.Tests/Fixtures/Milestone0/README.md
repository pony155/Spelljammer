# Milestone 0 content contract fixtures

These fixtures freeze the source and fingerprint contracts before the content
loader is implemented. They are test input, not proof that runtime loading is
available.

`valid/base` is the smallest complete base pack that exercises an Attribute,
Skill, Access gate, training project, learned Feat, Race, and Racial Perk.
All content references resolve. Runtime primitive IDs used by the Skill are the
version 1 built-ins listed in `ContentContractsV1.md`.

`invalid/cases.json` defines virtual-file-system cases layered over
`valid/base`. The content contract target applies each case independently:

- `removeFiles` removes relative paths from the base layer;
- `writeText` replaces or adds a file with the exact UTF-8 string value;
- `writeHex` replaces or adds a file with the exact hexadecimal bytes;
- `duplicateFiles` adds a second virtual entry for an already-present path,
  which represents duplicate normalized entries without relying on host
  filesystem behavior;
- `additionalPacks` adds separately rooted virtual packs;
- `gameVersion` replaces the default test game version `0.1.0`; and
- `limitOverrides` may only reduce production limits for a focused capacity
  case.

Every case declares one `expectedPrimaryDiagnostic`. The outer fixture JSON is
valid even when a virtual file contains malformed JSON or invalid UTF-8.

`expected/canonical-semantic.json` contains the exact canonical semantic bytes
for the valid pack. It has no BOM, is compact JSON, and ends in one LF.
`expected/fingerprints.json` records its lowercase SHA-256 digest. An
implementation must derive the bytes from `valid/base`; it must not copy the
expected file as output.
