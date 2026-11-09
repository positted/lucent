# Lucent 0.5.29 — 4 November 2026

- Custom GIF playback follows WPF composition timing, independent of eclipse animation. Frames remain decoded off the UI thread and cached; hidden/inactive playback pauses. GIFs retain their source timing (minimum 20 ms for nonzero short delays).
- Built-in scenes now use resolution-independent vector animation. Orbital Lines becomes Orbital Flow. The eclipse has traveling orbit lights and a breathing corona. Scene scaling is uniform and popup previews follow the window's proportions.
- Custom imports are copied into the app's local data folder and appear in Your backgrounds. Each has Preview, Use background and an animated trash action with red hover. Built-ins cannot be removed. Removing an active custom background restores Eclipse only. Removing a library entry never deletes the user's original imported file.
- Selected navigation and ordinary buttons use translucent surfaces. Noninteractive page/scroll containers no longer receive the dotted keyboard-focus frame. The long navigation label is now Visual tools; Dark Boost and Zoom remain separate tools within it. Video's tagline is removed.
- RAM cleanup uses a full-contrast button, vector spinner, pulsing meter and actual before/after physical RAM readings. The first reading appears directly instead of counting from zero. This is a presentation change, not a promise to reclaim a fixed amount of RAM.
- Resolution pair has side-by-side size selectors, a clear destination and retained-Hz status. Pair choices are filtered to modes supported at the current refresh and depth. Nothing is applied while choosing or on startup.
- Scaling adds Aspect ratio, No scaling, Full screen and Integer for NVIDIA displays. Display configuration is read on opening the page, never written. Explicit clicks modify only the selected target's scaling field and confirm it by reading back. Cloned/non-NVIDIA configurations are unavailable. Other unsupported modes report failure when requested.
- Crosshair Outline expands inline with its own color field, hue strip, RGB/hex controls and swatches, plus Show outline. Previews and overlay use the selected outline color. Existing settings default to black outlines.

## Verification

Build: build-local.cmd passed. Logic tests and isolated RAM fixture passed; no real app cleanup was used for this update's tests. GIF disposal, exact frame delays, replacement/cancellation, pause/hide and frozen image tests passed. UI tests passed custom import/apply/remove, preservation of original imports, built-in deletion rejection, running GIF clock with eclipse off, focus styling, outline picker/render combinations and scaling-field isolation. Full application capture harness launched successfully and rendered pages and distinct background previews; representative images were reviewed. These internal images are not included in the delivered folder.

Driver finding: NVIDIA's documented VALIDATE_ONLY call returned -1 but changed the secondary display's reported scaling from 6 to 8 during probing. The secondary was restored to its recorded original value 6 and read back; primary remained 2. Automatic validation probes have been removed from the app and test helper. ScalingReadOnly now only reads configuration. Four-mode application across all monitors has not been certified. Reference: https://docs.nvidia.com/nvapi/group__dispcontrol.html (NV_SCALING and NvAPI_DISP_SetDisplayConfig).

No sustained frame-rate benchmark, native border test across DPI settings, in-game zoom/color test, startup/UAC/reboot test or real media playback test was performed for this update. No game was launched or closed. Motion blur remains removed because of the prior display freeze.

Distribution: folder only, no ZIP, no preview images. Runtime is under release; source, tests and required build assets are preserved separately. The existing 0.5.28 runtime is retained.
