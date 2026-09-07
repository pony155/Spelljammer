# Ships and modules

## Status

Milestone 5 implements the first headless modular-ship slice: a Wayfarer frame,
Arcane and Industrial energy packages, common fittings, configurable cannons,
bounded power priority, shields, armor, Hull, module damage, and transactional
loadout validation. The WPF expedition shell still uses its aggregate voyage
values; compartments, refit UI, save serialization, and the broader module
catalog remain planned.
Ship engagements, boarding transitions, and shipboard combat are defined in
[`Battle.md`](Battle.md).
The wider Arcane-Industrial setting baseline is defined in
[`History.md`](History.md).

Arcane ships exist because their drives couple to the Aether discovered in open
space. Elven resonance vessels were the first documented magical spacecraft;
dwarf and gnome coupling science later made their power systems more measurable
and repeatable. Industrial propulsion became a parallel route to spaceflight,
rather than a replacement.

## Design goals

- Make the ship a persistent home whose layout, capabilities, damage, and scars
  record the campaign.
- Make every installed module create one clear choice about slots, energy,
  cargo, combat, or voyage capability rather than simulate machinery in detail.
- Support two viable upgrade paths—Arcane and Industrial—without making either
  a universal best choice.
- Let crew attributes, skills, and assignments affect module operation
  without creating classes or class-locked stations.
- Make failures local, inspectable, and simple to repair or work around.
- Keep definitions data-driven and use stable IDs for saves, scenarios,
  balancing, and eventual modding.
- Resolve ship engagements in real time with tactical pause: pausing allows
  inspection and order entry, while station readiness, crew workload, module
  timing, and resources determine execution after simulation resumes.
- Place ships on a bounded continuous 2D combat map using deterministic
  fixed-point position, heading, and velocity; tactical cells belong only to
  personal combat.

## Ship composition

A ship consists of distinct persistent layers:

| Layer | Responsibility |
| --- | --- |
| Frame | Exterior shape, Hull Value, module slots, weapon slots, armor slots, and cargo capacity |
| Compartments | Interior spaces, access paths, atmosphere zones, hazards, and room capacity |
| Modules | Installed capabilities such as propulsion, life support, workshops, weapons, armor, energy shields, and wards |
| Networks | Simple Power, Aether, supplies, and atmosphere availability checks |
| Cargo | Bounded stored goods that are not permanently installed |
| Crew stations | Places where assigned duties operate or assist modules |
| Ship state | Hull, armor, shield, energy, cargo, module condition, modifications, and history |

A frame defines named interior, propulsion, prow, and weapon slots plus a
single Armor Value. A room is navigable ship space. A module occupies one or
more slots; cargo remains cargo until a validated installation command commits
it. Exact mass distribution, structural-load arithmetic, and compartment-level
network routing are deferred unless a later feature creates a player-visible
choice that cannot be expressed with slots and tags.

## Simple-stat rule

Ships and modules favor readable choices over engineering simulation. The
standard ship HUD shows only Hull Value, Armor Value, current Shield Value,
available energy, cargo capacity, and occupied slots. A normal module has at
most four visible numeric fields:

1. Slot Cost;
2. Integrity;
3. one energy value (output, consumption, or storage); and
4. one primary effect value where needed.

Use tags and discrete states rather than extra numbers. A module is Intact,
Damaged, or Disabled; its effect either works, works at a declared reduced
level, or does not work. The deliberate exceptions are Energy Shields, with
their three documented values, and ship cannons, with the combat fields the
player explicitly inspects. Heat, noise, signature, vibration, exact plumbing,
and detailed wear are not core ship statistics in the first playable game.

## Module contract

Every module definition declares:

- a stable definition ID and localized presentation keys;
- Slot Cost and compatible slot tags;
- at most one energy input, output, or storage value;
- one primary effect and its tags;
- Integrity plus simple Damaged and Disabled behavior; and
- path compatibility, resource type, and any unique-installation limit.

Armor modules additionally declare Armor Value and Slot Cost. Ship cannon
configurations declare firing arc, damage,
rate of fire, effective and maximum range, reload time, damage type, damage
area, armor penetration, and a resource path. An Arcane cannon consumes Aether
charge; an Industrial cannon consumes physical ammunition. Cannons do not
declare recoil or require a Gunner, crew station, or Gunnery Skill check.
Energy-shield modules declare exactly three combat statistics: maximum Shield
Value, Recharge Rate per fixed tick, and Energy Consumption Rate per fixed
tick. An Energy Shield is distinct from a Ward Projector: shields intercept
ordinary ship attacks, while wards counter declared magical, psychic, and
environmental effects.
Propulsion modules declare Speed and Energy Consumption. Power modules declare
Energy Output or Storage. Prow modules declare one special effect such as Ram
or Figurehead. Additional simulation values require an explicit design review.

Module definitions use IDs in the form `module.<category>.<name>`. Installed
instances receive separate stable IDs so two cargo holds can share one
definition while retaining different damage, contents, and histories.

## Ship upgrade paths

Every ship frame supports two primary upgrade paths. The selected path defines
the ship's main energy backbone, core propulsion choices, upgrade tree,
maintenance economy, and characteristic failures. It does not determine the
crew's culture, morality, faction, or available character skills.

| Upgrade path | Stable ID | Energy and progression | Strengths | Costs and risks |
| --- | --- | --- | --- | --- |
| Arcane | `ship.path.arcane` | Ambient Aether, resonator crystals, runic conduits, and enchantments | Flexible energy use, wards, and energy weapons | Aether interference and scarce charge sources |
| Industrial | `ship.path.industrial` | Dieselpunk machinery advancing into atompunk reactors and drives | Reliable power, armor, and physical ammunition | Fuel or advanced-ammunition supply and larger slot cost |

### Arcane progression

Arcane is the ship's magical discipline; Aether is the non-material medium it
couples to through resonators, then captures as charge and routes as energy.
Elven resonance craft supplies starwood fittings, tuned hull components, and
long-lived navigation instruments for this path. Early upgrades stabilize an
Aether Dynamo and Flux Sail. Later upgrades improve
crystal storage, runic distribution, remote control, ward efficiency, and
enchantment capacity. Magic and Enchantment provide the main specialist work,
while Engineering remains important for the physical housings, controls, and
connections.

Arcane failures are represented by a damaged generator, empty stored charge, or
aether interference. Arcane energy is not limitless: crystals, reagents, and
stored charge remain bounded resources.
Arcane weapons use that stored charge through Aether Energy Cannons rather than
physical shells.

### Industrial progression

The Industrial path begins with dieselpunk compression engines, generators,
pumps, flywheels, analog controls, and high-pressure propellant systems. Later
upgrades introduce atompunk reactors, shielded turbine halls, radiothermal
generators, and nuclear-thermal propulsion. Engineering, Crafting, and Rigging
provide its main specialist work.

Dieselpunk failures are represented by a damaged generator, depleted fuel, or a
disabled drive. Fuel includes any required oxidizer without tracking a separate
combustion simulation.

Atompunk systems replace much of that fuel demand with long-lived reactor fuel
and higher Energy Output. Their failure state is a disabled reactor requiring
an anchorage repair or a specific recovery event; coolant, decay heat, and
radiation are narrative or event tags rather than continuous ship statistics.

Industrial weapons remain ammunition weapons at both tiers. Dieselpunk ships
use conventional diesel-shell cannon ammunition; atompunk ships use sealed
atomic-shell ammunition with advanced penetrators or shaped charges. Atomic
shells are not automatically nuclear warheads: nuclear payloads, if added,
must be separate high-consequence content with explicit safety and political
rules.

### Commitment and hybrid ships

A ship has one primary path at a time. Core generators, backbone upgrades, and
top-tier propulsion from the two paths are mutually exclusive while installed.
Changing paths is possible, but it is a major shipyard refit rather than a free
respec.

Common habitat, cargo, command, and work modules can be built for either
backbone. An off-path module requires a compatible converter and one extra Slot
Cost. Hybrid ships are deliberate specialist builds, but cannot collect both
paths' strongest benefits without a visible slot tradeoff.

## Player ship customization

A ship loadout is a validated composition, not a linear equipment score. The
player can replace, relocate, orient, configure, repair, or remove installed
modules when an appropriate facility and resources are available.
Configuration selects only options declared by the module definition or an
installed upgrade; it never permits arbitrary editing of combat statistics.

| System | Mount and choices | Important consequences |
| --- | --- | --- |
| Armor | Spend armor slots on plating or an armor upgrade | Armor Value and slot use |
| Energy shield | Install one shield module and connect a compatible energy feed | Shield Value, Recharge Rate, and Energy Consumption Rate |
| Power | Choose an Arcane or Industrial core, optional storage, and converters | Energy Output, storage, and resource type |
| Propulsion | Fit one path-compatible drive | Speed and Energy Consumption |
| Prow | Install a ram, figurehead, sensor fitting, boarding device, or leave the slot clear | One declared special effect |
| Weapons | Fit a Deck Battery to a hardpoint and select an Arcane or Industrial cannon configuration | Shared cannon statistics plus either Aether charge demand or physical-ammunition logistics |
| Support | Configure cargo, habitat, medical, workshop, sensor, ward, and utility modules | Voyage endurance, recovery options, information, salvage, spare capacity, and survivability |

Installed modules have persistent instance IDs. Two ships can use the same
module definition in different locations, and two instances on one ship can
have different slot placement, ammunition, Integrity, upgrades, and repair
history. Cosmetic paint and an unpowered figurehead carving do not alter
gameplay fingerprints; a figurehead enchantment, ward, sensor, or morale effect
must be an explicit gameplay definition.

The refit preview reports Slot Cost, cargo displacement, incompatible energy
type, missing ammunition, and blocked weapon or prow slots before the player
confirms. Installation is transactional: all removals, relocations, costs, and
installed instances commit together or the original ship remains unchanged.

## Planned module catalog

### Command and navigation

| Module | Stable ID | Function | Typical skill or position |
| --- | --- | --- | --- |
| Helm | `module.command.helm` | Commits maneuver and travel commands and exposes current ship-control status | Piloting; Pilot or Captain |
| Star Compass | `module.navigation.star-compass` | Supports route plotting, position fixes, and anomaly-aware astrogation | Astrogation; Navigator |
| Sensor Mast | `module.navigation.sensor-mast` | Detects contacts, hazards, emissions, and survey targets | Sensors; Navigator or Scout |
| Chart Archive | `module.navigation.chart-archive` | Stores verified routes, survey records, scripts, and discovered lore | Astrogation, Language and Literacy, or Ancient Lore; Cartographer |

### Propulsion and power

| Module | Stable ID | Path | Function | Typical skill or position |
| --- | --- | --- | --- | --- |
| Flux Sail | `module.propulsion.flux-sail` | Arcane | Converts aether charge and stellar currents into voyage movement | Piloting or Rigging; Pilot or Rigger |
| Aether Dynamo | `module.power.aether-dynamo` | Arcane | Couples to ambient Aether through resonator crystals to create ship power and Aether charge | Magic, Enchantment, or Engineering; Ship Mage or Engineer |
| Crystal Accumulator | `module.power.crystal-accumulator` | Arcane | Stores bounded aether charge for peak demand and emergency operation | Enchantment; Artificer |
| Runic Distributor | `module.power.runic-distributor` | Arcane | Routes and prioritizes aether while isolating unstable branches | Enchantment or Engineering; Artificer |
| Diesel Generator | `module.power.diesel-generator` | Industrial—Dieselpunk | Produces Power from fuel | Engineering; Engineer |
| Propellant Drive | `module.propulsion.propellant-drive` | Industrial—Dieselpunk | Produces sustained thrust by consuming power and stored propellant | Piloting or Engineering; Pilot or Engineer |
| Flywheel Bank | `module.power.flywheel-bank` | Industrial—Dieselpunk | Stores bounded mechanical energy for peak demand and emergency operation | Engineering or Crafting; Engineer |
| Atomic Reactor | `module.power.atomic-reactor` | Industrial—Atompunk | Provides high Power from reactor fuel | Engineering; Chief Engineer |
| Nuclear-Thermal Drive | `module.propulsion.nuclear-thermal-drive` | Industrial—Atompunk | Provides high Speed by consuming reactor power and reaction mass | Piloting or Engineering; Pilot or Chief Engineer |
| Vector Vanes | `module.propulsion.vector-vanes` | Either | Provides docking, evasion, and close maneuver control through a path-compatible drive | Piloting or Engineering; Pilot |

### Habitat and care

| Module | Stable ID | Function | Typical skill or position |
| --- | --- | --- | --- |
| Atmosphere Recycler | `module.habitat.atmosphere-recycler` | Maintains breathable atmosphere and filters smoke, spores, and toxins | Engineering or Xenology; Engineer |
| Crew Quarters | `module.habitat.crew-quarters` | Provides bounded bunks, privacy, rest quality, and environmental tuning | Steward |
| Galley | `module.habitat.galley` | Turns provisions into safe meals for compatible diets | Cooking; Chef |
| Sickbay | `module.habitat.sickbay` | Supports diagnosis, surgery, quarantine, recovery, and medical storage | Medicine; Doctor or Medic |
| Common Room | `module.habitat.common-room` | Supports recreation, meetings, belonging, and conflict mediation | Insight or Negotiation; Steward or First Mate |
| Provision Locker | `module.habitat.provision-locker` | Preserves food, medicine, and other tagged consumables | Cooking, Medicine, or Alchemy |

### Work, research, and cargo

| Module | Stable ID | Function | Typical skill or position |
| --- | --- | --- | --- |
| Cargo Hold | `module.cargo.hold` | Stores bounded cargo by mass, volume, hazard, and environment tags | Merchant; Quartermaster or Trader |
| Workshop | `module.workshop.fabricator` | Repairs and fabricates tools, fittings, ammunition, and replacement parts | Crafting or Engineering; Artificer |
| Alchemy Laboratory | `module.workshop.alchemy-lab` | Identifies reagents and produces medicines, compounds, and volatile mixtures | Alchemy; Alchemist |
| Enchanting Chamber | `module.workshop.enchanting-chamber` | Creates and maintains persistent magical bindings under controlled conditions | Enchantment; Artificer or Ship Mage |
| Lore Vault | `module.research.lore-vault` | Preserves artifacts, inscriptions, translations, and authenticated discoveries | Ancient Lore or Language and Literacy; Antiquarian |
| Salvage Rig | `module.utility.salvage-rig` | Recovers cargo and wreck material without bringing every hazard aboard | Salvage or Rigging; Salvager |

### Defense, prow, weapons, and contact

| Module | Stable ID | Function | Typical skill or position |
| --- | --- | --- | --- |
| Reinforced Plating | `module.defense.reinforced-plating` | Adds Armor Value by using armor slots | Engineering |
| Energy Shield | `module.defense.energy-shield` | Uses power or aether to provide a whole-ship Shield Value that absorbs damage and replenishes at its Recharge Rate | No dedicated Skill or crew position requirement |
| Ward Projector | `module.defense.ward-projector` | Sustains a bounded defense against magical, psychic, or environmental threats | Magic, Psionics, or Enchantment; Warden |
| Prow Ram | `module.prow.ram` | Reinforces a compatible prow for deliberate collision attacks while transmitting impact risk into the frame | Piloting or Engineering; Pilot |
| Ship Figurehead | `module.prow.figurehead` | Provides a customizable prow fitting that can host declared enchantments, wards, sensors, or command effects | Crafting or Enchantment; Artificer |
| Deck Battery | `module.weapon.deck-battery` | Mounts one configured Aether Energy, Diesel Shell, or Atomic Shell Cannon with declared damage, rate of fire, range, reload, damage type and area, armor penetration, arc, and resource path | No Skill or crew position requirement |
| Boarding Lock | `module.contact.boarding-lock` | Controls docking, boarding, quarantine seals, and ship-to-ship access | Engineering or Defense; Master-at-Arms |
| Signal Lantern | `module.contact.signal-lantern` | Sends identification, negotiation, warning, and distress signals | Language and Literacy or Negotiation; Envoy |
| Psychic Resonator | `module.contact.psychic-resonator` | Amplifies permitted mindlinks and detects nearby psychic signaling | Psionics; Mindwarden or Envoy |

The catalog is a design vocabulary, not a promise that every module belongs in
the first playable voyage. New definitions should create new system
interactions rather than duplicate an existing module with larger numbers.

### Ship cannon families

Each Deck Battery selects exactly one compatible cannon configuration. All
three families use the shared ship-cannon fields; their resource source and
failure consequences are different.

| Cannon family | Stable ID | Resource path | Role and risk |
| --- | --- | --- | --- |
| Aether Energy Cannon | `ship.weapon.arcane.aether-cannon` | Aether network and available Aether charge | A magical energy cannon. It needs no physical ammunition, but it cannot fire when its charge request is unmet and is vulnerable to aether interference. |
| Diesel Shell Cannon | `ship.weapon.industrial.diesel-shell-cannon` | Logistics path from a magazine of physical shells | A dieselpunk cannon using manufacturable shells and propellant. It remains usable during Aether disruption, but spends ammunition. |
| Atomic Shell Cannon | `ship.weapon.industrial.atomic-shell-cannon` | Logistics path from sealed advanced shells; optional Industrial power for its loader | An atompunk ammunition cannon using high-density penetrators or shaped-charge shells. Its advanced ammunition is costly but has no extra subsystem to manage. |

An Aether Energy Cannon's `reloadTime` is its charge-cycle time. An Industrial
cannon's `reloadTime` is its shell-handling cycle. Neither changes the common
combat fields or introduces a recoil statistic, a Gunner post, or a Gunnery
Skill requirement.

## Networks and allocation

Modules use a small set of resource checks rather than a simulated pipe graph:

| Network | Carries | Example failure |
| --- | --- | --- |
| Power | Industrial energy | An active module is unpowered for the tick |
| Aether | Arcane energy and magical charge | An Arcane module cannot use its effect or fire |
| Supplies | Fuel, shells, spare parts, and provisions | A drive, cannon, or repair has no required supply |
| Atmosphere | Air and pressure for crew spaces | A compartment is unsafe for crew |

Arcane backbones provide Aether; Industrial backbones provide Power. A module
declares which it accepts. A converter occupies one extra slot and lets an
off-path module operate, so an Industrial ship cannot power a ward for free and
an Arcane ship cannot operate heavy machinery without an enchanted variant.

The player assigns a simple priority order rather than directly distributing
every unit each tick. The authoritative simulation powers modules in that
stable order until Energy Output is spent, then records which module did not
receive energy. Allocation and module updates occur on the fixed simulation
tick and never depend on UI or rendering cadence.

When raised, an Energy Shield requests its fixed Energy Consumption Rate every
simulation tick. A fully supplied shield protects the entire ship and restores
current Shield Value by its Recharge Rate, up to its maximum. Replenishment
starts on the next fully powered tick after damage. If the request is not fully
supplied, the shield provides no protection or recharge for that tick. Lowering
it stops consumption but also stops protection and recharge. An Arcane variant
accepts Aether, an Industrial variant accepts Power, and a hybrid installation
needs an explicit compatible converter.

## Operation and crew

Each installed module is On or Off and is Intact, Damaged, or Disabled. A
module becomes operational when it has its required energy or supply and is
not Disabled. There is no separate standby state, detailed connection damage,
or cascading-fault simulation in the first playable game.

Crew positions provide responsibility and authority, while duties describe the
actual work performed at a station. Skills and contextual attributes determine
performance. A module may be operated by an unconventional crew member with
visible penalties unless a physical, safety, or policy requirement prevents
it. Automation can replace some labor but has explicit capacity and failure
rules.

Examples include:

- Intelligence plus Engineering to diagnose a dynamo fault;
- Strength plus Rigging to deploy a jammed Flux Sail;
- Toughness plus Engineering to seal an atomic coolant leak;
- Intelligence plus Crafting to calibrate a diesel injection system;
- Toughness plus Salvage to continue an exterior recovery under strain;
- Willpower plus Enchantment to stabilize a failing Ward Projector;
- Intelligence plus Merchant to arrange cargo for inspection and sale; and
- Charisma plus Negotiation to transmit acceptable docking terms through the
  Signal Lantern.

## Damage, repair, and refit

Damage targets the Hull or a visible module. It reduces Integrity and may make
the target Damaged or Disabled. Named faults, individual leaking pipes, and
cascading failures are deferred until they add a distinct player decision.

Emergency work can restore an Intact or Damaged module to its previous state by
spending the declared repair resource and time. The UI shows that cost and the
result before the repair command is committed.

Installation, removal, and replacement are transactional. The simulation
validates available slots, cargo displacement, energy type, Shield Energy
Consumption, cannon resource type, and unique limits before changing the
working configuration. Failed validation leaves the previous ship intact.
Major refits normally require an anchorage or shipyard; explicitly tagged field
modules may be swapped during a voyage.

## Data and persistence

An authored module definition resembles:

```json
{
  "schemaVersion": 1,
  "id": "module.habitat.sickbay",
  "nameKey": "ship.module.habitat.sickbay.name",
  "slotCost": 2,
  "mountTags": ["interior", "pressurized"],
  "compatiblePathIds": ["ship.path.arcane", "ship.path.industrial"],
  "recommendedSkillIds": ["skill.medicine"],
  "energyConsumption": 2,
  "capabilityTags": ["treatment", "surgery", "quarantine"]
}
```

Energy Shield definitions add only these combat fields:

| Field | Meaning |
| --- | --- |
| `shieldValue` | Positive maximum Shield Value for the installed module |
| `rechargeRate` | Shield Value restored per fully powered fixed tick, capped at the maximum |
| `energyConsumptionRate` | Power or Aether consumed per fixed tick while the shield is raised |

All three values use bounded integer units. Current Shield Value and raised or
lowered state belong to the installed module instance; the three authored
statistics remain on its definition.

Persistent ship state stores primary path ID and revision, Hull Value, Armor
Value, cargo, occupied slots, and available energy.

Persistent module state stores instance ID, definition ID and revision, slot,
Integrity, On or Off state, contents, installed upgrades, current Shield Value
where applicable, and bounded history references. Saves never store a localized
path or module name as identity.

Definition loading rejects duplicate or missing IDs, unknown path references,
invalid Slot Cost, negative energy values, invalid Shield Value, Recharge Rate,
or Energy Consumption Rate, incompatible path or resource tags, and unknown
tags before publishing a replacement catalog.

## First playable ship scope

The first complete crew-enabled voyage uses one authored frame, a shared set of
modules, and a choice between two fixed energy packages:

| Module | Slice purpose |
| --- | --- |
| Helm and Star Compass | Travel commands and inspectable route checks |
| Atmosphere Recycler and Crew Quarters | Crew survival and rest |
| Galley and Provision Locker | Meals, supplies, and varied diets |
| Sickbay | Injury treatment and the Doctor position |
| Cargo Hold and Salvage Rig | Bounded recovery and cargo capacity |
| Workshop | Engineering repair and replacement parts |
| Signal Lantern | Contact, negotiation, warnings, and distress calls |
| Reinforced Plating and Energy Shield | Passive armor behind a powered, depleting, and recharging defense layer |
| Deck Battery | One configurable cannon mount using Aether charge or physical ammunition |
| Prow Ram or Ship Figurehead | One visible loadout choice with a mechanical or customizable identity tradeoff |

| Energy package | Starting modules | Slice tradeoff |
| --- | --- | --- |
| Arcane | Flux Sail, Aether Dynamo, Crystal Accumulator, and Ward Projector | Flexible Aether use against scarce charge and interference |
| Industrial | Propellant Drive, Diesel Generator, and Flywheel Bank | Reliable Power against fuel use and larger slot cost |

The player selects one energy package, one armor arrangement, one Energy Shield
module, one prow fitting, and one cannon configuration before departure. The
slice needs shield depletion and replenishment, damage overflowing into armor,
one Aether-charge or physical-ammunition decision, one damaged or disabled
module, one crew-operated repair, and one choice where cargo, protection,
firepower, or maneuverability competes for limited capacity. Damage must expose
the affected module and repair path. Full hybrid construction,
changing paths during a voyage, atompunk Industrial upgrades, multiple frames,
structural frame editing, large equipment catalogs, module manufacturing, and
unrestricted shipyards remain deferred until that loop is deterministic and
readable.
