# Status and Conditions

## Status

This document defines the planned status model for characters, crew, and
personal encounters. It covers temporary conditions, needs, injuries,
capability effects, morale, and recovery. It does not replace the separate
ship module condition model in [`Ships.md`](Ships.md) or the encounter
resolution rules in [`Battle.md`](Battle.md).

The current simulation has partial support for character capability effects,
observable evidence, personal injuries, active encounter effects, and the
`CanAct` flag. A unified status lifecycle, needs simulation, treatment model,
and campaign recovery rules remain planned.

## Purpose

Status records describe how current circumstances change what an entity can do.
They are authoritative simulation state, not presentation labels. A status
must have a stable identity, an inspectable source, bounded duration or a
declared removal rule, and an explicit effect on commands, abilities,
resources, targeting, movement, or relationships.

Examples include:

- a wound that makes strenuous movement dangerous;
- decompression exposure that requires immediate stabilization;
- fatigue that reduces recovery and work capacity;
- fear that changes morale and available choices;
- a magical ward that blocks a declared effect;
- a psionic link that remains active until consent, range, or concentration
  ends; and
- hunger or thirst that creates a growing need rather than an unexplained
  numerical penalty.

Statuses must never silently rewrite permanent abilities, learned skills,
Race, Heritage, equipment identity, or player commands.

Health, Stamina, Mana, Resolve, and Strain are numeric character resources,
not statuses. A threshold crossing may apply an authored status such as Shaken
or Psionic Overload, but the status records its own source and lifecycle while
the resource remains in `CharacterResourceSet`. See
[`CharacterResourceSystem.md`](CharacterResourceSystem.md).

## Status categories

### Conditions

Conditions are temporary or persistent states that directly change action
legality or resolution. Examples include Stunned, Frightened, Restrained,
Hidden, Exposed, Burning, Poisoned, and Psionics Shock.

Each condition defines its tags, affected actions, resistance or prevention
rules, stacking behavior, duration, and removal conditions. A condition may
block an action, add a visible modifier, change available targets, or create a
new recovery command.

### Injuries

Injuries are authored consequences of harm. They are not merely reductions to
a health pool. The first roster includes severity bands such as:

- Superficial;
- Wounded;
- Critical; and
- Incapacitated.

Injury tags explain the specific consequence: bleeding, fracture, burn, poison,
vacuum exposure, radiation, or psionic shock. Medicine, equipment, time,
facilities, and supernatural support may stabilize or treat an injury, but
serious injuries persist after an encounter.

An incapacitated character may be rescued, captured, abandoned, stabilized, or
killed only through explicit commands and rules. Incapacitation must not be
treated as an automatic death result.

### Needs

Needs represent ongoing crew requirements rather than instantaneous effects.
The planned initial needs are:

- Rest;
- Food;
- Water or compatible hydration;
- Safe atmosphere;
- Medical attention;
- Belonging and social contact; and
- Safety and morale.

Needs use bounded integer bands and advance only on authoritative simulation
ticks. A need should pass through authored thresholds such as Satisfied,
Strained, Critical, and Crisis. Thresholds create visible decisions, such as
resting, rationing provisions, changing assignments, visiting the common room,
or seeking medical care.

Needs do not all apply to every species or environment. Race and Heritage may
declare compatibility, consumption, or environmental rules, but they do not
automatically define personality or morality.

### Capability effects

Capability effects are explicit changes granted by a spell, psionic active Feat,
feat, equipment, module, environment, or event. They may grant access, add a
ward, expose evidence, alter an action, or apply a condition.

Effects must identify their source and target. A source may be removed without
removing another independent source of the same capability. Derived values such
as magical access are recomputed from their grant sources during validation.

### Morale and relationships

Morale is a current condition shaped by visible losses, leadership, faction
standing, danger, supplies, authority, and credible escape options. It can
affect willingness to continue, surrender, negotiate, protect another actor,
or refuse a suicidal order.

Morale is not mind control. Magical or psionic coercion uses separate authored
effects, resistance, consent, and evidence rules. A morale change must record
the event or source that caused it so the player can understand and respond to
it.

Resolve is the numeric capacity to resist immediate mental pressure; morale is
an inspectable social and psychological condition with causes and choices.
Resolve reaching a configured threshold can contribute to Shaken, Vulnerable,
or Mental Break effects without replacing morale or forcing one universal
behavior.

## Status definition contract

An authored status or effect definition declares:

- a stable ID and localized name and description keys;
- category and tags;
- affected scopes, such as actor, equipment, room, ship, or encounter;
- magnitude bounds and any resistance type;
- duration, expiration, or removal rule;
- stacking and replacement behavior;
- whether it blocks, modifies, reveals, consumes, or creates an action;
- treatment, counter-effect, or recovery tags; and
- the evidence or event key shown to the player.

Definitions reference reviewed primitive formulas and effects. Content supplies
parameters; simulation code owns execution order, capacity, validation, and
rollback.

An authored definition should resemble:

```json
{
  "schemaVersion": 1,
  "id": "status.injury.vacuum-exposure",
  "nameKey": "status.injury.vacuum-exposure.name",
  "descriptionKey": "status.injury.vacuum-exposure.description",
  "category": "injury",
  "tags": ["physical", "medical", "environmental"],
  "severityMinimum": 1,
  "severityMaximum": 3,
  "stacking": "replace-weaker",
  "treatmentTags": ["medicine", "atmosphere", "sickbay"],
  "effectIds": ["effect.injury.vacuum-exposure"]
}
```

Applied state stores the definition ID and revision, instance ID, source ID,
target ID, start tick, expiration or removal condition, severity, stacks, and
relevant evidence. Localized text, renderer handles, and UI selection state
are never stored as status identity.

## Stacking and replacement

Every status declares one stacking rule:

| Rule | Behavior |
| --- | --- |
| `unique` | A second application is rejected or becomes evidence |
| `refresh-duration` | Existing magnitude remains and duration is refreshed |
| `add-stacks` | Bounded stacks increase up to the authored maximum |
| `replace-weaker` | The stronger application replaces the weaker one |
| `independent` | Separate instances remain, each with its own source and expiry |

No status may stack without a declared maximum. Replacement is transactional:
the new status is validated before the old working state is retired.

## Fixed-tick lifecycle

Status processing follows a stable order on each authoritative tick:

1. Publish commands and environmental observations for the tick.
2. Expire statuses whose removal tick has been reached.
3. Resolve needs, hazards, and scheduled status transitions.
4. Validate actions against the resulting status snapshot.
5. Commit actions, injuries, effects, resources, and evidence atomically.
6. Apply recovery progress and emit bounded status events.
7. Publish the next immutable snapshot.

Rendering cadence, WPF layout, animation, and background completion order must
not alter this sequence. Status effects that affect the same target and tick
are ordered by stable source ID and then instance ID.

## Resistance and recovery

Resistance is an explicit part of an effect or action. It may use an ability,
skill, equipment tag, ward, environment, or consent rule. Resistance changes
the declared outcome; it does not silently make the action invalid unless the
definition says that the effect is prevented.

Recovery is also authored. Examples include:

- Medicine stabilizing bleeding or treating a wound;
- Engineering restoring safe atmosphere;
- Cooking and provisions reducing food-related need;
- Rest reducing fatigue when the habitat is safe;
- Negotiation or Leadership restoring morale;
- Magic or Enchantment maintaining a ward; and
- Psionics ending or resisting a mindlink under its declared rules.

Treatment consumes time, facilities, supplies, or specialist capacity where
the definition requires it. Successful treatment emits evidence and a
recovery event; it does not erase the history of the injury.

## Ownership and boundaries

The character owns personal conditions, injuries, needs, morale, capability
effects, and recovery state. Encounters own local hazards, tactical effects,
temporary objectives, and combat timing. Ships own Hull, Armor, Shield Value,
module condition, networks, and ship-wide hazards.

Cross-boundary effects use explicit commands or events. For example, a reactor
hazard may apply a radiation injury to a character only through a declared
encounter or ship event. Character status code must not directly mutate ship
modules, and ship code must not edit character capability arrays in place.

## Persistence

Campaign saves persist status instances at documented commit boundaries. The
save must include:

- definition ID and revision;
- stable instance and source IDs;
- target character or encounter ID;
- severity, stacks, and bounded values;
- start tick and expiration or recovery progress;
- treatment or removal state; and
- evidence references needed to explain the consequence.

Loading validates fingerprints, revisions, IDs, bounds, source ownership,
stacking rules, and treatment references before publishing the restored state.
An incompatible status definition requires a migration; it must not be
silently dropped or rerolled.

## Presentation

The UI should group statuses by consequence rather than display an unreadable
list of icons. Each entry should show:

- name, category, severity, and stacks;
- source and start time when known;
- current effect on actions, abilities, resources, or needs;
- remaining duration or the condition required for removal; and
- available treatment, resistance, or recovery commands.

Hidden information remains hidden according to the encounter's detection and
knowledge rules. The player may see that an action failed because of an
unknown condition without automatically learning its full definition.

## First playable scope

The first crew-enabled slice should implement only the statuses needed to make
the voyage loop readable:

| Area | Initial statuses |
| --- | --- |
| Action availability | Incapacitated, Stunned, Restrained |
| Injury | Wounded, Critical, Bleeding |
| Crew needs | Fatigued, Hungry, Unsafe Atmosphere |
| Recovery | Stabilized, Under Treatment |
| Morale | Shaken, Surrendered |
| Supernatural | One bounded ward and one temporary magical or psionic effect |

The first slice must exercise status application, inspection, expiration or
treatment, persistence through encounter cleanup, and deterministic save/load
restoration. It does not need a complete disease, addiction, weather, trauma,
or long-term relationship simulation.

## Validation and bounds

Content loading rejects duplicate or missing status IDs, missing localization
keys, invalid category or tag values, negative magnitudes, unbounded stacks,
missing removal rules, unknown effect references, and treatment definitions
that cannot be resolved.

Runtime limits bound statuses per character, status instances per encounter,
stacks per instance, evidence entries, recovery projects, and status events per
tick. When a limit is reached, the simulation returns a stable rejection or
uses the definition's declared replacement rule; it never grows an unlimited
collection.

CI-owned tests should cover deterministic ordering, stacking and replacement,
resistance, treatment cost rollback, incapacitation and recovery, encounter
cleanup, status persistence, invalid definitions, and content migration.
