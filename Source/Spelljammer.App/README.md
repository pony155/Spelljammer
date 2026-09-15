# Spelljammer.App source briefing

`Spelljammer.App` is the Windows x64 .NET 10 WPF executable and the current
application entry point. It owns window lifetime, presentation orchestration,
and the narrow managed/native bridge to SpriteForge. Authoritative gameplay,
settings persistence, and localization behavior remain in their respective
game-owned class libraries.

This briefing covers authored project and source files. Generated `bin/` and
`obj/` files are build output and are not part of the source inventory.

## Current startup path

```text
App.OnStartup
  -> show borderless SpriteForge startup splash
  -> load GameSettingsRegistry on a worker thread
  -> load embedded application catalogs through GameText
  -> create MainMenuWindow
  -> close startup splash after the first menu render is scheduled
       -> SpriteForgeMainMenuView
            -> New Game     -> GalaxyMapGeneratorScreen
                                  -> GalaxyMapGeneratorView
                                  -> CharacterCreationScreen
                                       -> SpriteForgeCharacterCreationView
            -> Game Settings -> GameSettingsDialog
                                  -> SpriteForgeSettingsView
                                  -> GameSettingsRegistry.Apply
            -> Quit Game     -> Application.Shutdown
```

The application currently opens the main menu. The retired expedition window
and parallel expedition simulation remain removed. Every current screen owns a
SpriteForge child surface; WPF remains only the top-level window and lifecycle host.

## File inventory

### Project root

| File | Responsibility |
| --- | --- |
| `Spelljammer.App.csproj` | Declares the WPF `WinExe`, .NET 10 Windows target, x64 platform, application `VersionPrefix`, project references, embedded localization catalogs, and the packaged menu/creation backdrop. Its pre-compile target builds the `menu`, `settings`, `creation`, `galaxy`, and `calendar` source catalogs into bounded `.sfloc` artifacts. Setting `CopySpriteForgeNativeRuntime` activates the repository-level target that copies SpriteForge DLLs from `SpriteForgeNativeDir`. |
| `App.xaml` | Declares the WPF application type and application resource scope. It deliberately has no `StartupUri`; startup is orchestrated in code so settings can be loaded before a window is published. |
| `App.xaml.cs` | The executable startup boundary. It temporarily uses explicit shutdown mode, shows and advances the SpriteForge splash, resolves the per-user settings path, loads the settings registry away from the UI thread, loads localized application text, creates `MainMenuWindow`, and then makes that window the normal shutdown owner. |
| `AssemblyInfo.cs` | Configures WPF theme-resource lookup. There is no theme-specific dictionary; fallback resources are resolved from the source assembly. |

### `Hosting/`

| File | Responsibility |
| --- | --- |
| `StartupSplashWindow.cs` | Borderless startup-only HWND host for the SpriteForge splash. It owns no drawn controls and disposes the native-backed splash surface when startup completes. |
| `MainMenuWindow.cs` | Current maximized top-level window and application flow coordinator. It owns the shared settings registry, settings path, and `GameText`; reads the display version from assembly metadata; hosts `SpriteForgeMainMenuView`; coordinates the retained galaxy and captain drafts; adds new-game or settings screens to the same visual tree; forwards status to the menu; shuts down on Quit; and detaches/disposes the views when closed. |
| `GalaxyMapGeneratorScreen.cs` | Full-window in-app host for the galaxy generator. It forwards completion and cancellation and owns deterministic view and native-surface cleanup. |
| `CharacterCreationScreen.cs` | Full-window in-window modal host for `SpriteForgeCharacterCreationView`. Its borderless fill-scaling child replaces the menu visually without creating another operating-system window, forwards completion/cancellation, and owns deterministic cleanup. |
| `GameSettingsDialog.cs` | In-window WPF overlay around `SpriteForgeSettingsView`. Its scrim blocks the menu without creating another operating-system window. It starts settings publication on a worker thread, prevents duplicate Apply operations and closure during a write, reports failures without replacing active settings, and emits completion only after successful durable publication. |

### `Presentation/`

| File | Responsibility |
| --- | --- |
| `SpriteForgeStartupSplashView.cs` | Animated 960x540 SpriteForge startup surface with the packaged space backdrop, orbiting star mark, product title, and bounded service-initialization progress. |
| `GalaxyMapSelection.cs` | Immutable new-campaign draft containing the explicit galaxy seed and bounded generator settings accepted by the player. |
| `CharacterCreationChoices.cs` | Bounded application presentation mapping for the 11 authored first-voyage character, Race, Heritage, and shared Background stable IDs. A confirmed selection carries the accepted galaxy seed until campaign composition is implemented. |
| `GalaxyMapGeneratorView.cs` | Full-window 1600x900 SpriteForge galaxy setup surface. It edits and validates a positive 64-bit seed, selects one of six bounded sizes and the Spiral, Elliptical, or Ring topology strategy, exposes Spiral central-bar strength and arm-count controls, invokes the simulation-owned deterministic generator, and submits the complete panel, controls, metrics, routes, systems, and visual star field through the renderer. |
| `GalaxyVisualField.cs` | Bounded presentation-only particle field deterministically derived from the accepted seed and settings. It supplies a distant star field, localized nebulae, adjustable barred or unbarred spiral arms, elliptical dust, or an annular band plus stellar color and brightness hints, but creates no gameplay systems and is never serialized. |
| `SpriteForgeRenderSurface.cs` | Shared `HwndHost` renderer owner for all current screens. It creates the child HWND and ABI v2.6 session, uploads textures including procedural circle and glow masks, realizes quads, lines, and additive particles as sprite batches, creates native font/layout resources, submits localized text through renderer labels, maps HWND input to logical coordinates, and destroys resources before the child window. |
| `EacEatTimestampFormatter.cs` | Presentation-only formatter for localized EAC dates, 24-hour EAT times, and combined timestamps. It consumes `WorldDateTime` projections and never enters simulation or save state. |
| `GameText.cs` | Application localization facade. It reads the embedded `en-US`, `fr-FR`, and `zh-Hant-TW` menu/settings/creation/galaxy/calendar artifacts with size bounds, stages a selected locale and explicit fallback transactionally, supports live locale publication, begins formatting frames, and exposes helpers for static text, option names, formatted values, percentages, and stable diagnostics. |
| `SpriteForgeCharacterCreationView.cs` | Full-window SpriteForge character dossier on a 1600x900 logical canvas. It renders the direct 11-captain roster, portrait treatment, localized lineage/heritage/Background details, summary, and accepted galaxy seed; Escape/Back returns to galaxy setup and Confirm emits a copied selection. |
| `SpriteForgeMainMenuView.cs` | SpriteForge main-menu surface on a fixed 1280x720 logical canvas. The renderer realizes the packaged backdrop, transparent normal menu, hover/press feedback, localized labels, status, and version. Mapped HWND pointer records are processed by a SpriteForge UI document, which owns hit testing and emits only the three top-level navigation actions. |
| `SpriteForgeSettingsView.cs` | SpriteForge settings surface and immutable draft editor on a 960x680 logical canvas. It renders and handles language/resolution cycling, volume and scale sliders, accessibility toggles, Reset, Cancel, Apply, and persistence status without WPF controls or drawing. |
| `SpriteForgeAudioService.cs` | WPF-owner-thread lifetime wrapper for SpriteForge audio. It creates the opaque audio instance from native defaults, applies Master/Music/Sound Effects gains from the active settings profile, pumps maintenance updates on the dispatcher, reports non-fatal availability failure, and destroys the instance at application exit. |

### `Interop/`

| File | Responsibility |
| --- | --- |
| `SpriteForgeNative.cs` | Sole low-level SpriteForge P/Invoke boundary for this application. It mirrors the renderer v2.6, UI, and audio ABIs used by the current application. Its static initializer verifies every used managed structure size before the first native call. Renderer, UI, and audio handles are destroyed on their owner thread, copied arrays are bounded, and no native pointer enters game or save state. |

## Runtime and ownership boundaries

| Concern | Owner in the current application |
| --- | --- |
| WPF startup and window lifetime | `App`, `MainMenuWindow`, `GameSettingsDialog` |
| Current screen interaction state | Game-owned bounded view state fed by child-HWND input |
| Raster, topology, and text realization | SpriteForge renderer ABI v2.6 through `SpriteForgeRenderSurface` |
| Localized application text | `GameText` over `Spelljammer.Localization` |
| Active settings and durable publication | `Spelljammer.Settings` through `GameSettingsRegistry` |
| Native audio lifetime and volume buses | SpriteForge through `SpriteForgeAudioService` |
| Authoritative gameplay state | `Spelljammer.Simulation`; application composition is planned |

The child-HWND surfaces translate physical pointer coordinates into their
logical canvases and submit copied renderer records. WPF never realizes visible
game pixels. Localized text remains presentation data and never becomes command
or simulation identity.

## Build-time inputs outside this directory

`Spelljammer.App.csproj` deliberately links rather than duplicates these files:

- `Content/Packs/base/Assets/UI/MainMenu/Background.png` becomes the shared pack
  resource uploaded to SpriteForge by the menu, creation, and galaxy surfaces.
- `Content/Packs/base/Localization/en-US/menu.sfloc.json` is compiled and
  embedded as `Spelljammer.Localization.en-US.menu.sfloc`.
- `Content/Packs/base/Localization/en-US/settings.sfloc.json` is compiled and
  embedded as `Spelljammer.Localization.en-US.settings.sfloc`.
- `Content/Packs/base/Localization/en-US/creation.sfloc.json` is compiled and
  embedded as `Spelljammer.Localization.en-US.creation.sfloc`.
- The corresponding `fr-FR/menu.sfloc.json` and `fr-FR/settings.sfloc.json`
  catalogs are compiled and embedded for the installed French locale.
- The corresponding `zh-Hant-TW/menu.sfloc.json` and
  `zh-Hant-TW/settings.sfloc.json` catalogs are compiled and embedded for the
  installed Traditional Chinese locale.
- The corresponding French and Traditional Chinese `creation.sfloc.json`
  catalogs are compiled and embedded for both translated locales.
- SpriteForge native DLLs come from the configurable `SpriteForgeNativeDir`;
  no developer-specific absolute engine path belongs in the project.

## Where changes belong

- Add or change top-level window flow under `Hosting/`.
- Add game-specific SpriteForge display adapters under `Presentation/`.
- Extend `SpriteForgeNative.cs` only for a deliberate, versioned public
  SpriteForge C ABI addition.
- Put reusable rendering, platform, input, or UI capabilities in SpriteForge,
  not in this application project.
- Put settings validation/storage, localization runtime behavior, and gameplay
  simulation in their existing class libraries rather than in a window.
- Add player-visible menu or settings wording to the authored localization
  catalogs, not as presentation literals.

## Build and launch

From the repository root:

```powershell
$env:SPRITEFORGE_ROOT = (Resolve-Path ..\SpriteForge)
$nativeDir = Join-Path $env:SPRITEFORGE_ROOT 'build\windows-msvc-debug\release\bin'

dotnet build .\Spelljammer.slnx -p:SpriteForgeNativeDir="$nativeDir"
dotnet run --project .\Source\Spelljammer.App\Spelljammer.App.csproj `
    -p:SpriteForgeNativeDir="$nativeDir"
```
