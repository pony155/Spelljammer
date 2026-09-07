# Localization Phases 1 and 2

The Game-owned localization runtime resolves immutable static and typed
messages from validated `.sfloc` artifacts. `en-US` is the source locale;
`qps-ploc` (accented/expanded) and `qps-keyecho` are development-only
transforms. The WPF host now stages embedded `en-US`, `fr-FR`, and
`zh-Hant-TW` `menu`, `settings`, and `creation` catalogs on the UI owner thread,
displays resolved text in the main menu and its in-window flows, and republishes
those surfaces when language changes.
Localization of the older expedition prototype remains planned.

## Runtime contract

- Create stable keys with `LocalizationKey.Create` and locales with
  `LocaleId.Create`. Names remain attached to their 64-bit FNV-1a IDs so a
  collision cannot silently resolve another message.
- Create and initialize one `LocalizationService` on the application/UI owner
  thread. Staging, publication, lookup, diagnostics, and shutdown must remain on
  that thread.
- Pass all installed artifacts to `StageLocale`. Every non-source locale stores
  its complete ordered fallback chain, ending in `en-US`; every catalog for one
  locale must repeat identical profile and fallback metadata.
- Publish only a successfully staged `LocaleGeneration`. Publication swaps one
  complete immutable generation, and a failed stage leaves the current
  generation unchanged.
- `GetStatic` returns copied managed text and locale/generation metadata. With
  `DevelopmentMarker`, a missing key returns `ItemNotFound` and also supplies a
  conspicuous `[missing:key]` message. Diagnostics contain metadata, not message
  contents.
- `Format` accepts at most 32 named `LocalizationArgument` values. Names, kinds,
  fixed scales, and select allow-lists must exactly match the compiled schema.
  Text arguments are inserted as opaque text and are never reparsed.
- Call `BeginFormattingFrame` at the application/UI update boundary. Formatting
  is limited to the configured maximum, at most 4,096 messages per frame.
- Every `LocalizedMessage` carries a copied `LocalizationLanguageProfile` with
  its resolved BCP-47 tag, direction, number symbols, and pinned locale-data
  identity. Engine UI/Text consumers receive that profile and resolved text,
  never Game keys or catalog storage.

Catalogs are limited to 64 namespaces and 100,000 keys per locale, an eight-
locale fallback chain, 127-byte canonical keys, 64-KiB message programs, a
256-KiB formatted output, a 16-MiB per-generation static cache, and a 32-MiB
artifact. Runtime source parsing is intentionally absent.

## Source catalogs

Authored files use strict UTF-8 `.sfloc.json` with schema version 1:

```json
{
  "schemaVersion": 1,
  "locale": "en-US",
  "namespace": "ui",
  "fallbacks": [],
  "textDirection": "ltr",
  "messages": {
    "ui.example.window-title": {
      "description": "Translator context that is not displayed.",
      "arguments": {
        "count": "integer"
      },
      "message": "{count, plural, one {# regiment} other {# regiments}}"
    }
  }
}
```

Root and entry members are strict, duplicate JSON members are rejected, keys
must start with the declared namespace, and empty messages require
`"emptyAllowed": true`. Argument schemas accept `integer`, `unsigned`, `fixed`,
`percent`, `text`, `select`, `boolean`, and `localizable`; fixed and percent
objects declare a `scale` from 0 through 9, select objects declare canonical
`values`, and sensitive text may set `"sensitive": true`.

SpriteForge Message Format supports `{name}`, `{count, number}`, `{ratio,
percent}`, `select`, `plural`, and `selectordinal`. Plural/select branches require
`other`; plural exact cases such as `=0` take priority, and `#` is valid only in
plural branches. A doubled apostrophe produces one apostrophe. A single
apostrophe starts a literal quoted region ending at the next single apostrophe,
which is how literal braces and `#` are authored. Unmatched apostrophes or braces
are errors.

Locale tags use the documented canonical structural subset (for example
`en-US`, `fr-FR`, or `zh-Hant-TW`); registry-backed BCP-47 deprecation and alias
data remains a later pinned-data task.

Phase 2 pins bounded `en`, `fr`, `ru`, `ar`, and `zh`
decimal/percent/plural profiles from CLDR 48.2.0 under Unicode-3.0. These are
formatting fixtures and available runtime profiles. English, French, and
Traditional Chinese application translations ship; Russian and Arabic
translations and production RTL UI do not. No host culture or platform locale
data is consulted.

Compile a catalog from the Spelljammer repository root:

```powershell
dotnet run --project Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj -- `
  compile Content/Packs/base/Localization/en-US/core.sfloc.json out/core.sfloc
```

Generate a deterministic development pseudo-catalog or print a sorted
completeness report:

```powershell
dotnet run --project Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj -- `
  pseudo AccentedExpanded Content/Packs/base/Localization/en-US/core.sfloc.json out/core.qps.sfloc
dotnet run --project Tools/Spelljammer.Localization.Compiler/Spelljammer.Localization.Compiler.csproj -- `
  report Content/Packs/base/Localization/en-US/core.sfloc.json path/to/translation.sfloc.json
```

The compiler sorts artifact key tables by stable ID and canonical name, writes
fixed little-endian typed schemas and validated message bytecode, includes the
compiler and locale-data identities plus a SHA-256 payload checksum, and
re-reads its output through the runtime validator before success. Completeness
reports also identify source/translation schema or selection-structure drift.
Normal builds do not fetch localization data or third-party dependencies.

## Build and test ownership

CMake exposes `SpelljammerLocalizationRuntime`,
`SpelljammerLocalizationCompiler`, `SpelljammerLocalizationCatalogs`, and
(when `BUILD_TESTING` is enabled) `SpelljammerLocalizationUnitTests`. The last
target compiles the headless localization test executable only. CTest registers
it as `SpelljammerLocalizationTests`, but CI/CD owns execution under repository
policy.
