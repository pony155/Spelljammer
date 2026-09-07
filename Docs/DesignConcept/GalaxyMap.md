# Procedural galaxy map

## Status

This document defines the planned procedural galaxy, star-system, route,
discovery, and map-persistence systems. They are not implemented yet. The
current prototype uses a deterministic 4 × 4 sector grid with six sector kinds;
that grid proves the voyage command and resource loop but is not the final
galaxy representation.
Optional late-campaign threats that can transform this graph are defined in
[`Endgame_Crisis.md`](Endgame_Crisis.md).
Seeded situations that occur while crossing its routes are defined in
[`Events.md`](Events.md). The ownership and persistence rules for player-facing
campaign configuration are defined in [`GameSettings.md`](GameSettings.md).

## Design goals

- Generate a different but reproducible galaxy for each explicit campaign seed.
- Create strategic geography through clusters, long routes, chokepoints,
  hazardous shortcuts, isolated pockets, and competing faction territories.
- Make exploration reveal useful knowledge instead of simply uncovering icons.
- Support both Arcane and Industrial ships without making either upgrade path
  unable to traverse the generated galaxy.
- Place Arcane research sites, dieselpunk industrial ports, and atompunk-era
  reactor installations as authored site content rather than treating advanced
  technology as a rare exception.
- Generate nebulae, Aether tides, and exceptional worlds that attract elven
  observatories, as well as asteroid fields, comet belts, and gas giants that
  attract dwarven prospectors and extraction fleets.
- Combine authored system definitions and event content in new arrangements
  rather than generating unreviewed narrative prose.
- Keep generation, pathfinding, visibility, and per-tick simulation bounded.
- Preserve stable identities and old saves when the generator or content
  revision changes.

The design takes inspiration from the broad appeal of large procedurally
generated strategy-game galaxies, varied system layouts, and connected travel
networks. Spelljammer uses its own topology rules, terminology, content,
factions, cosmology, and voyage-scale simulation.

Arcane navigation began with the elven discovery of open-space Aether. Dwarf
and gnome scientists later developed repeatable coupling methods for ships and
instruments. Aether currents and interference therefore create meaningful route
conditions, while Industrial vessels retain a separate propulsion path through
the same galaxy.

## Map hierarchy

| Layer | Responsibility |
| --- | --- |
| Galaxy | Campaign seed, shape, global bounds, generator version, and immutable topology |
| Region | Spatial partition used for generation, discovery summaries, factions, and bounded searches |
| Star system | A graph node containing one star or another primary celestial feature |
| Starway | A traversable graph edge between systems with distance, conditions, and hazards |
| Orbit | An ordered local band around a system primary used to place worlds, stations, and sites |
| Site | A visitable anchorage, world, moon, ruin, wreck, settlement, resource field, or anomaly |

The galaxy map is a graph. Display coordinates provide a stable layout, but
ships travel along Starways or through an explicitly unlocked special route;
screen-space distance alone never creates adjacency.

## Galaxy settings

Generation consumes a validated immutable settings record:

| Setting | Examples | Rule |
| --- | --- | --- |
| Size | Voyage, Small, Standard, Large, Grand | Selects a bounded system count and generation budget |
| Shape | Spiral, Ring, Clustered, Shattered, Open | Selects a topology strategy, not a background image |
| Starway density | Sparse, Normal, Dense | Adjusts bounded extra edges after connectivity is guaranteed |
| Hazard prevalence | Low, Normal, High | Adjusts eligible hazard weights, never mandatory-route lethality |
| Habitable-site prevalence | Scarce, Normal, Abundant | Adjusts viable settlement candidates |
| Faction presence | Sparse, Normal, Crowded | Adjusts initial faction count and territorial pressure |
| Ancient activity | Quiet, Normal, Awakened | Adjusts ruins, sealed routes, and ancient hazards |

Planned system-count presets are 64, 128, 256, 512, and 1,024. The maximum is
an authored limit rather than an unbounded player integer. The first complete
voyage uses only a 16-system test region; larger presets remain planned until
generation, map rendering, saves, and path searches meet their budgets.

## Deterministic generation pipeline

Given the same generator version, content revision, settings, and seed, the
pipeline produces the same immutable galaxy:

1. Validate settings and derive named random streams for topology, systems,
   sites, factions, hazards, and names.
2. Reserve the starting anchorage and any scenario-required authored systems.
3. Place bounded region centers and integer display coordinates according to
   the selected shape.
4. Place system nodes with stable ordinal IDs and minimum-separation rules.
5. Connect every system through a deterministic spanning graph.
6. Add bounded optional Starways to create loops, alternate routes, and
   controlled chokepoints.
7. Assign system primaries, orbit counts, environmental tags, and system seeds.
8. Place required services, habitable sites, resources, ruins, anomalies,
   hazards, and rare authored landmarks.
9. Select faction origins and grow initial territories through deterministic
   bounded graph expansion.
10. Calculate discovery hints and starting knowledge from scenario, crew, and
    faction context.
11. Validate reachability, path costs, content references, density bounds, and
    starting-region fairness before publishing the galaxy.

If validation fails, the generator advances to a bounded named retry stream.
It never loops until success. Exhausting the attempt limit returns an explicit
generation error and leaves the previous campaign or preview unchanged.

Random streams are derived independently from the campaign seed and stable
purpose IDs. Adding a new ruin table should not silently rearrange the entire
Starway graph. Generation uses defined integer or fixed-point operations where
platform floating-point differences could change ordering.

## Shapes and topology

Galaxy shapes bias placement and connection without dictating exact results:

| Shape | Topological character |
| --- | --- |
| Spiral | Several curved arms joined by a contested core and occasional cross-arm routes |
| Ring | Strong circular routes with dangerous or scarce crossings through the center |
| Clustered | Dense local groups connected by a few long inter-cluster Starways |
| Shattered | Irregular pockets, dead ends, broken ancient routes, and risky reconnection opportunities |
| Open | Evenly distributed systems with many possible loops and fewer forced chokepoints |

Every normal galaxy begins connected. Sparse settings may create bridges and
dead ends, but never an unreachable ordinary system. Sealed systems and
one-way special routes are explicit content exceptions and are excluded from
ordinary connectivity validation.

The starting anchorage receives at least two viable outward routes. At least
one return path must avoid any single optional encounter. Generation does not
guarantee that every route is safe, profitable, or currently affordable.

## Star systems and sites

A star-system summary is generated with the galaxy topology. Detailed sites
materialize deterministically when sensors, charts, or travel reveal them.
Possible content includes:

- single, binary, unstable, dead, or artificial stellar primaries;
- rocky, oceanic, gaseous, frozen, molten, shattered, or constructed worlds;
- moons, rings, asteroid families, crystal shoals, and comet trails;
- ports, free anchorages, faction stations, monasteries, shipyards, and markets;
- wrecks, derelicts, battle debris, salvage claims, and distress signals;
- ancient ruins, sealed vaults, abandoned gates, and script-bearing artifacts;
- aether storms, radiation zones, psychic echoes, gravity shear, and predators;
  and
- authored unique systems placed through explicit eligibility constraints.

System generation first selects compatible physical and supernatural tags,
then chooses authored definitions whose constraints match. It rejects
impossible combinations instead of silently ignoring their requirements.

Procedural placement never invents final quest prose. It selects localized
authored templates, parameters, factions, sites, and consequences. Unique
landmarks have explicit minimum distance, mutual exclusion, and maximum-count
rules.

When a site produces a hostile encounter, tactical layout and resolution follow
[`Battle.md`](Battle.md). Returning to exploration preserves opened routes,
disabled defenses, damage, hazards, recovered objects, and abandoned actors.

## Starways and travel

Each Starway stores stable endpoint IDs and authored or generated values for:

- route distance and base travel time;
- fuel, propellant, or Aether-charge demand;
- known and hidden hazard tags;
- stability, current direction, and seasonal state;
- minimum navigation or ship-capability requirements;
- faction control, tolls, blockades, and legal restrictions; and
- discovery state and confidence.

Arcane Flux Sails and Industrial drives traverse the same galaxy graph but
calculate route cost differently. Arcane ships may exploit strong aether
currents yet suffer interference. Dieselpunk and atompunk ships carry explicit
fuel or reaction mass and may tolerate magically dead routes more reliably.
Neither path receives exclusive access to all routes; special advantages have
alternate solutions or visible costs.

Route planning uses bounded graph search. The player can optimize for travel
time, fuel, known danger, tolls, secrecy, or a weighted custom policy. Unknown
values appear as uncertainty rather than being passed secretly to the planner.
Equal-cost choices use stable system IDs as deterministic tie-breakers.

Travel through a selected route creates bounded event opportunities at
committed progress points. Eligibility, seeded selection, player choices, and
persistent consequences follow [`Events.md`](Events.md). Opening the galaxy map
or waiting in a paused decision screen never rolls an event.

## Discovery and map knowledge

The authoritative galaxy exists independently from what a crew knows. Each
campaign records knowledge per system, Starway, and site:

| State | Player-visible meaning |
| --- | --- |
| Unknown | No map entry exists |
| Rumored | A possible system, route, or feature is known with uncertain identity or position |
| Detected | Sensors confirm existence and approximate properties |
| Surveyed | Major physical features, ordinary routes, and hazards are recorded |
| Charted | Navigation data and common sites are reliable enough for confident planning |

Individual facts also record source, confidence, observation tick, and whether
they are stale. A map can correctly show a system while being wrong about its
current faction, market, blockade, or hazard.

Piloting operates the ship, Astrogation plans routes, Sensors reveal distant
features, Language and Literacy interprets foreign charts, and Ancient Lore
recognizes obsolete routes and landmarks. Crew may share knowledge, sell maps,
compare conflicting sources, or keep discoveries private according to policy.

## Factions and changing space

Generation establishes faction origins, initial claims, known settlements,
and disputed frontiers. Territory follows the Starway graph rather than simple
circles around display coordinates. Placement observes minimum origin distance,
habitable-site needs, required neutral space, and bounded expansion budgets.

After campaign creation, faction control, wars, markets, stations, hazards, and
route access are mutable simulation state. They are not regenerated when a
save loads. The immutable generated baseline and subsequent committed events
remain separate so the UI can explain how the galaxy changed.

The home anchorage begins neutral or under an explicitly authored protector. A
starting faction cannot seal every exit, and required early services cannot be
placed exclusively behind hostile territory.

## Streaming and simulation bounds

Galaxy creation generates the complete node and edge graph plus lightweight
system summaries. Detailed sites, local maps, inhabitants, cargo inventories,
and encounters materialize from their stored system or site seed when first
needed. Once published, persistent changes are saved rather than regenerated.

Only active systems and explicitly scheduled remote processes receive detailed
simulation ticks. Other systems advance through bounded aggregate events at
documented commit boundaries. Opening the map, changing zoom, or rendering a
distant system never advances simulation or consumes gameplay randomness.

Generation and runtime limits include maximum systems, Starways per system,
sites per system, factions, landmarks, search expansions, generation attempts,
and visible map labels. Capacity failures return actionable errors or explicit
degradation behavior.

## Data and persistence

The immutable galaxy header resembles:

```json
{
  "schemaVersion": 1,
  "generatorVersion": 1,
  "seed": 731942,
  "settingsId": "galaxy.settings.voyage",
  "contentRevision": "content.prototype.1",
  "shapeId": "galaxy.shape.clustered",
  "systemCount": 16
}
```

Stable generated IDs use the campaign ID and a deterministic ordinal, such as
`system.731942.0007` and `starway.731942.0007.0012`. Presentation names are
generated from stable authored name-part IDs and localized independently; a
translated name never becomes simulation identity.

A released save stores the immutable topology needed to preserve its map,
alongside the seed, generator version, settings, content revision, discovery
records, and dynamic world changes. A newer generator creates new campaigns but
does not silently rewrite an existing galaxy. Any compaction or migration first
validates the complete replacement before publication.

Content loading rejects duplicate IDs, invalid weights, missing definitions,
unbounded tables, impossible placement constraints, invalid route costs,
orphaned Starways, and unreachable required systems. Save loading rejects or
migrates unknown generator versions and content revisions explicitly.

## First playable scope

The first complete voyage replaces the prototype grid with a 16-system
connected graph generated from one explicit seed and generator version. It
needs:

- one home anchorage and at least two initial outward Starways;
- two regions with a visible chokepoint and at least one alternate route;
- at least four system archetypes and six site types;
- three factions, one settlement, one neutral market, and one contested location;
- one ruin, one salvage site, one aether hazard, and one industrial hazard;
- four authored travel-event definitions, including a no-event result and no
  more than two events on one leg;
- Unknown, Detected, Surveyed, and Charted knowledge states;
- deterministic route planning by time, fuel, and known danger; and
- save/load preservation of topology, discoveries, and changed sites.

The slice succeeds when the same seed produces the same galaxy, limited
knowledge changes a meaningful route decision, and revisiting a system shows
committed consequences rather than a regenerated copy. Larger galaxies,
galactic conquest, simulated populations in every system, seamless planetary
maps, and unrestricted player-authored generation settings remain deferred.
