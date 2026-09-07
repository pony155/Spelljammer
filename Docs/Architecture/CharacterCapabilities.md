# Character capability implementation

## Status

Milestone 4 implements the data and runtime contracts in this document for
Attributes, Skills, learned Feats, Racial Perks, access grants, techniques,
character creation, eligibility, practice, training, and roster inspection.
Magic Missile and consensual Mindlink now provide the first complete
supernatural execution paths; the wider catalogs remain planned.

Product behavior is defined in
[`../DesignConcept/Attributes.md`](../DesignConcept/Attributes.md),
[`../DesignConcept/Skills.md`](../DesignConcept/Skills.md),
[`../DesignConcept/Races.md`](../DesignConcept/Races.md),
[`../DesignConcept/Spells.md`](../DesignConcept/Spells.md), and
[`../DesignConcept/PsychicAbilities.md`](../DesignConcept/PsychicAbilities.md).

## Core decision

Source code implements generic attribute, skill, grant, training, and action
machinery. The seven base Attributes and base Skill list are content, not enum
members or fixed properties on `CharacterState`.

Avoid:

```csharp
enum AttributeType { Strength, Agility, Toughness }

sealed class CharacterState
{
    public int Strength { get; set; }
    public int Engineering { get; set; }
}
```

Use validated stable-ID wrappers at content, command, event, and save
boundaries:

```csharp
public readonly record struct AttributeId(string Value);
public readonly record struct SkillId(string Value);
public readonly record struct FeatId(string Value);
public readonly record struct PerkId(string Value);
public readonly record struct AccessId(string Value);
public readonly record struct TechniqueId(string Value);
```

Default values are invalid, equality is ordinal, and syntax is validated at
construction so malformed IDs cannot travel through the simulation.

## Definition and state separation

| Concern | Immutable definition | Persistent state |
| --- | --- | --- |
| Attribute | ID, keys, bounds, tags, generation rule | Value by Attribute ID |
| Skill | ID, keys, range, progression curve, action tags | Value and bounded practice by Skill ID |
| Feat | ID, keys, training project, grants | Learned Feat IDs and provenance |
| Perk | ID, keys, Race/Heritage compatibility, grants | Perk IDs granted by Race and Heritage |
| Access | Stable capability gate | Effective grants with their sources |
| Technique | ID, prerequisites, costs, and effects | Known IDs and learning projects |

Definitions describe meaning; state records what happened to one character.
Character state never copies localized text or complete definitions.

## Definition examples

The authoritative envelope, fields, ranges, and reference behavior are frozen
in [`ContentContractsV1.md`](ContentContractsV1.md). The examples below follow
that contract.

An Attribute document resembles:

```json
{
  "schemaVersion": 1,
  "revision": 1,
  "id": "attribute.strength",
  "nameKey": "attribute.strength.name",
  "descriptionKey": "attribute.strength.description",
  "minimum": 1,
  "maximum": 10,
  "defaultValue": 5,
  "tags": ["physical", "force"]
}
```

A Skill document resembles:

```json
{
  "schemaVersion": 1,
  "revision": 1,
  "id": "skill.engineering",
  "nameKey": "skill.engineering.name",
  "descriptionKey": "skill.engineering.description",
  "minimum": 0,
  "maximum": 100,
  "progressionCurveId": "progression.skill.standard",
  "actionTags": [
    "action.machine.operate",
    "action.module.diagnose",
    "action.module.repair"
  ]
}
```

Skills do not own one permanent governing Attribute. Each action definition
declares allowed Attribute approaches, its recommended approach, Skill,
technique, equipment, target, cost, and circumstance requirements.

## Feats, Perks, and access

A trained Feat and Racial Perk can grant the same capability with different
provenance:

```json
{
  "schemaVersion": 1,
  "revision": 1,
  "id": "feat.access.magic",
  "nameKey": "feat.access.magic.name",
  "descriptionKey": "feat.access.magic.description",
  "trainingProjectId": "training.magic.spellcasting",
  "grantedAccessIds": ["access.magic"]
}
```

```json
{
  "schemaVersion": 1,
  "revision": 1,
  "id": "perk.race.elf.aether-sense",
  "nameKey": "perk.race.elf.aether-sense.name",
  "descriptionKey": "perk.race.elf.aether-sense.description",
  "compatibleRaceIds": ["race.elf"],
  "grantedAccessIds": ["access.magic"],
  "grantedTechniqueIds": []
}
```

| Access | Learned source | Initial innate source |
| --- | --- | --- |
| `access.magic` | `feat.access.magic` | Elf Aether Sense |
| `access.psionics` | `feat.access.psionics` | Somnari Mindwake |

Effective access is derived from validated grant sources, not stored as an
independent mutable boolean. Removing one source cannot disable access still
provided by another source, and inspection can always explain why access
exists.

Access grants permission only. They do not grant Skill ranks or all spells and
techniques. A cast needs `access.magic`, the known Spell ID, required Skill,
resources, and a legal context.

## Compiled registry and storage

The content compiler sorts stable IDs ordinally and assigns dense indices
scoped to one `ContentFingerprint`. Runtime values can then use bounded arrays:

```csharp
public sealed class CharacterCapabilities
{
    private readonly ImmutableArray<short> attributeValues;
    private readonly ImmutableArray<byte> skillValues;
    private readonly ImmutableArray<ulong> knownFeatBits;
    private readonly ImmutableArray<ulong> knownPerkBits;
    private readonly ImmutableArray<ulong> knownTechniqueBits;
}
```

These members illustrate layout rather than mandate exact collection types.
Published values are immutable and bounded by the registry. Dense indices never
cross a save, command-log, mod, or public serialization boundary.

Persistent stable ID/value pairs are translated to indices only after the exact
compatible snapshot is selected. Unknown IDs, duplicates, out-of-range values,
missing required Attributes, and incompatible Perks reject validation or
enter an explicit migration.

## Character state

The implemented immutable state boundary follows this shape:

```csharp
public sealed class CharacterState
{
    public required CharacterId Id { get; init; }
    public required RaceId RaceId { get; init; }
    public required HeritageId HeritageId { get; init; }
    public required CapabilityValues Capabilities { get; init; }
    public required ImmutableArray<TrainingProjectState> Training { get; init; }
    public required ImmutableArray<InjuryState> Injuries { get; init; }
}
```

Mutation occurs through accepted commands at a fixed-tick commit boundary. UI
receives a read-only snapshot and never edits these collections directly.

Race and Heritage grant Perk IDs during validated creation. Background can
grant initial Skills and documented pre-campaign training. Crew position grants
responsibility and authority, not personal Skills, Feats, or Perks.

## Action eligibility and resolution

`CharacterActionSystem` now performs this ordered validation, returns a
non-mutating `ActionReservation`, resolves the standard integer check from an
owned `(seed, sequence)` stream, and publishes costs, successful grants,
practice, and explanation events in one replacement state.

Eligibility is separate from outcome resolution. A command checks:

1. actor exists, is controllable, and can act;
2. action and target definitions exist;
3. required access IDs have valid grant sources;
4. the required Spell, psychic ability, or other technique is known;
5. Attribute and Skill prerequisites pass;
6. equipment, resources, position, range, consent, and environment pass;
7. bounded costs can be reserved atomically; and
8. success and failure both have declared commit and rollback behavior.

Rejection returns a stable code and safe structured arguments. It consumes
nothing and cannot reveal protected target information.

After eligibility, `CapabilityResolver` combines the selected Attribute,
Skill, equipment, assistance, technique, circumstances, and owned deterministic
random stream. The committed event records the stable IDs and numeric modifiers
needed to explain and replay the result.

## Training and advancement

`CharacterTrainingSystem` keeps bounded partial work separate from effective
grants and applies the Feat plus derived Access provenance only at completion.
Skill practice is keyed by a bounded evidence ID so repeating the same trivial
action cannot award progress indefinitely.

Skill practice and Feat training are separate state machines.

- Meaningful Skill actions grant bounded practice to one declared Skill.
- A training definition owns prerequisites, work, facilities, costs, safety,
  progress cap, and completion grants.
- Partial Feat training never provides partial supernatural access.
- Completion validates and commits the learned Feat and access source together.
- Race and Heritage Perks come from creation or an explicit transformation,
  never ordinary practice.
- Trivial repetition cannot generate unbounded progress.

Progress uses deterministic integer or fixed-point units. Wall-clock duration
and floating-point accumulation do not determine results.

## Formula and effect boundary

Version 1 data cannot embed arbitrary expressions. It references reviewed,
bounded primitive IDs implemented by Simulation:

```text
formula.check.standard
formula.training.progress
effect.resource.consume
effect.access.grant
effect.injury.apply
```

Content supplies parameters; code owns execution, ordering, limits, and
rollback. Adding a primitive effect is a source change with coverage. Combining
existing primitives into a Spell, Feat, item, or encounter is content work.

## Save contract

Serialized capabilities use stable IDs:

```json
{
  "raceId": "race.elf",
  "heritageId": "heritage.elf.dawnweave",
  "attributes": {
    "attribute.strength": 6,
    "attribute.agility": 9
  },
  "skills": {
    "skill.magic": 27,
    "skill.engineering": 12
  },
  "learnedFeatIds": [],
  "perkIds": [
    "perk.race.elf.aether-sense",
    "perk.heritage.elf.dawnweave"
  ],
  "knownTechniqueIds": ["spell.spirit.magic-missile"]
}
```

`access.magic` is reconstructed from the Perk instead of copied as an
unexplained flag. A cached derived value may be retained for diagnostics, but
load validation recomputes and compares it.

The campaign header stores the content fingerprint and exact pack list. A
different definition set requires an explicit compatible migration before
character state is published.

## Presentation

Simulation snapshots expose IDs, values, effective grant sources,
requirements, and safe result details. WPF resolves localized presentation
separately and iterates registry definitions instead of assuming fixed
Strength or Engineering properties.

`RosterInspection` is the initial headless presentation adapter. It iterates
the active Attribute and Skill registries and accepts a localization resolver,
including for disabled-action codes; it never mutates character state or binds
fixed capability properties.

The interface supports unknown future definitions, content limits, missing
optional icons, long localized names, and disabled actions with explicit
reasons.

## Validation and verification

Definitions reject invalid bounds, defaults outside range, unknown progression
or effect IDs, invalid grants, missing compatibility, grant cycles, and
technique requirements that cannot be satisfied.

Characters reject missing Attributes, unknown state IDs, duplicate Feats or
Perks, incompatible racial grants, out-of-range values, access without
provenance, unbounded training, and known techniques beyond capacity.

CI-owned tests cover:

- adding an Attribute or Skill without new enum members or state fields;
- deterministic indices and action results;
- trained and innate sources granting the same access independently;
- removal of one source while another valid source remains;
- high Skill failing active use without access or technique knowledge;
- atomic training completion and cost rollback;
- save round-trip through stable IDs; and
- invalid content preserving the previous registry and character.

Adding arbitrary Attributes is an advanced public mod feature because all
generation, action, formula, and UI consumers must already be generic. Storage
and registries support it from the beginning even if the first public release
temporarily restricts that content kind.
