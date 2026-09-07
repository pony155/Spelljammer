# Character Battle Statistics and Resources

## Purpose and implementation status

This document is the authoritative design and architecture contract for
personal-combat statistics, character resources, Turn Meter, and Action
Points. It combines the former battle-stat and character-resource documents so
that combat formulas cannot drift away from the resource economy they consume.

The simulation currently implements:

- the five-resource `CharacterResourceSet`;
- data-driven `CharacterTurnState`, Turn Meter, and Action Points;
- stamina-based Turn Meter modifiers;
- per-action AP costs;
- encounter recovery and Health damage; and
- save round-tripping for character and encounter-local resource state.

The scenario-selected profile at
`Content/Packs/base/Definitions/CharacterResourceProfiles/standard.json` owns
base values, thresholds, stamina speed bands, recovery, and personal-action AP
costs. `VoyageWorld` must not contain fallback combat-economy numbers.

Threshold-driven status application, authored Resolve attacks, psionic
overload outcomes, activity-sensitive recovery, attribute-derived Turn Meter
gain, final damage formulas, and final HUD/timeline presentation remain
planned. The current simulation exposes threshold and percentage queries but
does not assign one universal status or overload consequence.

## Design principles

Characters are classless combinations of Attributes, Skills, equipment,
Feats, species, and current state:

> Attributes represent innate capability. Skills represent trained
> proficiency. Equipment determines how those capabilities become combat
> performance. Resources determine how long that performance can be sustained.

Every statistic must answer a different tactical question. In particular:

- Turn Meter determines **when** a character acts.
- Action Points determine **how much** the character can do while active.
- Health, Stamina, Mana, Resolve, and Strain determine **how long** the
  character can sustain different pressures or actions.
- Armor, Evasion, Willpower, and optional Psi Shield modify attack resolution;
  they are not spendable resources.

All persistent identities, costs, thresholds, gains, decay, recovery, and
modifiers are data-driven. Values used below are illustrative balance examples,
not hard-coded rules.

## Combat layers

```text
Physical attack
    -> attack Skill versus Evasion
    -> Armor mitigation and penetration
    -> Health damage and physical statuses

Mental or psionic attack
    -> Psionics versus Psi Shield and Willpower
    -> Resolve damage and mental statuses

Activation economy
    -> Turn Meter determines when the actor activates
    -> Action Points limit actions during that activation
    -> Stamina, Mana, or Strain constrain repeated specialized actions
```

Psionics must not behave as magic with a renamed damage type. It primarily
interacts with Resolve, Willpower, Strain, control, positioning, morale, and
enemy-specific mental rules.

## Character resources

Health, Stamina, Mana, and Resolve are conventional resources:

```text
0 <= CurrentValue <= MaxValue
```

They normally begin an encounter at their configured maximum. Strain uses the
same bounded representation but has inverse semantics: it begins at zero and
accumulates toward `MaxStrain`.

Resources remain separate from Attributes and Skills so that maximum values,
recovery, costs, damage, accumulation, decay, and modifiers can evolve
independently.

### Health

Health represents physical injury and survivability. Melee, firearms, energy
weapons, explosions, hazards, and explicitly physical supernatural effects may
damage it after mitigation or penetration rules resolve.

Health reaching zero enters authored incapacitation or death handling. It does
not imply automatic death. Rescue, stabilization, capture, retreat, and death
remain explicit outcomes owned by encounter and status rules.

Health does not regenerate during ordinary combat unless an ability, item,
Feat, equipment effect, or status explicitly restores it. It is always clamped
and never becomes negative.

### Stamina

Stamina represents physical exertion across multiple activations. It does not
replace AP: AP limits the current activation, while Stamina limits repeated
sprinting, heavy attacks, defensive maneuvers, demanding movement, and other
physical techniques.

Basic actions should generally remain usable at low Stamina. For example, a
basic shot may cost AP without Stamina, while a heavy strike may consume both.
This prevents exhaustion from making a character completely unable to act.

Stamina recovers naturally according to the selected profile. Future recovery
may consider current activity, Attributes, armor weight, equipment, statuses,
Feats, and injuries.

Low Stamina may reduce Turn Meter gain through authored percentage bands. The
standard profile defines those bands and their multipliers; code must not
embed assumptions such as a particular threshold or penalty. Exhausted
characters can still act, but usually act less frequently.

### Mana

Mana is magical energy. A magical active Feat normally requires AP plus a
data-defined Mana cost. It is unavailable when either cost cannot be paid.

Mana and Psionics are separate systems. A psionic active Feat does not consume
Mana unless its definition explicitly declares a cross-resource cost. Mana
recovery is generally slower or more restricted than Stamina recovery and may
come from rest, consumables, equipment, magical environments, Feats, or other
authored effects.

### Resolve

Resolve is short-term mental resilience under fear, coercion, trauma, and
hostile mental or psionic pressure. It is primarily defensive and is not the
normal activation currency for psionic Feats.

Authored Resolve bands may represent states such as Stable, Shaken,
Vulnerable, and Mental Break. Crossing a threshold does not itself force one
universal result. The triggering action or status definition chooses whether
the outcome is panic, confusion, stun, retreat, susceptibility to domination,
or another bounded effect.

Resolve-related statuses may modify Turn Meter or AP, but those consequences
belong to the status system. Resolve recovery may consider Willpower,
leadership, nearby allies, rest, Feats, and current statuses.

Resolve is distinct from morale. Resolve is immediate numeric capacity;
morale is an inspectable social or psychological condition with causes and
choices. It is also distinct from optional campaign-level Sanity.

### Strain

Strain measures psionic exertion and overload risk:

```text
0 <= Strain <= MaxStrain
Default Strain = 0
```

A psionic active Feat normally costs AP and generates Strain rather than
spending Resolve. More powerful effects generally generate more Strain, while
decay may depend on Attributes, Feats, equipment, rest, mental status, and
current Resolve.

Authored percentage bands may represent Normal, Strained, Critical, and
Overload states. Possible consequences include reduced Resolve recovery,
accuracy or effectiveness penalties, backlash risk, reduced Turn Meter gain,
Resolve damage, interruption, confusion, or temporary loss of psionic access.
The exact consequence belongs to the triggering definition or applied status.

Maximum Strain must not automatically kill or permanently disable a character.
The player may deliberately accept a configured overload risk.

## Turn economy

### Turn Meter

Turn Meter is encounter timing state, not a conventional character resource.
It advances toward a profile-defined threshold. Reaching that threshold makes
the character eligible for activation; ending the activation resets or
reduces the meter according to the selected scheduling model.

Turn Meter gain may be modified by a speed-oriented Attribute, Stamina bands,
injuries, armor, statuses, haste or slow effects, equipment, and psionic
effects. Stable ordering and fixed simulation ticks determine ties and meter
advancement.

Do not let one Attribute strongly increase both Turn Meter gain and maximum AP.
That would multiply activation frequency by actions per activation and make
the two systems redundant.

### Action Points

At activation start, `CurrentAP` is restored to the configured `MaxAP`. Actions
consume authored AP costs and are rejected before mutation when insufficient
AP or another required resource is available. AP never becomes negative.

Unless an authored effect says otherwise, unused AP is discarded at activation
end. Maximum-AP modifiers should be uncommon, bounded, and source-owned.
Activation frequency belongs primarily to Turn Meter; actions per activation
belong to AP.

The default activation sequence is:

```text
Turn Meter reaches its threshold
    -> character becomes active
    -> CurrentAP is initialized
    -> actions validate and spend their declared costs
    -> player or AI ends the activation
    -> unused AP is discarded
    -> Turn Meter begins advancing again
```

### Reactions

Overwatch, opportunity attacks, counterattacks, intercepts, and defensive
blocks do not require a separate Reaction Point resource. The normal action
pays AP to create a bounded reaction state; the reaction may then trigger
before the character's next activation. A definition may explicitly choose a
different model later.

## Defensive battle statistics

### Armor

Armor protects against physical attacks and may come from worn equipment,
shields, natural protection, magical effects, vehicles, or cover. It is not
automatically extra Health.

```text
Incoming physical damage
    -> Armor mitigation and penetration
    -> Health damage
```

Weapons may declare different base damage, Armor damage, penetration, critical
behavior, and status effects so that equipment changes tactical behavior
rather than only damage per second.

### Evasion

Evasion is the ability to avoid a physical attack. Contributors may include
Agility, movement, cover, shields, equipment weight, statuses, and Feats.
Evasion primarily modifies hit resolution rather than damage:

```text
Hit chance = reviewed base formula
           + attacker Skill
           + situational modifiers
           - target Evasion
```

Minimum and maximum hit chances, if used, are data-owned bounds.

### Willpower

Willpower resists mental and psionic attacks. It is a defensive statistic, not
a spendable pool:

```text
Physical defense: attack Skill versus Evasion
Mental defense:   Psionics versus Willpower
```

A successful mental attack may then damage Resolve, apply an authored status,
interrupt an action, or trigger another declared effect. Willpower may be
derived from Attributes, Background, species, Feats, psionic training,
equipment, and current statuses.

### Psi Shield

Psi Shield is an optional specialized defense supplied by rare equipment,
artifacts, implants, species capabilities, magic, or active Feats. Not every
character has it. A configured resolution may process Psi Shield before
Willpower, Resolve damage, and mental effects.

### Sanity

Sanity is an optional future campaign statistic for long-term psychological
change caused by isolation, loss, cosmic hazards, parasites, forbidden
artifacts, or repeated psionic exposure. It is not required by the initial
combat model:

```text
Resolve = short-term encounter pressure
Sanity  = long-term campaign condition
```

Persistent consequences should be authored statuses or Feats with explicit
causes and recovery, not invisible permanent mutations.

## Attributes, Skills, and weapon scaling

Attributes and Skills must produce different builds:

```text
Attributes = natural physical or mental capability
Skills     = trained proficiency
Weapons    = scaling, requirements, and tactical role
```

For melee combat, Strength should influence damage, penetration, carrying or
weapon requirements, while Melee Weapons primarily influences accuracy,
criticals, and access to special active Feats. Skill may make a smaller damage
contribution without becoming an indirect multiplier for every Strength
effect.

```text
Final melee damage = reviewed weapon formula
                   x Strength modifier
                   x Skill modifier
                   x critical modifier
                   x target mitigation
```

All coefficients and bounds come from data. Heavy hammers may scale strongly
with Strength, rapiers with Melee Weapons, and balanced weapons with both. This
supports strong but inaccurate deckhands, precise duelists, and trained
all-round marines without requiring character classes.

Initial or planned combat Skills include Melee Weapons, Firearms, Energy
Weapons, Throwing, and Psionics. Heavy Weapons, Arcane Weapons, Ship Weapons,
or Unarmed may be added only when the split creates meaningful choices. Weapon
specialization should normally use Feats, familiarity, Background bonuses, or
equipment requirements rather than fragmenting every weapon family into a
separate Skill.

Throwing governs accuracy, effective range, scatter, and placement for
grenades, knives, bombs, alchemical weapons, and magical charges. Strength may
separately affect the range of heavy thrown objects.

## Psionic attack model

Psionic actions emphasize disruption and control. Examples include:

- Mind Blast: Psionics versus Willpower, Resolve damage, Daze, or interruption.
- Psionic Scream: area Resolve pressure and Fear against vulnerable targets.
- Dominate: a resisted, temporary control effect gated by an authored Resolve
  threshold or a significant attacker advantage.
- Mental Barrier: temporary Psi Shield or Willpower support.
- Psionic Lance: focused Resolve damage with optional physical interaction for
  specifically authored creatures.

Enemy definitions declare exceptional behavior. Constructs may ignore Fear or
ordinary mind control; a hive mind may resist control but remain vulnerable to
disruption; an aberration may resist or retaliate against failed attacks.

## Costs and cross-system interactions

Every action declares costs and resource effects through content data. Action
implementations use shared primitives and must not embed bespoke resource
logic. Typical patterns are:

```text
Basic physical action:       AP
Demanding physical action:   AP + Stamina
Magical action:              AP + Mana
Psionic action:              AP + Strain gain
Hostile mental action:       attack resolution + Resolve damage/status
```

Cross-resource actions are permitted when explicitly authored, but should be
exceptions. Validation checks all costs before publishing mutation so a failed
action cannot spend AP while leaving another required resource unchanged.

## Runtime ownership and shared operations

`CharacterResourceSet` owns the five long-duration values.
`CharacterTurnState` owns encounter timing and AP. They are separate immutable
states: AP is not stored as Stamina, and Turn Meter is not a conventional
resource.

Permanent and temporary modifiers retain stable source IDs so equipment,
Feats, injuries, and statuses can add and remove their own effects without
rewriting base values. Conventional resources support current, maximum, base
maximum, recovery, and source-owned modifiers. Strain additionally exposes
decay and threshold state; turn state exposes meter threshold, gain modifier,
current AP, and maximum AP.

Shared resource operations cover spending, restoration, damage, percentage
queries, clamping, Strain generation and reduction, and threshold queries.
The implemented turn API includes `AddTurnMeter`, `ResetTurnMeter`,
`CanActivate`, `BeginActivation`, `EndActivation`,
`CanSpendActionPoints`, `SpendActionPoints`, `RestoreActionPoints`, and
`GetActionPointCost`.

Abilities, AI, items, statuses, equipment, and hazards call these shared
operations rather than duplicating arithmetic.

## Bounds, validation, and persistence

All resource and turn values are clamped to authored bounds:

```text
Health, Stamina, Mana, Resolve: 0 .. corresponding maximum
Strain:                         0 .. MaxStrain
Turn Meter:                     0 .. configured threshold
CurrentAP:                      0 .. MaxAP
```

Definitions are validated before publication. Invalid maxima, thresholds,
recovery values, action costs, speed bands, unknown IDs, duplicate modifier
sources, and unbounded collections are rejected with stable diagnostics.

Campaign saves persist base/current/max resource values, modifier sources,
profile identity and revision, and encounter-local turn state at documented
commit boundaries. Loading validates schema, profile fingerprints, IDs,
bounds, and source ownership before replacing working state. Released contract
changes require migration rather than silent rerolls or fallback numbers.

Random outcomes use explicitly owned seeded streams. Fixed simulation ticks,
stable IDs, and documented command ordering ensure rendering cadence and UI
timing cannot change combat results.

## Status integration

Resources are numeric state, not statuses. A threshold crossing may request an
authored status such as Shaken, Critical Strain, or Psionic Overload, but that
status owns its own definition, source, duration, stacking, and removal rule.
See [`../Concept/Status.md`](../Concept/Status.md).

Physical statuses may include Bleeding, Burning, Poisoned, Stunned, Knocked
Down, Crippled, Suppressed, or Blinded. Mental statuses may include Shaken,
Afraid, Dazed, Confused, Panicked, Berserk, Dominated, or Catatonic. These
create tactical consequences beyond raw damage without silently rewriting
permanent Attributes, Skills, or Feats.

## Presentation

Do not display every value as an identical bar:

- Health, Stamina, Mana, Resolve, and Strain belong in character status.
- Turn Meter belongs in a timeline, turn-order display, or activation queue.
- AP is prominent for the active character.
- Armor, Evasion, Willpower, Psi Shield, and relevant offense Skills belong in
  a compact combat panel or contextual detail view.

Mana may be hidden or de-emphasized for characters without magical access.
Strain may be hidden or de-emphasized for characters without psionic access.
Resolve remains relevant to all characters. Strain presentation communicates
accumulating danger and names the current authored band so players never need
to calculate threshold percentages.

Derived statistics appear only when they affect a current decision; detailed
formulas and source breakdowns belong in tooltips or inspection panels.

## Non-redundancy rules

Keep these meanings stable across combat, AI, content, equipment, statuses,
UI, balancing, and progression:

| Value | Tactical question |
| --- | --- |
| Health | How physically injured is this character? |
| Stamina | How physically exhausted is this character? |
| Mana | How much magical energy can this character expend? |
| Resolve | How close is this character's mind to breaking? |
| Strain | How hard is this character pushing psionic capability? |
| Turn Meter | When does this character act again? |
| Action Points | How much can this character do this activation? |
| Armor | How much physical harm is mitigated? |
| Evasion | How difficult is this character to hit physically? |
| Willpower | How difficult is this character to affect mentally? |

Do not treat AP as Stamina, Mana as Strain, Resolve as psionic Mana, or Turn
Meter and AP as two versions of Speed. Weapons should differ through accuracy,
penetration, reach, costs, scaling, statuses, ammunition, and special actions,
not only damage output.

## Initial scope and future expansion

The first tactical combat slice should concentrate on the implemented five
resources and turn economy plus Armor, Evasion, Willpower, Strength, Melee
Weapons, Firearms, Energy Weapons, Throwing, and Psionics. It should exercise
physical and mental attack paths, resource validation, incapacitation,
encounter cleanup, and deterministic save/load restoration.

Possible later additions include morale and crew-wide panic, permanent
injuries, limb damage, cover, suppression, advanced reactions, specialized
shields and resistances, psionic schools and backlash, weapon familiarity,
zero-gravity and vacuum combat, boarding hazards, creature-specific mental
rules, and long-term Sanity. Add them only when they create meaningful tactical
decisions rather than bookkeeping.

## Verification ownership

CI-owned and user-run tests should cover resource bounds, threshold queries,
cost rollback, Turn Meter ordering, AP activation lifecycle, modifier source
removal, recovery, incapacitation, encounter cleanup, save round-tripping,
invalid profiles, and content migration. Test runners remain user-owned under
repository policy.
