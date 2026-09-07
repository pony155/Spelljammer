# Game settings architecture

## Implemented boundary

`Spelljammer.Settings` owns the first versioned local player-preference
profile. It is headless and has no WPF, SpriteForge, localization, native
pointer, or authoritative campaign dependency. The active profile contains
stable language and resolution choices, bounded master, music, and effects
volumes, subtitles, reduced motion, screen shake, and interface scale.

The WPF host owns startup loading and the in-window overlay lifecycle. The
overlay delegates its retained document, fixed logical layout, category
navigation, focus, nested modal choice menus, pointer/keyboard interaction,
sliders, toggles, buttons, and typed action queue to SpriteForge UI. The game
maps copied stable actions to a draft profile and publishes that draft only
after durable persistence succeeds.

The integration consumes the single current SpriteForge managed-UI contract
from `Engine/Public/SpriteForgeUIInterop.h`. The engine intentionally provides
no legacy entry points or compatibility version query, so the public header,
native DLL, and managed structures must come from the same SpriteForge build.
The ABI remains generic; no Spelljammer setting name or rule is defined in the
engine. Native calls stay on the WPF owner thread. UTF-8 accessibility names
are copied during a revision-checked transactional commit, and actions,
element snapshots, and tagged presentation records are copied into
caller-owned bounded arrays.

## Profile and file contract

The current profile schema is 2. Volumes are integers from 0 through 100 and
interface scale is an integer percentage from 75 through 150. Language is one
of `en-US`, `fr-FR`, or `zh-Hant-TW`; resolution is `desktop`, `1280x720`,
`1600x900`, `1920x1080`, or `2560x1440`. The strict JSON codec rejects duplicate or unknown
properties, malformed input, unsupported schema versions, unknown stable
options, out-of-range values, trailing data, and documents larger than 64 KiB.
Schema 1 profiles migrate in memory to schema 2 with `en-US` and `desktop`
defaults while retaining all prior values. Invalid input resolves to safe
defaults with a stable diagnostic; it is never partially published.

The current-user path is:

```text
%LOCALAPPDATA%\Spelljammer\settings.v1.json
```

The historical filename remains stable so existing profiles are discovered;
the `schemaVersion` field inside the document is the authoritative format
version.

Apply encodes the complete candidate, writes a uniquely named temporary file
in the same directory, flushes it durably, decodes and compares the staged
artifact, and then replaces only the exact settings target. Replacing an
existing target records `settings.v1.json.recovery`. A failed stage or replace
keeps the previous active profile and target. Recovery validates the exact
recovery artifact before staging another durable exact-target replacement;
cleanup deletes only that recovery path.

## Presentation and localization

The settings source strings live in the `en-US`, `fr-FR`, and `zh-Hant-TW`
catalogs under `Content/Packs/base/Localization`. The offline compiler produces
validated artifacts before WPF compilation and embeds them in the application. At
startup the game-owned localization runtime decodes all installed application
catalogs, then stages and publishes the selected locale and its explicit
fallback on the UI thread. Applying a new language republishes the locale and
rebuilds the main-menu presentation text and copied accessibility names.
SpriteForge receives copied resolved accessible names and stable numeric
element/action keys; it never receives localization catalog storage or
translated identity.

SpriteForge returns tagged, clipped presentation commands and document-logical
element snapshots. The current WPF host realizes solid commands and draws the
resolved game-localized text because it does not yet own native FontSystem text
layout handles. Direct realization through SpriteForge's renderer and a native
UI Automation bridge for this managed child surface remain planned;
keyboard-only operation is implemented, but full screen-reader integration
must not yet be claimed.

The implemented settings overlay uses an original three-category layout:
General contains language and resolution, Audio contains the volume controls,
and Interface contains accessibility and interface-scale controls. Language
and resolution open engine-anchored modal option menus. SpriteForge places the
popup relative to its selector, flips or clamps it inside the logical safe
area, dismisses it on an outside press, and restores focus by stable key. The
popup receives input exclusively while open, and Cancel or Escape closes the
popup before it can close the settings overlay.

## Determinism and deferred work

Desktop resolution maximizes the WPF window. Fixed resolution presets restore,
center, and clamp the window to the current desktop work area; they do not
change the monitor mode or renderer resolution.

The local profile is not stored in a campaign, content lock, semantic
fingerprint, replay, or simulation state. Reduced motion changes only the
presentation timer. Audio, subtitle, screen-shake, and interface-scale values
are retained now, but their live consumers await those presentation systems.

Campaign-affecting settings remain authoritative save work. They require an
explicit campaign schema revision, stable IDs and resolved values, validation
before publication, and migration coverage. They must not be added to this
local preference document.

## Verification ownership

`Tests/Spelljammer.Settings.Tests` is a compile-only contract executable for
codec determinism, schema-1 migration, stable language/resolution validation,
invalid-input fallback, transactional publication, replacement failure,
recovery, and exact cleanup. SpriteForge's
`UIInteropTests.cpp` compiles the current ABI, transactional mutations,
anchored/nested modal behavior, copied layout/presentation, and failed-batch
atomicity. CI or the user owns executable test runs under repository policy.
