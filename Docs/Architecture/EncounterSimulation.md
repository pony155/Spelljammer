# Encounter simulation

## Status

Milestone 5 implements this boundary in `Spelljammer.Simulation.Encounters`.
It is headless: the WPF expedition shell does not yet create or render a
`VoyageWorld`.

## Authoritative time and publication

`VoyageWorld` advances at 20 fixed ticks per second and accepts at most eight
catch-up ticks per call. Ship tactical pause and a player Ready pause stop tick
advancement. Rendering cadence, UI event timing, and wall-clock duration are
not inputs. `VoyageWorldSnapshot` is immutable and is produced after the last
complete tick commit.

The world owns its seed, content fingerprint, tick, deterministic sequence,
bounded command queue, scheduled actions, Ready actors, command log, and event
log. Accepted commands are ordered by target tick, priority, issuer, sequence,
and command ID. A rejected command returns the same world instance. Accepted
cancellations remain visible in the command log.

## Action transaction

Scheduled actions pass through declare, validate, reserve, prepare, commit,
recover, and complete phases. Reservation records an intended resource cost
without mutating published state. Commit publishes costs, state changes, and
events together. Cancellation is legal before commit and cannot spend the
reservation. Bounded active effects and reactions expire on simulation ticks.

## Ship encounter

Ship state uses fixed-point continuous 2D position and velocity rather than a
combat grid. The first slice supports scan, course, thrust, turn, brake,
intercept, fire, ram, shield, defend, damage-control, signal, and retreat
orders. Contacts carry knowledge, firing-solution, observation-tick, and
witness state. Power allocation uses a caller-supplied bounded priority list
with stable module-ID tie breaks.

Loadout publication validates the frame's slot and cargo budgets, unique
mounts, technology-path compatibility, network-compatible weapon battery, and
ammunition resource. Damage commits shield absorption, armor mitigation, Hull
loss, selected-module condition, and persistent evidence atomically.

## Personal encounter

Personal boards compile bounded cells and directed or bidirectional links.
Cells carry zone, axial hex coordinate, capacity, cover, visibility,
atmosphere, gravity, and hazard data; links carry access and retreat rules.
Validation rejects duplicate placement, excess occupancy, disconnected
required space, illegal links, and missing retreat cells. Movement uses a
bounded deterministic breadth-first search.

Each actor has an independent Turn Meter and Action Point budget. Ready order
is player team first and then stable Actor ID. The first slice supports
movement, defense, reserved reactions, melee, ranged, Spell, psychic,
Engineering, Medicine, interaction, surrender, and retreat. Cleanup retains
injuries, prisoners, exploration changes, and damaged desired objects.

## Ownership boundary

Simulation state contains stable IDs and immutable values only. It has no WPF
controls, localized strings, filesystem paths, renderer handles, native
pointers, or wall-clock timestamps. `Spelljammer.Content` compiles authored
definitions and passes immutable definitions across this boundary; Simulation
does not reference the content loader.
