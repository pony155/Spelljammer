# Character skills

## Status

This document defines the planned classless skill, language, script, and lore
systems. All 29 Skill definitions and their typed immutable content registry
are implemented; character values, advancement, actions, languages, scripts,
and lore state remain planned. Broad capability is defined in
[`Attributes.md`](Attributes.md), while race, heritage, identity, background,
and Racial Perks are defined in [`Races.md`](Races.md). Spellcasting rules and the
authored spell catalog are defined in [`Spells.md`](Spells.md), and psychic
techniques are expanded in
[`PsychicAbilities.md`](PsychicAbilities.md). Combat actions and tactical
contexts are defined in [`Battle.md`](Battle.md).
The planned data registry, training state, and action-eligibility implementation
are specified in
[`../Architecture/CharacterCapabilities.md`](../Architecture/CharacterCapabilities.md).

## Classless skill system

Characters have no class, global character level, class skill, or class-locked
ability. A background answers “what has this person done before?” but does not
decide what they can become. Every character may improve every skill, equip any
compatible item, perform any ship duty, and learn any technique whose explicit
requirements they meet.

Skills express learned competence and advance independently. Their planned
player-facing range is 0–100. A character can be an excellent pilot and novice
navigator, or a skilled doctor who later learns swordplay, without selecting or
changing a class.

Racial Perks and learned Feats are not skills. Racial Perks are racial feats
granted by Race and Heritage; learned Feats are discrete capabilities earned through
training. Neither has a 0–100 rating or improves through repeated use. See
[`Races.md`](Races.md) and [`Perks.md`](Perks.md) for Racial Perk rules.

## Ability access feats

Magic and Psionics use explicit access gates. A skill rating measures knowledge
and control but does not grant permission to perform supernatural actions.

| Learned Feat | Stable ID | Grants |
| --- | --- | --- |
| Spellcasting Training | `feat.access.magic` | `access.magic`, allowing the character to cast known spells |
| Psionic Training | `feat.access.psionics` | `access.psionics`, allowing the character to use known psychic techniques |

A character earns an access Feat by completing a validated training project.
The project declares a mentor or instructional source, minimum skills,
facilities, time, cost, practice tasks, safety rules, and completion check. It
awards the Feat atomically; partial training remains project progress and never
temporarily unlocks the ability.

A Race or Heritage Racial Perk may explicitly grant the same access ID. This is
innate access and substitutes for the corresponding learned Feat. It does not
grant free skill ranks, unrelated techniques, or immunity to costs and failure.
The initial examples are the Elf **Aether Sense** Racial Perk for `access.magic` and
the Somnari **Mindwake** Racial Perk for `access.psionics`.

Access, knowledge, and competence are separate checks:

1. the character has the required access ID from a learned Feat or racial
   Racial Perk;
2. the character knows the specific spell or psychic technique; and
3. the character meets its skill, resource, target, equipment, and contextual
   requirements.

Without access, a character can study theory, identify evidence, assist an
authorized practitioner, and make allowed knowledge checks, but cannot cast a
spell or initiate a psychic technique. A focus item, spellbook, psychic
implement, or high skill rating never bypasses the access gate.

## Skill catalog

| Area | Initial skills |
| --- | --- |
| Movement | athletics, acrobatics, EVA, stealth |
| Voyage | piloting, astrogation, sensors, rigging |
| Ship work | engineering, salvage, crafting |
| Combat | melee, archery, gunnery, defense |
| Mystic | magic, psionics, enchantment |
| Care and knowledge | medicine, alchemy, cooking, xenology, ancient lore, language and literacy |
| Social | command, insight, deception, merchant, negotiation |

The following skills form the core of personal combat, item creation, crew
care, and discovery:

| Skill | Stable ID | Scope and boundaries |
| --- | --- | --- |
| Athletics | `skill.athletics` | Covers running, jumping, climbing, swimming, lifting endurance, and sustained physical movement. |
| Acrobatics | `skill.acrobatics` | Covers balance, tumbling, controlled falls, narrow footing, and rapid body positioning. |
| EVA | `skill.eva` | Covers suit operation, tether use, handholds, propellant movement, and work in vacuum or microgravity. |
| Stealth | `skill.stealth` | Reduces observable evidence through positioning, timing, noise control, and concealment without removing authoritative presence. |
| Piloting | `skill.piloting` | Controls ship maneuvers, approach vectors, docking, evasion, and disengagement within the vessel's capabilities. |
| Astrogation | `skill.astrogation` | Plans routes, interprets Starways, estimates travel costs, and evaluates navigation uncertainty. |
| Sensors | `skill.sensors` | Operates detection systems, improves contact confidence, identifies signatures, and distinguishes noise from evidence. |
| Rigging | `skill.rigging` | Deploys sails, tethers, lines, external fittings, and load-bearing arrangements under changing motion. |
| Salvage | `skill.salvage` | Assesses wrecks, extracts bounded recoverable material, preserves evidence, and avoids destabilizing hazards. |
| Gunnery | `skill.gunnery` | Operates personal firearms and manually controlled mounted weapons. Ship cannons use their module statistics and ship targeting state without a Gunnery requirement. |
| Defense | `skill.defense` | Covers active guarding, shields, parries, protective positioning, and learned defensive techniques. |
| Magic | `skill.magic` | Measures spell theory and control. Active casting requires `access.magic` plus a known spell; individual spells are techniques or discoveries, not classes. |
| Psionics | `skill.psionics` | Measures psychic theory and control. Active techniques require `access.psionics`; every effect defines its target, range, consent or resistance rule, and strain cost. |
| Enchantment | `skill.enchantment` | Designs, binds, identifies, maintains, and removes persistent magical effects on equipment, ship fittings, and locations. |
| Melee | `skill.melee` | Covers unarmed combat and hand-held weapons. Weapon tags and learned techniques create differences without separate weapon classes. |
| Archery | `skill.archery` | Covers bows, crossbows, unusual string weapons, ammunition choice, and aimed physical projectiles. Personal firearms use Gunnery; ship cannon commands require neither Skill. |
| Alchemy | `skill.alchemy` | Identifies reagents and produces medicines, stimulants, toxins, blood substitutes, volatile mixtures, and magical compounds. |
| Crafting | `skill.crafting` | Fabricates and improves personal equipment, ammunition, tools, fittings, and replacement parts from known designs. |
| Engineering | `skill.engineering` | Diagnoses, operates, maintains, and repairs ship modules, machinery, power systems, and habitat infrastructure. |
| Merchant | `skill.merchant` | Appraises goods, reads markets, manages cargo contracts, recognizes commercial customs, and detects pricing fraud. |
| Negotiation | `skill.negotiation` | Bargains, mediates disputes, proposes terms, and secures concessions without overriding another character's agency. |
| Medicine | `skill.medicine` | Diagnoses, stabilizes, treats, and supports recovery from injuries, illness, poisoning, and environmental exposure. |
| Cooking | `skill.cooking` | Plans meals, preserves provisions, safely handles unusual diets, and turns limited ingredients into nutrition and morale. |
| Xenology | `skill.xenology` | Studies unfamiliar organisms, ecologies, physiology, behavior, and contamination without assigning cultural stereotypes. |
| Ancient Lore | `skill.ancient-lore` | Recognizes lost civilizations, obsolete customs, relic functions, old routes, and the historical context of discoveries. |
| Language and Literacy | `skill.language-literacy` | Governs speaking, understanding, reading, writing, translation, and script decipherment for learned languages and scripts. |
| Command | `skill.command` | Coordinates crew priorities, communicates orders, and sustains organized action without replacing another character's expertise. |
| Insight | `skill.insight` | Interprets behavior, intent, uncertainty, and social evidence without becoming an automatic truth detector. |
| Deception | `skill.deception` | Creates plausible false impressions through words, disguise, timing, and evidence while preserving counter-observation. |

Engineering diagnoses, operates, and repairs ship systems; Crafting manufactures
their physical components. Enchantment binds persistent magical effects to a
prepared item, fitting, or location, while Magic performs active spells.
Salvage extracts usable material; Alchemy transforms suitable reagents. These
skills can cooperate in one job without collapsing into a single universally
optimal skill.

An enchantment requires a known design, a compatible prepared target, explicit
reagents or stored power, and enough time. Failure can waste bounded resources,
produce an unstable effect, or leave a detectable flaw; it cannot silently
replace a valid existing enchantment. Removing or replacing an enchantment is
validated before the old effect is retired.

Merchant determines what cargo is worth, where demand exists, and which risks
or obligations hide in a deal. Negotiation determines whether the parties can
reach acceptable terms. Merchant is a learned skill rather than a class; any
character may study it. Negotiation cannot
force consent, erase faction policy, or make an impossible agreement valid.

Every character may pursue Spellcasting Training, but only a character with
`access.magic` may cast a known spell. Personal casting uses a simple Focus
cost; ship spells use Aether from their required Arcane module. Armor, Race,
Heritage, or Background may alter an explicit effect but never creates a hidden
class restriction. The short casting flow, visible spell values, and
counterplay are specified in [`Spells.md`](Spells.md).

Every character may pursue Psionic Training, but only a character with
`access.psionics` may initiate a known psychic technique. The Somnari Mindwake
Racial Perk supplies innate access to basic psychic contact, not free skill ranks or
unrestricted mind reading. Failed or resisted psychic actions may create
strain, distorted impressions, or detectable psychic feedback.
Consent, resistance, Psychic Strain, information boundaries, and technique
definitions are specified in [`PsychicAbilities.md`](PsychicAbilities.md).

## Action resolution

Skills do not have one permanently governing attribute. Each action selects the
attribute appropriate to its method and context. The interface must show the
selected attribute, skill, equipment, help, conditions, and difficulty before
or after resolution as appropriate.

An action's capability is derived from:

```text
relevant attribute + relevant skill + equipment + assistance
    + situational modifiers + deterministic random result
```

The exact formula is deferred until implementation and balancing. Random
results must come from an explicitly owned seeded stream, and the action record
must preserve enough information to reproduce the outcome.

## Advancement

- Using a skill against a meaningful challenge grants bounded practice for that
  skill; trivial repeated actions do not provide unlimited advancement.
- Instruction, manuals, simulators, and downtime projects can grant practice at
  a resource and time cost.
- High skill ranks require progressively more practice and may require a
  teacher, facility, discovery, or dangerous field experience.
- Techniques can require skill thresholds, attributes, equipment, discoveries,
  or circumstances—not class levels. Casting and psychic techniques also
  require their explicit access ID from a learned Feat or innate Racial Perk.
- Practice awards are deterministic, capped per committed action, and included
  in the authoritative command result.
- Enemies and encounters do not automatically scale to a global character
  level. Location, faction, world state, and authored threat determine danger.

## Languages, scripts, and ancient lore

Language knowledge is important expedition equipment. An unread warning can be
as dangerous as a damaged hull, while a correctly interpreted ledger, epitaph,
or star chart can reveal routes, recipes, claims, allies, and sealed locations.

The model separates three concepts:

- a **language** is a spoken, signed, or otherwise communicated system;
- a **script** is a writing system that can encode one or more languages; and
- **lore** is contextual knowledge about an era, people, event, place, object,
  ritual, or belief.

Learning a language or script adds it to a character's known knowledge. The
single Language and Literacy skill determines how effectively the character
speaks, understands, reads, writes, translates, and deciphers what they know.
Knowing a script does not guarantee knowledge of every language written with
it, and translating words does not supply their historical meaning.

### Script traditions

Each race has an associated script tradition, but scripts are learned
inventions rather than biological abilities. Heritage determines starting
language and script knowledge. Anyone may learn another script, and communities
may adopt, adapt, or reject the script associated with their race.

| Tradition | Stable ID | Common association | Visual and practical character |
| --- | --- | --- | --- |
| Tidemark | `script.tidemark` | Human ports and merchant fleets | Compact strokes suited to ledgers, cargo seals, and rapidly amended charts |
| Lumenbranch | `script.lumenbranch` | Elven observatories and memory houses | Branching marks whose spacing can encode emphasis and aetheric relationships |
| Bridgehand | `script.bridgehand` | Half-elven enclaves and mixed crews | A phonetic hand designed to represent borrowed sounds and switch languages cleanly |
| Deepcut | `script.deepcut` | Dwarven holds, foundries, and hullwrights | Angular marks readable when carved, stamped, or felt through smoke and darkness |
| Knotstroke | `script.knotstroke` | Orc convoy clans and oath circles | Connected strokes derived from route cords, banners, and witnessed agreements |
| Coilscript | `script.coilscript` | Gnome workshops, schools, and compact habitats | Nested curves combine ordinary prose with measurements and mechanical relationships |
| Patchsign | `script.patchsign` | Goblin flotillas, markets, and maintenance crews | Modular marks remain legible when stenciled, rearranged, or repaired on reused material |
| Dreamtrace | `script.dreamtrace` | Somnari sanctuaries, navigators, and memory circles | Layered curves annotate ordinary language with emotion, certainty, and the source of a remembered impression |
| Sable Cipher | `script.sable-cipher` | Veyr courts, memorial houses, and catastrophe archives | Layered notation separating public text from ancestry, obligation, and sealed catastrophe records |
| Echo Rings | `script.echo-rings` | Eidolon reliquaries, memorial houses, and vessel workshops | Concentric breaks record sequence, speaker identity, and revision when etched, embossed, or projected |
| Waynotch | `script.waynotch` | Tharun stations, route guilds, and traveling communities | Directional notches encode bearings, pressure warnings, and concise messages readable by sight or touch |

Sable Cipher, Echo Rings, and Waynotch are associated with particular
communities but are not automatic race knowledge. Half-elves likewise do not
automatically know Bridgehand or the scripts used by either parent's community.

Living scripts may have regional hands and historical forms. These are
definition variants under one stable script identity unless they require a
genuinely different decipherment rule. Ancient or unknown scripts use their own
IDs and are never mislabeled as a modern race's writing merely because
their glyphs look similar.

### Language and Literacy skill

Each character stores one bounded `skill.language-literacy` value rather than
separate speaking, reading, and writing skills. The character also stores
bounded sets of known language and script IDs. Heritage, background, authored
personal history, study, and discoveries add knowledge; race alone does not.

Reading an unfamiliar discovery proceeds through explicit stages:

1. **Identify:** recognize the script family, age, medium, and signs of forgery.
2. **Transliterate:** convert visible marks into known symbols or sounds.
3. **Translate:** determine literal meaning using known languages and the skill.
4. **Contextualize:** use Ancient Lore to understand obsolete names, metaphors,
   laws, rituals, dates, and technical assumptions.
5. **Authenticate:** compare material, authorship, provenance, and conflicting
   sources before acting on the result.

Language and Literacy drives identification, transliteration, translation,
speaking, reading, and writing. Ancient Lore drives context and authentication.
Intelligence can notice patterns in damaged or hidden marks and reconstruct
missing text, while Willpower can read magical or psychic impressions. A single
check may combine several characters, tools, or ship facilities while
preserving who contributed each result.

Failure creates uncertainty, partial readings, wasted time, or a dangerous
interpretation; it does not silently replace the original inscription. The UI
must distinguish observed glyphs, transliteration, proposed translation,
confidence, known alternatives, and confirmed lore.

### Lore knowledge

`skill.ancient-lore` measures a character's ability to reason about the past;
specific facts are persistent discoveries with stable IDs. A high skill does
not conjure facts that the crew has never encountered. Lore can be acquired
from inscriptions, artifacts, oral histories, archives, mentors, visions, and
comparison between sources.

Lore discoveries may:

- reveal a safe course or hidden sector;
- identify an artifact's purpose, command phrase, or hazard;
- unlock a spell, alchemical recipe, crafting design, or ship upgrade;
- expose an old treaty, ownership claim, blood debt, or faction grievance;
- improve negotiation with someone who values the source;
- distinguish a genuine relic from a forgery; or
- revise an earlier interpretation when stronger evidence appears.

The knowledge model records subject ID, source ID, discoverer, confidence,
translation state, and whether the crew has shared the fact. Rumor, hypothesis,
translation, and confirmed knowledge remain distinct states.

In-world scripts are authored glyph, font, or image assets, not substitutes for
the player's selected UI language. Every inscription needs accessible
transliteration, translation, scale, contrast, and non-visual presentation when
the character has earned that information. Simulation and saves store script,
language, and lore IDs rather than rendered glyphs or translated strings.

## Data and persistence

Skills, learned Feats, access grants, training projects, languages, scripts,
techniques, and lore subjects use stable canonical IDs and localized
presentation keys. Persistent character state stores one Language and Literacy
skill value alongside the other skill values, bounded sets of learned Feat,
access, language, script, spell, and technique IDs, and bounded training-project
state. Lore progress uses records keyed by stable character, knowledge, and
source IDs.

Discovery publication validates every reference and replaces the previous
crew-knowledge snapshot atomically. Failed training, translation, or content
loading cannot partially grant an access Feat or alter known facts and skill
progress.

## First playable scope

The first crew slice initially exercises Piloting, Astrogation, Engineering,
Salvage, Medicine, Cooking, Command, Merchant, Negotiation, and Language and
Literacy. The first encounter milestone adds Magic, Psionics, Enchantment,
Melee, Archery, Alchemy, and Crafting with both trained and innate access paths,
at least one usable spell, psychic technique, enchantment design, melee
technique, ranged technique, alchemical recipe, and enchanted crafted item.

That milestone also includes one ancient ruin inscription. At least two crew
members with different script or lore knowledge must be able to collaborate on
its reading, and the resulting interpretation must change a real voyage choice
without requiring the player to read the fictional glyphs personally.
