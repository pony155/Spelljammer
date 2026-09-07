# Content ID ownership and collision review

## Status

This document completes roadmap tasks `M0.1.2` and `M0.1.3` and includes the
Milestone 5 character, supernatural, encounter, and ship ID extensions. It classifies the explicit IDs in
[`ContentIdInventory.md`](ContentIdInventory.md) and records
the spelling and collision review performed before version 1 loader work.

The gameplay loader and M5 base definitions now exist. No public save
format exists yet; classifications for later milestones remain design
commitments rather than implementation claims.

## Classification model

Ownership and delivery are separate:

- **Base-owned** means the ID is reserved for the Spelljammer base pack.
- **Example-only** means the string illustrates a future definition but is not
  committed as base content yet.
- **Deferred** means the ID is base-owned but its runtime definition is not
  required in the first implementation milestone that introduces its registry.
- **Prototype-serialized** means an existing save or public artifact already
  persists that exact string ID.

An ID may therefore be both base-owned and deferred. Example-only takes
precedence over base ownership until content authoring promotes it explicitly.

## Complete classification

Every ID in an inventory section inherits the classification below unless an
exception is listed. This grouping marks every inventoried ID without copying a
second 152-row table that could drift from the canonical inventory.

| Inventory group | Count | Ownership | Earliest registry | Delivery note |
| --- | ---: | --- | --- | --- |
| Attributes | 7 | Base-owned | M2 | All seven are required base definitions |
| Skills | 29 | Base-owned | M2 | All 29 are required base definitions |
| Racial Perks | 22 | Base-owned | M3 | Eleven Race and eleven first-slice Heritage grants are implemented |
| Races | 11 | Base-owned | M3 | All eleven base definitions are implemented |
| First-slice Heritages | 11 | Base-owned | M3 | One compatible Heritage per Race is implemented |
| Backgrounds | 1 | Base-owned | M3 | Shared expedition-veteran starting rules |
| Authored Characters | 11 | Base-owned | M3 | One deterministic first-voyage character per Race |
| Character techniques | 2 | Base-owned | M3 | Soul reconstitution and observed-trail interpretation |
| Training Projects | 4 | Base-owned | M3/M4 | Access projects plus Magic Missile and Mindlink learning |
| Roster equipment primitives | 15 | Base-owned | M3/M5 | Validated by first-voyage support; migrate to linked Item definitions in M5 |
| Crew positions | 11 | Base-owned | M3 | Authored responsibility IDs, not capability grants |
| Character languages | 9 | Base-owned | M3 | First-roster knowledge IDs |
| Character scripts | 7 | Base-owned | M3 | First-roster script knowledge IDs |
| Crew support requirements | 19 | Base-owned | M3 | Care, environment, nutrition/reserve, quarters, and rest gates |
| Character resources | 5 | Base-owned | M3/M4 | Bounded stores including Focus and Psychic Strain |
| Scenarios | 1 | Base-owned | M3 | `scenario.first-voyage` |
| Character action primitives | 5 | Base-owned | M3 | Standard check, Soul Anchor action/context, and bounded Race effects |
| Equipment | 11 | Base-owned | M5 | Five encounter items are implemented; six catalog candidates remain deferred |
| Learned Feats | 2 | Base-owned | M4 | Both are required access paths |
| Access gates | 2 | Base-owned | M4 | Both are required access definitions |
| Spells | 7 | Base-owned | M4 | Magic Missile is implemented; six remain catalog candidates |
| Psychic techniques | 4 | Base-owned | M4 | Mindlink is implemented; three remain catalog candidates |
| Combat contexts | 6 | Base-owned | M5 | Ruin is implemented; boarding, ship, and three later contexts remain primitive IDs |
| Travel events | 8 | Base-owned | M8 | Four first-voyage events and four deferred events |
| Travel-event choices | 3 | Base-owned | M8 | All three belong to the first Coolant Leak event |
| Ship modules | 35 | Base-owned | M5 | Eleven first-slice definitions are implemented; individual catalog delivery remains phased |
| Ship weapon configurations | 3 | Base-owned | M5 | Aether and Diesel are first-slice; Atomic is deferred atompunk content |
| Factions | 6 | Base-owned | M8 | Initial voyage selects three; remaining roster is deferred |
| Crisis families | 7 | Base-owned | M9 | All endgame content is deferred beyond the first voyage |
| Crisis phases | 7 | Base-owned | M9 | Shared M9 phase vocabulary |
| Crisis resolutions | 3 | Example-only | M9 | IDs appear only in the Shattered Meridian JSON example |

No inventoried ID is prototype-serialized. Current prototype enums and numeric
properties exist only in memory; they neither use these strings nor establish a
public save contract.

## First-slice and deferred exceptions

The planned first character-combat Spell set is:

```text
spell.elemental.burning-hands
spell.spirit.detect-invisibility
spell.spirit.magic-missile
spell.spirit.phantasmal-image
```

Magic Missile is implemented by M4. The other six Spell IDs remain base-owned
catalog candidates for later combat work.

The initial M5 combat contexts are:

```text
combat.context.ship
combat.context.boarding
combat.context.ruin
```

The following are deferred beyond the first Battle slice:

```text
combat.context.eva
combat.context.settlement
combat.context.surface
```

The first M8 travel-event slice is:

```text
event.travel.aether-squall
event.travel.coolant-leak
event.travel.derelict-signal
event.travel.distress-call
```

The other four Event IDs remain base-owned deferred catalog content.

The implemented M5 ship slice contains these eleven module definitions:

```text
module.cargo.hold
module.defense.arcane-energy-shield
module.defense.industrial-energy-shield
module.defense.reinforced-plating
module.power.aether-dynamo
module.power.diesel-generator
module.propulsion.flux-sail
module.propulsion.propellant-drive
module.prow.ram
module.weapon.arcane-deck-battery
module.weapon.industrial-deck-battery
```

The remaining inventoried modules remain base-owned deferred catalog content.
The first set supports energy packages, armor, Energy Shield, prow, weapon, and
cargo choices without requiring the full catalog.

The first M5 weapon slice includes `ship.weapon.arcane.aether-cannon` and
`ship.weapon.industrial.diesel-shell-cannon`. The Atomic Shell Cannon remains
base-owned deferred atompunk content.

The initial M8 faction candidate set is the Free Anchorage Compact, Horizon
Salvagers' Union, Meridian Foundry League, and Lumenwake Covenant; a campaign
uses the first two plus one of the latter pair. Pilgrim Garden Fleet and Quiet
Chorus Assembly are deferred roster content.

## Missing IDs resolved during review

The compact Skill table named 15 capabilities without Stable IDs. Version 1
cannot compile a dynamic Skill registry while those names remain implicit, so
the review added the following IDs to `Skills.md` and the inventory:

```text
skill.acrobatics
skill.astrogation
skill.athletics
skill.command
skill.deception
skill.defense
skill.eva
skill.gunnery
skill.insight
skill.piloting
skill.rigging
skill.salvage
skill.sensors
skill.stealth
skill.xenology
```

These are new explicit IDs derived directly from unique existing Skill labels;
no prior ID was renamed. `skill.medicine` already existed in the Ships sickbay
example and was moved into the authoritative Skill table without changing it.

Milestone 3 assigned and reviewed explicit Perk IDs for Hearthworld,
Dawnweave, Concord, Redwake, Coilwhisper, Hullrunner, Chorusborn, Crimson
Court, Reliquary-Bound, and Startrail. Cometdelver was already explicit.
Other Heritage rows remain deferred until their individual Perk IDs are
reviewed.

## Collision review

The 270 inventoried IDs are unique. No one ID maps to two different display
labels, and ordinal comparison produces no case-only collision.

The following similar forms are intentional:

| Forms | Decision |
| --- | --- |
| `skill.magic`, `feat.access.magic`, `access.magic` | Separate competence, learned grant source, and effective capability gate |
| `skill.psionics`, `feat.access.psionics`, `access.psionics` | Same three-layer distinction for psychic use |
| `spell.*` and `psychic.*` techniques | Preserve established domains; document kind validates the more specific prefix |
| `perk.race.*` and `perk.heritage.*` | Preserve grant provenance in identity |
| `crisis.<family>`, `crisis.phase.*`, and nested resolution IDs | Preserve separate family, phase, and resolution definition kinds |
| Hyphenated names such as `half-elf` and `nuclear-thermal-drive` | Hyphens are canonical within one segment; underscores are forbidden |

The design directory is `Docs/DesignConcept`. Architecture links,
README references, and repository guidance now use that path. Filename
`Endgame_Crisis.md` retains its underscore, but filenames do not participate in
gameplay identity.

Illustrative third-party IDs previously used a pack ID that looked more
specific than their shared namespace. Version 1 resolves this by using a pack
ID of the form `mod.<namespace>`, for example `mod.starwrights`, with definitions
such as `skill.mod.starwrights.gravimetry`. These examples are not base IDs and
are excluded from the 152 count.

## Review conclusion

No inventoried base ID requires a rename before loader work. The only
example-only gameplay IDs are the three Shattered Meridian resolutions, and no
current ID is constrained by prototype serialization.

Future ID additions must pass the grammar and ownership rules in
[`ContentContractsV1.md`](ContentContractsV1.md) and be added to the inventory
before their definitions are treated as base content.
