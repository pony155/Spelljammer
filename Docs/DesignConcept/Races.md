# Races and heritage design

## Status

Milestone 3 implements the first character-capability slice: eleven base Race
definitions, one compatible Heritage and authored character per Race,
deterministic creation, capability grants, and mixed-crew support validation.
The WPF shell still presents the ship-level expedition loop; broader needs,
relationships, schedules, and campaign persistence remain planned.

## Design goals

- Make every crew member mechanically understandable and narratively distinct.
- Support humans, elves, half-elves, dwarves, orcs, gnomes, goblins, Somnari,
  Veyr, Eidolons, Tharun, and future races without hard-coding content
  into simulation code.
- Use classless progression: attributes describe capability, skills improve
  through use and training, and no class limits what a character may learn.
- Give each race recognizable strengths, needs, and complications without
  prescribing personality, morality, intelligence, profession, or faction.
- Use heritage to combine a character's subrace, formative tradition,
  languages, customs, and starting knowledge.
- Let race matter aboard a ship: atmosphere, gravity, food, sleep, medical
  care, room design, and travel hazards should all interact.
- Preserve stable character identities and deterministic generation across
  saves and replays.

## Terminology

The simulation uses **race** for a broad people and their baseline physiology.
It uses **heritage** for the subrace and formative tradition that provide a
focused adaptation, language and script knowledge, customs, and other starting
knowledge. Save data and content definitions use `raceId` and
`heritageId`.

A heritage is not required to match the community stereotypically associated
with a race. Race-specific heritages describe physiological variants, while
shared or adopted heritages represent characters raised in another community.
For example, a dwarf raised in a human port retains the dwarf race while using
a port heritage compatible with dwarves.

## Character composition

A persistent character is composed from independent, stable layers:

| Layer | Examples | Simulation responsibility |
| --- | --- | --- |
| Identity | character ID, name seed, pronouns, portrait seed | Persistence and presentation lookup |
| Race | human, elf, half-elf, dwarf, orc, gnome, goblin, Somnari, Veyr, Eidolon, Tharun | Body plan and baseline physiological rules |
| Heritage | voidborn, deepforge, free-anchorage | Subrace, formative tradition, languages, customs, and starting knowledge |
| Background | academy graduate, dockhand, caravan guard | Starting skill package and history, never a class |
| Attributes | Strength, Agility, Willpower, Intelligence, Luck, Charisma, Toughness | Broad capability shared by many actions |
| Skills | piloting, engineering, merchant, negotiation, language and literacy | Learned competence that improves independently |
| Racial Perks | Versatility, Aether Sense, Braced Stance | Racial feats granted by Race and Heritage |

No translated name or description becomes gameplay identity. Definitions use
canonical lowercase ASCII IDs; localized keys provide player-facing names.

## Shared physiological model

Races modify common systems rather than owning isolated minigames:

- body size and mass;
- movement and work stamina;
- breathable atmosphere and vacuum tolerance;
- comfortable gravity, temperature, and light;
- sleep or trance requirements;
- nutrition type and consumption rate;
- healing, toxin, disease, and radiation response;
- lifespan and age stage;
- senses and environmental perception; and
- compatibility with equipment, quarters, and medical treatment.

Modifiers should usually change a cost, threshold, recovery rate, or available
option. Flat combat bonuses are used sparingly. Every advantage must create a
different planning opportunity rather than making one race universally
better.

## Race roster

### Humans

Humans originate from their own homeworld. When elven and dwarven explorers
first arrived, humanity was between a medieval and renaissance age and had not
yet achieved spaceflight. The First Concord gave human communities access to
elven Aether navigation and dwarven engineering methods; human crews then made
those methods their own and became widespread through social and technical
adaptation. Their Racial Perk is **Versatility**: one starting aptitude may be
reassigned during recruitment. Their bodies have no extreme environmental
adaptation and depend on ordinary atmosphere, food, sleep, and medical support.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Hearthworld | `heritage.human.hearthworld` | Faster recovery in comfortable gravity and atmosphere | Suffers acclimation penalties sooner in extreme environments |
| Voidborn | `heritage.human.voidborn` | Lower food use and better low-gravity movement | Reduced tolerance for high gravity and heavy recoil |
| Emberworld | `heritage.human.emberworld` | Better heat and dehydration tolerance | Cold exposure accumulates faster |

### Elves

Elves originate from a homeworld distinct from human and dwarven worlds. Their
long-lived nervous systems sense patterns in light and the void's aetheric
currents. Elven records preserve the earliest documented successful Arcane
spaceflight. Their Racial Perk, **Aether Sense**, can reveal
weak anomalies or unstable routes before ordinary instruments, but intense
interference causes sensory strain. Aether Sense grants innate `access.magic`,
allowing an Elf to learn and cast spells without the Spellcasting Training
Feat; it provides no free Magic ranks or spells. Elves enter a short trance
instead of normal sleep; they still need safe downtime.

Many elven cultures actively disdain industrial technology, especially heavy
factories, reactors, powered armor, and mass-produced energy weapons. They
favor natural environments, living craft, Arcane navigation, wards, and
enchantment instead. This is a cultural and historical tendency, not a
biological restriction: an individual Elf may study Engineering or use any
compatible Industrial equipment.

This preference does not make elven craft primitive. Elven resonance smiths
produce exceptional blades, armor, starwood hull components, Aether resonators,
and Arcane instruments. Each is a carefully tuned masterwork intended to keep
its harmony with a bearer or ship over a long life, rather than a standardized
industrial product.

Elven culture is also shaped by fascination with the beauty of the galaxy:
nebulae, starfields, Aether tides, strange worlds, and unbroken night skies.
Many elven voyages prioritize observation, star-charting, art, and the
protection of exceptional natural sites. An individual elf can still become a
miner, trader, or industrial engineer; this is a cultural passion, not a rule.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Dawnweave | `heritage.elf.dawnweave` | Gains focus in strong natural or stellar light | Darkness increases fatigue unless quarters provide tuned lighting |
| Gloamroot | `heritage.elf.gloamroot` | Excellent low-light sight and reduced sensory signature | Bright or rapidly changing light causes strain |
| Glassleaf | `heritage.elf.glassleaf` | Reads crystalline structures and nearby Aether patterns with unusual precision | Resonant machinery and storms can overwhelm that sense |

### Half-elves

Half-elves are a first-class race in the initial content model, representing
characters with both human and elven parentage. Their Racial Perk, **Blended
Physiology**, lets a character select one minor human adaptation and one minor
elven sense, each at reduced strength. It does not determine heritage or
guarantee social acceptance.

Half-elves emerged after the First Concord, especially in shared ports,
diplomatic stations, and mixed settlements. They are not an ancient empire or
a single people with a common political loyalty. Living across elven long-lived
traditions and rapidly changing human port cultures, many became interpreters,
negotiators, navigators, and arbitrators. Their history makes them a natural
part of the Concord's promise and its unresolved tensions, without assigning
any individual a social role.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Concord | `heritage.half-elf.concord` | Switches between normal sleep and trance when quarters permit | Neither rest mode is as efficient as its specialist form |
| Starling | `heritage.half-elf.starling` | Adapts quickly to changing gravity and duty schedules | Needs more recovery after repeated schedule changes |
| Threshold | `heritage.half-elf.threshold` | Learns unfamiliar languages and customs faster | Starts with fewer specialist skill ranks |

Future mixed-race support may generalize parentage, but released save IDs
for half-elves must remain valid through an explicit migration.

### Dwarves

Dwarves originate from a homeworld distinct from human and elven worlds. They
are compact, dense-bodied people adapted to confined habitats and demanding
physical environments. Their Racial Perk, **Braced Stance**, reduces
forced movement and work interruption. Their mass raises acceleration costs,
and cramped does not mean weightless: ship layout and rescue equipment must
support them.

Many dwarven cultures devote generations to technology and industry. Their
foundries, shipyards, reactors, precision fabrication, powered armor, laser
weapons, and plasma weapons represent a deliberate industrial tradition rather
than an innate racial ability. An individual dwarf may still reject that
tradition and pursue Arcane or natural practices.

Dwarven engineering values repeatable standards, interchangeable parts, and
field repair. Where an elven resonance smith tunes one object to its bearer or
ship, a dwarven foundry builds a reliable pattern that can be maintained,
upgraded, and deployed across a fleet.

Many dwarven fleets treat space as a resource frontier. They prospect
asteroids, comet fragments, dead moons, and gas-giant atmospheres for rare
metals, volatiles, reactor material, and industrial gases. Mining claims,
extraction rights, and safe transport routes are major dwarven economic and
political interests, not an obligation imposed on every dwarf.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Deepforge | `heritage.dwarf.deepforge` | Exceptional high-gravity and heat tolerance | Low gravity reduces precision until acclimated |
| Cometdelver | `heritage.dwarf.cometdelver` | Detects structural weakness and valuable mineral seams | Requires more calories during heavy work |
| Brasswake | `heritage.dwarf.brasswake` | Maintains concentration amid vibration and machinery noise | Quiet natural environments provide less effective recreation |

### Orcs

Orcs are powerfully built, warlike people who recover well from exertion. They
regard victory in conflict, command through strength, and territorial conquest
as central virtues. Alongside goblin societies, they uncovered ancient wrecks
on their shared ancestral homeworld: ships made by an unknown civilization that
travelled the void before the earliest elven record. Their spacefaring cultures
grew through generations of salvage and reverse engineering, not through the
First Concord. Their Racial Perk, **Second Wind**, allows a controlled burst of
work or combat stamina followed by a visible recovery debt.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Redwake | `heritage.orc.redwake` | Sustains strenuous labor and emergency damage control | Higher food and oxygen consumption while exerting |
| Stormborn | `heritage.orc.stormborn` | Better resistance to electrical and aether-storm injury | Medical recovery uses more conductive supplies |
| Greenmoon | `heritage.orc.greenmoon` | Heals minor wounds quickly with adequate food and rest | Starvation and sleep loss suppress regeneration sharply |

### Gnomes

Gnomes are small-bodied people with extremely sensitive touch and fine spatial
judgment. Several dwarf and gnome research communities developed the first
repeatable instruments and scientific methods for measuring and coupling to
Aether; this is a shared historical achievement, not an innate trait. Their
Racial Perk, **Closework**, reduces penalties when manipulating
compact mechanisms or working in confined stations. Their reach and unaided
carrying capacity are limited, and shared equipment must be adjustable rather
than assuming one body scale. Gnomes are not inherently inventive or
scholarly; those qualities come from individual attributes, skills, and
heritage.

Their homeworld's conductive mineral veins, high-altitude storms, and unstable
terrain fostered traditions of careful measurement long before spaceflight.
After elven navigators demonstrated open-space Aether travel, gnome and dwarf
researchers turned rare resonance craft into calibrated instruments, safe
couplers, and repeatable shipyard practice. Gnome influence therefore travels
through observatories, repair stations, calibration guilds, and technical
standards more often than territorial states.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Coilwhisper | `heritage.gnome.coilwhisper` | Feels subtle vibration, current, and mechanical imbalance through contact | Heavy vibration and electrical surges cause sensory strain |
| Sporegarden | `heritage.gnome.sporegarden` | Symbiotic microbiome improves food variety and resistance to organic toxins | Sterile quarters and broad-spectrum medicine can disrupt recovery |
| Cloudstep | `heritage.gnome.cloudstep` | Excellent orientation and fine movement in low gravity | High-gravity labor and heavy recoil accumulate fatigue quickly |

### Goblins

Goblins are compact, quick-moving people adapted to crowded habitats and
improvised routes. Alongside orc societies, their salvagers and makers
recovered precursor ships on their shared ancestral homeworld and
reverse-engineered enough of their Aether couplers to begin independent
spaceflight. Their Racial Perk, **Tight Passage**, reduces movement and work
penalties in ducts, wreckage, and congested decks. Their lighter frames are
easier to throw off balance, and standard armor, furniture, and controls may
require refitting. Goblins are not inherently dishonest, reckless, or crude;
behavior comes from the individual and their society.

The first recovered couplers, life-support systems, and navigational components
of the orc-goblin fleets were often restored by goblin workshops. Goblin
engineers learned to extract usable principles from incomplete precursor
designs and keep them working with limited materials. Some goblin communities
are committed partners in the conquest fleets, while others profit from their
logistics or leave for neutral recovery ports. Their shared claim to precursor
knowledge is a lasting source of leverage and friction within the alliance.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Cindervein | `heritage.goblin.cindervein` | Tolerates smoke, heat, and poor industrial air better | Cold exposure and sudden cooling cause fatigue sooner |
| Nightglass | `heritage.goblin.nightglass` | Tracks motion and detail in very low light | Glare and rapid flashes impair vision without protection |
| Hullrunner | `heritage.goblin.hullrunner` | Moves efficiently across cluttered hulls and during brief decompression alarms | Heavy suits and high gravity consume stamina faster |

### Somnari

Somnari are a psychic race whose nervous systems resonate with nearby thought.
Their Racial Perk, **Mindwake**, lets them sense active psychic effects and
initiate a consensual short-range mindlink without a spell or device. Mindwake
grants innate `access.psionics`, not mastery: the Psionics skill governs
control, clarity, range, and learned techniques. Psychic storms, crowded minds,
and repeated use cause strain, and Mindwake never reveals private thoughts
without an explicit effect and a consent or resistance check.

See [`PsychicAbilities.md`](PsychicAbilities.md) for contact, consent,
resistance, strain, and information rules.

Somnari breathe, eat, and rest normally, but their sleep includes a vivid dream
phase needed to recover psychic strain. They are not inherently wiser, calmer,
more truthful, or more intelligent than other races.

On their storm-wreathed homeworld, early Somnari repeatedly dreamed of the same
unfamiliar skies, lost satellites, and distant catastrophes. Patient records
and star-chart comparisons eventually proved that some of these impressions
described real places beyond their world. The dreams were neither Aether magic
nor infallible prophecy, but a motive to build their first voyages toward
otherwise unreachable signals. Modern Somnari communities are prominent in
rescue work, memory archives, trauma care, and anomaly research, while other
states remain wary of what psychic contact might reveal.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Chorusborn | `heritage.somnari.chorusborn` | Maintains a consensual mindlink among several nearby participants | Crowds and overlapping psychic signals accumulate strain faster |
| Veilward | `heritage.somnari.veilward` | Shields themself or an assisted ally against psychic intrusion | Dropping the shield to receive beneficial contact takes focus and time |
| Farwhisper | `heritage.somnari.farwhisper` | Detects directed psychic signals and emotional distress at extended range | Aether storms and ancient sites can create misleading psychic echoes |

### Veyr

Veyr are living descendants of a once-powerful ancient empire, older than any
surviving star chart. That empire collapsed in a Great Cataclysm when a civil
war coincided with a stellar disaster, turning its core worlds, fleets, and
archives into ruins. The broad history is known, but vaults, contradictory
lineages, and fragmentary records leave the war's cause, its responsible
factions, and the exact nature of the stellar calamity unresolved. Character
creation stores `race.veyr` and one compatible heritage.

Veyr culture is deliberately gothic: sable architecture, high spires, memorial
gardens, formal mourning, candlelit rites, and elaborate records of ancestry
and obligation. This is a tradition, not evidence that they are undead or that
they feed on blood. Veyr breathe, eat, sleep, and receive medical care normally.
Their Racial Perk, **Dusk Sight**, gives clear low-light vision but makes sudden
bright flashes and prolonged glare more taxing.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Crimson Court | `heritage.veyr.crimson-court` | Reads formal etiquette, obligation, and status in structured social settings | Informal or rapidly changing groups are harder to interpret |
| Umbral Cloister | `heritage.veyr.umbral-cloister` | Moves and works effectively in low light | Bright flashes and glare cause strain sooner |
| Ashen Pilgrim | `heritage.veyr.ashen-pilgrim` | Tolerates dust, dry air, and long austere journeys | Comfortable, resource-rich routines provide less effective recovery |

### Eidolons

Eidolons are an undead spirit race whose identity persists through a crafted
Soul Anchor and an embodied vessel. Character creation stores `race.eidolon`
and one compatible Heritage. They are distinct from the living Veyr: an
Eidolon's spirit operates a replaceable or reconstructable form from an
external anchor.

Eidolons began with an emergency evacuation from a doomed colony. Its people
developed Soul Anchors to carry identity through a voyage their bodies could
not survive, then rebuilt vessels at their destination. The survivors retained
memory and will but were no longer conventionally alive. Their first political
tradition, the Anchor Covenant, established that an Eidolon is a person rather
than property, a disposable tool, or a copy for another's use. Eidolons are now
scattered across the galaxy, and disputes over anchor custody, continuity of
identity, and reconstruction rights remain central to their history.

Their Racial Perk, **Soul Anchor**, removes the need to breathe, eat ordinary
food, or sleep and provides resistance to vacuum and common disease. Activity
and recovery instead consume resonance held by the anchor, and serious bodily
disruption requires a safe anchor, suitable material, and time to rebuild a
usable vessel. This does not prevent injury, incapacitation, or removal from an
encounter. Loss, capture, or depletion of the anchor prevents recovery.
Conventional Medicine can treat a compatible organic vessel but not the spirit
itself; Eidolon care usually combines Enchantment, Crafting, and purpose-built
ship facilities.

Soul Anchor is supernatural physiology, not spellcasting or psionics. It does
not grant `access.magic`, `access.psionics`, a Skill rank, or a technique.
Eidolons may learn either access Feat through the same training available to
other characters. They are not inherently ancient, emotionless, truthful, or
knowledgeable; memories and personality belong to the individual.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Reliquary-Bound | `heritage.eidolon.reliquary-bound` | Reconstitutes more efficiently beside a portable, prepared reliquary | Concentrating anchor and reserve in one object makes its capture or damage especially dangerous |
| Huskbound | `heritage.eidolon.huskbound` | A preserved organic vessel fits ordinary armor, tools, and some medical procedures | Decay control consumes preservative supplies and contaminated environments can damage the vessel |
| Starwisp | `heritage.eidolon.starwisp` | A light semi-material vessel moves precisely in low gravity and through narrow openings | It carries little unaided mass and magical or electrical interference disrupts coordination |

### Tharun

Tharun are a spacefaring jackal-like race with long directional ears, keen
chemical senses in atmosphere, and strong spatial memory. Character creation
stores `race.tharun` and one compatible Heritage. Their Racial Perk,
**Trail Sense**, reduces uncertainty when following an already observed
physical, chemical, thermal, acoustic, or signal trail. It improves how
Sensors, Xenology, Ancient Lore, and relevant field work use available
evidence; it never reveals unobserved information and cannot smell through a
vacuum without a collected sample or instrument.

Tharun breathe, eat, and sleep normally. Their hearing and scent can be
overloaded by machinery, fumes, weapons, or unfamiliar atmospheres, so fitted
helmets and sensory filters matter. Suits, seats, beds, and armor must also
accommodate muzzle, ears, digitigrade legs, and tail. Tharun are not
inherently loyal, predatory, nomadic, or suited to a particular profession;
those qualities come from the character and their communities.

The Tharun homeworld was a dry, seasonally extreme frontier colony of the Veyr
Empire. For generations, Veyr authorities enslaved Tharun communities to
extract its water, ores, fuel, and biological resources; maintain frontier
infrastructure; and serve imperial routes. By the empire's end, systematic
extraction had left the world depleted and its surviving communities in severe
scarcity. When civil war and stellar disaster broke the empire, the colony was
abandoned with damaged shipyards, stranded vessels, and partial technical
archives. The Tharun reclaimed those remnants, learned to operate and repair
them, and re-entered space on their own terms because remaining homeworld
resources could no longer sustain them.

Their older survival practice—following water, weather, and migration trails
across vast distances—became a starfaring strength: engine traces, signals,
heat changes, and drifting cargo are all routes that can be read. Tharun did
not invent Aether travel, but their navigators, search crews, and frontier
scouts can reconstruct an incomplete route from the marks it leaves. Their
cultures often treat a route as a shared memory rather than merely a line on a
map, which supports both commercial service and fierce independence. The
history of enslavement, extraction, and abandonment created a deep, widespread
Tharun hatred of Veyr people. Many communities regard every Veyr presence as
a possible return of imperial power, and Veyr-Tharun contact is often marked
by hostility, exclusion, reprisals, or open conflict.

| Heritage | Stable ID | Heritage Perk | Cost |
| --- | --- | --- | --- |
| Sunwake | `heritage.tharun.sunwake` | Tolerates heat, dry air, and long periods of measured water rationing | Cold exposure and sudden temperature drops accumulate fatigue faster |
| Hull-Listener | `heritage.tharun.hull-listener` | Reads faint vibration and movement through direct contact with a hull or deck | Heavy machinery, impacts, and sustained alarm noise cause sensory strain |
| Startrail | `heritage.tharun.startrail` | Retains bearings and correlates weak route traces after partial sensor loss | Featureless space and contradictory interference require more time and can produce false leads |

## Racial perks

A **Racial Perk** is a racial feat: a discrete rule granted by a character's Race or
compatible Heritage. It represents inherited physiology, supernatural nature,
or a heritage-specific adaptation. It does not represent personality, beliefs,
professional training, or general learned competence.

A Racial Perk may explicitly grant an innate supernatural access ID. Innate access
substitutes for the corresponding learned access Feat, but never grants skill
ranks or unnamed abilities. Learned Feats come from training and are defined in
[`Skills.md`](Skills.md); they are not Racial Perks and are not inherited.
See [`Perks.md`](Perks.md) for the wider Perk terminology and identity rules.
The technical content namespace is `perk.*`, and Race and Heritage definitions
grant their Perks through `grantedPerkIds`.

Every character begins with two Racial Perks:

1. one Race Perk granted by their `raceId`; and
2. one Heritage Perk granted by their `heritageId`.

| Race | Race Perk | Stable technical ID | Innate access |
| --- | --- | --- | --- |
| Human | Versatility | `perk.race.human.versatility` | None |
| Elf | Aether Sense | `perk.race.elf.aether-sense` | `access.magic` |
| Half-elf | Blended Physiology | `perk.race.half-elf.blended-physiology` | None by default |
| Dwarf | Braced Stance | `perk.race.dwarf.braced-stance` | None |
| Orc | Second Wind | `perk.race.orc.second-wind` | None |
| Gnome | Closework | `perk.race.gnome.closework` | None |
| Goblin | Tight Passage | `perk.race.goblin.tight-passage` | None |
| Somnari | Mindwake | `perk.race.somnari.mindwake` | `access.psionics` |
| Veyr | Dusk Sight | `perk.race.veyr.dusk-sight` | None |
| Eidolon | Soul Anchor | `perk.race.eidolon.soul-anchor` | None by default |
| Tharun | Trail Sense | `perk.race.tharun.trail-sense` | None |

Heritage Perks use IDs such as `perk.heritage.dwarf.cometdelver`. A Racial Perk
definition owns its explicit effects, costs, requirements, and
incompatibilities. Race and Heritage definitions grant Racial Perk IDs rather than
duplicating those rules.

Racial Perks do not have a 0–100 value, improve through use, or unlock because of a
class. Learned techniques and professional expertise belong to Skills, while
trained supernatural access belongs to learned Feats. Content validation
rejects missing Racial Perk IDs, unknown access IDs, duplicate grants, and Racial Perks
that are incompatible with the granting Race or Heritage.

## Classless capabilities

Characters have no class, global character level, class skill, or class-locked
ability. Attributes describe broad capability, while independently advancing
skills describe learned competence. Backgrounds provide history without
restricting future progression.

See [Attributes.md](Attributes.md) for the attribute roster, contextual use,
and modifier rules. See [Skills.md](Skills.md) for the skill catalog, action
resolution, advancement, languages, scripts, and ancient lore.

## Character generation

Given a content revision, scenario, and explicit character seed, generation
must be deterministic:

1. Select an allowed race and compatible heritage.
2. Grant and validate the Race Perk and Heritage Perk.
3. Generate age stage, body parameters, name seed, pronouns, and appearance.
4. Allocate attributes, background skills, any documented pre-campaign
   training Feats, and one or more personal aptitudes.
5. Assign known language, script, and lore IDs from heritage and personal history.
6. Add bounded beliefs and memories.
7. Validate equipment, atmosphere, quarters, diet, and medical compatibility.
8. Publish the character only after the complete definition passes validation.

Scenarios may provide authored characters or weighted generation tables.
Weights affect frequency, never capability ceilings or moral alignment.

## Data and persistence contract

Authored definitions live under `Content/Packs/base/Definitions/` and compile
into validated bounded artifacts. A heritage definition references its compatible
race rather than copying the race definition:

```json
{
  "schemaVersion": 1,
  "id": "heritage.dwarf.cometdelver",
  "raceId": "race.dwarf",
  "nameKey": "character.heritage.dwarf.cometdelver.name",
  "descriptionKey": "character.heritage.dwarf.cometdelver.description",
  "grantedPerkIds": [
    "perk.heritage.dwarf.cometdelver"
  ]
}
```

Persistent race state stores race and heritage definition IDs, definition
revisions, and race-specific rolled parameters. It never stores localized names
as identity. Definition loading must reject duplicate IDs, missing races,
incompatible heritages, missing or incompatible Racial Perks, incompatible body
rules, unknown modifiers, and unbounded generation tables before publication.

## Balance rules

- No race receives a universal intelligence, morality, or social-value
  modifier.
- Every heritage begins with one legible strength and one relevant cost.
- Environmental advantages must matter often enough to be worth choosing but
  must not make a heritage mandatory for a particular activity.
- Equipment, training, medicine, and ship design provide alternate solutions
  to most physiological disadvantages.
- Mixed crews should create logistical and operational choices, not a hidden
  optimal mono-race strategy.
- Automated crew decisions expose the need, task, rule, and blocker that caused
  the choice.

## First playable character scope

The first crew-enabled vertical slice should use eleven authored characters:
one human, elf, half-elf, dwarf, orc, gnome, goblin, Somnari, Veyr, Eidolon,
and Tharun. Each needs a race, heritage, background, attributes, skills, two
Racial Perks, and compatible quarters. The two Racial Perks are racial feats:
one granted by Race and one by Heritage. The Elf must exercise innate
magical access and the Somnari must exercise innate psychic access. The
Eidolon's anchor logistics and the Tharun's evidence-bounded tracking must
each change at least one voyage decision. At least one character without
either access Racial Perk must demonstrate earning an access Feat through
documented training. Ship jobs and schedules are a separate future crew-system
concern, not a component of race or heritage. The slice needs only four shared
needs—rest, nutrition or equivalent reserve, safety, and belonging.
Attribute, skill, language, script, ancient-lore, and encounter scope is defined
in the linked capability documents. See [`Battle.md`](Battle.md) for combat,
injury, surrender, and tactical-layout rules.

The slice succeeds when race and heritage change real voyage decisions, every
crew action has an inspectable reason, and replaying the same seed and commands
produces the same character and ship state. Romance, children, aging campaigns,
full genetic inheritance, unrestricted hybrid races, and dozens of content
variants are explicitly deferred.
