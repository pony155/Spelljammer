# Battle System Design

## 1. Overview

The combat system is designed for a single-player tactical sandbox game with squad-based encounters similar in structure to *Battle Brothers*.

The system should support:

- Classless character builds
- Melee, ranged, thrown, energy, and psionic combat
- Distinct physical and mental defense layers
- Tactical positioning and equipment choices
- Character differentiation through Attributes, Skills, equipment, traits, and species
- Future expansion into wounds, morale, psionic powers, environmental hazards, and shipboard combat

The core design principle is:

> **Attributes represent innate capability. Skills represent trained proficiency. Equipment determines how those capabilities are converted into combat performance.**

The authoritative five-resource ownership and data contract are defined in
[`../Concept/CharacterResources.md`](../Concept/CharacterResources.md). Health,
Stamina, Mana, Resolve, and Strain are independent of Attributes and Skills.

---

# 2. Combat Layers

Combat is divided into two major defensive axes:

```text
Physical Combat
Attack Skill
    ↓
Accuracy / Defense
    ↓
Armor
    ↓
Health
    ↓
Wounds / Death

Psionic Combat
Psionic Skill
    ↓
Willpower / Psi Defense
    ↓
Resolve
    ↓
Mental Effects / Mental Break
```

Physical and psionic attacks should therefore feel mechanically different.

A Mind Blast should not simply behave like a fireball that deals a different damage type.

---

# 3. Core Character Combat Stats

## 3.1 Health

**Health** represents physical survivability.

```text
Health: 84 / 100
```

Health is reduced by physical damage that penetrates or bypasses armor.

At low Health, characters may suffer:

- Injury penalties
- Bleeding
- Reduced movement
- Reduced accuracy
- Knockdown
- Unconsciousness
- Death

Health should generally recover slowly outside combat compared with Resolve.

---

## 3.2 Armor

**Armor** represents protection against physical attacks.

Armor may come from:

- Body armor
- Helmets
- Shields
- Natural armor
- Magical protection
- Vehicle or environmental cover

Armor does not necessarily function as additional Health.

Preferred model:

```text
Incoming Damage
    ↓
Armor Mitigation / Penetration
    ↓
Health Damage
```

Weapons may have different:

- Armor Damage
- Armor Penetration
- Health Damage
- Critical behavior

Example:

```text
Boarding Axe

Damage:             20–30
Armor Damage:       120%
Armor Penetration:  35%
```

This allows weapons to fill distinct tactical roles.

---

## 3.3 Evasion

**Evasion** represents the ability to avoid an incoming physical attack.

Possible contributors:

- Dexterity / Agility
- Movement
- Cover
- Shield
- Status effects
- Equipment weight
- Traits

Evasion should primarily interact with attack accuracy rather than directly reducing damage.

Example:

```text
Hit Chance =
Base Accuracy
+ Attacker Skill
+ Situational Modifiers
- Target Evasion
```

Minimum and maximum hit chances may be capped for balance.

Example:

```text
Minimum Hit Chance: 5%
Maximum Hit Chance: 95%
```

---

# 4. Mental and Psionic Combat Stats

## 4.1 Resolve

**Resolve** represents short-term mental stability during combat.

Resolve is the mental equivalent of combat endurance, but it should not simply function as a second Health bar.

Example:

```text
Resolve: 76 / 100
```

Resolve can be reduced by:

- Mind Blast
- Psionic Scream
- Fear
- Horrific creatures
- Ally death
- Psionic domination attempts
- Certain magical effects
- Severe wounds
- Isolation or environmental effects

Suggested thresholds:

```text
100–76  Stable
75–51   Shaken
50–26   Disturbed
25–1    Breaking
0       Mental Break
```

Example penalties:

### Shaken

- Small Accuracy penalty
- Small Willpower penalty

### Disturbed

- Accuracy penalty
- Reduced initiative
- Increased susceptibility to Fear and Psionics

### Breaking

- Severe combat penalties
- Increased chance of panic
- Increased chance of losing actions

### Mental Break

Possible results:

- Panic
- Flee
- Stun
- Catatonia
- Berserk
- Temporary Mind Control
- Collapse

The exact result may depend on the attack, character traits, species, and current conditions.

Resolve should recover significantly faster than Health.

---

# 5. Willpower

**Willpower** represents resistance to mental and psionic attacks.

Willpower is a defensive stat, not a resource.

Conceptually:

```text
Physical Defense:
Attack Skill vs Evasion

Mental Defense:
Psionic Skill vs Willpower
```

Example:

```text
Mind Blast Power: 65
Target Willpower: 50
```

A successful psionic attack can then:

- Damage Resolve
- Apply mental status effects
- Interrupt actions
- Trigger special effects

Willpower may be influenced by:

- Base Attributes
- Background
- Species
- Traits
- Psionic training
- Equipment
- Current Resolve state

---

# 6. Psi Shield

**Psi Shield** is an optional specialized defense against psionic effects.

Unlike Willpower, Psi Shield should primarily come from rare or specialized sources:

- Psionic equipment
- Magical artifacts
- Psionic species
- Active powers
- Defensive implants
- Arcane technology

Not every character should naturally possess Psi Shield.

Example:

```text
Astral Circlet

Psi Shield:              +35
Willpower:               +10
Mind Control Resistance: +20%
```

Possible psionic resolution:

```text
Incoming Psionic Attack
    ↓
Psi Shield
    ↓
Willpower Check
    ↓
Resolve Damage
    ↓
Mental Status Effect
```

Psi Shield can therefore serve as the mental equivalent of specialized armor.

---

# 7. Sanity

**Sanity** is an optional campaign-level statistic.

Sanity represents long-term psychological stability rather than immediate combat morale.

```text
Resolve = short-term combat state
Sanity  = long-term psychological condition
```

Sanity may be affected by:

- Cosmic horror
- Aberrations
- Psionic parasites
- Prolonged isolation
- Exposure to the Astral Void
- Crew deaths
- Forbidden artifacts
- Failed psionic encounters

Low Sanity may produce persistent traits.

Examples:

```text
Nightmares
Paranoia
Void Whispers
Phobia: Aberrations
Psionic Sensitivity
```

Traits do not have to be purely negative.

Example:

```text
Void-Touched

Sanity Recovery:      -20%
Psionic Resistance:   +15%
Psionic Detection:    +1
```

Sanity is not required for the initial combat implementation and may be added later.

---

# 8. Attributes and Combat Skills

Attributes and Skills should not simply duplicate one another.

The intended relationship is:

```text
Attributes = natural physical / mental capability
Skills     = trained proficiency
Weapons    = determine scaling and tactical role
```

---

# 9. Strength and Melee Weapons

Strength and Melee Weapons should affect combat independently.

Do not use:

```text
Strength
    ↓
Melee Weapons
    ↓
Damage
```

This makes Strength indirectly control too many combat outcomes and reduces build diversity.

Preferred design:

```text
Strength ─────────────→ Damage / Penetration / Weapon Requirements

Melee Weapons ────────→ Accuracy / Criticals / Special Active Feats
        │
        └─────────────→ Small Damage Contribution
```

Interpretation:

> **Strength determines how hard a character can hit.**

> **Melee Weapons determines how effectively the character knows how to fight.**

---

# 10. Melee Damage Formula

Suggested conceptual formula:

```text
Final Damage =
Weapon Base Damage
× Strength Modifier
× Skill Modifier
× Critical Modifier
× Target Mitigation
```

Skill should generally contribute less raw damage than Strength.

Example:

```text
Boarding Axe
Base Damage: 20–30
STR Scaling: 0.8
```

Character:

```text
Strength:       15
Melee Weapons:  60
```

Example bonuses:

```text
Strength Bonus:      +30%
Melee Skill Bonus:   +10%
```

Exact numbers should be determined through balance testing.

---

# 11. Weapon Scaling

Different melee weapons should scale differently with Attributes and Skills.

Example:

| Weapon | Strength Scaling | Melee Skill Scaling | Role |
|---|---:|---:|---|
| Great Hammer | Very High | Low | Heavy armor breaker |
| Boarding Axe | High | Medium | General heavy melee |
| Longsword | Medium | Medium | Balanced |
| Rapier | Low | Very High | Precision |
| Dagger | Low | High | Critical / armor gaps |
| Power Axe | High | Low–Medium | Heavy energy melee |
| Arcane Blade | Low–Medium | High | Skill-based magical weapon |

This creates viable archetypes without requiring character classes.

Examples:

| Build | STR | Melee | Combat Identity |
|---|---:|---:|---|
| Strong Deckhand | High | Low | Inaccurate but devastating |
| Duelist | Low | Very High | Accurate and technical |
| Veteran Marine | Medium | High | Reliable all-round fighter |
| Heavy Boarder | Very High | Medium | Heavy weapons and armor |
| Blade Master | Medium | Very High | Precision and special attacks |

---

# 12. Combat Skills

Current or planned weapon skills may include:

```text
Melee Weapons
Firearms
Energy Weapons
Throwing
Psionics
```

Potential future skills:

```text
Heavy Weapons
Arcane Weapons
Ship Weapons
Unarmed
```

Avoid excessive fragmentation unless the additional skill creates meaningful build choices.

For example, melee weapons do not initially need separate Sword, Axe, Spear, and Hammer skills.

Weapon specialization can instead come from:

- Traits
- Perks
- Weapon familiarity
- Background bonuses
- Equipment requirements

---

# 13. Throwing

**Throwing** governs thrown combat weapons.

Examples:

- Grenades
- Throwing knives
- Bombs
- Alchemical weapons
- Magical charges

Throwing may influence:

- Accuracy
- Maximum effective range
- Scatter
- Critical placement
- Grenade landing precision

Strength may separately influence the maximum throwing range of heavy objects.

Example:

```text
Throwing Skill → Accuracy
Strength       → Maximum Range
```

This preserves the distinction between physical ability and technical proficiency.

---

# 14. Psionic Attacks

Psionic abilities should focus heavily on control, disruption, and mental pressure rather than functioning only as direct damage spells.

Examples:

## Mind Blast

```text
Target: Single / Cone
Attack: Psionics vs Willpower
Effect:
- Resolve Damage
- Chance to Daze
- Interrupt
```

## Psionic Scream

```text
Target: Area
Effect:
- Moderate Resolve Damage
- Fear
- Higher effect against low-Resolve targets
```

## Dominate

```text
Target: Single
Attack: Psionics vs Willpower
Requirement:
- Target below Resolve threshold OR
- Significant attacker advantage
Effect:
- Temporary Mind Control
```

## Mental Barrier

```text
Target: Self / Ally
Effect:
- Temporary Psi Shield
- Increased Willpower
```

## Psionic Lance

```text
Target: Single
Effect:
- High Resolve Damage
- Possible direct Health damage against certain creatures
```

Some enemies may interact differently with psionics.

Examples:

```text
Construct:
Immune to Fear
Immune to conventional Mind Control

Hive Mind Creature:
High Willpower
Vulnerable to psionic disruption

Aberration:
High Psionic Resistance
May retaliate against failed mental attacks
```

---

# 15. Status Effects

Physical statuses may include:

```text
Bleeding
Burning
Poisoned
Stunned
Knocked Down
Crippled
Suppressed
Blinded
```

Mental statuses may include:

```text
Shaken
Afraid
Dazed
Confused
Panicked
Berserk
Dominated
Catatonic
```

Status effects should be used to create tactical consequences beyond raw damage.

---

# 16. Suggested Character Combat Panel

Example:

```text
PHYSICAL

Health             84 / 100
Armor              42
Evasion             18%

MENTAL

Resolve             76 / 100
Willpower           55
Psi Shield          20

OFFENSE

Melee Weapons       64
Firearms            42
Energy Weapons      20
Throwing            38
Psionics            15
```

The UI should avoid displaying unnecessary derived values unless they are important for player decisions.

Detailed derived statistics can appear in tooltips or weapon panels.

---

# 17. Design Rules

## Rule 1 — Avoid Redundant Stats

Every statistic must answer a different tactical question.

Example:

```text
Strength      → How hard can I hit?
Melee Weapons → How well can I fight?
Armor         → How much physical protection do I have?
Health        → How much physical punishment can I survive?
Willpower     → How difficult am I to mentally attack?
Resolve       → How close am I to psychological collapse?
```

---

## Rule 2 — Do Not Make Psionics Reskinned Magic Damage

Psionics should interact with:

- Resolve
- Willpower
- Mental statuses
- Positioning
- Crowd control
- Morale
- Special enemy biology

This makes psionic characters tactically distinct from conventional damage dealers.

---

## Rule 3 — Attributes and Skills Must Produce Different Builds

A character with:

```text
STR 18
Melee 35
```

must feel significantly different from:

```text
STR 10
Melee 80
```

even if their average damage output is similar.

---

## Rule 4 — Equipment Should Change Combat Behavior

Weapons should not differ only by DPS.

Weapons can vary through:

- Damage
- Accuracy
- Armor penetration
- Armor damage
- Reach
- AP cost
- Strength scaling
- Skill scaling
- Critical chance
- Status effects
- Special attacks
- Ammunition
- Energy cost

---

## Rule 5 — Keep the Core System Small

Initial implementation should focus on:

```text
Health
Stamina
Mana
Resolve
Strain
Armor
Evasion
Willpower

Strength
Melee Weapons
Firearms
Energy Weapons
Throwing
Psionics
```

Optional systems such as Sanity, Psi Shield complexity, permanent wounds, and advanced weapon specialization can be introduced later.

---

# 18. Recommended Initial Combat Model

For the first playable combat implementation:

```text
PHYSICAL

Attack Accuracy
    ↓
Evasion
    ↓
Armor
    ↓
Health


PSIONIC

Psionic Accuracy / Power
    ↓
Willpower
    ↓
Resolve
    ↓
Mental Status Effects
```

Core statistics:

```text
Health
Stamina
Mana
Resolve
Strain
Armor
Evasion
Willpower
Strength
Melee Weapons
Firearms
Energy Weapons
Throwing
Psionics
```

This provides enough mechanical depth for meaningful tactical combat while keeping the system manageable for a solo-developed sandbox game.

---

# 19. Future Expansion

Potential future additions:

- Morale and crew-wide panic
- Permanent injuries
- Limb damage
- Shield systems
- Magical resistance
- Elemental resistances
- Psionic schools
- Psionic backlash
- Weapon perks
- Weapon familiarity
- Suppression
- Cover
- Overwatch
- Opportunity attacks
- deeper fatigue conditions beyond Stamina
- Initiative
- Action Points
- Shipboard environmental hazards
- Zero-gravity combat
- Vacuum exposure
- Boarding combat
- Creature-specific mental rules
- Sanity and long-term trauma

These systems should only be added when they create meaningful tactical decisions rather than additional bookkeeping.
