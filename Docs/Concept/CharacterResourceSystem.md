# Character Resource System

## Implementation status

The authoritative simulation now implements the common five-resource state,
data-driven Turn Meter and Action Point state, stamina-based Turn Meter
modifiers, per-action AP costs, encounter recovery, Health damage, and save
round-tripping. The scenario-selected profile at
`Content/Packs/base/Definitions/CharacterResourceProfiles/standard.json` owns
the base values, thresholds, stamina speed bands, and personal-action AP costs;
there are no fallback combat-economy numbers in `VoyageWorld`.

Threshold-driven status application, authored Resolve attacks, psionic
overload outcomes, activity-sensitive recovery, attribute-derived Turn Meter
gain, and final HUD/timeline presentation remain planned. The current
implementation exposes threshold and percentage queries without assigning one
universal status or overload consequence.

Implement the following character combat systems:

- Health
- Stamina
- Mana
- Resolve
- Strain
- Turn Meter
- Action Points

These systems must remain mechanically distinct.

Health, Stamina, Mana, and Resolve are conventional resources that range from `0` to their respective maximum values.

Strain works in the opposite direction: it starts at `0` and accumulates toward `MaxStrain`.

Turn Meter determines when a character becomes active.

Action Points determine how many actions the character can perform during that activation.

All values must be stored separately from Attributes and Skills so maximum values, regeneration, costs, damage, accumulation, decay, and temporary modifiers can be adjusted independently.

---

# Core Combat Structure

The combat economy is divided into three layers:

```text
Turn Meter
Determines WHEN the character acts.

Action Points
Determines HOW MUCH the character can do during an activation.

Character Resources
Determine HOW LONG the character can sustain specific types of actions.
```

The systems should not duplicate one another.

For example:

```text
AP limits how many attacks can be performed during one activation.

Stamina limits repeated physically demanding actions.

Mana limits sustained magical casting.

Strain limits sustained psionic use.

Resolve determines resistance to mental pressure and psychic attack.
```

---

# 1. Health

**Purpose:** Physical survivability.

Health represents physical injury and the character's ability to remain alive and combat-capable.

```text
Range:
0 <= Health <= MaxHealth

Default:
Health = MaxHealth
```

Health is reduced by effects that explicitly deal physical or Health damage.

Examples:

- Melee attacks
- Firearms
- Archery
- Energy weapons
- Explosions
- Fire
- Environmental hazards
- Physical psionic effects

When:

```text
Health <= 0
```

the character enters the game's incapacitation or death handling system.

Health does not automatically regenerate during normal combat unless explicitly restored by:

- Ability
- Item
- Trait
- Healing effect
- Regeneration effect
- Other gameplay system

Health must never become negative.

---

# 2. Stamina

**Purpose:** Physical exertion and combat endurance.

Stamina represents a character's ability to sustain physically demanding actions.

```text
Range:
0 <= Stamina <= MaxStamina

Default:
Stamina = MaxStamina
```

Stamina is not a replacement for Action Points.

Action Points determine what the character can do during the current activation.

Stamina determines how long the character can repeatedly perform demanding actions across multiple activations.

Typical Stamina costs include:

- Sprinting
- Heavy melee attacks
- Special melee techniques
- Defensive maneuvers
- Dodges
- Physically demanding abilities
- Heavy weapon techniques
- Certain movement abilities

Example:

```text
Power Strike

AP Cost: 6
Stamina Cost: 15
```

Normal basic actions should generally require AP but little or no Stamina.

Example:

```text
Basic Rifle Shot

AP Cost: 5
Stamina Cost: 0
```

This prevents a character at low Stamina from becoming completely unable to act.

Stamina regenerates naturally during combat.

Regeneration may depend on:

- Current activity
- Character Attributes
- Armor weight
- Equipment
- Status effects
- Traits
- Injuries

Characters performing fewer strenuous actions should generally recover Stamina faster.

Stamina must never become negative.

---

# Stamina and Turn Speed

Low Stamina may reduce Turn Meter generation.

Recommended default thresholds:

```text
Stamina > 50%
No Turn Meter penalty

Stamina <= 50%
Minor Turn Meter penalty

Stamina <= 25%
Major Turn Meter penalty
```

Example default modifiers:

```text
Above 50% Stamina:
100% Turn Meter gain

25–50% Stamina:
90% Turn Meter gain

Below 25% Stamina:
80% Turn Meter gain
```

These values must be data-driven and configurable.

The intent is:

```text
Exhausted characters can still act,
but they act less frequently.
```

---

# 3. Mana

**Purpose:** Magical energy.

Mana is primarily used by abilities classified as magical.

```text
Range:
0 <= Mana <= MaxMana

Default:
Mana = MaxMana
```

Casting a spell normally requires both:

```text
Action Points
+
Mana
```

Example:

```text
Fireball

AP Cost: 6
Mana Cost: 25
```

The ability cannot normally be used when:

```text
CurrentAP < APCost
OR
CurrentMana < ManaCost
```

Mana and Psionics are separate systems.

Psionic abilities must not consume Mana unless explicitly configured to do so.

Mana regeneration should generally be slower or more restricted than Stamina regeneration.

Possible Mana recovery methods include:

- Passive regeneration
- Resting
- Consumables
- Equipment
- Magical environments
- Traits
- Abilities

All recovery rules must remain configurable.

Mana must never become negative.

---

# 4. Resolve

**Purpose:** Mental resilience and psychological stability.

Resolve represents the character's ability to resist fear, psychological pressure, psychic assault, and hostile mental effects.

```text
Range:
0 <= Resolve <= MaxResolve

Default:
Resolve = MaxResolve
```

Resolve is primarily defensive.

Resolve should NOT normally be spent to activate psionic abilities.

Resolve may be damaged or suppressed by:

- Fear
- Terror
- Intimidation
- Psionic attacks
- Mind Blast
- Psychological warfare
- Supernatural mental effects
- Traumatic events

Resolve influences resistance against:

- Fear
- Panic
- Confusion
- Mental Stun
- Domination
- Mind Control
- Other hostile psionic effects

Recommended threshold structure:

```text
Resolve > 50%
Stable

Resolve <= 50%
Shaken

Resolve <= 25%
Vulnerable

Resolve <= 0
Mental Break
```

These thresholds must be data-driven.

Possible Mental Break results include:

- Panic
- Confusion
- Stun
- Forced retreat
- Loss of control
- Increased susceptibility to Domination

The triggering attack or status effect should determine the exact consequence.

Do not force every Resolve break to produce the same status.

---

# Resolve and Turn Economy

Mental states may influence Turn Meter or Action Points.

Example default behavior:

```text
Stable
No penalty

Shaken
Minor Turn Meter penalty

Panicked
Reduced AP and possible forced movement

Mental Break
Possible skipped activation, stun, panic, or other effect
```

These consequences must remain configurable through the status-effect system.

Resolve should recover when the character is no longer under significant mental pressure.

Resolve recovery rate may be influenced by:

- Willpower
- Leadership effects
- Nearby allies
- Traits
- Status effects
- Rest
- Abilities

Resolve must never become negative.

---

# 5. Strain

**Purpose:** Psionic exertion and overload.

Strain represents mental stress accumulated from using psionic abilities.

Unlike Health, Stamina, Mana, and Resolve:

```text
Strain starts low
and increases through use.
```

```text
Range:
0 <= Strain <= MaxStrain

Default:
Strain = 0
```

Psionic abilities normally require:

```text
Action Points
+
Strain Gain
```

Example:

```text
Mind Blast

AP Cost: 5
Strain Gain: 15
Resolve Damage: 20
```

Psionic abilities do not normally spend Resolve.

They generate Strain.

Example values:

```text
Telepathy
Strain Gain: 5

Force Push
Strain Gain: 10

Mind Blast
Strain Gain: 15

Telekinesis
Strain Gain: 20

Dominate
Strain Gain: 30
```

More powerful psionic abilities should generally generate more Strain.

Strain gradually decreases when the character is not heavily using Psionics.

Decay may depend on:

- Attributes
- Traits
- Equipment
- Rest
- Abilities
- Mental status
- Current Resolve

Strain must never become negative.

---

# Psionic Overload

High Strain creates escalating risk.

Recommended thresholds:

```text
0–49%
Normal

50–74%
Strained

75–99%
Critical

100%
Overload
```

Thresholds must be data-driven.

Possible effects:

## Normal

```text
No penalty
```

## Strained

Possible penalties:

- Reduced Resolve recovery
- Minor psionic accuracy penalty
- Minor psionic effectiveness penalty

## Critical

Possible penalties:

- Increased psionic failure chance
- Increased backlash chance
- Reduced Resolve recovery
- Reduced Turn Meter gain
- Increased Strain generated by advanced abilities

## Overload

Further use of Psionics may cause:

- Resolve damage
- Stun
- Confusion
- Ability failure
- Psionic Backlash
- Temporary loss of psionic abilities

Do not automatically kill or permanently disable a character at maximum Strain.

The player should be allowed to deliberately risk pushing a Psionic character beyond safe limits.

---

# 6. Turn Meter

**Purpose:** Determine when a character becomes active.

Turn Meter represents progress toward the character's next activation.

Recommended default:

```text
Range:
0 <= TurnMeter <= TurnMeterThreshold

Default Threshold:
100
```

When:

```text
TurnMeter >= TurnMeterThreshold
```

the character becomes eligible for activation.

After completing the activation:

```text
TurnMeter = 0
```

or is reduced according to the future scheduling model.

Turn Meter gain should primarily depend on a character stat such as:

```text
Speed
Initiative
Quickness
```

The exact Attribute name may be defined separately.

Example:

```text
Fast Character
Turn Meter Gain: 120%

Normal Character
Turn Meter Gain: 100%

Slow Character
Turn Meter Gain: 80%
```

Turn Meter modifiers may also come from:

- Stamina
- Injuries
- Armor
- Status effects
- Haste
- Slow
- Fear
- Psionic effects
- Equipment

---

# Turn Meter Design Rule

Turn Meter controls activation frequency.

Do not allow the same Attribute to strongly increase both:

```text
Turn Meter generation
AND
maximum Action Points
```

This would create multiplicative action-economy scaling.

A fast character may act more frequently.

That does not mean the character should also perform dramatically more actions per activation.

---

# 7. Action Points

**Purpose:** Determine how many actions a character may perform during one activation.

Recommended baseline:

```text
Base Action Points: 10
```

When a character becomes active:

```text
CurrentAP = MaxAP
```

The character spends AP on actions.

Example default costs:

```text
Move 1 Tile
2 AP

Quick Melee Attack
4 AP

Standard Attack
5 AP

Heavy Attack
6 AP

Fire Pistol
4 AP

Fire Rifle
5 AP

Reload
3–4 AP

Throw Grenade
5 AP

Use Item
3 AP

Psionic Ability
4–7 AP

Spell
4–8 AP
```

These numbers are balancing defaults only and must be fully data-driven.

Actions cannot normally be used when:

```text
CurrentAP < APCost
```

Action Points must never become negative.

---

# End of Activation

Default behavior:

```text
Turn Meter reaches activation threshold
        ↓
Character becomes Active
        ↓
CurrentAP = MaxAP
        ↓
Character performs actions
        ↓
Player or AI ends activation
        ↓
Unused AP is discarded
        ↓
Turn Meter begins filling again
```

Unused AP should not carry over by default.

Specific Traits, Abilities, or Status Effects may override this later.

---

# AP Scaling

Most characters should have approximately the same baseline AP.

Recommended:

```text
Base MaxAP = 10
```

Attributes should NOT dramatically increase MaxAP.

Modifiers such as:

```text
+1 AP
-1 AP
```

should be relatively rare and meaningful.

Potential sources:

- Traits
- Injuries
- Temporary buffs
- Temporary debuffs
- Special equipment
- Legendary abilities

Turn frequency should primarily be controlled by Turn Meter.

Actions per activation should primarily be controlled by AP.

---

# 8. Reaction Actions

Reaction mechanics should not directly depend on leftover CurrentAP after an activation ends unless explicitly designed that way.

Possible reactions include:

- Overwatch
- Opportunity Attack
- Counterattack
- Intercept
- Defensive Block

Recommended model:

```text
Overwatch

AP Cost: 4

Effect:
Enter Overwatch state.

Allows one reaction attack before the character's next activation.
```

The AP cost is paid during the character's normal activation.

The resulting reaction may occur later.

This avoids the need for an additional Reaction Point resource.

---

# Resource Interaction Model

The complete system should follow this conceptual structure:

```text
TURN ECONOMY

Turn Meter
↓
Determines WHEN the character acts.

Action Points
↓
Determines HOW MUCH the character can do during activation.


PHYSICAL

Health
↓
Physical survivability.

Stamina
↓
Physical exertion and long-term combat endurance.


MAGIC

Mana
↓
Magical ability resource.


MENTAL

Resolve
↓
Mental survivability and psychological stability.

Strain
↓
Psionic exertion and overload.
```

---

# Basic Combat Relationships

```text
Physical Attack
    ↓
Armor / Physical Defense
    ↓
Health
```

```text
Physical Special Ability
    ↓
AP Cost
+
Stamina Cost
```

```text
Magic Ability
    ↓
AP Cost
+
Mana Cost
```

```text
Hostile Mental Attack
    ↓
Mental Resistance
    ↓
Resolve
```

```text
Psionic Ability
    ↓
AP Cost
+
Strain Gain
```

A Psionic attack may additionally deal:

```text
Resolve Damage
Health Damage
Status Effects
Forced Movement
```

depending on the specific ability.

---

# Ability Cost Examples

All abilities should declare costs and resource effects through data.

Do not hard-code resource logic inside individual abilities.

Example:

```text
Rifle Shot

AP Cost: 5
```

```text
Power Strike

AP Cost: 6
Stamina Cost: 15
```

```text
Fireball

AP Cost: 6
Mana Cost: 25
```

```text
Mind Blast

AP Cost: 5
Strain Gain: 15
Target Resolve Damage: 20
```

```text
Sprint

AP Cost: 2 per tile
Stamina Cost: 5 per tile
```

```text
Psionic Overcharge

AP Cost: 4
Stamina Cost: 10
Strain Gain: 35

Effect:
Increase Psionic Power for this activation.
```

Cross-resource abilities are allowed but should be exceptions rather than the default.

---

# Resource Data Structure

The runtime represents the five long-duration values with
`CharacterResourceSet` and encounter timing with `CharacterTurnState`. They are
separate immutable states: AP is not stored as Stamina, and Turn Meter is not a
conventional resource. Permanent and temporary modifiers retain stable source
IDs so equipment, Feats, injuries, and statuses can add and remove their own
changes without rewriting base values.

Each conventional resource should support at minimum:

```text
CurrentValue
MaxValue
BaseMaxValue
RegenRate
TemporaryModifiers
PermanentModifiers
```

Applicable to:

```text
Health
Stamina
Mana
Resolve
```

Strain should support:

```text
CurrentStrain
MaxStrain
BaseMaxStrain
DecayRate
TemporaryModifiers
PermanentModifiers
ThresholdState
```

Turn Meter should support:

```text
CurrentTurnMeter
TurnMeterThreshold
BaseTurnMeterGain
CurrentTurnMeterGainModifier
```

Action Points should support:

```text
CurrentAP
MaxAP
BaseMaxAP
TemporaryAPModifiers
PermanentAPModifiers
```

---

# Shared Resource Functions

Implement reusable resource functions.

Recommended API concepts:

```text
CanSpendResource()
SpendResource()
RestoreResource()
ApplyResourceDamage()
GetResourcePercentage()
ClampResource()
```

Strain-specific functions:

```text
GenerateStrain()
ReduceStrain()
GetStrainPercentage()
GetStrainThresholdState()
CheckPsionicOverload()
```

Turn system functions:

```text
AddTurnMeter()
ResetTurnMeter()
CanActivate()
BeginActivation()
EndActivation()
```

Action Point functions:

```text
CanSpendAP()
SpendAP()
RestoreAP()
GetRemainingAP()
```

Abilities, AI, items, status effects, equipment, and environmental effects should use shared systems rather than duplicating logic.

The implemented turn API uses `AddTurnMeter`, `ResetTurnMeter`,
`CanActivate`, `BeginActivation`, `EndActivation`, `CanSpendActionPoints`,
`SpendActionPoints`, `RestoreActionPoints`, and `GetActionPointCost`.

---

# Value Clamping

Always clamp values.

```text
Health =
Clamp(Health, 0, MaxHealth)

Stamina =
Clamp(Stamina, 0, MaxStamina)

Mana =
Clamp(Mana, 0, MaxMana)

Resolve =
Clamp(Resolve, 0, MaxResolve)

Strain =
Clamp(Strain, 0, MaxStrain)

TurnMeter =
Clamp(TurnMeter, 0, TurnMeterThreshold)

CurrentAP =
Clamp(CurrentAP, 0, MaxAP)
```

---

# UI Requirements

Do not display all systems as identical resource bars.

Recommended presentation:

## Character Status

```text
Health
Stamina
Mana
Resolve
Strain
```

## Combat Timeline

Turn Meter should be represented through:

```text
Initiative Timeline
Turn Order Display
Activation Queue
```

rather than as a prominent conventional resource bar.

## Active Character

Action Points should be clearly displayed during activation.

Example:

```text
AP: 7 / 10
```

---

# Resource Visibility

Characters without magical capability may hide or visually de-emphasize Mana.

Characters without Psionic capability may hide or visually de-emphasize Strain.

Resolve remains relevant to all characters.

Health and Stamina remain relevant to nearly all conventional biological characters.

Turn Meter and AP are combat systems and should be visible whenever tactically relevant.

---

# Strain UI

Strain should visually communicate danger accumulation rather than resource depletion.

Example:

```text
Strain  20 / 100
Normal

Strain  62 / 100
STRAINED

Strain  87 / 100
CRITICAL

Strain 100 / 100
OVERLOAD
```

The player should never need to manually calculate threshold percentages.

---

# Final Design Definitions

Keep these meanings consistent throughout the project:

**Health answers:**

```text
How physically injured is this character?
```

**Stamina answers:**

```text
How physically exhausted is this character?
```

**Mana answers:**

```text
How much magical energy can this character still expend?
```

**Resolve answers:**

```text
How close is this character's mind to breaking?
```

**Strain answers:**

```text
How hard is this character currently pushing their Psionic abilities?
```

**Turn Meter answers:**

```text
When does this character get to act again?
```

**Action Points answer:**

```text
How much can this character do during this activation?
```

These distinctions must remain consistent across:

- Combat
- AI
- Abilities
- Skills
- Attributes
- Equipment
- Injuries
- Status effects
- Character UI
- Tooltips
- Balancing
- Future progression systems

Do not treat AP as Stamina.

Do not treat Mana as Strain.

Do not treat Resolve as Psionic Mana.

Do not allow Turn Meter and AP to become redundant measures of Speed.

Each system must have one clear gameplay responsibility.
