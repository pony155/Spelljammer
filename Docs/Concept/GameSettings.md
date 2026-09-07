# Game settings

## Status

This document defines the settings experience, ownership model, campaign-rule
configuration, accessibility options, persistence rules, and determinism
requirements. The first local player-preference profile and an in-window
SpriteForge UI settings dialog are implemented. They cover English, French,
and Traditional Chinese language selection, Desktop or bounded
window-resolution presets, master, music, and effects
volume, subtitles, reduced motion, screen shake, and interface scale. Language
and resolution apply to the current main menu; reduced motion applies to the
retained drive animation. The other values are persisted for presentation
systems that do not yet exist. Display-device selection, input remapping, and
campaign-rule settings remain planned.

Settings fall into two different domains. Player and device preferences may be
changed without rewriting a campaign. Rules that affect generation or
authoritative outcomes are selected when a campaign is created and then stored
with that campaign. The game never treats a presentation preference as a
gameplay identity or silently changes campaign rules after creation.

## Design goals

- Make common choices easy to understand without hiding their concrete effect.
- Separate accessibility from difficulty: a player should not have to accept
  harsher rules to use readable text, reduced motion, or additional cues.
- Preserve deterministic simulation regardless of resolution, frame rate,
  animation speed, locale, audio, or input device.
- Record every campaign-affecting choice needed to reproduce generation and
  outcomes.
- Apply valid changes transactionally and retain the last working settings if
  parsing, validation, device selection, or persistence fails.
- Keep configuration bounded, local by default, and compatible with eventual
  declarative content packs without allowing settings to execute code.

## Settings domains

| Domain | Examples | Owner | Lifetime |
| --- | --- | --- | --- |
| Device | Display mode, monitor, render scale, audio device | Local installation | Applied to one machine |
| Player profile | Locale, volumes, text size, input bindings, tutorials | Player profile | Shared by campaigns on that profile |
| Session | Temporary mute, current simulation speed, open panels | Running application | Not required after exit |
| Campaign rules | Seed, galaxy preset, voyage pressure, event and crisis rules | Campaign | Immutable after campaign creation unless explicitly migrated |
| Content configuration | Installed and enabled pack roots | Installation/new-campaign setup | Separate from settings and locked per campaign |

Content installation is not a preference toggle. Enabled packs are validated
before campaign setup, and the chosen ordered pack set becomes the campaign's
`CampaignContentLock`. Removing a pack cannot be used as an informal way to
disable an inconvenient rule in an existing campaign.

## Player and device preferences

### Display and interface

The first display choices are Desktop mode and bounded 1280x720, 1600x900,
1920x1080, and 2560x1440 window-size presets. Desktop maximizes to the current
work area; a fixed preset is centered and clamped to that work area, so it
cannot make the settings entry point unreachable. Monitor selection, exclusive
fullscreen, render scale, frame limit, vertical synchronization, safe-area
margin, cursor confinement, and confirmation/revert for riskier future video
changes remain planned.

UI scale is independent from world-render scale. Information needed to issue a
command remains available at every supported scale; reducing visual detail may
remove decoration but not state, warnings, routes, or target distinctions.

### Audio

Master, music, ambience, effects, interface, and voice channels have separate
bounded volume controls. Output device, dynamic-range profile, mono output,
subtitles, captions, and background-audio behavior are explicit options.

Audio cues that communicate hazards, command readiness, damage, or dialogue
also have a visual or textual equivalent. Muting a channel never changes event
timing or simulation eligibility.

### Controls

Bindings map physical inputs to stable game commands, not translated labels or
WPF control names. Keyboard and mouse are the first supported scheme; controller
and remapping support are planned. A binding editor detects conflicts before
publication and always retains a reachable way to open menus and cancel an
operation.

Hold/toggle behavior, pointer sensitivity, camera pan speed, edge scrolling,
and confirmation preferences belong to the profile. Input repeat is bounded,
and one physical event cannot enqueue an unbounded number of simulation
commands.

### Language

Text locale, voice locale, subtitle language, number formatting, and any
preferred fallback are separate when supported. Locale selection uses stable
locale identifiers and the game-owned catalog fallback rules defined in
[`../Architecture/Localization.md`](../Architecture/Localization.md).

Changing language rebuilds presentation text only. It cannot change stable-ID
sorting, command dispatch, generated names, random choices, or save identity.
English (`en-US`), French (`fr-FR`), and Traditional Chinese (`zh-Hant-TW`)
menu/settings catalogs are currently installed; both translated locales
explicitly fall back to the complete English source locale.

## Accessibility

Accessibility options are ordinary first-class settings rather than a single
preset. Planned controls include:

- scalable text and interface density, readable typeface choices, and increased
  line or paragraph spacing;
- high-contrast panels, adjustable contrast, and color-vision palettes while
  retaining shapes, labels, or patterns as non-color distinctions;
- reduced camera motion, screen shake, flashes, particles, and animated
  backgrounds;
- subtitles, speaker labels, closed captions, directional cue text, and visual
  equivalents for important sounds;
- hold/toggle alternatives, remappable inputs, adjustable double-click and
  drag thresholds, and reduced precision requirements;
- optional auto-pause at defined committed events, longer decision windows
  where real-time presentation is used, and confirmation for costly or
  irreversible commands; and
- additional explanation of costs, blockers, target eligibility, formulas,
  and likely consequences without revealing information the crew has not
  learned.

Reduced motion and slower animation change presentation cadence only. An
auto-pause request is applied at a completed authoritative tick and becomes
part of the recorded command/state history; wall-clock timing never decides an
outcome. Accessibility cues expose already observable information and do not
grant hidden faction, route, encounter, or target knowledge.

## Campaign setup

A new campaign presents one summary of every choice that can affect its rules.
The exact list grows with the corresponding systems, but the planned groups are:

| Group | Choices | Persistence rule |
| --- | --- | --- |
| Scenario | Starting anchorage, ship, crew premise, required content | Store stable Scenario and definition IDs |
| Seed | Generated value or validated explicit value | Store the exact numeric seed |
| Galaxy | Size, shape, Starway density, hazards, habitable sites, faction presence, Ancient activity | Store the versioned galaxy-settings ID and resolved values |
| Voyage pressure | Supplies, recovery margins, hostile preparation, and economic tolerance | Store a preset ID plus bounded overrides |
| Events | Frequency, severity, repetition tolerance, and excluded sensitive themes | Store resolved option IDs before event scheduling |
| Crises | Off/one/chained, intensity, warning horizon, eligible families, and continuation policy | Store before galaxy generation |

Galaxy fields are defined in [`GalaxyMap.md`](GalaxyMap.md), event behavior in
[`Events.md`](Events.md), and late-campaign crisis choices in
[`Endgame_Crisis.md`](Endgame_Crisis.md).

The setup screen validates the entire candidate before creating state. It shows
the seed, content lock summary, generator version, selected preset, overrides,
disabled content families, and any combination that is invalid. Cancelling or
failing setup leaves the existing active campaign unchanged.

## Difficulty and voyage pressure

Difficulty is a set of inspectable authored budgets, not an invisible global
multiplier. Initial presets provide coherent starting points:

| Preset | Intended experience |
| --- | --- |
| Story | More recovery margin and warning while retaining resource, injury, retreat, and consequence systems |
| Standard | Baseline authored economy, opposition, event, and recovery budgets |
| Severe | Leaner margins and better-prepared opposition without secret knowledge, immunity, or unavoidable opening losses |
| Custom | Any validated combination of exposed bounded fields |

A preset may select initial supplies, service availability, price or reward
budgets, recovery opportunities, enemy preparation, coordination error margins,
and event pressure. It does not alter descriptive odds after they are shown,
change rules because the player is winning, inspect private player intent, give
AI actors knowledge they could not possess, or continuously mirror upgrades to
the player's ship.

The game displays resolved values and major consequences of each override.
Changing one field marks the setup as Custom; it does not silently move other
controls. Accessibility settings never change the difficulty label.

## Determinism and authority

Only campaign rules may influence authoritative generation or simulation.
Their resolved stable IDs and bounded values are part of the campaign header
and the inputs to named random streams where relevant. Given the same content
fingerprint, generator/formula/effect versions, campaign settings, seed, and
command stream, the authoritative result must be identical.

These preferences must not change authoritative results:

- locale, text and number formatting;
- resolution, UI scale, animation, particles, or frame rate;
- audio device, mix, subtitles, captions, or volume;
- physical input bindings; and
- tooltip, confirmation, or information-panel layout.

Simulation-speed controls change how quickly fixed ticks are requested, not
the meaning or duration of a tick. Pause, speed, or automatic pause may change
which commands the player chooses, but never resolve work from elapsed wall
time or rendering cadence.

## Persistence and compatibility

Player/device preferences use a separately versioned settings document. They
are not included in `CampaignContentLock`, semantic gameplay fingerprints, or
replay identity. Device-specific values remain local even if profile syncing is
introduced later.

Campaign rules are authoritative save data. Adding them to the implemented
Milestone 6 payload requires an explicit save-schema revision and migration;
the current campaign model does not yet serialize a game-settings record. A
load validates the stored settings version and every stable option ID before
reconstructing campaign state. Unknown campaign rules cannot be replaced with
defaults silently. The existing envelope and migration boundary are documented
in [`../Architecture/CampaignSaves.md`](../Architecture/CampaignSaves.md).

Settings writes follow the same safety principles as campaign saves: write one
same-directory temporary artifact, flush it, validate it, and replace only the
exact target. A malformed new document retains the last working profile. Reset
to defaults is explicit and scoped to one category or the whole profile; it
never deletes campaigns, saves, content packs, or unrelated files.

Autosave interval, autosave triggers, and retained-save count are profile
preferences, but save creation still occurs only at a complete campaign commit.
Retention is bounded and operates only on artifacts recorded as belonging to
that campaign; no broad directory cleanup is allowed.

## Apply behavior

Each setting declares one apply policy:

| Policy | Behavior |
| --- | --- |
| Immediate | Preview and commit without recreating authoritative state |
| Next view | Apply when the affected panel or renderer view is recreated |
| Restart required | Persist now and explain that the application must restart |
| New campaign only | Disable editing after creation and show the stored campaign value |

Applying a category builds and validates a complete candidate, then publishes
it once. If an audio device, display mode, locale catalog, binding set, or file
write fails, the previous working category remains active and the UI shows an
actionable localized diagnostic.

## Settings interface

The implemented in-window dialog has a left-side General, Audio, and Interface
category list, clear current values, popup language and resolution selectors,
whole-profile Reset, Apply, and Cancel. Multiple-choice selectors open bounded
modal option menus, and Cancel or Escape dismisses the option menu before the
settings dialog. SpriteForge owns its retained document, bounded controls,
layout, stable-key focus, anchored popup placement, modal routing,
pointer/keyboard input, and typed actions through its single current managed UI
C ABI. The WPF host draws copied tagged solid presentation records and
game-localized text while direct SpriteForge renderer realization remains
engine work. Searchable settings, per-category reset, and a campaign summary
reachable after creation remain planned. Labels describe effects directly;
terms such as “harder” are supplemented with the affected budgets or rules.

The implemented dialog supports keyboard-only navigation, predictable focus
order, and engine-owned accessibility names and values. Exposing that native
semantic tree through the managed WPF child to screen readers remains planned.
The target interface has no interaction that depends only on hover, color,
sound, or fine pointer movement. Video changes will use a confirmation
countdown with an automatically safe revert. Campaign setup will require one
final summary confirmation but will not hide values behind nested screens.

## First implementation status

The delivered slice adds:

- a versioned local profile with language, resolution, master/effects/music
  volumes, subtitles, UI scale, reduced motion, and screen shake;
- bounded strict JSON parsing, transactional load/apply/reset, durable
  same-directory replacement, exact-file recovery, and stable diagnostics;
- a localized in-window SpriteForge UI modal with mouse interaction and keyboard focus,
  navigation, adjustment, acceptance, and cancellation; and
- compile-only settings and engine-interop contracts covering bounds,
  corruption, rollback, recovery, action routing, and batch atomicity.

The remaining first-slice work is:

- a small remappable keyboard-command set and broader locale coverage;
- a versioned campaign-settings record containing the explicit seed, the
  16-system voyage galaxy preset, Standard voyage pressure, event controls,
  and crises Off;
- complete validation before campaign creation or load publication;
- presentation tests showing that locale, render cadence, and accessibility
  changes do not alter an identical campaign command stream; and
- save migration coverage when the campaign-settings record is added to the
  existing save schema.

Cloud synchronization, controller-specific layouts, broad graphics tuning,
multiple profile accounts, unrestricted numeric generation controls, adaptive
difficulty, multiplayer host rules, and mid-campaign rule editing remain
deferred.

## Acceptance criteria

The first two criteria and keyboard operation are covered by the implemented
player-profile slice. English/French/Traditional Chinese locale switching and
safe window-size presets are live; the campaign and full text-scaling criteria
remain gates for later work.

- Invalid or unsupported settings retain the previous working configuration.
- Reset and recovery touch only the exact intended settings artifacts.
- Every authoritative option is visible before campaign creation and survives
  exact save/load.
- Campaign-affecting changes require a new campaign or explicit migration.
- Accessibility and presentation options never alter results for an identical
  authoritative command stream.
- The UI remains operable using keyboard-only navigation and supported text
  scaling.
- No settings path scans unrelated directories, accesses the network, executes
  code, or uses localized text as identity.
