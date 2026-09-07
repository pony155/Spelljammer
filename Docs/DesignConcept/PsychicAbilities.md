# Psychic abilities

## Status

Mindlink, Psychic Strain, explicit invitation and consent, bounded deliberate-
message scope, learning, innate Mindwake provenance, sustain, and release are
implemented as a headless character slice. Other psychic techniques, hazards,
and visual encounter integration remain planned.

## Core boundary

Psychic abilities use directed awareness and mental focus to sense, contact,
project, shield, or exert force. They do not use aetheric spell patterns.

| System | Primary medium | Typical use |
| --- | --- | --- |
| Psionics | Minds, attention, perception, and psychic strain | Mindlinks, emotional impressions, shielding, remote sense, and psychokinesis |
| Magic | Aether, learned spells, reagents, and focus | Active supernatural effects on characters, objects, environments, and energy |
| Enchantment | Prepared targets, bindings, and persistent charge | Lasting supernatural effects on equipment, modules, and locations |

An effect uses its authored system even when the fiction appears similar. A
psychic impression is not a divination spell, a telekinetic push is not a
Vectoring spell, and a mind shield is not automatically an aetheric ward.
Explicit hybrid techniques may interact with both systems.

## Classless access

Any character may improve `skill.psionics`, study psychic theory, and pursue
training. There is no psychic class, global character level, or race-locked
skill ceiling. Active use nevertheless requires `access.psionics`.

Most characters earn access by completing Psionic Training and receiving
`feat.access.psionics`. A mentor, school, authenticated instructional record,
or safe psychic facility can support that bounded training project. An unusual
experience or psychic implement may begin or modify training, but cannot
silently bypass it.

A Race or Heritage Racial Perk may instead grant `access.psionics` innately. The
Somnari Race Perk **Mindwake** does so and also grants basic psychic sensing
and a consensual short-range mindlink. It grants neither free Psionics ranks
nor unrestricted access to another mind.

Each technique is learned separately and stored by stable ID. Psionics skill
measures control and practice; it does not automatically reveal every psychic
ability. Access, technique knowledge, and competence are validated separately;
a high skill rating, crew position, or psychic implement is never sufficient
without a learned Feat or explicit innate Racial Perk.

## Consent and mental privacy

Mental privacy is an explicit rules boundary, not an assumption left to event
text. Every technique declares one contact mode:

| Contact mode | Rule |
| --- | --- |
| Self | Affects only the user and requires no external consent |
| Invited | Resolves only after the target has knowingly accepted the specific contact |
| Permitted | Uses a current bounded permission such as an active crew mindlink policy |
| Resistible | May target without permission but always gives the target an explicit resistance action |
| Hostile | Is treated as psychic intrusion even if it fails and can trigger witnesses, Alarm, law, or combat |

Consent specifies participants, allowed technique tags, information scope,
duration, and revocation rule. Consent to receive a warning does not grant
permission to inspect memories. A target can end an ordinary link; a technique
that attempts to prevent withdrawal is Hostile and resistible.

Crew policy can pre-authorize narrow emergency contact, such as transmitting an
evacuation warning to an unconscious crew member. The policy, legal authority,
medical justification, and later audit remain visible. It does not create
unlimited consent.

NPCs follow the same contact, resistance, evidence, and consequence rules as
player characters.

## Psychic contact sequence

A psychic action resolves through explicit phases:

1. **Declare:** choose the known technique, target, intended information or
   effect, contact mode, and bounded parameters.
2. **Establish reach:** validate range, line of awareness, anchors, shielding,
   environmental noise, and target identity.
3. **Request or resist:** obtain required consent or resolve the target's
   resistance without exposing protected information.
4. **Reserve strain:** reserve the user's attention, Psychic Strain capacity,
   time, and any implement or amplifier.
5. **Resolve:** combine the contextual attribute, `skill.psionics`, equipment,
   assistance, circumstances, and deterministic random result.
6. **Commit:** publish the allowed impression or effect, strain, feedback,
   evidence, and events atomically.
7. **Release:** end the link immediately or continue its bounded sustain cost.

A rejected invitation reveals only that contact was declined unless the target
chooses otherwise. A failed intrusion cannot leak the private information it
failed to reach.

## Attributes and resolution

Psychic techniques are not permanently bound to one attribute:

- Intelligence interprets complex signals, memories, and psychic patterns.
- Willpower establishes control, resists intrusion, and sustains shields.
- Charisma communicates emotion, identity, intent, or a coherent mental voice.
- Agility coordinates psychokinesis with precise movement.
- Toughness endures feedback, strain, or a hostile psychic environment.
- Luck applies only when a technique explicitly permits a chance-driven
  discovery or escape.

Resistance uses the target's explicit approach, commonly Willpower plus
Psionics, Insight, or a suitable defense. The UI shows known participants,
approaches, modifiers, uncertainty, and results without disclosing secrets the
observer has not earned.

## Psychic Strain

Psychic Strain is a temporary bounded resource from 0 through 100. Using or
sustaining a technique adds strain after reservation. Environmental noise,
injury, poor rest, interference, and repeated contact can increase its cost.

| Strain | State | Effect |
| ---: | --- | --- |
| 0–24 | Clear | No general strain penalty |
| 25–49 | Taxed | Difficult techniques cost more attention |
| 50–74 | Overloaded | Clarity and sustained-link capacity are reduced |
| 75–99 | Critical | Feedback and involuntary signal leakage become serious risks |
| 100 | Collapse | The character cannot initiate another technique and must recover |

Crossing a threshold publishes an explicit event. Collapse does not grant a
writer permission to seize control of the character or invent unrelated
behavior.

Quiet rest reduces strain. Sleep, Somnari dream recovery, Medicine, mental
health care, trained assistance, a shielded room, or a compatible device can
modify recovery through explicit rules. Repeated trivial actions cannot be
used to farm skill progression while recovering.

## Ability disciplines

Disciplines organize psychic techniques for learning and counterplay. They are
not classes.

| Discipline | Stable ID | Scope |
| --- | --- | --- |
| Contact | `psychic.discipline.contact` | Identity exchange, directed thoughts, mindlinks, and psychic signaling |
| Empathy | `psychic.discipline.empathy` | Emotional impressions, distress detection, calming, and emotional projection |
| Shielding | `psychic.discipline.shielding` | Detecting, resisting, masking, and interrupting psychic contact |
| Projection | `psychic.discipline.projection` | Sending images, sounds, concepts, dreams, or deliberate false impressions |
| Memory | `psychic.discipline.memory` | Guided recall, consensual sharing, provenance, and protection of memories |
| Far-sense | `psychic.discipline.far-sense` | Remote awareness through a known person, place, object, or signal anchor |
| Psychokinesis | `psychic.discipline.psychokinesis` | Bounded force, movement, restraint, and manipulation without physical contact |

One technique may carry several discipline tags. A Contact-Empathy technique
might transmit an urgent feeling without words; a Shielding-Projection
technique might create a decoy psychic signature.

## Information rules

Psychic information is bounded evidence, not direct access to simulation truth:

- Emotion reveals an impression, not objective motive, guilt, or truth.
- Surface contact transmits only the authored information scope of the
  technique.
- Memory is subjective, incomplete, and associated with a source and
  confidence. It is not a perfect recording.
- Detecting a mind does not identify it without a known signature or other
  evidence.
- Far-sense requires an anchor and returns limited observations with range,
  noise, and uncertainty.
- Psychic contact does not automatically translate language or explain ancient
  cultural context.
- A failed or resisted technique never reveals the protected answer through UI
  text, logs, or probability previews.

Language and Literacy can structure complex shared communication. Insight can
interpret behavior and uncertainty. Ancient Lore can contextualize old psychic
records or echoes. None becomes redundant.

Deliberate deception can transmit a false impression, but it creates authored
evidence and resistance opportunities. Psionics is not an automatic lie
detector.

## Influence and agency

Psychic influence may create bounded distraction, calm, fear, urgency,
confusion, or attention shifts. It cannot permanently rewrite personality,
silently change faction loyalty, force suicide, erase player commands, or turn
another character into unrestricted property.

Non-consensual influence is Hostile, resistible, detectable according to its
trace rules, and subject to faction law. Strong effects require greater strain,
limited duration, narrower targets, sustained contact, preparation, or several
participants. Repeated attempts face explicit resistance and feedback rules
rather than infinite retries.

## Psychokinesis

Psychokinesis applies force at range but remains bounded by mass, distance,
precision, duration, and line of awareness. It cannot create momentum or energy
without paying the technique's strain and action costs.

Unattended objects are easier than held, anchored, living, or ship-scale
targets. Moving a resisting character requires a hostile check. Manipulating a
ship module may require Engineering knowledge, while delicate remote work may
combine Agility with Psionics.

## Range, amplification, and communication

Unaided contact is local. Distance tiers, blockers, psychic noise, and target
familiarity are explicit technique data. A personal ability does not provide
instant galaxy-wide communication.

Prepared anchors, paired objects, trained relays, a Psychic Resonator, or an
Arcane ship network can extend range. An Industrial ship can support the same
equipment through an isolated source or converter. Amplification increases
capacity but also power demand, detectable signature, interference, and
feedback risk.

Faction reports sent psychically still create message records with sender,
source confidence, dispatch tick, delivery state, and possible interception.
Psionics does not bypass the political knowledge rules in
[`Factions.md`](Factions.md).

## Hazards and counterplay

Psychic hazards include crowded signals, hostile intrusion, dream contagion,
ancient echoes, predatory lures, feedback loops, memory fragments, and aether
storms that couple magic to thought.

Counterplay includes mental shields, breaking range, ending consent, sensory
grounding, psychic silence, decoy signatures, protective equipment, trained
assistance, Ward Projectors, and interrupting an amplifier. Every defense
declares whether it blocks detection, content, influence, projection,
psychokinesis, or feedback.

Magic and Psionics do not automatically counter each other. A hybrid ward or
hazard must explicitly declare both system tags.

## Learning and discovery

Techniques may be learned from mentors, schools, faction training, guided
practice, psychic implements, shared memories, ancient records, or survivable
contact with an anomaly. The source defines consent, language, script, lore,
facility, time, and safety requirements.

Psionic Training and technique learning are separate projects. A character may
complete them in either order, but cannot initiate the technique until both
`access.psionics` and its known-technique ID are present. An innate Racial Perk
replaces only the access project unless it explicitly grants a named technique.

Receiving a psychic impression does not automatically teach the technique that
produced it. Learning is a deterministic project, and unsafe instruction can
add strain or uncertainty without granting partial hidden abilities.

Practice improves `skill.psionics` according to [`Skills.md`](Skills.md).
Learning a technique adds its stable ID to the character's bounded known-
technique collection.

## Data and persistence

An authored psychic technique resembles:

```json
{
  "schemaVersion": 1,
  "id": "psychic.contact.mindlink",
  "nameKey": "psychic.technique.contact.mindlink.name",
  "skillId": "skill.psionics",
  "requiredAccessId": "access.psionics",
  "disciplineIds": ["psychic.discipline.contact"],
  "contactMode": "invited",
  "rangeId": "range.near",
  "targetTags": ["character"],
  "strainCost": 4,
  "sustainCostPerTick": 1,
  "informationScopeId": "psychic.scope.deliberate-message",
  "effectIds": ["effect.psychic.shared-channel"]
}
```

Persistent state stores known technique IDs, Psychic Strain, active learning
projects, permissions, links, shields, effects, and bounded memory-impression
records. An active effect records source technique, user, targets, consent or
resistance result, start tick, duration, sustain source, information scope, and
termination rule.

Definitions are validated for missing references, illegal contact modes,
unbounded information access, negative strain, unlimited range or duration,
recursive effects, unsafe stacking, absent access requirements, absent
resistance, and rollback behavior before publication. Technique-command
validation checks `requiredAccessId` before reserving strain or exposing target
information.

## First playable scope

The first crew encounter milestone needs four techniques:

| Technique | Stable ID | Purpose |
| --- | --- | --- |
| Mindlink | `psychic.contact.mindlink` | Exchange deliberate messages through an invited short-range link |
| Echo Sense | `psychic.empathy.echo-sense` | Detect nearby psychic activity or distress without reading thoughts |
| Quiet Mind | `psychic.shielding.quiet-mind` | Resist or mask one category of psychic contact |
| Kinetic Nudge | `psychic.psychokinesis.kinetic-nudge` | Move one small nearby unattended object |

Mindwake gives a Somnari innate psychic access, Mindlink, and basic psychic
detection. Other characters need Psionic Training plus the learned Mindlink
technique; the remaining techniques require learning for everyone unless an
explicit Racial Perk says otherwise. The slice exercises one trained access path,
one innate access path, one consensual link, one resisted hostile contact, one
shield, one strain threshold, one ambiguous impression, and one recovery
period.

The slice succeeds when consent and resistance are visible, failure reveals no
protected information, and the same seed and commands reproduce the same
strain, evidence, and effects. Mass mind control, perfect memory transfer,
unlimited telepathy, certain prophecy, and galaxy-wide unaided communication
remain deferred or outside the system.
