# Status and Effect System

# 1. Overview

This document defines temporary conditions, persistent modifiers, and one-time gameplay results.

~~~text
Effect
    A gameplay result that happens once or applies a Status.

Status
    A condition that remains on a target and changes its rules or behavior.
~~~

Effects and Statuses are independent from their source. They may be produced by weapons, spells, abilities, traits, environmental hazards, traps, or combat events.

---

# 2. Core Principles

## One Effect, One Result

Each Effect performs one gameplay operation.

~~~text
HealHealth
ReduceStrain
PhysicalDamage
ApplyStatus: Charmed
RemoveStatus: Poisoned
~~~

Effects should not hide multiple unrelated results inside one operation.

## Statuses Describe Ongoing State

An Effect describes what happens. A Status describes what remains true afterward.

~~~text
ApplyStatus: Confused
        ↓
Confused Status
        ↓
AI decision penalties while active
~~~

## Statuses Do Not Directly Run AI

AI-affecting Statuses provide rules, restrictions, and preferences. The AI reads those rules while generating and scoring actions.

---

# 3. Effect

Common Effect types include:

~~~text
HealHealth
RestoreMana
RestoreStamina
RestoreResolve
ReduceStrain

PhysicalDamage
ThermalDamage
ShockDamage
ArcaneDamage
ArmorDamage

ApplyStatus
RemoveStatus

ModifyDamage
ModifyDefense
ModifyAccuracy
ModifyMovement
ModifyResistance
GrantShield
~~~

Strain is reduced with ReduceStrain. It is not restored with RestoreStrain.

A simple Effect definition is:

~~~text
Effect
{
    type
    source
    target
    amount
    duration
}
~~~

Some Effects finish immediately:

~~~text
HealHealth
ReduceStrain
PhysicalDamage
~~~

Other Effects create or remove an ongoing Status:

~~~text
ApplyStatus: Burning
RemoveStatus: Poisoned
~~~

---

# 4. Damage Resolution Through Effects

The final result of weapon damage is expressed as one or more Effects. The damage formula itself remains in the Combat System rather than inside the Effect.

```text
Weapon + WeaponAction
        ↓
Combat Calculation
        ↓
Damage Effects
        ↓
Effect Resolver
        ↓
Target State
```

Weapons and Weapon Actions provide damage parameters. The Combat System calculates the final values using the attacker, action, target, armor, defenses, and relevant modifiers. The resulting Effects represent changes that have actually occurred.

```text
DamageHealth
{
    damage_type: Physical
    source: Attacker
    target: Defender
    amount: 25
}
```

Armor Damage and Health Damage are separate results and must be represented by separate Effects:

```text
DamageArmor
{
    source: Attacker
    target: Defender
    amount: 40
}

DamageHealth
{
    damage_type: Physical
    source: Attacker
    target: Defender
    amount: 25
}
```

An attack may therefore produce several atomic Effects:

```text
DamageArmor: 40
DamageHealth: 25 Physical
ApplyStatus: Bleeding
```

These are separate Effects because they represent separate gameplay results.

Weapon and Action fields such as `armor_damage` and `armor_penetration` are calculation inputs. They are not themselves final target changes.

Damage types may include:

```text
Physical
Thermal
Shock
Arcane
```

The recommended responsibility split is:

```text
Weapon / WeaponAction
    Provide damage parameters.

Combat System
    Calculate hit results, mitigation, penetration, and final amounts.

Effect
    Express the resolved result.

Status
    Preserve ongoing consequences such as Burning or Bleeding.
```

---

# 5. Status Definition

StatusDefinition is static data describing the rules of a Status. It does not store the current duration of a particular target.

~~~text
StatusDefinition
{
    id
    name
    category
    tags[]

    default_duration
    duration_type

    stack_policy
    max_stacks

    exclusive_group
    priority

    modifiers[]
    restrictions[]
    ai_rules[]

    on_apply[]
    on_tick[]
    on_expire[]
}
~~~

## Identity

~~~text
id: Charmed
name: Charmed
category: Mental
tags:
- Control
- AIInfluence
~~~

## Duration

~~~text
default_duration: 3
duration_type: Timed
~~~

Supported duration types:

~~~text
Timed
Permanent
UntilRemoved
Conditional
~~~

## Stacking and Conflict

~~~text
stack_policy: Refresh
max_stacks: 1

exclusive_group: MentalControl
priority: 50
~~~

## Modifiers

Modifiers change a value or rule while the Status is active.

~~~text
modifiers:
- type: ModifyAccuracy
  amount: -20
- type: ModifyResolve
  amount: -10
~~~

Each Modifier should represent one gameplay result.

## Restrictions

Restrictions define actions or targets that are not allowed.

~~~text
restrictions:
- type: CannotAttack
  target_rule: StatusSource
~~~

## AI Rules

AI rules alter target selection, action preference, or decision reliability.

~~~text
ai_rules:
- type: PreferTarget
  target_rule: NearestEnemy
- type: DiscourageAction
  action_tag: Retreat
~~~

## Lifecycle Rules

Lifecycle Effects are optional:

~~~text
on_apply[]
    Effects resolved when the Status is applied.

on_tick[]
    Effects resolved at the Status interval.

on_expire[]
    Effects resolved when the Status ends.
~~~

Each entry is a separate Effect. Lifecycle rules must not hide unexplained composite behavior.

---

# 6. Status Instance

StatusInstance represents one active Status on one target.

~~~text
StatusInstance
{
    definition_id
    source_id
    target_id

    remaining_duration
    stacks
    potency
}
~~~

The definition stores reusable rules. The instance stores runtime state.

~~~text
StatusInstance
{
    definition_id: Confused
    source_id: EnemyMage_01
    target_id: Crewman_04

    remaining_duration: 2
    stacks: 1
    potency: 1
}
~~~

source_id is important for Statuses such as Charm, which may distinguish the source from other entities.

---

# 7. Duration Rules

Timed Statuses use turn-based durations.

~~~text
duration: 3
~~~

Recommended lifecycle:

~~~text
Status applied
    ↓
Target receives its normal turn
    ↓
Target turn ends
    ↓
remaining_duration -= 1
    ↓
Remove when remaining_duration <= 0
~~~

This makes Status expiration predictable to the player.

---

# 8. Stacking Rules

Every Status must define a stack_policy.

## Refresh

Reapplying the Status resets its duration without increasing its strength.

~~~text
Charmed: 2 turns remaining
Apply Charmed: 3 turns
Result: Charmed with 3 turns remaining
~~~

Recommended for:

~~~text
Charmed
Confused
Haste
~~~

## Extend

Reapplying the Status adds the new duration to the remaining duration.

~~~text
2 turns remaining
+ 3 turns
= 5 turns remaining
~~~

Recommended for:

~~~text
Poisoned
Burning
Bleeding
~~~

## IntensityStack

Reapplying the Status increases its intensity.

~~~text
Bleeding: 1 stack
+ Bleeding: 1 stack
= Bleeding: 2 stacks
~~~

The Status must define a maximum:

~~~text
max_stacks: 3
~~~

## StrongerWins

Only the stronger version remains active.

~~~text
Confusion: -10 Accuracy
Confusion: -20 Accuracy
Result: -20 Accuracy
~~~

Recommended for:

~~~text
Shielded
Resistance modifiers
Stat modifiers
~~~

## Independent

Each application creates a separate Status instance.

~~~text
Poison A: 2 damage for 3 turns
Poison B: 5 damage for 2 turns
~~~

Use this only when tracking separate sources is valuable.

## Reject

The new application is ignored if the target already has the Status.

---

# 9. Status Replacement and Exclusive Groups

Statuses that cannot normally coexist should share an exclusive_group.

~~~text
Charmed
exclusive_group: MentalControl
priority: 50

Confused
exclusive_group: MentalControl
priority: 30
~~~

When a new Status enters an occupied group:

~~~text
New priority > existing priority
    → Remove existing Status and apply new Status

New priority < existing priority
    → Reject new Status

Equal priority
    → Use the Status-specific tie rule
~~~

Suggested control priority:

~~~text
HardControl
    Stunned
    Dominated

FactionControl
    Charmed

BehaviorControl
    Raging
    Feared

DecisionDisruption
    Confused
~~~

Different exclusive groups may coexist:

~~~text
Charmed + Burning
    Can coexist.

Confused + Poisoned
    Can coexist.
~~~

If active Statuses produce contradictory rules, the higher-priority rule wins. The result must not depend on application order unless explicitly designed.

---

# 10. AI-Affecting Statuses

AI-affecting Statuses modify the AI's inputs instead of directly taking over the AI controller.

The AI should query:

~~~text
CanAct?
CanAttack(target)?
IsTargetValid(target)?
PreferredTargets()
PreferredActions()
ActionRestrictions()
DecisionReliability()
~~~

## Charmed

Charm primarily changes faction relationships and target validity.

~~~text
StatusDefinition
{
    id: Charmed
    category: Mental
    tags:
    - Control
    - AIInfluence

    default_duration: 3
    duration_type: Timed

    stack_policy: Refresh
    max_stacks: 1

    exclusive_group: MentalControl
    priority: 50

    restrictions:
    - type: CannotAttack
      target_rule: StatusSource

    ai_rules:
    - type: TreatAsAlly
      target_rule: StatusSource
    - type: PreferTarget
      target_rule: SourceEnemies
}
~~~

Charm does not automatically mean complete control of every action. A limited version may only prevent attacks against the source and modify target preference.

## Confused

Confusion reduces decision reliability without necessarily changing faction relationships.

~~~text
StatusDefinition
{
    id: Confused
    category: Mental
    tags:
    - Disruption
    - AIInfluence

    default_duration: 2
    duration_type: Timed

    stack_policy: Refresh
    max_stacks: 1

    exclusive_group: MentalControl
    priority: 30

    modifiers:
    - type: ModifyAccuracy
      amount: -20

    ai_rules:
    - type: ReduceDecisionReliability
      amount: 50
    - type: UnstableTargetSelection
      amount: 25
}
~~~

Confusion should normally select from valid actions and targets, but alter their weights or reliability. Fully random behavior is difficult for players to understand and debug.

## Raging

Rage changes behavior toward aggression.

~~~text
StatusDefinition
{
    id: Raging
    category: Mental
    tags:
    - Emotion
    - Aggressive

    default_duration: 3
    duration_type: Timed

    stack_policy: Refresh
    max_stacks: 1

    exclusive_group: EmotionalState
    priority: 40

    restrictions:
    - type: DiscourageAction
      action_tag: Retreat
    - type: DiscourageAction
      action_tag: Defend

    ai_rules:
    - type: PreferTarget
      target_rule: NearestEnemy
    - type: PreferAction
      action_tag: Attack
    - type: PreferAction
      action_tag: Charge
}
~~~

Rage should normally influence action scoring rather than make retreat mathematically impossible, unless it is explicitly designed as hard control.

---

# 11. Categories and Tags

Categories are used for organization, resistance rules, UI, and conflict resolution.

~~~text
Mental
Emotion
Control
Disruption
DamageOverTime
Defensive
Resource
Physical
Magical
~~~

Tags provide more specific filtering:

~~~text
AIInfluence
HardControl
FactionControl
DamageOverTime
Removable
Hidden
Positive
Negative
~~~

Category and tags are metadata. They must not replace the actual rules of the Status.

---

# 12. Initial Design Rules

The first implementation should follow these defaults:

~~~text
Timed Statuses use turn-based durations.

Same Status defaults to Refresh.

Damage-over-time Statuses may use IntensityStack.

Shields and similar defenses use StrongerWins.

Mutually exclusive control Statuses use Exclusive Groups and Priority.

AI Statuses provide rules to AI queries; they do not directly run the AI.

Trait is separate from Status.

Effect is separate from the ongoing Status it creates.

One Effect performs one gameplay result.
~~~

This structure keeps Status definitions reusable across combat, abilities, weapons, magic, traits, and environmental systems without coupling them to any particular item type.

---

# 13. Implementation Status

The first headless implementation is available under
`Source/Spelljammer.Simulation/Effects`. It separates immutable
typed `EffectDefinition` payloads and `StatusDefinition` content from
`EffectApplicationDefinition`, `EffectRequest`, and `StatusInstance` runtime
state. Each request owns a deterministic `EffectInvocationId`; applications
describe target selection, timing, probability, power, and tags independently
from the reusable Effect payload. Status application, removal, timed turn
advancement, refresh, extend, intensity stacking, stronger-wins replacement,
independent instances, reject policy, exclusive-group priority, modifiers,
restrictions, and bounded lifecycle Effect queues publish immutable state
atomically.

The content compiler accepts camel-case JSON under `Definitions/Effects` and
`Definitions/Statuses`, validates every Feat, gear, melee-action,
ranged-action, and Status Effect reference against the unified Effect catalog,
and includes both kinds in the canonical content fingerprint. Effect payloads
are compiled into resource, damage, status application/removal, modifier,
shield, or event types instead of one nullable catch-all record. The base pack
defines the combat damage operations and the Effects referenced by spells,
psionics, equipment, and racial Feats, including the persistent Mindlinked
Status.

Character and personal-encounter actor state persist Status instances through
campaign save schema 11. Schema 7 through 10 `activeEffects` fields are accepted
only as legacy input and discarded during migration; `ActiveCapabilityEffect`
and encounter-level `ActiveEffectState` are no longer simulation contracts.
Melee and ranged hit resolution emits deterministic Effect requests for armor
and Health damage plus authored action Effects, and exposes an atomic
`ResolveEffects` boundary. Spells and psionic actions emit the same request
type. Reload and general character actions consult Status restrictions, while
melee and ranged accuracy include active Status modifiers.

Applying an attack's resolved target snapshot back into the encounter,
scheduled non-instant applications, status-driven defense and movement, AI
scoring, conditional expiration, and resistance checks remain planned.
