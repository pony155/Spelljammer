# Main menu architecture

## Implemented slice

Spelljammer starts in `MainMenuWindow`. Its current actions are New Game, Game
Settings, and Quit Game. New Game opens the galaxy generator and then the first
character-creation UI; it does not yet advertise Continue or launch an
authoritative campaign.

The authored background is:

```text
Content/Packs/base/Assets/UI/MainMenu/Background.png
```

The WPF project links that exact base-pack file as a compiled application
resource. `SpriteForgeMainMenuView` decodes it without filesystem discovery,
uploads it as a SpriteForge texture, and submits it edge-to-edge with
aspect-preserving cover scaling. The main window
opens maximized. The background contains no interactive or localized text.

## UI ownership

`SpriteForgeRenderSurface` owns the native child HWND and renderer ABI v2.6
session. It maps physical HWND input to the fixed logical canvas, while the
menu keeps bounded hit regions and emits stable managed navigation events.
Background, panels, buttons, hover/press outlines, and localized text are all
submitted to SpriteForge as texture/sprite/font/layout/label resources. WPF
does not draw any visible menu pixel.

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
`zh-Hant-TW` menu, settings, creation, galaxy, and calendar catalogs under
`Content/Packs/base/Localization`. The offline compiler builds all 15 catalogs
before WPF compilation and embeds the artifacts. The application stages and
publishes all five complete namespaces in the selected locale on the UI thread
before constructing the menu. Applying a language change republishes the
catalogs and rebuilds the SpriteForge font/layout resources.

New Game and Game Settings add modal overlays to the existing main-window
visual tree; neither creates a second operating-system window or taskbar entry.
The 1600x900 galaxy generator is the first new-campaign step. It exposes the
explicit deterministic seed, six bounded size presets from 16 through 1,024
systems, and Spiral, Elliptical, and Ring shape strategies. Scenario and
generator version remain fixed first-slice values. Its
read-only topology projection calls the simulation-owned `GalaxyGenerator`,
shows the validated systems and starways without revealing campaign discoveries,
and carries the accepted seed into character creation. Invalid input and failed
validation leave the last accepted draft unpublished.

Character creation is the second new-campaign step and visually replaces the
complete client area with a 1600x900 dossier. Its persistent left roster directly
selects any of the 11 authored first-voyage captain templates, while the preview
and detail panels expose the current lineage, heritage, Background, summary, and
the read-only galaxy seed accepted on the previous screen. Back returns to the
retained galaxy draft. Confirm currently returns both selections to the host and
reports the captain on the main menu; campaign construction, persistence, and
launch remain planned. Quit Game emits a copied stable action and requests
ordinary application shutdown.
Closing the operating-system window has the same shutdown result. Renderer
layouts, fonts, textures, and sessions are destroyed on their owner thread
before each child HWND is released.

The base content manifest does not yet declare a general runtime asset root.
The main menu and character-creation dossier share this explicitly linked
backdrop; generalized pack asset loading, hot reload, and mod-provided
presentation assets remain planned.
