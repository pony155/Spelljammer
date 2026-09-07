# Content contracts version 1

## Status

This document freezes the decisions required by roadmap phase M0.2 and the
version 1 fixtures. The Milestone 1 loader implements the foundation;
Milestones 3 through 5 add strict character, supernatural, encounter,
equipment, ship, module, and weapon-configuration profiles. Changes
to these decisions require updating the implementation, fixtures, and expected
fingerprints together; changes after a public save contract also require the
compatibility rules below.

The terms MUST, MUST NOT, SHOULD, and MAY describe normative requirements.

## Stable ID grammar

A gameplay Stable ID:

- is ASCII and therefore has identical character and UTF-8 byte counts;
- is between 3 and 127 bytes inclusive;
- contains 2 through 8 period-separated segments;
- matches this anchored regular expression:

```regex
\A[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*){1,7}\z
```

Segments start with a lowercase ASCII letter. Periods separate hierarchy;
hyphens separate words inside a segment. Uppercase letters, underscores,
whitespace, empty segments, consecutive hyphens, leading or trailing
punctuation, and Unicode are invalid.

Comparison and sorting use bytewise ordinal ascending order over the ASCII ID.
Code MUST NOT apply case folding, locale collation, path normalization, or
Unicode normalization to an ID.

The first segment identifies a broad content domain. A definition schema also
declares its exact kind, because domains such as `psychic` and `crisis` contain
families, phases, techniques, and resolutions with more specific prefix rules.

## Ownership and namespaces

The base pack ID is `spelljammer.base`. IDs without a `.mod.` namespace segment
are reserved to the base game unless a future contract explicitly delegates a
prefix.

A version 1 third-party pack ID MUST be `mod.<namespace>`, where `<namespace>`
is one valid ID segment. Its new definition IDs MUST begin
`<domain>.mod.<namespace>.` and contain at least one local-name segment, for
example:

```text
mod.starwrights
skill.mod.starwrights.gravimetry
feat.mod.starwrights.void-savant
spell.mod.starwrights.anchor-pulse
```

One enabled pack owns one mod namespace in version 1. A pack MUST NOT claim the
base namespace or another enabled pack's namespace. Namespace authentication
and publisher identity are distribution concerns and are not inferred from a
display name.

## Source encoding and JSON profile

Manifest and definition documents MUST be UTF-8 without a byte-order mark. A
single final LF is recommended but source line endings and insignificant JSON
whitespace do not affect semantic content.

Localization keys in definition envelopes and manifests use the existing
game-owned key contract: 2 or more dotted segments, 127 UTF-8 bytes maximum,
and segments that start with `a` through `z`, then contain only lowercase ASCII
letters, digits, or hyphens and do not end in a hyphen. Content validation does
not infer a key from a gameplay ID or display text.

Version 1 JSON:

- MUST contain one top-level object;
- MUST NOT contain comments, trailing commas, duplicate properties, `NaN`,
  infinities, or numbers outside the declared integer range;
- rejects unknown properties unless that schema explicitly reserves an
  extension object;
- uses case-sensitive property names;
- treats set-like arrays as unordered semantic sets but rejects duplicate
  entries; and
- preserves order only for fields whose schema explicitly calls them sequences.

Paths are forward-slash-separated, relative to the resolved pack root, and use
UTF-8 ordinal comparison. Empty components, `.`, `..`, absolute roots, drive
letters, URI schemes, NUL, and platform device paths are invalid. The loader
resolves the final target and rejects any escape from the pack root.

Every pack has exactly one root entry named `manifest.json`. Duplicate
normalized entries, including duplicate archive entries, are
`CONTENT_MANIFEST_MULTIPLE`. Each declared definition root is recursively
enumerated for regular files whose final extension is exactly lowercase
`.json`; normalized relative paths are sorted ordinally before parsing. Each
file contains exactly one definition object.

## Manifest schema version 1

Required properties are:

| Property | Type | Rule |
| --- | --- | --- |
| `schemaVersion` | integer | Exactly `1` |
| `id` | string | `spelljammer.base` or `mod.<namespace>` |
| `version` | string | Three nonnegative decimal components, no prerelease/build metadata in v1 |
| `displayNameKey` | string | Localization key, excluded from gameplay semantics |
| `gameVersionRange` | string | Exactly `>=X.Y.Z <A.B.C` with the lower version less than the upper |
| `dependencies` | array | Set of dependency objects |
| `loadAfter` | array | Set of pack IDs |
| `definitionRoots` | array | Nonempty set of valid relative directories |
| `localizationRoots` | array | Set of valid relative directories |
| `contentRevision` | integer | `1` through `2147483647` |

A dependency object contains only `id` and `versionRange`, using the same pack
ID and range syntax. The base pack has no dependencies or `loadAfter` entries
and is always first.

Each version component is either `0` or a nonzero decimal digit followed by
decimal digits, has no leading zero, and is at most `2147483647`.
`gameVersionRange` and dependency `versionRange` contain exactly one ASCII
space between the closed lower bound and open upper bound.

Dependency and `loadAfter` edges point from the named predecessor to the pack
that declares the edge. An absent `loadAfter` target is ignored; an absent
required dependency is an error. After fixing the base pack first, ordering
uses Kahn's topological algorithm and chooses the ordinally smallest pack ID
whenever more than one node is ready. A cycle in either edge kind is an error.

## Initial definition envelope

Every version 1 definition contains `schemaVersion` exactly `1`, `revision`
from `1` through `2147483647`, `id`, `nameKey`, and `descriptionKey`. The
registered directory kind and ID prefix must agree. Unknown fields fail rather
than being ignored.

Initial semantic fields are:

| Kind | Required semantic fields |
| --- | --- |
| Attribute | `minimum`, `maximum`, `defaultValue`, `tags` |
| Skill | `minimum`, `maximum`, `progressionCurveId`, `actionTags` |
| Access | `tags` |
| Background | `compatibleRaceIds`, `attributeBonusIds`, `focusSkillIds` |
| Character | `raceId`, `heritageId`, `backgroundId`, `scenarioIds`, `positionId`, `languageIds`, `scriptIds`, `equipmentIds`, `focusSkillIds`, `resourceIds` |
| Feat | `trainingProjectId`, `grantedAccessIds` |
| Heritage | `raceId`, `grantedPerkIds` |
| Perk | `compatibleRaceIds`, `grantedAccessIds`, `grantedTechniqueIds`; optional `grantedPerkIds`, `effectIds` |
| Race | `grantedPerkIds`; optional `requiredSupportIds` |
| Spell | `requiredAccessId`, `skillId`, `focusResourceId`, `focusCost`, `rangeId`, `castTimeTicks`, `cooldownTicks`, `targetTags`, `effectIds` |
| Psychic Technique | `requiredAccessId`, `skillId`, `resistanceSkillId`, `strainResourceId`, `strainCost`, `sustainCostPerTick`, `contactModeId`, `rangeId`, `informationScopeId`, `disciplineIds`, `targetTags`, `effectIds` |
| Technique | `requiredAccessIds`, `grantedPerkIds` |
| Training Project | `requiredSkillIds`, `workUnits`, `progressCap`, `facilityId`, `resourceId`, `resourceCost`, `safetyId`, `grantedFeatIds`, `grantedTechniqueIds` |
| Equipment | `slotId`, `initialStateId`, `resourceId`, `resourceCapacity`, `actionIds`, `effectIds` |
| Board Cell | `zoneId`, `q`, `r`, `capacity`, `cover`, `visibility`, `atmosphereId`, `gravityId`, `hazardTags` |
| Zone Link | `fromCellId`, `toCellId`, `accessId`, `oneWay`, `allowsRetreat` |
| Personal Board | `maximumOccupants`, `cellIds`, `linkIds`, `requiredObjectiveIds`, `retreatCellIds` |
| Encounter | `personalBoardId`, `contextId`, `hostileTeamId`, `ancientDefenseId`, `nonCombatObjectiveId`, `extractionObjectiveId` |
| Ship Frame | `maximumHull`, `baseArmor`, `maximumSlots`, `cargoCapacity`, `mountIds` |
| Ship Module | `slotCost`, `cargoDisplacement`, `maximumIntegrity`, `networkId`, `energyGeneration`, `energyConsumption`, `mountId`, `primaryEffectId`, `armorValue`, `shieldValue`, `shieldRechargeRate`, `shieldEnergyConsumptionRate`, `compatiblePathIds` |
| Ship Weapon Configuration | `networkId`, `resourceId`, `resourceCost`, `damage`, `rateOfFireTicks`, `effectiveRange`, `maximumRange`, `reloadTicks`, `damageTypeId`, `areaId`, `armorPenetration` |

Version 1 recognizes these exact first-level directories under a definition
root:

| Directory | Kind | Required ID prefix |
| --- | --- | --- |
| `Attributes` | `Attribute` | `attribute.` |
| `Skills` | `Skill` | `skill.` |
| `Access` | `Access` | `access.` |
| `Backgrounds` | `Background` | `background.` |
| `Characters` | `Character` | `character.` |
| `Feats` | `Feat` | `feat.` |
| `Heritages` | `Heritage` | `heritage.` |
| `Perks` | `Perk` | `perk.` |
| `Races` | `Race` | `race.` |
| `Spells` | `Spell` | `spell.` |
| `PsychicTechniques` | `PsychicTechnique` | `psychic.` |
| `Techniques` | `Technique` | `technique.` |
| `TrainingProjects` | `TrainingProject` | `training.` |
| `Equipment` | `Equipment` | `equipment.` |
| `BoardCells` | `BoardCell` | `cell.` |
| `ZoneLinks` | `ZoneLink` | `link.` |
| `PersonalBoards` | `PersonalBoard` | `board.` |
| `Encounters` | `Encounter` | `encounter.` |
| `ShipFrames` | `ShipFrame` | `frame.` |
| `ShipModules` | `ShipModule` | `module.` |
| `ShipWeaponConfigurations` | `ShipWeaponConfiguration` | `ship.weapon.` |

An unregistered directory, a file directly in a definition root, or a prefix
that disagrees with its directory produces `CONTENT_KIND_MISMATCH`. Any kind
not listed above requires a later schema-contract revision before acceptance.

The M0 valid fixture includes the Race and Training Project support definitions
needed to resolve the required Feat and Perk references.

A Race that grants a Perk MUST be listed by that Perk's
`compatibleRaceIds`. Both documents can be structurally valid and fully linked
while violating this cross-definition rule; that failure is
`CONTENT_SEMANTIC_INVALID`.

A Heritage references exactly one Race. Each granted Perk must be compatible
with that Race. An authored Character's Race must match its Heritage and be in
its Background compatibility set. Perk/Technique grant edges form one bounded
acyclic graph; missing targets fail linking and a cycle is
`CONTENT_SEMANTIC_INVALID`.

`progressionCurveId` and entries in `actionTags` identify allowlisted runtime
primitives in version 1; they are validated against the loader's built-in
primitive registry rather than linked as content definitions. The M0 registry
contains `progression.skill.standard`, `action.spell.cast`, and
`action.spell.identify`.

`tags` and `targetTags` contain zero through 64 values matching the single-segment portion of
the Stable ID grammar above. All `*Ids` arrays
are duplicate-free semantic sets. A definition has at most 256 content
references across all of its fields. `requiredSkillIds`, `grantedAccessIds` on
a Feat, and `compatibleRaceIds` are nonempty; other reference sets may be
empty. A training project must grant at least one Feat or
known technique. `workUnits` is an integer from 1 through 1,000,000 inclusive,
`progressCap` is at least `workUnits`, Spell Focus costs are positive, and
psychic strain costs remain within the runtime's 0–100 meter.

Base Attribute definitions use an exact 1–10 value range. Their default must be
inside that range. Base Skill definitions use an exact 0–100 range. General
version 1 storage supports signed 16-bit Attribute bounds and unsigned-byte
Skill bounds, but changing the base ranges is a semantic contract change.

## Version responsibilities

| Version | Owner | Changes when |
| --- | --- | --- |
| Pack `version` | Manifest | Any distributed pack release changes |
| Pack `contentRevision` | Manifest | Gameplay semantic content in that pack changes |
| Definition `schemaVersion` | Loader schema | Required structure or interpretation changes incompatibly |
| Definition `revision` | Definition owner | That definition's gameplay semantics change |
| Gameplay semantic fingerprint | Compiler | Any effective semantic value, pack order, or referenced definition changes |
| Generator version | Simulation subsystem | Seed-to-world generation algorithm or stream allocation changes |
| Formula version | Simulation subsystem | Allowlisted formula behavior or numeric order changes |
| Effect version | Simulation subsystem | Primitive effect behavior, ordering, or rollback changes |
| Save schema version | Persistence subsystem | Serialized campaign envelope or state interpretation changes |

Display text and optional presentation assets have their own pack release
version but do not increment definition revision or gameplay fingerprint unless
a gameplay field also changes.

## Compatibility decisions

| Change | Version 1 treatment |
| --- | --- |
| Localized text, translation, optional icon, or layout metadata only | Presentation-only; gameplay semantics remain exact |
| Pack version differs but ordered IDs and gameplay semantic fingerprint match | Compatible presentation update |
| Add, remove, or rename a gameplay definition | New semantic fingerprint; new campaign or explicit migration |
| Change numeric bounds, default, cost, tags, grants, prerequisite, target, effect, or formula reference | Semantic break for an existing campaign; migration required |
| Change generator, formula, or effect implementation | Version and fingerprint contract change; migration or retained old implementation required |
| Change Stable ID | Explicit ID migration; never inferred from name or file path |
| Change schema interpretation | New schema version and migration required |

Version 1 does not automatically label semantic differences compatible.
`Compatible` is reserved for an exact gameplay semantic fingerprint with a
presentation-only pack release difference. All other differences are Exact,
Migratable, Missing, or Incompatible under `Modding.md`.

## Canonical semantic serialization

The gameplay fingerprint is SHA-256 over a canonical UTF-8 JSON document. The
canonical byte contract is named `spelljammer-semantic-v1`:

- UTF-8 without BOM and exactly one final LF;
- no insignificant whitespace;
- object properties sorted by bytewise ordinal property name;
- definition kinds use the canonical order Access, Attribute, Background,
  Character, Feat, Heritage, Race, Skill, Perk, Technique, Training Project,
  Equipment, Board Cell, Zone Link, Personal Board, Encounter, Ship Frame,
  Ship Module, and Ship Weapon Configuration, with definitions sorted by
  Stable ID within each kind. This
  preserves the relative order and bytes of the reviewed M0 kinds;
- packs retained in resolved load order;
- set-like arrays deduplicated during validation and sorted ordinally;
- sequence arrays retain validated authored order;
- integers use shortest base-10 form with `0` as the only zero form;
- booleans are `true` or `false`, and null is `null`;
- strings escape quote and reverse solidus, and encode U+0000 through U+001F as
  lowercase `\u00xx`; other valid Unicode scalar values remain UTF-8; and
- presentation-only fields, source paths, timestamps, comments, whitespace,
  and filesystem metadata are excluded.

The root object has exactly `definitions`, `format`, and `packs` properties.
Each semantic definition includes its `id`, `kind`, `revision`,
`schemaVersion`, and kind-specific semantic fields. Each pack entry includes
only `contentRevision` and `id`. The campaign content lock stores the full pack
release version separately; excluding it here allows a presentation-only pack
release to preserve the gameplay semantic fingerprint.

The expected M0 canonical bytes and lowercase hexadecimal SHA-256 value are
stored with the fixtures. The implementation must reproduce those bytes rather
than treating the checked-in canonical file as compiler input.

## Stable command rejection codes

The current `CommandRejection` enum is not serialized. Future commands expose
stable string codes while retaining internal typed values.

Current expedition mappings are frozen as:

| Enum meaning | Stable code |
| --- | --- |
| Expedition ended | `command.expedition-ended` |
| Sector boundary | `command.sector-boundary` |
| Insufficient fuel | `command.insufficient-fuel` |
| Nothing to salvage | `command.nothing-to-salvage` |
| Already salvaged | `command.already-salvaged` |
| Hull already sound | `command.hull-already-sound` |
| Insufficient cargo | `command.insufficient-cargo` |
| Not at anchorage | `command.not-at-anchorage` |
| Insufficient prize | `command.insufficient-prize` |

`None` has no rejection code because the command was accepted.

Planned generic rejection IDs reserve:

```text
command.action-unknown
command.actor-missing
command.actor-cannot-act
command.target-missing
command.target-illegal
command.access-required
command.technique-unknown
command.skill-required
command.attribute-required
command.equipment-required
command.context-required
command.resource-insufficient
command.out-of-range
command.consent-required
command.queue-capacity
command.content-mismatch
```

Rejection IDs are localized by separate message keys and can carry bounded safe
arguments. Display text is never the dispatch value.
