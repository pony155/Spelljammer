# Interstellar travel events

## Status

This document defines the planned event system for interstellar voyages. It is
not implemented. The current expedition prototype derives fixed sector hazards
and salvage from a seed but does not schedule event definitions, choices,
checks, delayed consequences, or event instances.

Travel events are seeded random situations that can occur while a ship crosses
a Starway, waits in deep space, approaches a system, or reacts to an ongoing
voyage condition. They turn route environment, ship configuration, crew,
cargo, knowledge, factions, and crises into short systemic stories without
using unbounded procedural prose.

Galaxy routes and travel are defined in [`GalaxyMap.md`](GalaxyMap.md).
Ship and personal encounter resolution is defined in [`Battle.md`](Battle.md),
while standing, reports, and agreements are defined in
[`Factions.md`](Factions.md). Campaign-setting ownership and accessibility
boundaries are defined in [`GameSettings.md`](GameSettings.md).

## Design goals

- Make voyages uncertain without making their results depend on wall-clock
  time, frame rate, save reload timing, or UI behavior.
- Select only events that fit the current route, ship, crew, knowledge, faction,
  and campaign state.
- Give the player meaningful choices such as investigate, assist, evade,
  negotiate, repair, exploit, conceal, or continue the voyage.
- Allow Attributes, Skills, Racial Perks, Feats, equipment, ship modules, supplies,
  and learned information to create additional approaches rather than one
  mandatory answer.
- Persist injuries, damage, cargo, discoveries, obligations, witnesses,
  reputation, route changes, and delayed follow-ups.
- Prevent excessive repetition, unavoidable lethal surprises, unbounded event
  chains, and unlimited reward farming.
- Keep authored event definitions separate from localized text and persistent
  event-instance state.

## When events can occur

The scheduler does not roll once per rendered frame or after arbitrary real
time. A committed travel leg creates a bounded sequence of deterministic event
opportunities based on route progress, such as departure, early passage,
midpoint, hazard boundary, late passage, and approach.

At each opportunity the scheduler:

1. builds an eligible set from authored event definitions;
2. removes events blocked by scope, prerequisites, exclusions, cooldowns,
   repetition limits, or the remaining event budget;
3. calculates deterministic integer weights from the route and current
   authoritative state;
4. includes a no-event result so every opportunity need not produce content;
5. samples from the owned `travel-events` random stream; and
6. commits the chosen Event ID, instance ID, seed, trigger tick, and route
   progress before revealing it.

Selection uses stable Event IDs to order equal candidates. Each committed event
instance derives separate named streams for selection details, checks, rewards,
and follow-ups. Adding a visual effect, opening a menu, or consulting another
random subsystem cannot change which event occurs.

## Eligibility and weighting

An event definition can require or modify weight from:

- Starway region, distance, stability, hazard, aether, traffic, law, and
  faction-control tags;
- departure, transit, approach, deep-space, blockade, or special-route phase;
- Arcane, Industrial, or hybrid energy and propulsion capabilities;
- installed modules, armor coverage, current Energy Shield Value and raised
  state, damage, faults, heat, signature, fuel, air, provisions, medicine,
  spare parts, cargo, and free capacity;
- crew count, positions, Skills, access Feats, Racial Perks, injuries,
  fatigue, needs, and current duties;
- known factions, agreements, warrants, standing, witnesses, rumors, charts,
  scripts, Ancient Lore, and discovered sites;
- prior event outcomes, unresolved follow-ups, voyage count, and crisis state;
  and
- campaign settings for frequency, severity, repetition, and accessibility.

Requirements test authoritative state, but the UI reveals only facts the crew
can currently observe. A hidden pirate contact may make an event eligible
without displaying "pirates nearby" before Sensors or other evidence discovers
it.

Weights adjust relative likelihood; they do not rewrite event consequences.
Content cannot set negative weights, overflow totals, create recursive weight
queries, or become infinitely likely. A maximum per leg, minimum spacing, and
per-event cooldown prevent event floods.

## Event lifecycle

A travel event instance moves through explicit states:

```text
Scheduled -> Revealed -> AwaitingDecision -> Resolving
          -> ActiveEncounter -> Resolved
          -> Expired
```

- **Scheduled:** identity, seed, opportunity, and trigger conditions are
  committed but may still be unknown to the player.
- **Revealed:** observations and available information are published.
- **AwaitingDecision:** travel pauses at a completed simulation tick while the
  player inspects options and issues one valid response.
- **Resolving:** costs, checks, random results, and effects commit atomically.
- **ActiveEncounter:** the event has created a timed ship situation, tactical
  battle, site, conversation, or multi-step objective handled by its owning
  system.
- **Resolved:** immediate results and any follow-up references are committed.
- **Expired:** an explicitly optional opportunity passed or its trigger became
  invalid under a documented rule.

The time a player spends reading does not advance travel, consume supplies, or
alter random results. A time-sensitive choice uses simulation ticks only after
the player resumes. Closing a window never selects a choice; every event that
can wait for input has an explicit Leave, Continue, Delay, or other safe option.

## Choices, checks, and consequences

Each choice declares:

- stable choice identity and localized label, description, and confirmation
  keys;
- facts required for the choice to be visible and facts required for it to be
  enabled;
- required ship capability, module, item, position authority, Skill, Feat,
  Racial Perk, known language, script, spell, or psychic technique;
- immediate costs, reservations, duration, exposure, and cancellation rules;
- contextual Attribute and Skill approaches rather than a character class;
- deterministic outcome bands and the information the player may preview;
- immediate effects, event-chain transitions, and delayed follow-ups; and
- which actors, factions, witnesses, ships, routes, sites, and items retain the
  consequence.

A check can improve or worsen an outcome, but every enabled choice must define
failure and rollback behavior. Failure cannot consume a cost twice, leave a
half-installed module, or reveal protected information that the failed action
did not discover. Success is not guaranteed merely because an option is
visible.

Consequences can change fuel, aether, provisions, time, cargo, module faults,
ship damage, injuries, practice, knowledge, maps, standing, agreements, Debt,
Alarm, witnesses, sites, Starways, encounter placement, or later event weights.
Effects publish normal simulation events so other systems can react without
the travel-event scheduler directly mutating their private state.

## Event categories and initial examples

| Category | Example | Stable ID | Possible approaches |
| --- | --- | --- | --- |
| Environmental | Aether Squall | `event.travel.aether-squall` | Alter course, brace wards, ground the aether network, or ride the current |
| Ship failure | Coolant Leak | `event.travel.coolant-leak` | Isolate the loop, reduce power, spend parts, or accept heat and delay |
| Discovery | Derelict Signal | `event.travel.derelict-signal` | Scan, approach, mark the location, salvage, board, or continue |
| Rescue | Distress Call | `event.travel.distress-call` | Authenticate, answer, conceal, negotiate, render aid, or avoid a trap |
| Crew | Crew Dispute | `event.travel.crew-dispute` | Mediate, investigate, change duties, enforce policy, or defer the issue |
| Hidden passenger | Stowaway | `event.travel.stowaway` | Treat, question, shelter, recruit, detain, ransom, or return them lawfully |
| Navigation | Starway Echo | `event.travel.starway-echo` | Compare charts, translate an old beacon, follow it, or preserve the route data |
| Threat | Pirate Shadow | `event.travel.pirate-shadow` | Evade, mask the ship, signal, bargain, prepare weapons, or enter ship combat |

These are base-owned Event IDs, not guaranteed rolls. Definitions and weights
decide whether they can appear in a given voyage.

## Relationship to encounters and combat

An event is a trigger and decision structure, not a second combat engine. It
may resolve immediately or create an existing type of encounter:

- a sensor contact or pursuit becomes real-time-with-pause ship combat on the
  continuous ship map;
- boarding a derelict or answering a hostile distress call can create personal
  combat on a hex board with Turn Meters and Action Points;
- a discovery can add a persistent site to the galaxy map;
- a crew problem can create duties, treatment, training, or a later follow-up;
  and
- a faction contact can create an offer, report, agreement, warrant, or market
  change.

Transitioning to another system preserves the Event instance ID and seed so
the resulting encounter, cleanup, and follow-up can be traced to its cause.
Returning from the encounter resumes or changes the committed travel leg rather
than silently restoring its previous state.

## Pacing, repetition, and fairness

Each definition declares its recurrence scope: repeatable, once per route,
once per voyage, once per campaign, or unique. It also declares minimum spacing,
cooldown, maximum occurrences, mutual-exclusion tags, and whether variants count
as the same repetition family.

The scheduler keeps a bounded recent-event history and reduces or removes the
weight of repeated families. Content cannot evade repetition limits by changing
localized text or presentation variants while retaining the same mechanics.

Events with catastrophic outcomes require at least one of:

- prior observable warning;
- a route-risk disclosure or known uncertainty;
- an enabled retreat, mitigation, or resource-sacrifice choice;
- a player-created state that clearly accepted the risk; or
- a campaign setting that explicitly permits harsher untelegraphed outcomes.

Random events may create losses and difficult tradeoffs, but they cannot erase
a campaign through an unavoidable choice-free result in the standard setting.
Rewards account for cost, risk, recurrence, and route difficulty, and repeatable
events cannot produce unbounded profit or Skill practice.

## Authored data

A planned event definition resembles:

```json
{
  "schemaVersion": 1,
  "revision": 1,
  "id": "event.travel.coolant-leak",
  "nameKey": "event.travel.coolant-leak.name",
  "descriptionKey": "event.travel.coolant-leak.description",
  "opportunityTags": ["transit", "industrial"],
  "requiredTags": ["ship.cooling.loop"],
  "weight": 20,
  "recurrence": "once-per-voyage",
  "minimumSpacingTicks": 600,
  "choiceIds": [
    "event.travel.coolant-leak.choice.isolate",
    "event.travel.coolant-leak.choice.reduce-power",
    "event.travel.coolant-leak.choice.repair"
  ]
}
```

Weights use bounded nonnegative integers. Conditions, checks, and effects refer
only to allowlisted deterministic primitives. Event data cannot execute code,
access the network, read arbitrary files, format its own executable expression,
or inject unreviewed generated prose.

Player-visible prose and option labels use localization keys. Stable Event and
choice IDs determine state, saves, commands, sorting, cooldowns, and follow-ups;
translated text never becomes identity.

## Persistence and replay

Campaign state stores:

- travel-event scheduler version and named random-stream state;
- current leg ID, opportunity ordinal, event budget, and next eligible progress;
- Scheduled, Revealed, AwaitingDecision, Resolving, and ActiveEncounter event
  instances;
- Event definition ID and revision, instance ID, seed, trigger tick, route
  progress, revealed observations, and committed choice;
- bounded recurrence counts, cooldowns, repetition-family history, and unique
  completion flags; and
- pending follow-up IDs with earliest tick, expiry, prerequisites, and owning
  system.

Saving and loading cannot reroll a scheduled event, its details, check, reward,
or follow-up. A save made while awaiting a choice restores the same revealed
information and choices and opens paused. Removing required event content from
a campaign fails content-lock preflight rather than discarding pending state.

## Bounds and validation

Runtime limits include opportunities per leg, events per leg and voyage,
eligible candidates, choices per event, condition evaluations, effects per
outcome, chain depth, active instances, pending follow-ups, cooldown records,
recent-history entries, and retained event log records.

Content validation rejects:

- duplicate or invalid Event and choice IDs;
- missing localization, condition, check, effect, encounter, faction, route,
  item, module, Skill, Feat, Racial Perk, spell, or psychic references;
- negative or excessive weights, costs, delays, cooldowns, or occurrence counts;
- events with no reachable resolution or no safe response when one is required;
- choice costs without atomic reservation and rollback rules;
- chains, follow-ups, or effects capable of unbounded recursion;
- catastrophic standard-setting outcomes without an authored warning,
  mitigation, accepted risk, or safe alternative;
- hidden-condition text that leaks information not yet observed; and
- repeatable rewards or practice that can exceed their declared budgets.

If a candidate event set fails validation, the previous working content
snapshot remains active. If no event is eligible during travel, the no-event
result is valid and the voyage continues.

## First playable scope

The first complete voyage implements four base events:

```text
event.travel.aether-squall
event.travel.coolant-leak
event.travel.derelict-signal
event.travel.distress-call
```

The slice needs one event opportunity near the middle of each eligible travel
leg, a no-event outcome, a maximum of two travel events per leg, recurrence
tracking, and at least two choices per event. Across the four definitions it
must exercise one ship configuration response, one crew Skill approach, one
resource sacrifice, one persistent discovery or follow-up, one optional combat
transition, and one safe continue-or-withdraw response.

The slice succeeds when the same seed, content fingerprint, voyage state, route,
and command stream reproduce the same event IDs, revealed details, choices,
checks, consequences, and follow-ups. Large event catalogs, unrestricted event
modding, freeform procedural stories, romance chains, and crisis-specific event
families remain deferred.
