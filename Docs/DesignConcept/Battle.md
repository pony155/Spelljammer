# Battle and tactical encounters

## Status

Milestone 5 implements the first headless combat framework: fixed-tick ship
orders with tactical pause, continuous fixed-point movement, modular damage,
and a bounded personal hex encounter with Turn Meters, Action Points, injuries,
objectives, surrender, and retreat. The WPF host still presents the expedition
prototype; boarding transitions, EVA, settlements, save serialization, and the
broader catalog in this document remain planned.

Battle is one possible way to resolve an encounter, not a separate campaign
genre. Exploration, negotiation, stealth, rescue, sabotage, surrender, and
retreat remain valid before and during combat.

## Design goals

- Use one deterministic action and injury framework across different battle
  locations instead of creating unrelated combat minigames.
- Let ship layout, ruin structure, cover, atmosphere, gravity, visibility,
  hazards, and objectives matter as much as raw damage.
- Make crew attributes, skills, equipment, positions, learned techniques,
  access Feats, and Racial Perks produce understandable tactical options.
- Connect ship-to-ship fire, module damage, boarding, internal defense, and
  disengagement without discarding consequences between scales.
- Support Arcane, Industrial, and hybrid equipment without making one path the
  mandatory combat build.
- Make injury, ammunition, Focus, Psychic Strain, damaged modules, lost cargo,
  prisoners, witnesses, and faction consequences persist after battle.
- Keep surrender, rescue, capture, escape, delay, and partial success as real
  outcomes; eliminating every opponent is not the default objective.
- Bound pathfinding, visibility, reactions, projectiles, effects, AI planning,
  and per-tick work.
- Give ship engagements and personal combat distinct pacing while keeping both
  deterministic on the same authoritative fixed-tick simulation.

## Combat contexts

All contexts share actors, action phases, detection, damage, conditions, and
save rules while supplying different spatial, environmental, and command-time
models.

| Context | Stable ID | Command-time model | Tactical space | Typical objectives |
| --- | --- | --- | --- | --- |
| Ship engagement | `combat.context.ship` | Real-time with tactical pause | Bounded continuous 2D coordinates, headings, velocity vectors, firing arcs, and local objects | Escape, disable, escort, blockade, capture, or destroy |
| Boarding and ship interior | `combat.context.boarding` | Personal timeline with Turn Meter and Action Points | Hex cells grouped into compartments and connected through doors, hatches, ladders, ducts, and breaches | Repel, seize a module, rescue crew, sabotage, or reach a lock |
| Space ruin | `combat.context.ruin` | Personal timeline with Turn Meter and Action Points | Authored or generated hex boards, rooms, seals, traps, and vertical links | Explore, recover an artifact, survive defenses, or withdraw |
| EVA and hull exterior | `combat.context.eva` | Personal timeline with Turn Meter and Action Points | Hex cells anchored to surfaces, open gaps, debris, and tether routes | Repair, cross hulls, cut entry, rescue, or defend an exterior module |
| Station or settlement | `combat.context.settlement` | Personal timeline with Turn Meter and Action Points | Hex cells grouped into rooms, streets, docks, crowds, restricted areas, and exits | Protect civilians, arrest, escape, hold ground, or negotiate |
| Surface expedition | `combat.context.surface` | Personal timeline with Turn Meter and Action Points | Bounded hex terrain with elevation, weather, hazards, and extraction points | Survey, hunt, rescue, defend camp, or reach transport |

Ship combat is continuous in both time and space: ships do not occupy cells and
do not jump between range bands. The player may pause it to inspect the
situation, issue or revise uncommitted orders, and then resume. Personal combat
is not organized into global rounds: each actor becomes ready on an individual
timeline and receives a bounded Action Point budget for that activation.

The first personal-combat implementation uses bounded hex boards grouped into
tactically meaningful zones rather than seamless three-dimensional terrain. A
zone can be a ship compartment, ruin chamber, stretch of hull, street, or
terrain area. Cells and zone links encode occupancy, doors, cover, distance,
height, pressure seals, line of sight, and movement requirements.

## Encounter structure

A tactical encounter declares:

- stable encounter definition and instance IDs;
- context, location, participants, teams, knowledge, and initial placements;
- objectives, optional objectives, failure conditions, and escape routes;
- ship tactical geometry or personal hex board, zone graph, cover, visibility,
  atmosphere, gravity, lighting, and hazards;
- neutral actors, civilians, prisoners, cargo, and protected infrastructure;
- reinforcement sources, limits, arrival conditions, and warning rules;
- surrender, negotiation, retreat, pursuit, and cleanup behavior;
- rewards, salvage rights, witnesses, laws, and faction consequences; and
- owned deterministic random streams and bounded simulation budgets.

An encounter is published only after every participant has a legal placement
and at least one declared way to pursue, abandon, or fail the primary objective.
Procedural assembly selects compatible authored rooms, zones, opponents,
hazards, and objectives; it does not generate unreviewed narrative text.

## Time and command model

Authoritative combat advances on the fixed simulation tick. Rendering cadence,
animation, wall-clock delay, and the time a player spends paused never advance
the simulation or alter a result. Ship and personal combat use different
command-time models over that shared clock.

### Ship combat: real-time with tactical pause

Ship engagements advance continuously while unpaused. Navigation, weapons,
defenses, sensors, Power or Aether, damage control, hazards, and opposing ships
may all progress during the same ticks.

Ship position, heading, velocity, and maneuver state use deterministic
fixed-point values on a bounded continuous 2D map. A spatial index may divide
the map internally for bounded queries, but those partitions are not movement
cells and cannot quantize a ship's legal position.

The player can pause at any completed tick boundary to:

- inspect only information currently available to the crew;
- issue maneuver, targeting, station, allocation, damage-control, boarding,
  communication, or retreat orders;
- reorder or cancel commands that have not begun their Reserve phase; and
- review predicted paths, firing arcs, costs, dependencies, and rejection
  reasons without receiving a guaranteed outcome preview.

Pausing is a command interface, not a character ability or consumable. AI does
not advance, hidden timers do not run, and resources are not consumed while the
authoritative clock is paused. Once play resumes, orders execute when their
declared conditions and station requirements become valid. An order that has
reserved resources or committed an effect follows its explicit cancellation
and rollback rule rather than receiving an automatic refund.

Ship orders do not use personal Action Points. Their pacing comes from crew and
station availability, preparation and recovery ticks, module readiness,
resource flow, and bounded command queues.

### Personal combat: individual timeline activations

Boarding, ruin, EVA, settlement, and surface combat use a tactical hex board
and individual readiness rather than team turns or global rounds. Every actor
owns two separate bounded values:

- **Turn Meter:** determines when that actor becomes Ready. It advances only
  with authoritative combat time. Agility, equipment, injuries, conditions,
  preparation, and recovery can modify its documented fill rate.
- **Action Points (AP):** determine how much the actor can plan during one
  activation. Moving through cells, attacking, using an item, operating an
  object, casting, assisting, and changing stance have explicit AP costs.

When an actor's Turn Meter reaches its threshold, the actor enters the Ready
queue. Equal-tick readiness is ordered by documented priority, then stable
Actor ID; UI selection order never breaks ties. A player-controlled Ready actor
pauses authoritative time while the player assembles or confirms a bounded
action plan. AI-controlled actors choose plans from the same observed state and
legal-action rules without wall-clock delay affecting the result.

An activation ends when its AP is spent, the actor ends voluntarily, the actor
becomes unable to act, or a declared plan reaches its bounded limit. Unspent AP
does not carry into the next activation. AP deliberately reserved for a
reaction remains unavailable to ordinary actions until it is spent or expires
at that actor's next activation.

Committing a plan does not guarantee every step. Planned actions still prepare,
validate at their execution boundary, trigger reactions, commit atomically,
and recover over simulation ticks. Movement may be interrupted by a newly
blocked cell, an actor can be incapacitated before a later planned attack, and
reserved but uncommitted costs follow their declared rollback rules. After the
activation resolves, recovery delays and Turn Meter progression determine when
the actor becomes Ready again. A fast actor may therefore activate more often
without receiving unlimited AP in one activation.

### Shared action lifecycle

Actions use explicit phases:

1. **Declare:** select actor, action, target, route, equipment, and parameters.
2. **Validate:** check knowledge, range, access, resources, friendly fire, and
   current command authority.
3. **Reserve:** reserve ammunition, charge, Focus, Stamina, Psychic Strain
   capacity, stations, and required items.
4. **Prepare:** spend wind-up time, aim, move, speak, cast, reload, or operate a
   station.
5. **Commit:** resolve the action and publish its effects atomically.
6. **Recover:** apply recovery time, sustain costs, exposure, and cooldown.

Ship movement, weapon fire, spells, psychic techniques, repairs, medical
actions, personal plans, and environmental changes can overlap on the fixed
timeline. Equal-tick conflicts use documented priority categories and stable
actor IDs as final tie-breakers, never UI order or thread completion timing.

A reaction reserves attention, personal AP, a station, or equipment before its
trigger occurs. Overwatch, parry, intercept, counterspell, emergency seal, and
protective movement are examples. Actors cannot receive unlimited free
reactions from one event.

## Action resolution

Combat uses the same contextual capability model as ordinary crew work:

```text
relevant attribute + relevant skill + equipment + assistance
+ technique and access + circumstances
```

Examples include:

- Strength plus Melee to force an armored opponent away from a hatch;
- Agility plus Archery to fire a bow across a low-gravity ruin chamber;
- Intelligence plus Sensors to establish a firing solution through moving
  debris;
- Willpower plus Magic to sustain Brace Ward under fire, requiring
  `access.magic` and the known spell;
- Charisma plus Command to coordinate a fighting withdrawal;
- Toughness plus EVA to remain functional after suit damage; and
- Intelligence plus Engineering to disable an ancient defense without
  destroying it.

Attack resolution separates contact, protection, and harm. First determine
whether the action reaches the target, then apply cover, armor, wards, shields,
and resistance, and finally commit bounded harm and secondary effects. This
keeps a missed shot distinct from a shot stopped by armor.

The exact formula and numeric scale remain deferred until prototype balancing.
The action record must preserve inputs, modifiers, random result, mitigation,
harm, resource use, and rejection or failure reason.

## Spatial rules

### Personal boards

Personal-combat boards contain a bounded number of hex cells. Each cell belongs
to one tactical zone and can be exposed, covered, elevated, sealed, unstable,
anchored, occupied, or hazardous. Cell edges and zone links define movement AP
and time, direction, access, traversal skill, visibility, and whether a door or
barrier can close across them. Large creatures and objects declare multi-cell
footprints explicitly.

### Continuous ship map

The ship battlefield is a bounded continuous 2D coordinate space with no
movement grid. Ships, stations, wrecks, projectiles, hazards, and other local
objects have fixed-point position and bounded collision or interaction shapes.
Ships also track heading, velocity, maneuver commitment, and the capabilities
that limit turning, acceleration, braking, and reverse thrust.

A maneuver order declares a desired course, thrust, facing, orbit, intercept,
escort offset, or disengagement vector. The ship moves through every
intermediate position as fixed ticks commit; it never teleports from cell to
cell or range band to range band. Terrain, collisions, weapon arcs, projectile
paths, sensor confidence, and boarding alignment query this continuous state.

The implementation may use a deterministic bounded spatial partition to avoid
checking every object against every other object. That partition is an
acceleration structure only and is neither visible gameplay topology nor a
source of position rounding.

Distance presentation remains contextual:

- personal encounters derive engaged, near, far, and beyond from hex distance,
  elevation, intervening edges, and the action's scale;
- ship encounters derive grappled, close, exchange, and long labels from exact
  continuous distance, relative motion, and the observing system; and
- sensors can detect beyond weapon range with confidence and identity limits.

Ship movement, collision, and targeting use authored ship-scale distance units;
range labels never replace coordinates. Personal actions may map their bands to
hex-distance rules. A personal bow cannot target a ship merely because both
interfaces display "far."

Movement does not provide cost-free attacks. Crossing a watched link, leaving
engagement, opening a pressure door, climbing, drifting without an anchor, or
carrying an injured character can expose explicit reactions and extra time.

## Detection and surprise

Combat begins from encounter knowledge rather than omniscient team vision.
Each actor can have Unknown, Detected, Located, Identified, and Assessed
knowledge states with source, confidence, and last observation tick.

Sensors, sight, sound, aether traces, psychic signals, tracks, damage, and
faction reports reveal different facts. Stealth changes evidence and detection;
it does not make an actor absent from authoritative collision or hazard rules.

Surprise provides a bounded preparation or position advantage. It never grants
an entire team unlimited unanswered turns. An ambush must have a valid concealment
source, observation path, and trigger.

## Ship-to-ship engagements

Ship combat combines navigation, crew stations, weapons, defenses, damage
control, and objectives in real time with tactical pause. It is not a duel
between two aggregate health bars, and it does not wait for alternating ship
turns.

The tactical state tracks:

- fixed-point position, heading, velocity, movement commitment, collision
  shape, exact relative vectors, and escape route;
- sensor contact and identification confidence;
- available firing arcs, weapon damage, rate of fire, effective and maximum
  range, reload state, damage type and area, armor penetration, ammunition,
  or Aether charge;
- Energy Shield maximum and current Shield Value, Recharge Rate, Energy
  Consumption Rate, and raised or lowered state;
- energy allocation across propulsion, defense, weapons, and sensors;
- Hull, Armor Value, and simple module condition;
- nearby terrain such as debris, ruins, stations, storms, and gravity hazards;
  and
- boarding alignment, docking locks, tethers, breaches, and separation risk.

Crew act through stations while the ship clock runs. A Pilot maneuvers,
Engineer reroutes power or performs damage control, Ship Mage operates Arcane
effects, Mindwarden handles psychic threats, and Captain sets objectives and
priorities. The player directly orders installed ship cannons; firing does not
require a Gunner position or Gunnery Skill check. Other station orders still
need any declared operator, preparation, resources, and recovery time.
Positions grant responsibility and authority, not automatic skill ranks.

Typical ship actions include scan, identify, set course, apply thrust, turn,
brake, intercept, match velocity, hold formation, evade, fire, ram, raise or
lower shields, brace, reroute power, repair, jam, signal, negotiate,
launch boarding, repel boarding, rescue, disengage, and surrender.

Weapons target a ship, visible module, exposed compartment, projectile, or
declared area according to their tags. Precision targeting requires sufficient
knowledge and firing solution. Damage can breach hull, disable modules, ignite
cargo, injure crew, interrupt a resource supply, change maneuver capability,
or force evacuation.

A legal cannon order requires a ready, undamaged-enough installed weapon, a
target or area inside its firing arc and range, and its declared resource. An
Aether Energy Cannon reserves Aether charge from the ship network; a Diesel
Shell Cannon or Atomic Shell Cannon reserves one physical shell through its
magazine and logistics route. Rate of fire schedules shots; reload time controls
when the cannon can fire again. Damage, damage type, damage area, and armor
penetration resolve against the target's protection. Cannon resolution does not
add a recoil stat or wait for an assigned Gunner.

Ship attack damage crosses three explicit defensive layers:

1. If the whole-ship Energy Shield is raised and powered for the tick, subtract
   damage from its current Shield Value. Excess damage continues.
2. Apply remaining damage to the ship's Armor Value. Armor penetration
   determines how much passes through.
3. Commit penetrating damage to Hull or one selected visible module. A damaged
   module uses its simple Damaged or Disabled state.

Current Shield Value is a finite state value, not bonus armor. While the shield
is raised and receives its full Energy Consumption Rate, each fixed tick adds
its Recharge Rate up to the maximum Shield Value. Those are its only combat
statistics. Lowering a shield stops both consumption and replenishment and
never repairs armor. Ward Projectors resolve only their declared magical,
psychic, or environmental effects and do not silently substitute for an Energy
Shield.

A ram requires a compatible installed prow module, a legal collision course,
and sufficient relative velocity. Impact resolves damage and impulse for both
ships, including the attacking frame; a ram is not a cost-free melee attack.
A figurehead is visual customization unless an installed enchantment, ward,
sensor, or command effect gives it explicit mechanics and resource demands.
Cannon and other weapon modules retain their installed orientation, firing arc,
damage profile, rate of fire, range, reload state, damage area, armor
penetration, resource type, and simple condition.

Disengagement is a contest of position, propulsion, detection, terrain, and
pursuit commitment. A faster ship does not automatically escape if trapped at
a dock, attached by boarding gear, or unable to navigate an unknown route.

## Boarding and shipboard melee

Boarding begins only through a valid transfer path: agreed docking, captured
Boarding Lock, matched-vector grapple, shuttle, EVA crossing, breached hull, or
an explicit Passage spell. The transition preserves ship motion, exterior
hazards, damage, crew positions, and reinforcement travel time.

The ship interior is a compartment graph whose active encounter areas are
resolved as bounded hex boards. Doors, pressure seals, gravity, lighting,
smoke, fire, coolant, radiation, noise, narrow passages, and fragile systems
shape cell movement, AP costs, line of sight, action timing, and close combat.
Defenders can lock routes, evacuate compartments, cut gravity, vent an empty
zone, move cargo as cover, isolate networks, or counter-board through another
path.

Melee weapons are valuable in cramped compartments because they are controllable
and do not require ammunition. Ranged weapons offer reach but must declare
penetration, recoil, noise, ammunition, firing clearance, and risk to hull,
crew, cargo, or machinery. A missed shot still follows its weapon's explicit
stray-shot rule.

Boarding objectives include capturing the bridge, disabling propulsion,
releasing prisoners, taking cargo, planting a device, rescuing survivors,
opening a lock, or holding until separation. Killing the entire opposing crew
is never an assumed requirement.

## Space-ruin expeditions

Ruin battles combine exploration state with tactical state. Entering combat
does not reveal the whole map or discard previous discoveries, opened seals,
translated scripts, disabled traps, moved objects, contamination, or consumed
supplies.

A ruin board may contain:

- ancient guardians, scavengers, rival expeditions, predators, or trapped
  survivors;
- dormant defenses triggered by movement, sound, aether, heat, identity, or an
  incorrectly translated command;
- vacuum, unstable gravity, radiation, spores, psychic echoes, shifting
  passages, collapsing floors, or failing life support;
- control stations that can be understood through Engineering, Magic,
  Psionics, Language and Literacy, or Ancient Lore; and
- artifacts, records, living systems, or infrastructure that combat can
  permanently damage.

Players can scout, mark safe routes, translate warnings, disable systems,
negotiate with rivals, lure threats, seal chambers, or withdraw before combat.
During battle, completing the expedition objective may be more valuable than
holding every zone.

Ruin generation guarantees an entrance, a retreat or declared one-way rule,
bounded zone count, valid objective placement, and compatible environmental
requirements. It cannot place the only required tool beyond the obstacle that
requires it.

## EVA and exterior combat

EVA movement requires suit capability, propellant or anchors, and an explicit
route. Momentum, tether state, handholds, visibility, debris, radiation,
temperature, and decompression affect action timing and risk.

Forced movement is especially dangerous because leaving an anchor can turn an
ordinary hit into separation from the ship. A character drifting away enters a
rescue state with position and remaining support; they do not disappear merely
because they left the local battle graph.

Projectile recoil, hull penetration, suit puncture, and loose equipment use
explicit tags. Magic and Psionics still require their access Feats or innate
Racial Perks and do not ignore vacuum, range, line of effect, or Psychic Strain.

## Settlement and surface conflicts

Ports, habitats, and settlements include civilians, laws, witnesses, property,
security responses, restricted weapons, and routes that remain important after
combat. Starting violence can raise Alarm, void agreements, close markets, or
create arrest and restitution demands even when the tactical action succeeds.

Surface expeditions add terrain, weather, local ecology, gravity, atmosphere,
vehicle access, and extraction timing. The player's crew remains a small unit;
armies and galaxy-scale battles are represented by strategic fronts while the
ship handles one bounded objective within them.

## Weapons, armor, and techniques

Weapons are data-authored items with stable IDs and tags appropriate to their
scale. Personal weapons can declare skill, grip, range, delivery, damage type,
penetration, recoil, ammunition or charge, preparation, action time, recovery,
noise, trace, and valid targets. Ship cannons instead declare damage, rate of
fire, effective and maximum range, firing arc, reload time, damage type, damage
area, armor penetration, and physical ammunition or Aether charge.

Ship cannon definitions also declare one cannon family. Arcane Aether Energy
Cannons spend Aether charge; Industrial Diesel Shell and Atomic Shell Cannons
spend physical ammunition. The family changes resource availability, reload
failure, damage-control risk, and loadout planning, not the shared targeting or
damage sequence.

- Melee covers unarmed attacks and handheld close-combat weapons.
- Archery covers bows, crossbows, and unusual string-launched weapons.
- Gunnery covers personal firearms and manually operated mounted weapons. Ship
  cannon commands use the installed weapon and ship targeting state without a
  Gunnery requirement.
- Magic and Psionics use their own skills, learned content, resources, and
  access requirements.
- Defense covers active protection, positioning, shields, and learned defensive
  techniques without becoming universal armor expertise.

Techniques provide bounded maneuvers such as disarm, suppress, pin, guard,
aimed shot, controlled burst, shield another, or fighting withdrawal. They are
learned separately from skills when their definition requires it. A high skill
improves execution but does not automatically grant every technique.

Ship armor declares one Armor Value and Slot Cost. Personal armor remains a
separate tactical item system. Ship armor is intentionally a simple defense
layer rather than a mass, coverage, heat, or maintenance simulation.

## Magic, Psionics, and enchantment

A character cannot cast because combat started. Spellcasting requires
`access.magic`, the known spell, sufficient resources, a legal target, and the
simple cast flow in [`Spells.md`](Spells.md). Only channeled or ritual spells
can be interrupted or sustained across ticks.

Psychic techniques require `access.psionics` and follow consent, resistance,
privacy, information, and Psychic Strain rules in
[`PsychicAbilities.md`](PsychicAbilities.md). A failed intrusion cannot reveal
the information it failed to reach. Mental influence never silently replaces a
player command.

Enchantments modify compatible equipment, modules, or locations through
explicit effects. They do not grant knowledge of a spell or bypass its access
Feat unless the item definition casts independently and owns its costs,
targeting, and failure behavior.

## Harm, injury, and incapacitation

Characters do not rely on a large undifferentiated health pool. Harm resolves
through bounded severity bands such as superficial, wounded, critical, and
incapacitated, with authored injury tags for bleeding, fracture, burn, poison,
vacuum exposure, psychic shock, and similar consequences.

Armor, cover, wards, resistance, and Toughness can reduce or redirect harm when
their tags apply. Toughness improves endurance and survival; it does not make a
character immune to a blade, decompression, or reactor radiation.

Injuries can affect movement, senses, action time, equipment use, consciousness,
and recovery. Medicine can stabilize and treat them; Vitality magic can support
stabilization and natural recovery without erasing medical gameplay. Serious
injuries persist after the encounter.

An incapacitated actor can be rescued, captured, abandoned, or killed only
under explicit rules. Death-causing effects, bleed-out deadlines, stabilization,
and coup-de-grace actions are visible and do not arise from hidden narrative
fiat.

Ships resolve harm through finite current Shield Value, Armor Value, Hull, and
simple module condition as defined in [`Ships.md`](Ships.md). Character and
ship damage commit together when one attack affects both.

## Morale, surrender, and prisoners

Combatants have authored goals and risk tolerances. They may retreat, surrender,
negotiate, protect another actor, abandon an objective, or refuse a suicidal
order when their state and authority rules allow it.

Surrender declares who yields, what equipment or ship control is transferred,
which actors are protected, and what happens if terms are broken. Prisoners
remain characters with needs, injuries, affiliation, knowledge, and legal
status. Detention consumes guarded space, food, air, medical care, and crew
attention.

Command, Negotiation, faction standing, visible losses, escape routes, and
credible threats can affect morale without becoming magical mind control.
Race never supplies a universal courage, aggression, or surrender rule.

## Tactical AI

AI actors use the same authoritative observations and legal actions as player
crew. Their definitions provide bounded priorities such as protect, capture,
delay, escape, feed, patrol, investigate, or negotiate, plus risk tolerance,
coordination, and known doctrine.

Planning searches have fixed depth, candidate, path, and time budgets. If no
preferred plan fits the budget, the actor chooses a documented safe fallback
such as hold, take cover, assist, withdraw, or wait. Stable IDs break equal
choices before a deterministic random stream is consulted.

Difficulty changes authored resources, coordination, preparation, and error
margins. It does not give AI secret player knowledge, unlimited reactions,
free ammunition, or damage immunity.

## End of battle and consequences

An encounter ends when its objective, withdrawal, surrender, separation, or
terminal conditions commit. The cleanup phase records:

- surviving, injured, incapacitated, dead, missing, captured, and rescued
  characters;
- ship, module, equipment, cargo, ruin, and environmental damage;
- ammunition, charge, Focus, Stamina, Psychic Strain, medicine, fuel, and time
  spent;
- recovered items, registered salvage, contraband, prisoners, and evidence;
- witnesses, reports, law violations, agreements, standing, and Alarm changes;
- incomplete objectives, escaping opponents, pursuit opportunities, and
  altered return routes; and
- knowledge gained about actors, weapons, locations, factions, and hazards.

Victory does not restore resources or injuries automatically. Retreat preserves
survivors and information at the cost of abandoned objectives, cargo, position,
or reputation. A battle result becomes normal persistent campaign state rather
than a temporary reward screen.

## Data and persistence

An authored encounter definition resembles:

```json
{
  "schemaVersion": 1,
  "id": "encounter.ruin.sealed-observatory",
  "nameKey": "encounter.ruin.sealed-observatory.name",
  "contextId": "combat.context.ruin",
  "zoneGraphId": "zone-graph.ruin.observatory-small",
  "objectiveIds": [
    "objective.ruin.recover-chart",
    "objective.common.withdraw"
  ],
  "participantTableId": "participants.ruin.guardians-low",
  "hazardTableId": "hazards.ruin.aether-vacuum",
  "actorLimit": 12,
  "reinforcementLimit": 4,
  "durationLimitTicks": 1800
}
```

Persistent battle state stores definition revisions, encounter seed and random
streams, tick, participants, teams, knowledge, continuous ship positions,
headings and velocities, current Energy Shield value and raised state,
Armor Value, Hull, module condition, personal zone and cell state, objectives,
Turn Meters, current AP and reserved reaction AP, Ready-queue state, queued ship
orders, reserved resources, scheduled actions, reactions, projectiles, active
effects, hazards, injuries, ship damage, reinforcement state, retreat paths,
and committed event history.

Saving is allowed only at documented commit boundaries. Loading restores the
same next authoritative tick and never rerolls an attack, AI choice,
reinforcement, injury, or ruin layout. Presentation animation and selected UI
panels are not save state. A loaded battle opens paused so wall-clock time
cannot advance before the player receives the restored snapshot.

## Bounds and validation

Runtime limits include ships, continuous-map extent, local space objects,
spatial-query candidates, modules per ship, actors, personal-board dimensions,
hex cells, zones, links, AP per activation, Ready
actors, queued ship orders, planned personal actions, carried items, active
effects, scheduled actions, reactions per trigger, projectiles, reinforcements,
path expansions, visibility checks, AI candidates, event history, and maximum
encounter duration.

Content loading rejects:

- duplicate or missing encounter, actor, zone, objective, item, effect, and
  localization IDs;
- disconnected required objectives, illegal placements, missing retreat rules,
  or a board, cell set, or zone graph beyond capacity;
- attacks without skill, range, target, delivery, protection, harm, evidence,
  ammunition or charge, and miss behavior;
- ship weapons or defenses without bounded Shield Value, Recharge Rate, Energy
  Consumption Rate, armor, penetration, overflow, and hit-location behavior
  where applicable;
- supernatural actions without access, learned-content, cost, targeting, and
  resistance requirements;
- reinforcements without source, limit, route, warning, and arrival condition;
- hazards, triggers, reactions, or AI plans capable of unbounded recursion;
- boarding without a valid transfer path or ship separation rule;
- ship attacks whose compartment and character consequences cannot commit
  atomically; and
- encounter completion that discards injuries, damage, spent resources,
  witnesses, faction consequences, or changed exploration state.

New content is validated completely before replacing the active catalog. A
failed reload leaves the previous definitions available.

## First playable battle scope

The first battle milestone should contain two connected encounters:

1. a small real-time-with-pause engagement on a continuous 2D ship map with
   scanning, course and thrust control, one weapon, Energy Shield depletion and
   replenishment, armor penetration, module damage, damage control,
   signaling, disengagement, queued station orders, and an optional boarding
   route;
2. a bounded hex-board derelict or ruin expedition with six tactical zones,
   four active crew, individual Turn Meters and AP, limited visibility, one
   environmental hazard, one hostile group, one ancient defense, one
   non-combat solution, and an extraction objective.

The same crew, equipment, ammunition, injuries, spell resources, Psychic
Strain, ship damage, knowledge, and faction consequences persist between the
two. The slice includes one melee action, one ranged action, one defensive
reaction, one spell or psychic technique with valid access, one medical
stabilization, one surrender or retreat, and one damaged object the player
wanted to preserve.

The milestone succeeds when the same seed and command sequence reproduces the
same battle regardless of pause duration, every personal actor follows the same
Turn Meter and AP schedule, the player can explain every major modifier and
injury, and eliminating every opponent is not required. Large fleets, armies,
seamless three-dimensional movement, procedural tactical prose, destructible
planets, competitive multiplayer, and continuous rigid-body space physics
remain deferred.
