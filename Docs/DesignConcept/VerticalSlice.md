# First playable voyage

## Feature status

- [x] Windows application host and SpriteForge renderer prototype
- [x] Game-owned localization runtime and catalog compiler
- [x] Headless deterministic expedition state and typed command boundary
- [x] Seeded bounded sector chart with stable sector identities
- [x] Travel, finite salvage, hull damage, repair, supplies, and return loop
- [x] Prototype WPF command and status surface
- [x] Compile-only deterministic simulation contracts
- [ ] Fixed-tick host that separates committed simulation snapshots from input
- [ ] Data-authored sector, ship, resource, and encounter definitions
- [ ] Versioned procedural galaxy graph, Starways, and discovery-state contracts
- [ ] Seeded bounded travel events with choices, recurrence rules, and
  persistent consequences
- [ ] Three-faction standing, agreement, territory, and delayed-report slice
- [ ] Arcane and diesel-tier Industrial ship energy packages with distinct
  resource and failure rules
- [ ] Data-authored race, heritage, and character definitions
- [ ] Classless crew attributes, core skills, needs, injuries, positions, and inspectable duties
- [ ] Trained and innate Magic/Psionics access, authored spell and psychic
  technique catalogs, enchantment, melee, archery, alchemy, and crafting
  content definitions
- [ ] Language and Literacy skill, race-associated scripts, and ancient-lore
  discovery
- [ ] Tactical ship engagement connected to a boarding or ruin encounter
- [ ] Versioned save/load with validation and atomic publication
- [ ] Runtime localization bootstrap for all prototype UI text
- [ ] CI execution of headless deterministic scenario coverage

## Goal

The first complete slice should prove a voyage, not the breadth of the final
universe. A small crew leaves an anchorage aboard a vulnerable ship, chooses a
route through a compact generated region, acquires something valuable through
at least one risky encounter, responds to a ship or crew problem, and either
returns with a prize or is lost.

The checked-in prototype currently proves only the navigation/resource spine:
a 4 × 4 chart, deterministic hazards and salvage, consumable fuel and supplies,
hull repair, and a return threshold. It is playable scaffolding, not the full
vertical slice.

## Required slice loop

1. Start from an explicit seed with a known ship and crew manifest.
2. Inspect reachable destinations and their known costs or uncertainty.
3. Commit travel through the authoritative command boundary.
4. Resolve any seeded travel event through a player choice or safe continuation.
5. Resolve a location through exploration, salvage, negotiation, or combat.
6. Apply persistent costs and consequences to crew, ship, cargo, and factions.
7. Change plans in response to damage or dwindling supplies.
8. Return to the anchorage and atomically save the voyage result.

## Required boundaries

- `Spelljammer.Simulation` owns authoritative state and has no dependency on
  WPF, renderer handles, native pointers, wall-clock time, or localized strings.
- The host translates player intent into typed commands and presents committed
  snapshots rather than directly mutating state.
- Race and Heritage definitions grant validated Racial Perk IDs.
- Generated results are pure functions of versioned definitions, seed, and
  command history.
- Commands have stable, inspectable rejection reasons and never partially
  mutate state.
- Save data uses versioned stable IDs and validates completely before replacing
  an active campaign.
- Collections, searches, queues, and per-step work have explicit bounds.

## Explicitly deferred

An open-ended galaxy, endgame crises, multiplayer, seamless planets, large
fleets, procedural narrative prose, broad mod support, and production-scale
content are not required for the first playable voyage.
