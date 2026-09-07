# Endgame crises

## Status

This document defines the planned late-campaign crisis framework, crisis
families, escalation, faction response, resolution, aftermath, and persistence
rules. It is not implemented yet. The current prototype has no procedural
galaxy, factions, campaign clock, strategic threats, or endgame state.

An endgame crisis is optional campaign content for a mature generated galaxy.
It is not part of the first playable voyage and does not replace ordinary
exploration, trade, crew survival, or faction conflict.
Campaign-setting ownership and persistence are defined in
[`GameSettings.md`](GameSettings.md).

## Design goals

- Turn established galaxy systems, routes, factions, discoveries, and player
  history into a systemic late-campaign test.
- Telegraph danger early enough for investigation, preparation, evacuation,
  diplomacy, and deliberate risk-taking.
- Offer several credible responses instead of requiring one fleet battle or
  one prescribed heroic ending.
- Let factions disagree about evidence, priorities, sacrifices, ownership, and
  acceptable solutions without assigning permanent good or evil roles.
- Make local losses, partial containment, retreat, adaptation, and negotiated
  survival playable outcomes.
- Preserve player and character agency during psychic, political, and magical
  threats.
- Use explicit seeds, stable IDs, bounded work, and inspectable events so the
  same state and commands reproduce the same crisis outcome.
- Leave a persistent aftermath that changes routes, markets, settlements,
  diplomacy, knowledge, and later voyages.

## Campaign configuration

Crisis settings are chosen before galaxy generation and stored in the campaign
header:

| Setting | Options | Effect |
| --- | --- | --- |
| Crisis count | Off, One, Chained | Disables crises, selects one family, or allows a later second crisis after recovery |
| Intensity | Story, Standard, Severe | Selects authored budgets, timers, resistance, and recovery margins |
| Warning horizon | Long, Standard, Short | Changes the minimum time between confirmed warning and major escalation |
| Eligible families | Per-family toggles | Excludes unwanted themes before crisis selection |
| Campaign continuation | Continue, Ask, Conclude | Controls whether play continues automatically after final aftermath |

Settings do not secretly adjust themselves after campaign creation. Story
intensity preserves the crisis systems and decisions while providing longer
warnings, lower pressure budgets, and more recovery opportunities. Severe
intensity does not remove counterplay or create unavoidable opening losses.

## Eligibility and selection

A crisis does not trigger merely because a fixed number of turns elapsed.
Eligibility uses a bounded campaign-maturity summary that can include:

- the number of Charted regions and restored or exploited landmarks;
- faction logistical capacity, wars, alliances, and unresolved warnings;
- player ship capability, known routes, reserves, and home-anchorage services;
- accumulated aether, psychic, ecological, or industrial instability;
- authenticated Ancient Lore discoveries and completed precursor events; and
- scenario-specific minimum and maximum campaign ages.

The maturity summary determines whether the endgame system may advance; it
does not scale hostile values continuously to counter the player's current
build. Each intensity selects authored budgets before the crisis emerges.

At galaxy creation, a dedicated deterministic `stream.crisis` selects an
eligible crisis family, dormant anchors, and bounded variants. The campaign
stores the selection privately so loading, thread timing, or unexplored-system
generation cannot reroll it. The UI reveals only information legitimately
learned by the crew or a reporting faction.

Selection validation guarantees that required sites, approaches, resources,
and at least one viable resolution can exist in the generated galaxy. A crisis
cannot require a disabled technology path or an excluded faction.

Political crises store deterministic faction-role criteria rather than forcing
specific factions into fixed roles at galaxy creation. When confirmation
approaches, the system binds only factions whose current power, relations,
knowledge, territory, and goals make the roles valid. If no valid pairing
exists, the crisis waits or follows a declared deterministic fallback; it never
invents an alliance or war that contradicts committed faction state.

## Crisis lifecycle

Every crisis advances through explicit phases:

| Phase | State | Player-facing character |
| ---: | --- | --- |
| 0 | Dormant | Anchors and causes exist, but no active threat is asserted |
| 1 | Omens | Ambiguous anomalies create investigation opportunities |
| 2 | Confirmed | Evidence identifies a bounded threat and likely escalation paths |
| 3 | Outbreak | Active fronts begin changing systems, routes, or populations |
| 4 | Escalation | Unanswered fronts gain budget and produce wider consequences |
| 5 | Confrontation | One or more final resolution projects become achievable |
| 6 | Aftermath | The outcome is committed and long-term consequences begin |

Phase transitions occur only at documented event boundaries. They reserve and
commit all affected state transactionally. A transition records its cause,
inputs, changed fronts, spawned objectives, and next known deadline.

The warning horizon guarantees a bounded preparation window after confirmation
unless a clearly previewed player action accelerates the crisis. Omens can be
missed, misunderstood, concealed, sold, or suppressed, but the simulation does
not fabricate a report that no observer could have produced.

## Pressure and fronts

A crisis operates through a bounded set of fronts rather than simulating every
ship and inhabitant continuously. A front represents one active strategic
problem such as an unstable Starway chain, an infected habitat, a coercive
signal source, a harvesting engine, a refugee route, or a contested research
site.

Each front stores:

- stable instance ID, crisis ID, location, phase, tags, and parent objective;
- pressure, resistance, capacity, propagation rules, and next evaluation tick;
- observed and authoritative state as separate records;
- involved factions, ships, settlements, populations, and infrastructure;
- available containment, rescue, research, diplomatic, and combat actions;
- known deadlines with source and confidence;
- consequences at each threshold; and
- resolution, abandonment, retreat, and cleanup rules.

Pressure is a bounded integer budget. Crisis actions spend it on authored
operations such as spreading to an adjacent Starway, corrupting a module,
interfering with a market, attacking a convoy, or concealing evidence. Unspent
pressure does not create unlimited background work.

Resolving one front can expose, weaken, strengthen, redirect, or delay another.
These dependencies are explicit graph edges with cycle and depth limits.

## Initial crisis families

These are original planned content families. A generated campaign varies their
anchors, affected regions, faction positions, vulnerabilities, and aftermath;
it does not generate unreviewed crisis prose.

| Crisis | Stable ID | Domain | Central pressure |
| --- | --- | --- | --- |
| The Shattered Meridian | `crisis.shattered-meridian` | Starways and space | Routes shear, reconnect, and isolate inhabited regions |
| The Glasswake Bloom | `crisis.glasswake-bloom` | Aether and ecology | Self-propagating crystal growth feeds on active Arcane networks |
| The Unbidden Chorus | `crisis.unbidden-chorus` | Psionics and identity | An ancient signal recruits minds into a coercive shared pattern |
| The Cinder Crown | `crisis.cinder-crown` | Industrial and atompunk | Autonomous stellar foundries awaken and harvest inhabited systems |
| The Mawflight | `crisis.mawflight` | Biological invasion | Migrating brood-fleets consume biomass and seed new war-organism nests |
| The Distant Throne | `crisis.distant-throne` | Extragalactic empire | A powerful imperial expedition establishes bridgeheads and demands submission |
| War of the Severed Banners | `crisis.severed-banners-war` | Galactic total war | Two emergent coalitions mobilize the galaxy into opposing strategic blocs |

### The Shattered Meridian

Ancient Starway anchors begin losing agreement about distance and destination.
Early omens include duplicate signals, inconsistent travel times, phantom
destinations, and records written in scripts associated with extinct
navigators. Outbreaks can close routes, create temporary crossings, divide
territory, interrupt trade, and isolate settlements.

The crisis tests navigation, Engineering, Magic, Ancient Lore, logistics, and
faction coordination. Arcane Flux Sails can read aether changes but risk
instability; Industrial drives can cross weakened conventional routes but pay
fuel and transit-time costs. Neither path is the universal answer.

Possible final resolutions include:

- stabilize a minimal safe Starway network through distributed anchor rituals;
- sever the unstable Meridian and accept a permanently fragmented galaxy;
- redirect the cascade into uninhabited sacrificial routes after evacuation;
- reconstruct its original navigation accord from authenticated records; or
- lead a fleet migration through a temporary passage before the network closes.

No route closure commits while a ship is in transit without an authored
arrival, diversion, rescue, or loss rule.

### The Glasswake Bloom

Translucent Aether-reactive crystal begins growing through ruins, habitats,
cargo, and Arcane power systems. It stores extraordinary energy, making early fragments
valuable and politically divisive. Mature growth drains or overloads nearby
Aether resonators, alters local environments, and launches seeded fragments along
trade routes.

The Bloom is neither a race nor automatically a conscious enemy. Research may
find ecological behavior, damaged control inscriptions, emergent intelligence,
or a combination selected from authored variants. Claims about its sentience
remain hypotheses until supported by campaign evidence.

Possible final resolutions include:

- cultivate a stable low-energy form that no longer propagates destructively;
- create a quarantine lattice and maintain protected exclusion regions;
- starve coordinated growth by shutting down or converting major aether hubs;
- communicate with an intelligent variant and negotiate bounded habitats; or
- burn out its seed vault while managing contamination and displaced users.

Alchemy, Enchantment, Medicine, Magic, Engineering, and ecological knowledge
all contribute. Industrial power is not immune: contamination travels through
cargo, crews, converters, markets, and mixed-technology ports.

### The Unbidden Chorus

A repeating psychic structure emerges from old relays and dream records. It
offers effortless communication, shared calm, and relief from isolation before
pressuring linked minds to surrender privacy and independent choice. Hosts and
victims remain people with affiliations, needs, and rights rather than becoming
a disposable monster category.

All contact uses the consent, resistance, information, and Psychic Strain rules
in [`PsychicAbilities.md`](PsychicAbilities.md). The crisis cannot read private
state after a failed check, silently rewrite a playable character, or erase a
player command. Coercive effects are Hostile, bounded, attributable, and
counterable.

Possible final resolutions include:

- isolate and silence the primary relay chain while treating affected people;
- separate the Chorus into voluntary, revocable local networks;
- expose a concealed directing intelligence and negotiate or defeat it;
- construct a galaxy-wide counterpattern from diverse scripts and minds; or
- evacuate signal-dense regions and let the pattern decay without new hosts.

Psionics, Language and Literacy, Medicine, Negotiation, Ancient Lore, and
signal Engineering provide different evidence and solutions. Psychic-capable
races receive neither automatic guilt nor automatic immunity.

### The Cinder Crown

An ancient ring of autonomous foundries wakes around a dim star. Its machines
classify inhabited systems as abandoned feedstock and dispatch surveyors,
harvesters, claim beacons, and mobile reactors. Each captured resource expands
its bounded production budget and pushes its technology from diesel-scale
machinery toward dangerous atompunk systems.

The Crown acts through machines, logistical routes, contracts, and recoverable
command records rather than an omniscient intelligence. Its forces obey the
same movement, sensor, damage, fuel, and communication rules as other actors,
with explicit exceptions supplied by modules or crisis effects.

Possible final resolutions include:

- authenticate a surviving ownership compact and issue a lawful shutdown;
- sabotage fuel, cooling, and command networks before confronting the core;
- capture and reprogram distributed foundries under faction oversight;
- redirect the Crown toward lifeless resource systems through negotiation or
  forged priorities whose risks remain visible; or
- destroy its central coordination while accepting uncontrolled remnant fleets.

Engineering, Crafting, Merchant, Negotiation, Ancient Lore, ship weapon
loadouts, and both ship technology paths can contribute. Direct combat is
possible but cannot be the only authored route.

### The Mawflight

Armored brood-vessels cross the galactic rim in a migration called the
Mawflight. Their castes strip unprotected habitats, agricultural worlds, and
living ships for biomass, then grow nests, scouts, soldiers, and new carriers
adapted to local hazards. Early omens include empty biospheres, drifting molts,
organic radio pulses, altered migration patterns, and refugee ships carrying
dormant spores.

The Mawflight is an invasive war ecology rather than a reskinned ordinary
faction. Broods share chemical and resonant signals but do not receive instant
knowledge across the galaxy. Each brood-fleet has bounded biomass, sensory
range, adaptation memory, reproductive sites, and supply routes. Destroying a
scout before it reports can matter; losing a nursery can reduce later spawn
budgets.

Authored variants determine whether command comes from queens, distributed
instinct, symbiotic navigators, or a concealed directing intelligence. The
player must discover which model applies instead of receiving universal hive
knowledge from the UI.

Possible final resolutions include:

- destroy or capture the primary nursery fleet before it seeds the inner
  regions;
- synthesize a false migration signal and redirect major broods toward lifeless
  systems;
- break coordination between brood castes so isolated populations can be
  contained;
- quarantine infested routes while Alchemy, Medicine, and Xenology eliminate
  dormant spores;
- communicate with an intelligent variant and negotiate a bounded feeding or
  settlement corridor; or
- evacuate vulnerable regions and fight a delaying campaign until the
  migration passes.

Ship weapon loadouts and boarding remain important, but Xenology, Alchemy,
Medicine, Sensors, Cooking and food logistics, Engineering, and signal research
can change the war. Adaptation counters repeated tactics through explicit
bounded tags; it never grants instant immunity to whatever last harmed a
brood.

### The Distant Throne

A disciplined imperial expedition enters through newly stabilized routes
beyond the known galactic rim. It brings warships, administrators, engineers,
client peoples, mobile shipyards, and a claim that the generated galaxy falls
under the authority of the **Distant Throne**. Initial envoys offer protected
status, tribute schedules, technology restrictions, and positions within its
order before resistant systems face blockade or occupation.

The invading empire is powerful but not omniscient or inexhaustible. Its fleets
depend on surveyed routes, bridgehead stations, command relays, replacement
crews, political legitimacy, and supply from outside the galaxy. Long
communications create space for local commanders, rival claimants, client
revolts, intercepted orders, and negotiated exceptions.

The Throne forms a crisis faction with ordinary knowledge, standing,
agreements, territory, logistics, and internal-interest records. Its military
advantages come from explicit ship modules, doctrine, preparation, and crisis
budgets—not arbitrary immunity to local weapons, Magic, or Psionics.

Possible final resolutions include:

- unite enough local factions to destroy the bridgeheads and close the outer
  routes;
- sever imperial supply and command links until the expedition negotiates or
  fragments;
- support a rival claimant, reform movement, or client revolt and accept the
  resulting political obligations;
- negotiate recognition, borders, technology exchange, and limited tribute
  without surrendering the whole galaxy;
- accept protected or vassal status and continue play under visible laws,
  duties, and resistance opportunities; or
- organize an exodus beyond occupied space when military defense has failed.

Diplomacy can be as decisive as combat, but agreements do not erase coercion or
their costs. Different imperial populations and commanders remain individuals;
membership in the invasion does not define a Race or make every subject an
identical enemy.

### War of the Severed Banners

Two powerful faction coalitions escalate a regional dispute into total war.
The crisis binds existing factions dynamically: economic dependencies,
treaties, rivalries, border geometry, military capacity, ideology, and recent
player actions determine the two leading blocs and which factions remain
neutral, divided, or nonaligned.

The trigger can be an attacked guarantee, failed summit, contested ancient
weapon, Starway blockade, succession dispute, or crisis aftermath selected
from authored causes that fit committed history. The simulation records the
actual cause; it does not declare war solely because an endgame timer expired.

Total-war fronts include mobilization, convoy interdiction, contested
Starways, sieges, occupation, refugee movement, propaganda, espionage,
strategic-weapon projects, and peace negotiations. Each coalition has bounded
war capacity, supply, cohesion, objectives, and exhaustion. Territory does not
change hands merely because map color advances; control requires valid presence
and supply under [`Factions.md`](Factions.md).

The player may join either bloc, remain neutral, sell services, defend the home
anchorage, build a third neutral league, expose the cause of escalation,
sabotage strategic weapons, rescue civilians, or broker limited ceasefires.
Neutrality has obligations and risks but is not silently treated as joining the
enemy.

Possible final resolutions include:

- achieve one coalition's bounded war aims and negotiate the resulting order;
- broker an armistice through concessions, guarantees, inspections, and
  enforceable demobilization;
- exhaust both war machines by cutting access to strategic resources;
- form a credible neutral league that forces recognition of protected routes
  and settlements;
- split hardline chapters from factions willing to settle; or
- survive a partitioned galaxy in which the total war ends as a hostile cold
  peace.

No faction is forced permanently into one banner. Chapters can defect, accords
can expire, and war crimes or broken promises create persistent responsibility
for the actors involved rather than collective racial guilt.

## Investigation and knowledge

Crisis knowledge follows the same separation as galaxy and faction knowledge.
The authoritative state is not automatically visible to the player. Evidence
can come from surveys, survivors, intercepted traffic, psychic impressions,
ancient texts, markets, faction reports, or direct encounters.

Every conclusion records source, confidence, age, and scope. Contradictory
reports remain visible until resolved. Ancient Lore supplies context, while
Language and Literacy determines whether scripts and terminology can be read;
neither skill turns uncertain evidence into guaranteed truth.

Warnings move through faction couriers, relays, treaties, and psychic channels.
They have dispatch and delivery ticks, interception risk, and credibility. A
faction cannot react to an outbreak it has not observed or been told about.

## Faction response

Existing factions keep their normal goals, knowledge, resources, standing, and
decision rules. A crisis adds bounded response goals such as investigation,
quarantine, evacuation, profiteering, containment, research, alliance,
appeasement, or denial.

Factions can form temporary accords without merging into one galactic polity.
An accord declares participants, objective, contributions, command rules,
information sharing, prohibited actions, exit terms, and dispute process. The
player may join, broker, exploit, oppose, or remain outside it.

Crisis behavior can split chapters when policies and local survival pressures
diverge. Such splits use ordinary faction-instance and agreement state, not
hard-coded betrayal. Contribution records prevent the final outcome from
crediting only the last attack or delivery.

## Player participation

The player's ship is influential but not assumed to command the galaxy. Useful
actions include:

- surveying omens and authenticating ancient records;
- carrying warnings, specialists, medicine, refugees, or scarce materials;
- repairing anchors, relays, habitats, wards, and industrial infrastructure;
- negotiating access, ceasefires, evacuations, research rights, and shared
  costs;
- escorting, raiding, blockading, salvaging, or fighting;
- developing countermeasures through classless skills and crew projects;
- choosing which threatened places receive limited time and resources; and
- retreating, relocating the home anchorage, or preparing a survival voyage.

When a crisis objective becomes a crew- or ship-scale fight, it uses the shared
rules in [`Battle.md`](Battle.md). Strategic fronts supply context and
consequences; they do not create a second incompatible combat system.

Objectives expose their likely contribution, risk, deadline, and dependencies.
Hidden consequences require discoverable evidence rather than arbitrary
reversals. No single character Race, Heritage, Racial Perk, skill, spell, ship path,
or faction membership is mandatory for all viable resolutions.

## Escalation and fairness

A crisis may punish ignored warnings, failed plans, faction conflict, greed, or
unprepared expansion, but it follows consistent constraints:

- confirmed threats provide at least the configured minimum warning horizon;
- the first outbreak cannot erase the home anchorage without an intervening
  playable response;
- propagation follows valid Starways, signal paths, cargo routes, anchors, or
  explicitly described special movement;
- ordinary defenses work when their tags and capacity apply;
- crisis immunity is narrow, visible, and justified by a definition;
- difficulty budgets do not secretly mirror every improvement to the player
  ship;
- losses publish causes and do not mutate unrelated systems; and
- at least one viable response remains until the campaign reaches a clearly
  communicated terminal state.

Procedural placement validation reserves necessary approach diversity. For
example, a required research site cannot be placed only beyond the route it is
needed to stabilize.

## Resolution and campaign outcomes

A final resolution is a multi-stage project with explicit prerequisites,
participants, resources, time, risks, and commit point. Combat may protect or
complete a project, but reducing a universal health bar is not the framework's
default ending.

Outcome grades are descriptive state, not a single score:

| Outcome | Meaning |
| --- | --- |
| Transformed victory | The main threat is resolved and its systems become a managed part of the galaxy |
| Containment | Active spread stops, but permanent borders, maintenance, or watch duties remain |
| Costly survival | The crisis ends after major route, settlement, faction, or ship losses |
| Exodus | The crew and some allies escape a region or galaxy that cannot be preserved |
| Collapse | No viable player continuation remains under the scenario's declared terminal rules |

The aftermath commits surviving settlements, changed Starways, faction
standing, agreements, debts, refugees, technologies, contamination, memorials,
and unresolved remnants. The player can continue when campaign settings and
the outcome permit it. Continuation is not labeled as a perfect victory, and a
partial outcome is not silently converted into failure.

## Data and persistence

An authored crisis definition resembles:

```json
{
  "schemaVersion": 1,
  "id": "crisis.shattered-meridian",
  "nameKey": "crisis.shattered-meridian.name",
  "domainTags": ["starway", "spatial", "ancient"],
  "minimumMaturity": 70,
  "phaseIds": [
    "crisis.phase.dormant",
    "crisis.phase.omens",
    "crisis.phase.confirmed",
    "crisis.phase.outbreak",
    "crisis.phase.escalation",
    "crisis.phase.confrontation",
    "crisis.phase.aftermath"
  ],
  "frontLimit": 12,
  "resolutionIds": [
    "crisis.shattered-meridian.resolution.stabilize",
    "crisis.shattered-meridian.resolution.sever",
    "crisis.shattered-meridian.resolution.migrate"
  ]
}
```

Persistent state stores the crisis definition revision, private selection,
owned random-stream state, phase, intensity, fronts, pressure budgets, anchors,
objectives, deadlines, crisis actors, coalition membership, occupation state,
faction knowledge, player knowledge, contributions, resolution projects,
committed losses, and aftermath.

Save loading never reselects a crisis or regenerates its anchors. Definitions
are versioned, and migrations validate the complete replacement before
publication. Player-visible names and descriptions remain localization keys;
translated text is never authoritative identity.

## Simulation bounds and validation

The crisis system caps active fronts, actions per evaluation, propagation
distance, dependency depth, spawned actors, queued reports, participants per
accord, objective count, and retained history. Distant activity uses scheduled
strategic summaries rather than per-entity simulation.

Content loading rejects:

- duplicate or missing IDs, phases, fronts, resolutions, or localization keys;
- a resolution whose required sites, skills, resources, or technology path
  cannot exist under eligible campaign settings;
- propagation without bounded range, cost, path, target rules, or next tick;
- phase transitions without warnings, causes, or transaction boundaries;
- unbounded spawning, recursive front creation, circular required objectives,
  and unlimited history growth;
- psychic effects that bypass consent, resistance, privacy, or player agency;
- route mutations that can leave traveling ships without a defined result;
- invasion fronts without a legal entry route, logistics budget, sensing rules,
  or bounded reinforcement source;
- total-war selection without two valid coalitions, limited war aims, neutral
  participation rules, exhaustion, and at least one non-conquest resolution;
- faction reactions based on knowledge the faction does not possess; and
- aftermath that partially commits or invalidates stable galaxy identities.

## Delivery order

Endgame work begins only after the galaxy, faction, crew, ship-module,
encounter, campaign-save, and content-validation foundations are playable.

1. Prototype one dormant-to-confirmed omen chain without destructive galaxy
   changes.
2. Add a single bounded front with investigation, rescue, containment, and
   combat contributions.
3. Implement one crisis family through aftermath at Story intensity.
4. Add faction accords, alternative resolutions, and Standard intensity.
5. Add the remaining families and Severe or Chained configurations only after
   deterministic campaign-scale tests are practical.

The first playable voyage explicitly excludes endgame crises. Crisis fleets,
galaxy-scale populations, procedural narrative prose, unrestricted world
rewrites, and simultaneous unbounded crises remain deferred.
