# Lucent 0.6.3 — player, settings and interaction polish

Includes the adaptive-surface changes in [0.6.2](CHANGES-0.6.2.md) and typography/settings/player work in [0.6.1](CHANGES-0.6.1.md).

- Artwork is checked every 250 ms rather than every two seconds. Background and surface blends take approximately 300 ms; backdrop regeneration is debounced by 35 ms. Provider publication delay remains outside Lucent's control.
- The representative artwork hue supplies flat muted cards/options and richer hover labels. The sidebar now continues down through Backgrounds and Settings, with a subtler opaque palette-derived shade.
- Music leaves the sidebar. A single miniature-player button sits at the bottom right in a small rounded dock. It toggles the floating player open/closed; the global shortcut uses the same behavior.
- Mini-player progress shading spans the whole window, including behind the controls, in fixed neutral gray. The bottom seek line also spans the full width. Artwork no longer colors either progress indicator. Pause bars are shorter, with slightly rounded ends.
- The player paints an opaque surface through its native edges, avoiding mismatched WPF/native corner curves. DWM is asked to round the native window and suppress its border. Windows versions that do not support these attributes retain their native fallback.
- Right-click opens the actual Windows window menu, using the supported [SystemCommands.ShowSystemMenu API](https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemcommands.showsystemmenu?view=netframework-4.8.1). Move, Minimize, Close and disabled unavailable commands follow the window's real state. The player remains fixed size.
- Settings no longer captures/renders a screenshot on each opening. Its layout stays measured while hidden; a cached main layer receives a live blur during the transition. The overlay gear occupies the original gear's exact coordinates, and only one is drawn. Opening retains a short slide/fade. Closing keeps the width, compresses vertically by 3% from a bottom anchor and moves down only 4 px, concentrating motion at the top.
- Window controls have short hover fades, a red Close highlight and subtle palette-derived highlights for Minimize/Maximize.

## Evidence and remaining checks

Framework64 compilation, the expanded UI suite and internal capture passed. Tests exercise exact gear alignment/no duplicate original, live blur cleanup, retained hidden settings layout, rapid toggles, page preservation, bottom-anchored closing, dock open/close, no obsolete music rail item, full-width neutral progress and fixed-canvas pause geometry, in addition to the previous palette/font/GIF/binding checks. Warm/cool pages, settings and player progress before/behind/after the controls were rendered and inspected. The 0.6.2 GIF suite passed; unchanged logic and isolated Boost suites passed during this polish session.

Native corner appearance and the real Windows right-click menu still need a visual test. Sustained transition FPS and provider-specific artwork latency are not established by compilation or offscreen rendering. Computer Use was stopped with physical Escape while preparing the newer test launch, so no further desktop input was sent. The running 0.6.1 instance was left untouched after that stop. User approval remains pending.

Folder-only distribution, no ZIP or preview images. Old version folders, source/resources/helpers and compatibility settings paths remain preserved. No game, display mode, security service, startup entry or system setting was modified for this visual pass.
