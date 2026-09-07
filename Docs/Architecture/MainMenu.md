# Main menu architecture

## Implemented slice

Spelljammer starts in `MainMenuWindow`. Its current actions are New Game, Game
Settings, and Quit Game. New Game opens the first character-creation UI; it
does not yet advertise Continue or connect the retained expedition prototype.

The authored background is:

```text
Content/Packs/base/Assets/UI/MainMenu/Background.png
```

The WPF project links that exact base-pack file as a compiled application
resource. `SpriteForgeMainMenuView` loads it without filesystem discovery and
draws it edge-to-edge with aspect-preserving cover scaling. The main window
opens maximized. The background contains no interactive or localized text.

## UI ownership

The single current SpriteForge managed-UI contract owns the retained main-menu
grouping, transactional creation, fixed logical layout, button state, modal
trapping, pointer hit testing, and copied stable actions. Its authoritative
presentation mapping converts WPF host coordinates to document-logical pointer
coordinates. The managed WPF host realizes the authored image, copied tagged
and clipped solid presentation commands, hover outline, and game-localized
text. The menu grouping and button fills are transparent so the labels appear
directly over the authored background. The outline follows only the copied
bounds of the button under the pointer. Main-menu selection and activation are intentionally
mouse-only; keyboard navigation and acceptance are not forwarded to the
SpriteForge document. Settings and character creation retain their documented
keyboard behavior.

The logical menu canvas is 1280 by 720 pixels. It is uniformly scaled and
centered inside the client area, while the background independently uses cover
scaling so resizing does not distort the artwork or controls. The dark right
side of the composition holds the transparent menu labels and preserves the
ship silhouette on the left.

A localized version label is right-aligned at the bottom-right of the logical
canvas. Its value comes from the WPF application's informational-version
metadata with build metadata removed; `VersionPrefix` is currently `0.1.0`.
This keeps the visible version aligned with the executable rather than with an
independent presentation constant.

## Localization and lifecycle

Player-visible strings are authored in the `en-US`, `fr-FR`, and
`zh-Hant-TW` menu, settings, and creation catalogs under
`Content/Packs/base/Localization`. The offline compiler builds all nine
catalogs before WPF compilation and embeds the artifacts. The application
stages and publishes all three complete namespaces in the selected locale on
the UI thread before constructing the menu. Applying a language change
republishes the catalogs and rebuilds the retained menu document.

New Game and Game Settings add modal overlays to the existing main-window
visual tree; neither creates a second operating-system window or taskbar entry.
Character creation visually replaces the complete client area with a 1600x900
dossier. Its persistent left roster directly selects any of the 11 authored
first-voyage captain templates, while the preview and detail panels expose the
current lineage, heritage, Background, summary, and explicit rerollable voyage
seed. Confirm currently returns the selection to the host and reports it on the
main menu; campaign construction, persistence, and launch remain planned. Quit
Game emits a copied stable action and requests ordinary application shutdown.
Closing the operating-system window has the same shutdown result. Native UI
documents are destroyed on their owner thread when the window unloads.

The base content manifest does not yet declare a general runtime asset root.
The main menu and character-creation dossier share this explicitly linked
backdrop; generalized pack asset loading, hot reload, and mod-provided
presentation assets remain planned.
