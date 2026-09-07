# Content limits and diagnostics version 1

## Status

This document freezes roadmap phase M0.3. It defines the initial loader limits,
content-caused diagnostic codes, and safe diagnostic fields implemented by the
Milestone 1 content foundation. They remain hard bounds until a reviewed
contract revision replaces them.

## Initial limits

| Limit | Version 1 value |
| --- | ---: |
| Enabled packs | 64 |
| Dependency and `loadAfter` edges | 256 total |
| Dependency depth | 32 |
| Definition roots per pack | 8 |
| Localization roots per pack | 8 |
| Definition files per pack | 4,096 |
| Definition files in one content set | 32,768 |
| Manifest bytes | 65,536 |
| One definition file bytes | 1,048,576 |
| Definition bytes per pack | 268,435,456 |
| Definition bytes in one content set | 1,073,741,824 |
| JSON nesting depth | 32 |
| JSON tokens per file | 131,072 |
| Properties per object | 256 |
| Entries per array | 4,096 |
| Stable ID bytes | 127 |
| Localization key bytes | 127 |
| Generic source string bytes | 4,096 |
| Definitions per kind | 16,384 |
| Definitions in one content set | 65,535 |
| Tags per definition | 64 |
| References per definition | 256 |
| References in one content set | 1,048,576 |
| Graph nodes | 65,535 |
| Graph edges | 262,144 |
| Validation traversal depth | 64 |
| Retained diagnostics | 1,000 |
| UTF-8 bytes retained per diagnostic argument | 512 |

Each listed value is an inclusive maximum. Attempting to add or read one more
entry or byte is an error, not truncation. The loader may stop discovering
additional failures after 999 retained records and use the final available
slot for `CONTENT_LIMIT_EXCEEDED` identifying the diagnostic limit itself; it
never retains more than 1,000 records.

Limits apply before large allocations where possible. Checked arithmetic is
required when combining per-file or per-pack totals. A pack cannot evade a
semantic limit by splitting one definition across files or dependencies.

## Diagnostic record

Every content diagnostic has:

```text
code
severity
packId?
relativePath?
definitionId?
propertyPath?
arguments[]
```

Version 1 content-caused diagnostics are errors. `relativePath` is normalized
and relative to its configured pack root. `propertyPath` uses a bounded
JSON-pointer-like sequence of property names and decimal array indices; it
never contains arbitrary rendered values.

Arguments are typed as safe ID, integer, version, limit name, normalized
relative path, or bounded authored token. The renderer localizes the code and
formats those arguments later.

Diagnostics MUST NOT contain:

- an absolute pack, profile, workspace, or save path in normal UI output;
- file contents, arbitrary JSON objects, secrets, environment variables, or
  stack traces;
- protected simulation information not already observed by the recipient;
- translated text as an identifier; or
- more entries or bytes than the limits above.

Developer logs may attach exception type and resolved root through a separate
privacy-reviewed channel. They do not change the stable diagnostic payload.

## Diagnostic registry

The first 24 entries were frozen in M0. Milestone 2 adds the final
default-locale completeness entry below without changing the earlier codes.

| Code | Trigger |
| --- | --- |
| `CONTENT_MANIFEST_MISSING` | Configured pack root contains no manifest |
| `CONTENT_MANIFEST_MULTIPLE` | More than one candidate root manifest exists |
| `CONTENT_INVALID_UTF8` | Required source is not strict UTF-8 without BOM |
| `CONTENT_JSON_INVALID` | JSON grammar is invalid |
| `CONTENT_JSON_DUPLICATE_PROPERTY` | One object repeats a property name |
| `CONTENT_SCHEMA_UNSUPPORTED` | Required schema version is not supported |
| `CONTENT_REQUIRED_PROPERTY_MISSING` | A required property is absent |
| `CONTENT_UNKNOWN_PROPERTY` | A strict schema receives an undeclared property |
| `CONTENT_ID_INVALID` | Pack, definition, reference, or diagnostic ID violates its grammar |
| `CONTENT_PATH_INVALID` | A declared path is absolute, malformed, or escapes its pack root |
| `CONTENT_PACK_ID_DUPLICATE` | Two configured roots declare the same pack ID |
| `CONTENT_VERSION_INVALID` | Pack version or range syntax is invalid |
| `CONTENT_GAME_VERSION_INCOMPATIBLE` | Current game version is outside the pack range |
| `CONTENT_DEPENDENCY_MISSING` | A required pack is absent |
| `CONTENT_DEPENDENCY_VERSION_MISMATCH` | Required pack exists outside the requested range |
| `CONTENT_DEPENDENCY_CYCLE` | Dependency or ordering edges contain a cycle |
| `CONTENT_NAMESPACE_VIOLATION` | A pack defines an ID outside its owned namespace |
| `CONTENT_DEFINITION_ID_DUPLICATE` | Two candidate definitions use one Stable ID |
| `CONTENT_REFERENCE_UNKNOWN` | A required Stable ID cannot be resolved |
| `CONTENT_KIND_MISMATCH` | Definition directory/schema kind and ID prefix disagree |
| `CONTENT_VALUE_OUT_OF_RANGE` | A scalar violates schema or semantic bounds |
| `CONTENT_COLLECTION_DUPLICATE` | A set-like array repeats an entry |
| `CONTENT_SEMANTIC_INVALID` | Individually valid fields form an impossible definition |
| `CONTENT_LIMIT_EXCEEDED` | Input would exceed any named inclusive capacity limit |
| `CONTENT_LOCALIZATION_KEY_MISSING` | A definition's required name or description key is absent from its pack's default-locale catalogs |

Environmental I/O failures use a separate non-content operation result with
one bounded category: `not-found`, `access-denied`, `changed-during-read`, or
`read-failed`. They abort candidate publication and may carry only a normalized
relative path in ordinary output. They are not retained content diagnostics or
fingerprint input; platform exceptions and absolute paths remain confined to
the separate developer log. Save compatibility and fingerprint mismatch
diagnostics enter scope in M6, not this initial content registry.

## Diagnostic precedence

A source can be invalid in several ways. Tests requiring one focused diagnostic
use this precedence:

1. root and manifest count;
2. bytes and UTF-8;
3. JSON grammar and duplicate properties;
4. schema and required or unknown properties;
5. ID, version, and path syntax;
6. dependency order and namespaces;
7. duplicate definitions and kind;
8. reference linking;
9. value, collection, and semantic validation; and
10. total content limits; and
11. required default-locale key completeness.

The loader may report several independent errors after safe parsing, but it must
emit the precedence winner first for the same property or candidate document.

## Fixture mapping

[`invalid/cases.json`](../../Tests/Spelljammer.Content.Tests/Fixtures/Milestone0/invalid/cases.json)
contains one focused case for every diagnostic code above. Each case supplies
virtual input files, optional test-only reduced limits or game version, and one
expected primary code.

Test-only limit overrides can only reduce a limit and are never accepted from a
real pack. The invalid UTF-8 fixture uses hexadecimal bytes because this
repository normalizes authored text files to UTF-8.

The valid fixture and canonical fingerprint live beside those cases under
`Tests/Spelljammer.Content.Tests/Fixtures/Milestone0`.
