# Lucent 0.6.0 — 4 November 2026

Lucent is now Lucent. The interface uses lowercase **lucent**, while the executable, tray tooltip and Windows product name use **Lucent**. The new abstract folded-light mark has no background in the app. Windows icons use the same mark on a black rounded tile. Existing preferences, saved profiles, favorites, shortcuts and native worker locks retain their previous internal locations for compatibility.

## Interface

- Compact 64 px icon rail, monochrome vector icons, hover labels, new window controls and consistent rounded surfaces.
- Bundled official Inter 4.1 Regular, Medium, SemiBold and Bold. Liner's installed CSS identifies Inter Variable as its default interface font. Static Inter faces support the existing .NET Framework WPF renderer. Grayscale antialiasing replaces ClearType color fringes; headings, controls and small labels use larger/heavier weights.
- Floating settings panel below center, with a blurred snapshot of the app behind it. It slides/fades in and out and restores the current page without rebuilding it. Appearance, shortcut tiles, playback and audio settings remain reachable. Esc during shortcut capture clears the bind; otherwise Esc dismisses settings.
- Adaptive colors blend muted tones from the current custom GIF or Windows media-session artwork into the theme. A neutral dark theme is the default when no image is available. Strength and adaptive coloring can be adjusted in Settings.
- Preset backgrounds and the eclipse have been removed. Only custom GIFs remain, with managed local copies, preview/apply, visibility controls and animated removal. Original imported files are preserved.
- Compact 334 × 52 music player with rounded album art, title/artist, local track favorites, previous/play/pause/next and a thin seek bar. Right-click or Escape closes it. Heart favorites are local to Lucent; they do not modify a music service's library.
- The existing tray menu behavior remains, including switch resolution, navigation and explicit exit.

## Game Boost

- Single process snapshot with executable/publisher identity and per-process interval CPU sampling normalized by processor count. All sizes are included.
- Searchable Apps, Protected and Excluded views. Exclusions persist and are also honored by RAM cleanup for catalog apps.
- Verified Riot Client UI helpers and the VALORANT bootstrap launcher may be reviewed as candidates. Actual game executables, foreground apps/helpers and launcher backends used by running Riot games are protected.
- Vanguard names, files, paths, security products, critical processes, Windows components, driver components, services and other sessions remain protected. Service enumeration is read-only: Lucent does not stop, disable or reconfigure services.
- Closure requires explicit selection and rechecks PID, executable path, process start time, current protection and foreground state immediately before closing. Scanning never closes anything. New/reused processes are not silently substituted for scanned instances.

## Evidence and limits

The Framework64 build passed. Current logic/state/color tests, GIF decoding/cancellation tests, UI interaction tests and Boost safety tests passed. A disposable fixture verified that exclusion and changed start time prevent closure, and only the selected fixture closes. The separate RAM fixture observed about 77 MB working-set reduction. No user app was closed or trimmed to validate this update.

A read-only check on the real desktop examined 231 processes, producing 14 eligible groups and 63 protected groups in 342 ms including CPU sampling. This is a point-in-time detection measurement, not a gaming performance benchmark. A real Windows media-session query succeeded; playback, seek and favorite commands were not sent to external apps. UI capture covered pages, adaptive warm/cool palettes, settings, and compact player states. Computer Use also inspected the test window and settings; desktop input stopped when Escape cancelled Computer Use.

The font/rendering update and final Lucent branding were verified through isolated renders after that cancellation. Live game/anti-cheat behavior, sustained animation FPS, audio limiting, media playback/seek, HDR/capture compatibility, display mode/scaling changes, UAC/startup/reboot and broad DPI/multi-monitor behavior have not been certified by this update. Motion blur remains removed after its previous display freeze.

## Distribution and compatibility

Folder only: no ZIP and no UI preview images. `release/Lucent.exe` needs the adjacent DLLs, license and fonts folder. The original Lucent releases remain separate. No installer, service, new system font installation or Windows configuration change is performed by building or copying the folder. The historical `source/legacy/build.cmd` still retains its original copy behavior; use only `source/build-local.cmd`.

The retained settings folder is `%LOCALAPPDATA%\lucent`. The original WinForms settings remain under `%LOCALAPPDATA%\ResolutionSwitcher`. Normal Lucent/Lucent instances share a single-instance lock so they cannot compete over global display effects.

Font source: https://github.com/rsms/inter/releases/tag/v4.1. Full SIL Open Font License is included under `fonts/LICENSE.txt`. Existing SharpDX MIT license is retained.
