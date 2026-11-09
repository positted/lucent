# Current release: Lucent 0.6.3

See [CHANGES-0.6.3.md](CHANGES-0.6.3.md) for current validation evidence. The notes below are retained history and do not certify live features in the current release.

# Verification — orbital preview 0.1

The separate new x64 WPF app and multi-size icon compile successfully with `source/build-local.cmd`. No original release, original user settings, installer or startup entry was replaced. The prior application manifest and all C# helpers remain archived in source/legacy; the new app has its own asInvoker, per-monitor-DPI manifest.

Passing isolated logic checks cover neutral identity, grayscale, red-only luminance, channel-neutral dimming, finite high-strength matrices, different Video/Resolution icon paths, independent profile copies, XML save/load and atomic backups, system/Riot/lucent process protections, and construction/disposal without automatically starting software effects. These are computational/policy checks, not live color-driver tests.

The UI harness renders all seven pages, including scroll-bottom captures where needed, with TestMode preventing persistence and live actions. It asserts the lowercase title, distinct icons, effects/native color/zoom/crosshair/audio inactive on launch, and a routed Escape that keeps the window visible. No physical Escape is injected. The old app's actual settings.ini and desktop-vibrance.ini were checked before/after and remained byte-identical. Renderings were visually inspected for page layout, readable slider values, dark pickers and grouped Settings rows.

RAM cleanup is implemented using EmptyWorkingSet on freshly revalidated, explicitly selected optional processes. A disposable 64 MB allocation helper was started for a focused check: cleanup revalidated and trimmed only that fixture, observed a 77 MB working-set reduction including its runtime pages, and the helper exited normally through a stop file. It has not been executed on real user apps. Working-set reduction is not equivalent to committed-memory release or a proven FPS improvement. No process was terminated, no system cache purged, and no game was started or stopped for verification.

The software color/zoom helper, native driver bridge, recovery journaling, audio limiter and restore paths are implemented but have not been exercised against the actual desktop/audio device in this preview. Real native API success, error/restore behavior, parent-crash recovery, per-game automatic switching and effect continuity still need controlled live tests. The matrix checks do not establish clinical effectiveness of color-vision remaps, anti-cheat acceptance or equivalent output to PlexusX.

No Windows startup registration, installer/UAC, elevated security path, reboot, HDR behavior, monitor/DPI matrix, touch routing under zoom, OBS/Discord/exclusive-fullscreen capture, media-session playback, system-audio audition, Discord service acknowledgment, update-feed request or sustained performance benchmark was tested. Media/audio/online controls remain opt-in; integrations need real destinations. No administrator or security setting was changed.

## Engineering references

- [WPF rendering overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/wpf-graphics-rendering-overview): retained visual rendering rather than repainting every control on every scene tick.
- [Windows Magnification overview](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/magapi/magapi-intro), [full-screen color transform](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetfullscreencoloreffect), [zoom transform](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetfullscreentransform): system software color/zoom APIs, including global state and input-routing limits. No input-transform privilege or permanent bitmap-smoothing setting is requested.
- [EmptyWorkingSet](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-emptyworkingset) and [Windows working sets](https://learn.microsoft.com/en-us/windows/win32/memory/working-set): selected-process resident-page trimming and its limits.
- [PlexusX website](https://plexusx.lol/) was fetched as a public reference on 3 November 2026 after the web reader could not open it. Its site describes a software color layer, all-monitor saturation and borderless/windowed scope, and says v2.1.16 corrected a persistent smoothing setting. These are vendor statements, not independent compatibility tests. The new code does not set that Windows smoothing flag. No website instructions concerning warnings, sign-in, purchases or downloads were followed.

The final packaged executable was launched on the desktop and inspected with Computer Use. Video and Settings were opened; navigation to Game Boost ran its read-only CPU/RAM scan. It showed six optional app groups, all unselected, and displayed the RAM cleanup action. No real app was selected, closed, restored or trimmed. Native colors, software effects, zoom, crosshair, audio limiting and integrations stayed off. The lowercase window title and white in-app orb were visible. The separately rendered Windows icon was inspected as a black orb on a white rounded square.

Motion Blur was removed in 0.5.26 after the 0.5.25 GPU overlay froze the display. Keep it out of releases until capture feedback and recovery have been tested in isolation. The 0.5.25 folder preserves the old implementation.
