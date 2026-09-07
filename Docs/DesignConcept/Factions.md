# Factions

## Status

This document defines the planned faction, membership, standing, diplomacy,
territory, law, market, conflict, and procedural-placement systems. They are
not implemented yet. The current voyage prototype has no faction state,
characters, markets, treaties, or territorial simulation.
Faction behavior during optional late-campaign threats is defined in
[`Endgame_Crisis.md`](Endgame_Crisis.md).

## Design goals

- Create factions with understandable interests, resources, laws, and internal
  pressures rather than universal good or evil alignment.
- Keep Race and Heritage separate from citizenship, ideology, profession, and
  faction membership.
- Make trade, negotiation, reputation, contracts, trespass, salvage, and
  violence leave persistent political consequences.
- Let factions pursue bounded goals and respond to world events without
  scripting one mandatory campaign story.
- Place authored factions differently in each seeded galaxy while preserving
  coherent origins, territory, services, and conflicts.
- Show why a faction acted and which observation, policy, agreement, need, or
  grievance informed its decision.
- Use stable IDs and versioned data for saves, generation, localization, and
  eventual modding.

## Arcane-Industrial civilization

The galaxy is technologically advanced: Arcane craft and dieselpunk-to-atompunk
industry coexist in ports, fleets, markets, and law. Factions differ in how
they regulate, finance, and deploy these tools—not in whether advanced
technology or magic exists at all. Aether resonators, charge accumulators,
reactor material, fuel, enchantments, spare parts, and skilled labor are all normal political and
economic concerns. The setting baseline is in [`History.md`](History.md).

## Race, culture, and faction

A faction is an organization, polity, movement, guild, fleet, faith, company,
or alliance. It is not a race. Humans, elves, half-elves, dwarves, orcs,
gnomes, goblins, Somnari, Veyr, Eidolons, Tharun, and future races may
belong to the same faction. Members of one race may support rival factions or
none at all.

Heritage supplies formative tradition and starting language or script
knowledge; it does not assign loyalty. A faction may use official languages,
scripts, uniforms, titles, or rituals, but these are learned practices rather
than biological properties.

Characters can hold bounded affiliation records such as citizen, member,
officer, employee, contractor, guest, debtor, prisoner, exile, or wanted
person. Affiliation is separate from the character's ship position. A faction
officer may serve as a ship's Doctor, and a Captain need not hold any external
faction rank.

## Faction model

Faction data is separated into an authored definition and mutable campaign
state.

### Authored definition

| Field | Responsibility |
| --- | --- |
| Stable ID | Persistent identity independent from localized names |
| Organization | Council, league, fleet, guild, compact, court, company, or other governance form |
| Doctrines | Public priorities and decision-making biases, not moral alignment |
| Policies | Default laws for docking, trade, salvage, weapons, magic, psychic contact, and conduct |
| Economy | Desired resources, exports, shortages, services, and market behavior |
| Capabilities | Typical fleets, stations, ship modules, knowledge, and technology preferences |
| Communication | Official languages, scripts, signals, and diplomatic protocols |
| Generation rules | Valid origins, environmental needs, minimum distance, rarity, and exclusions |
| Presentation | Localized keys, emblem definition, colors, music tags, and map treatment |

### Campaign state

| State | Examples |
| --- | --- |
| Leadership | Current office holders, claimants, succession, and vacancies |
| Holdings | Controlled stations, settlements, fleets, depots, and mobile assets |
| Resources | Treasury, fuel, food, reactor material, Aether charge hardware, influence, and logistics |
| Goals | Secure a route, relieve a shortage, recover a relic, enforce a claim, or survive a threat |
| Policies | Active laws, emergency orders, embargoes, amnesties, and rules of engagement |
| Relations | Treaties, disputes, wars, truces, debts, and opinions of other factions |
| Knowledge | Known systems, Starways, sites, threats, markets, and reported player actions |

Definitions describe what a faction can be. Campaign state records what has
happened to this particular instance. Generated placement does not mutate the
authored definition.

## Initial planned faction roster

These factions are an initial original content set, not implemented features.
Each can contain characters of any compatible race or heritage.

| Faction | Stable ID | Primary interests | Characteristic pressure |
| --- | --- | --- | --- |
| Free Anchorage Compact | `faction.free-anchorage-compact` | Neutral docking, repair, arbitration, markets, and mutual defense | Requires dues, contraband rules, and restraint from conflicts that threaten the anchorage |
| Meridian Foundry League | `faction.meridian-foundry-league` | Dieselpunk industry, atompunk development, shipbuilding, reactor fuel, and technical contracts | Demands inspections, patent recognition, reliable supply, and responsibility for contamination |
| Lumenwake Covenant | `faction.lumenwake-covenant` | Arcane navigation, aether research, wards, enchantment, and preservation of dangerous sites | Restricts unstable magic, unlicensed relic removal, and reckless disturbance of aether routes |
| Horizon Salvagers' Union | `faction.horizon-salvagers-union` | Wreck recovery, rescue, claim registration, auctions, and hazardous-site expertise | Treats claim jumping, concealed finds, and abandoned crews as serious offenses |
| Quiet Chorus Assembly | `faction.quiet-chorus-assembly` | Psychic communication, mediation, couriers, mental medicine, and privacy law | Requires consent for psychic contact and aggressively investigates coercive intrusion |
| Pilgrim Garden Fleet | `faction.pilgrim-garden-fleet` | Food, seed banks, living habitats, medicine, and restoration of damaged worlds | Enforces quarantine and resists trade that threatens protected ecologies |

No faction has a universal racial composition, permanent diplomatic role, or
guaranteed relationship with the player. A generated campaign may omit a
faction, place it beyond the starting region, split it into rival chapters, or
alter its immediate priorities through scenario rules.

## Standing and reputation

Player relations are not represented by one omniscient friendship number. Each
faction or local chapter tracks bounded, independently inspectable values:

| Measure | Range | Meaning |
| --- | ---: | --- |
| Regard | -100 to 100 | Approval or hostility based on known actions and shared interests |
| Trust | 0 to 100 | Confidence that the player tells the truth and honors agreements |
| Alarm | 0 to 100 | Immediate security attention caused by threats, contraband, trespass, or violence |
| Favor | Bounded ledger | Explicit services owed to or by the player |
| Debt | Bounded ledger | Currency, cargo, service, restitution, or contractual obligations |

Regard and Trust change through recorded reputation events with stable cause
IDs. Alarm usually rises quickly and decays according to policy, communication,
and time. Favor and Debt are explicit obligations with issuer, owner, value,
terms, and settlement state; they are not free-floating currencies.

A high Regard does not cancel a warrant, unpaid debt, quarantine, or treaty
obligation. High Alarm does not erase years of Trust. The UI shows relevant
values, thresholds, recent causes, and the local authority that will act on
them.

### Knowledge and propagation

A faction reacts only to information it possesses. An action can become known
through witnesses, sensors, official reports, captured records, trade gossip,
psychic communication, or player confession. Reports store source, confidence,
subject, location, observation tick, and transmission state.

News propagates through bounded communication routes and takes time unless an
explicit network provides immediate contact. Local chapters may disagree or
act on incomplete information. Correcting a false report requires evidence or
influence; it does not happen automatically because the simulation knows the
truth.

## Diplomacy and agreements

Diplomatic actions include:

- hail, identify, threaten, apologize, or request parley;
- negotiate prices, docking, passage, salvage rights, prisoner exchange, or
  restitution;
- accept, reject, counter, fulfill, breach, suspend, or terminate a contract;
- request safe conduct, amnesty, sponsorship, recognition, or mediation;
- establish trade access, non-aggression, mutual defense, research exchange,
  embargo, blockade, or truce; and
- surrender, demand surrender, ransom a prize, or arrange evacuation.

Every agreement is an explicit persistent record containing stable agreement
ID, signatories, authorized representatives, scope, terms, start and end ticks,
obligations, permissions, breach conditions, witnesses, enforcement rules, and
current state. Local officials cannot promise authority they do not possess.

Merchant identifies value, scarcity, market practice, and hidden commercial
risk. Negotiation shapes acceptable terms and concessions. Language and
Literacy handles communication and documents; Ancient Lore may reveal old
claims or treaty meanings; Insight detects motives and uncertainty. Skills can
improve an offer or expose alternatives but cannot force consent or make an
impossible promise valid.

## Territory and jurisdiction

Faction geography follows the generated Starway graph. The system distinguishes:

- **presence:** ships, agents, or a temporary operation are nearby;
- **claim:** a faction asserts a right that others may dispute;
- **control:** the faction can currently patrol and enforce policy;
- **occupation:** control is held against another recognized claimant; and
- **shared jurisdiction:** multiple parties hold explicit rights under an
  agreement.

A colored map border is a summary, not authority by itself. System and Starway
records identify claims, controllers, enforcement reach, disputes, and the
source and confidence of the player's knowledge.

Relevant laws may govern docking, tariffs, salvage, relic removal, weapons,
reactor fuel, aether use, enchantment, psychic contact, medical stores,
quarantine, prisoners, and environmental protection. The interface
shows applicable known rules before the player commits an action when their
crew could reasonably know them.

Trespass and violations create evidence, reports, Alarm, warrants, fines,
seizure claims, or hostile orders according to policy. They do not make every
member of a faction immediately and permanently hostile.

## Markets and logistics

Faction markets have local inventories, currencies or accepted value tags,
production, consumption, reserves, tariffs, embargoes, and shipment routes.
Prices respond to bounded supply, demand, danger, law, relationships, and
recent events rather than a single universal market.

Factions need physical or Arcane logistics to move goods and information.
Blockades, lost freighters, damaged Starways, piracy, harvest failure, reactor
accidents, and aether storms can create shortages and contracts. Successful
player deliveries affect the destination and do not silently replenish every
market in the galaxy.

Transactions commit atomically. Failed validation, interrupted negotiation, or
insufficient cargo leaves the previous inventory, funds, ownership, and
agreement state intact.

## Goals and autonomous action

Each faction selects a bounded set of active goals from authored goal
definitions whose requirements match current state. A goal declares its
priority inputs, resource budget, valid actions, completion conditions,
abandonment conditions, and explanation keys.

Possible goals include:

- secure or survey a Starway;
- acquire food, fuel, reactor material, Aether resonators, ships, or knowledge;
- protect a settlement, convoy, ruin, ecology, or allied faction;
- enforce a claim, embargo, quarantine, warrant, or treaty;
- investigate a disappearance, psychic intrusion, relic, or anomaly;
- repair a station, recover a wreck, escort civilians, or evacuate a hazard;
- expand influence, negotiate access, or undermine a rival; and
- survive a war, succession crisis, disaster, or internal split.

Faction decisions execute through the authoritative fixed-tick command and
event boundary. Stable priorities and faction IDs break equal choices.
Background activity has explicit action, path-search, communication, and
resource budgets. A faction that cannot act records its blocker rather than
silently receiving resources or teleporting assets.

## Conflict and force

Conflict states include dispute, restricted access, embargo, blockade,
hostilities, limited war, and truce. A faction's rules of engagement define
when its forces warn, inspect, shadow, arrest, seize, disable, board, retreat,
or destroy. These rules can differ by system, commander, target status, and
current agreement.

Violence produces witnesses, casualties, damage, salvage claims, prisoners,
missing persons, and political consequences. Surrender, retreat, ransom,
restitution, and rescue remain valid outcomes. A hostile faction is not an
infinite source of identical enemies; ships and supplies come from bounded
holdings and logistics.

Encounter difficulty follows actual ships, crews, locations, knowledge, and
world state. It does not automatically scale to the player.

## Procedural placement

The galaxy generator selects a bounded faction roster from authored definitions
and scenario requirements. Placement proceeds on its own named deterministic
random stream and validates:

- eligible origin system and environmental tags;
- minimum distance between major origins;
- access to required resources and at least one expansion route;
- territory size and graph-expansion budget;
- neutral space and services near the starting anchorage;
- at least one understandable source of cooperation and one source of tension;
- mutually exclusive factions or scenario roles; and
- reachable early contracts without mandatory hostility.

Initial territory grows through bounded multi-source graph expansion weighted
by distance, route quality, environment, doctrine, and existing claims. A
contested system can receive several claims but only explicit current
controllers.

Generation creates the initial state only. Later territory, leadership,
relations, markets, and goals change through committed simulation events and
are never regenerated when loading a save.

## Data and persistence

An authored faction definition resembles:

```json
{
  "schemaVersion": 1,
  "id": "faction.horizon-salvagers-union",
  "nameKey": "faction.horizon-salvagers-union.name",
  "descriptionKey": "faction.horizon-salvagers-union.description",
  "organizationId": "organization.guild",
  "doctrineIds": ["doctrine.rescue-duty", "doctrine.registered-claims"],
  "defaultPolicyIds": ["policy.salvage.claim-registry"],
  "serviceTags": ["salvage", "rescue", "auction"],
  "originRequirements": ["site.station", "region.wreck-rich"],
  "expansionBudget": 12
}
```

Persistent state stores definition ID and revision, generated faction-instance
ID, leadership, holdings, resources, goals, policies, knowledge, relations,
standing, reports, warrants, agreements, markets, and event references.
Localized names, descriptions, titles, and policy text are never gameplay
identity.

Definition loading rejects duplicate or missing IDs, unknown doctrines or
policies, invalid ranges, unbounded goals, impossible origin constraints,
unknown languages or scripts, and circular mandatory relationships. Campaign
updates validate all participating factions and agreements before atomically
publishing replacement state.

## First playable scope

The first complete voyage uses three factions:

- the Free Anchorage Compact as the neutral home authority;
- the Horizon Salvagers' Union as a source of contracts and contested claims;
  and
- either the Meridian Foundry League or Lumenwake Covenant as a buyer whose
  technology interests complicate the salvage dispute.

The slice needs one settlement, one neutral market, one contested salvage site,
and one delayed report path. It exercises Regard, Trust, Alarm, a cargo Debt,
one salvage agreement, one negotiation with alternative terms, one witnessed
breach or fulfillment, and one faction response on returning to the anchorage.

The slice succeeds when the player can explain why each faction made its offer
or response, the same seed and commands reproduce the same political state,
and violence is not the only viable resolution. Galactic councils, large
alliances, civil wars, elections, espionage networks, ideological conversion,
and dozens of simulated factions remain deferred.
