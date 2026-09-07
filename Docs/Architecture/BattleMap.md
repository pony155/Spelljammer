# Combat Map & Turn-Based Combat Design

## 1. Design Goal

The combat system uses a **continuous-looking procedural map with a
hidden square grid**.

The player should experience the environment as a continuous RPG space
rather than an obvious board game. Internally, however, movement,
pathfinding, occupancy, action costs, AI navigation, and procedural
generation remain grid-based.

Core concept:

``` text
Procedural Map
      ↓
Hidden Square Grid
      ↓
Continuous Visual Layer
      ↓
Real-Time Exploration
      ↓ Encounter
Turn-Based Tactical Combat
      ↓
Real-Time Exploration
```

This approach combines:

-   RimWorld-style grid-backed map construction
-   Fallout-style continuous environmental presentation
-   Battle Brothers-style character-focused tactical combat
-   Procedural ship interiors, ruins, stations, and wasteland
    battlefields

------------------------------------------------------------------------

## 2. Core Principle: Logic Grid, Continuous Presentation

The logical map is a square grid.

Example:

``` text
□ □ □ □ □ □
□ □ ■ ■ □ □
□ □ ■ ■ □ □
□ □ □ □ □ □
```

Each cell stores gameplay data. The visual layer converts those cells
into floors, walls, corridors, debris, machinery, vegetation, ship
components, and other environmental assets.

The player normally does **not** see permanent grid lines.

### Suggested Cell Data

``` text
Cell
 ├─ Terrain
 ├─ Elevation
 ├─ Walkable
 ├─ Occupant
 ├─ MovementCost
 ├─ Cover
 ├─ Hazard
 ├─ RoomType
 ├─ FactionTheme
 └─ VisualVariant
```

Suggested initial scale:

``` text
1 cell ≈ 1 meter
```

The exact scale can be adjusted during prototyping.

------------------------------------------------------------------------

## 3. Map Architecture

Recommended map structure:

``` text
Map
 ├─ Cell[,]
 ├─ TerrainGrid
 ├─ OccupancyGrid
 ├─ CoverGrid
 ├─ HeightGrid
 ├─ HazardGrid
 ├─ RoomGrid
 └─ VisualChunks
```

Gameplay systems read the logical grids.

Rendering systems read the same data but are responsible only for
presentation.

This separation is important because visual decorations should not
unexpectedly change combat rules.

------------------------------------------------------------------------

## 4. Procedural Generation Pipeline

Recommended generation pipeline:

``` text
Seed
 ↓
Mission Type
 ↓
Map Topology
 ↓
Room / Zone Generation
 ↓
Grid Generation
 ↓
Connectivity Validation
 ↓
Doors / Walls / Obstacles
 ↓
Cover & Hazards
 ↓
Enemy / Player Spawn Areas
 ↓
Faction Theme
 ↓
Visual Reconstruction
 ↓
Decoration Pass
 ↓
Lighting
 ↓
Pathfinding / LOS Cache
 ↓
Playable Map
```

Connectivity validation must happen before visual decoration.

The generator should guarantee that required objectives and deployment
areas are reachable.

------------------------------------------------------------------------

## 5. Continuous Visual Reconstruction

The grid should not look like a collection of square tiles.

Adjacent cells are interpreted to select appropriate geometry or
sprites.

Example wall adjacency:

``` text
N
|
W—X—E
|
S
```

Possible visual states include:

``` text
isolated
straight
corner
T-junction
intersection
end-cap
door connection
damaged connection
```

Possible active Feats:

-   Marching Squares
-   Neighbor-aware prefab selection
-   Terrain edge blending
-   Random visual variants
-   Chunked terrain meshes

Example:

``` text
Logical Grid

■■■■■■
■....■
■....■
■■..■■

        ↓

Visual Result

╔════╗
║    ║
║    ║
╚═╗ ╔╝
  ╚═╝
```

The logical geometry remains unchanged.

------------------------------------------------------------------------

## 6. Visual Chunks

Do not create one independent heavy GameObject for every terrain cell if
it can be avoided.

Divide the visual map into chunks.

Example:

``` text
Map
 ├─ Chunk
 ├─ Chunk
 ├─ Chunk
 └─ Chunk
```

When terrain changes, rebuild only affected chunks.

This supports future systems such as:

-   destructible walls
-   damaged floors
-   explosions
-   fires
-   hull breaches
-   decompression
-   spreading hazards

------------------------------------------------------------------------

## 7. Exploration Mode

Exploration occurs in real time.

During normal exploration:

-   no permanent grid is displayed
-   characters move with smooth animation
-   the player explores the existing procedural environment
-   doors, containers, terminals, hazards, and environmental objects
    remain interactive

Example:

``` text
Real-Time Exploration
        ↓
Enemy Detected
        ↓
Combat Transition
```

Combat should preferably occur directly on the exploration map.

Avoid loading a separate generic battle arena.

------------------------------------------------------------------------

## 8. Combat Transition

When combat begins:

``` text
Enemy Detected
      ↓
Freeze / Combat Transition
      ↓
Determine Participants
      ↓
Roll / Calculate Initiative
      ↓
Turn-Based Combat
```

When combat ends:

``` text
Victory / Combat End
      ↓
Remove Tactical UI
      ↓
Resume Real-Time Exploration
```

The environment remains persistent between exploration and combat.

A door closed before combat remains closed. Destroyed cover remains
destroyed after combat.

------------------------------------------------------------------------

## 9. Turn Structure

Use **individual initiative**, rather than strictly alternating entire
teams.

Example:

``` text
Round 1

1. Elf Scout
2. Neogi Raider
3. Human Marine
4. Dwarf Gunner
5. Umber Hulk
6. Human Medic
```

Initiative may be calculated from:

``` text
Initiative
=
Base Attribute
+ Skills
+ Equipment Modifier
+ Status Modifier
```

A `Wait` action can allow a character to delay its activation and create
tactical combinations.

------------------------------------------------------------------------

## 10. Action Points

Characters use a small Action Point pool.

Initial prototype target:

``` text
AP = 6
```

Example costs:

``` text
Move 1 cell       1 AP
Rifle Shot        3 AP
Aim               2 AP
Reload            3 AP
Melee Attack      3 AP
Grenade           4 AP
Overwatch         3 AP
Use Item          2 AP
```

These values are placeholders and should be balanced through
playtesting.

The AP system should remain simple enough that turns resolve quickly.

------------------------------------------------------------------------

## 11. Movement

Movement uses the hidden grid.

Example logical path:

``` text
(12,8)
  ↓
(13,8)
  ↓
(14,8)
  ↓
(14,9)
  ↓
(14,10)
```

The player does not need to see these cells.

Instead, show a continuous movement path:

``` text
Character ● ──────────────╮
                         │
                         ╰──── ◎ Destination

Move: 3 AP
Distance: 12 m
```

Character animation interpolates smoothly between logical positions.

Therefore:

``` text
Gameplay Position = Grid Cell
Visual Position   = World Position
```

------------------------------------------------------------------------

## 12. Tactical Grid Visibility

The grid is normally hidden.

### Exploration

``` text
Grid Visibility = Off
```

### Combat Idle State

Prefer:

``` text
Grid Visibility = Off
```

### Movement Selection

Display only useful tactical information:

-   reachable area
-   destination
-   movement path
-   AP cost
-   dangerous cells
-   cover indicators

Example:

``` text
      · · ·
    · · · · ·
  · · · ● · · ·
    · · · · ·
      · · ·
```

This preserves RPG immersion while maintaining tactical clarity.

------------------------------------------------------------------------

## 13. Cover

Cover is stored in gameplay data rather than inferred exclusively from
visual appearance.

Initial cover categories:

``` text
None
Half Cover
Full Cover
```

Example placeholder modifiers:

``` text
None          +0 Defense
Half Cover   +25 Defense
Full Cover   +50 Defense
```

Exact numbers require balancing.

Cover information can be shown using icons when the player previews a
destination or attack.

Example:

``` text
Full Cover
Hit Chance: 42%
```

------------------------------------------------------------------------

## 14. Line of Sight

Use a hybrid approach.

### Movement

``` text
Grid-based
```

### Line of Sight

Prefer world-space raycasts or a hybrid grid/raycast system.

This allows walls, doorways, large machinery, cargo containers, and
unusual ship architecture to produce intuitive LOS behavior.

Combat calculations remain deterministic even if projectile animation is
continuous.

------------------------------------------------------------------------

## 15. Ranged Combat

Weapon attacks use RPG/statistical calculations.

Example:

``` text
Hit Chance
=
Weapon Skill
+ Weapon Accuracy
+ Aim Bonus
- Range Penalty
- Cover Penalty
- Target Modifier
- Status Effects
```

The visible projectile is primarily presentation.

Gameplay resolves the attack first; animation communicates the result.

This prevents physics simulation from controlling tactical outcomes.

------------------------------------------------------------------------

## 16. Melee Combat

Melee attacks use grid adjacency or weapon reach.

Examples:

``` text
Knife / Sword
Reach = 1 cell

Spear / Polearm
Reach = 2 cells
```

Special weapons may modify engagement rules.

Possible future mechanics:

-   attacks of opportunity
-   engagement zones
-   charge attacks
-   knockback
-   stun
-   shield blocking
-   melee overwatch / guard stance

------------------------------------------------------------------------

## 17. Large Creatures

Large units use logical footprints.

Example:

``` text
Human
1×1

Umber Hulk
2×2

Large Construct
3×3

Dragon / Major Boss
4×6
```

The visual model is not required to fit perfectly inside the footprint.

The footprint exists for:

-   pathfinding
-   occupancy
-   melee reach
-   collision rules
-   deployment
-   area attacks

This allows large monsters to appear visually imposing without
abandoning the grid system.

------------------------------------------------------------------------

## 18. Environmental Interaction

Because combat occurs on the exploration map, environmental preparation
can become tactical gameplay.

Examples:

-   close a bulkhead before enemies arrive
-   open a door to create a firing lane
-   position behind cargo
-   destroy cover
-   trigger machinery
-   ignite fuel
-   disable lighting
-   breach a hull
-   release gas
-   create decompression
-   block corridors

This is especially important for ship boarding and derelict exploration.

------------------------------------------------------------------------

## 19. Decoration Rule

Decorative objects should not affect gameplay unless explicitly promoted
to gameplay objects.

Examples of purely visual decoration:

-   shell casings
-   cables
-   papers
-   bones
-   small rubble
-   stains
-   minor pipes
-   tiny machine parts

Rule:

``` text
Decoration ≠ Gameplay Collision
```

Gameplay-relevant objects must be clearly represented in logical map
data.

This reduces procedural-generation bugs and prevents ambiguous cover or
pathfinding.

------------------------------------------------------------------------

## 20. Faction Visual Themes

The same logical generator can support multiple civilizations by
applying different visual themes.

### Human

``` text
Steel
Wood
Brass
Rivets
Steam Pipes
Industrial Lamps
Atomic-Punk Machinery
```

### Dwarf

``` text
Heavy Armor
Thick Bulkheads
Furnaces
Massive Machinery
Runes
Industrial Stone/Metal Forms
```

### Elf

``` text
Curved Architecture
Living Materials
Crystal
Organic Corridors
Arcane Lighting
Minimal Visible Machinery
```

The tactical rules remain consistent while the visual identity changes.

------------------------------------------------------------------------

## 21. Example: Procedural Ship Interior

First generate topology:

``` text
       Bridge
          │
      Command
          │
 ┌────────┼────────┐
 │        │        │
Crew    Cargo   Weapons
 │        │        │
 └──── Engineering ┘
          │
       Reactor
```

Convert topology into grid rooms and corridors:

``` text
████████████████████
█ BRIDGE █          █
█        █ CORRIDOR █
████D████████D███████
█ CREW   █ ARMORY   █
█        █          █
████D████████D███████
█      ENGINE       █
█      CORE         █
████████████████████
```

Then apply faction-specific visual reconstruction.

The same gameplay layout could therefore become a Human, Dwarven, Elven,
Neogi, or other faction ship without rewriting combat logic.

------------------------------------------------------------------------

## 22. Map Sizes

Initial targets:

``` text
Small Encounter
20 × 20 = 400 cells

Standard Battle
30 × 30 = 900 cells

Large Battle
40 × 40 = 1,600 cells
```

Ship interiors do not need to occupy rectangular playable shapes even
though the underlying data array is rectangular.

Unused cells can simply be invalid/void space.

------------------------------------------------------------------------

## 23. System Separation

Maintain a strict separation between simulation and rendering.

``` text
Simulation Layer
 ├─ Grid
 ├─ Movement
 ├─ AP
 ├─ Initiative
 ├─ Cover
 ├─ Occupancy
 ├─ AI
 ├─ Hazards
 └─ Combat Resolution

Presentation Layer
 ├─ Meshes
 ├─ Sprites / Models
 ├─ Animation
 ├─ Projectiles
 ├─ VFX
 ├─ Lighting
 ├─ Decorations
 └─ UI
```

Presentation communicates simulation state but should not become the
authoritative source of gameplay rules.

------------------------------------------------------------------------

## 24. Final Architecture

``` text
WORLD / MISSION
       │
       ▼
Procedural Generation
       │
       ▼
Hidden Square Grid
       │
       ▼
Continuous Visual Reconstruction
       │
       ├───────────────┐
       ▼               ▼
Real-Time          Turn-Based
Exploration         Combat
                       │
                 Individual
                  Initiative
                       │
                      AP
                       │
               Grid Movement
                       │
               Hybrid LOS
                       │
              RPG Combat Math
```

### Design Decision

The project will use:

**Hidden Square Grid + Continuous Visual Map + Real-Time Exploration +
Turn-Based Tactical Combat.**

The goal is to retain the implementation advantages of a
grid---procedural generation, deterministic combat, pathfinding, AI,
cover, occupancy, and debugging---without forcing the player to
experience the world as an obvious chessboard.

This architecture is particularly suitable for a solo-developed sandbox
game centered on magical spacefaring ships, boarding actions, derelict
exploration, ruins, wastelands, and character-focused tactical combat.
