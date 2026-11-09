# Lucent 0.6.1 — 4 November 2026

This polish pass continues the existing WPF app. The displayed name is Lucent, except for lowercase lucent in settings. Existing configuration paths, namespaces, worker locks, display functions, crosshair, profiles and Game Boost protections remain compatible.

## Interface changes

- Bundled Inter 4.1 with Display text formatting, fixed hinting, pixel rounding and grayscale antialiasing. Settings use compact 14 px semibold labels and 13 px descriptions. Hovering no longer scales or dims button text. Liner's Inter family is retained; WPF and Chromium still rasterize text differently.
- Stronger artwork-derived ambient colors. A dithered color field replaces the stepped linear gradient, crossfades between palettes, and is generated off the UI thread. Resize and intensity changes are debounced. Custom GIFs retain their own palette.
- 312 × 48 mini player with 30 px circular artwork, a smaller heart, thicker pause bars, actual progress shading behind the song details and a full-width rounded seek line with no thumb or side margins. Keyboard seeking is retained. Favorites are local and do not change a streaming service's library.
- Artwork is refreshed independently of title changes; unchanged image bytes are reused. Metadata is checked again after thumbnail reads to discard mixed song/image results during track changes. Player polling is 500 ms, with locally interpolated progress.
- Normal taskbar minimization is the default, including a one-time migration of the previous tray default. Minimize to tray remains an explicit option, preserved once selected in this version.
- Liner settings were inspected using Computer Use: search-first navigation, Interface/Application groups, compact label/description/control rows and shortcut tiles. The available sections are Appearance, Overlay, Shortcuts, General and About, plus a link to Backgrounds. Playback and the excluded service/help/storage categories are absent. Startup, audio limiting, binds and legacy-resolution import remain accessible.
- Settings retain slide/fade opening and a blurred snapshot of the main app. Closing fades and shrinks the card to 95%, preserving the current page. Player topmost and opening position can be adjusted in Overlay.
- Symmetric vector gear. Main sidebar icons brighten without gray hover/selection tiles. Tooltips wait one second, align to the icon's vertical center and fade/slide into place. Section navigation has a short fade and 6 px slide.

## Validation evidence

- Framework64 build completed successfully.
- Current logic/state/color, GIF, Game Boost safety and UI suites passed. Disposable RAM and Boost fixtures were used; no user application was trimmed or closed for validation.
- UI tests cover bundled font resolution and text formatting, capitalization, minimize migration and taskbar visibility, transparent icon buttons, tooltip delay/centering, settings groups and rapid toggles, 95% closing scale, page preservation, Escape unbinding, GIF import/preview/apply/removal, crosshair outline inputs, progress endpoints, same-title late artwork updates and local favorites.
- Internal renders were inspected for settings, player states, feature pages and distinct warm/cool adaptive palettes. Capture exits cleanly with live effects and persistence disabled. The initial background-generation threading fault found during capture was corrected by explicitly marshaling completed images to the WPF dispatcher.
- A real Windows media-session read succeeded with a session available. No playback or seek commands were sent by this check.

These checks establish compilation, isolated behavior and rendered layout. They do not certify sustained animation frame rate, perfect font equivalence to Chromium, every music provider's artwork timing, native playback/seek, display/HDR/capture compatibility, live game/anti-cheat behavior, UAC/startup/reboot or the full DPI/multi-monitor matrix. User approval remains pending after opening the test build. Motion blur remains removed.

## Distribution

Folder only, without ZIPs or UI preview images. Keep Lucent.exe beside its DLLs, licenses and fonts folder. Previous releases are preserved separately. No service, system font installation, display mode application or Windows tweak is performed by building the app.
