# Lucent 0.6.2 — adaptive surfaces

Continues the typography, settings, player and animation changes in [0.6.1](CHANGES-0.6.1.md).

Cards now use one flat, opaque, muted shade derived from the artwork's representative hue. Display cards, selected options, buttons, resolution tiles, crosshair preset tiles, settings groups, input fields, picker menus and slider tracks use coordinated lighter/darker shades. These colors are independent of the background's local gradient or GIF pixels. White text, slider fills, actual RGB swatches and primary actions retain their functional colors.

Sidebar hover labels use a richer, more saturated version of the same hue. Palette changes animate together. Turning adaptive colors off restores neutral surfaces. Shared brushes retain animation clocks so WPF cannot freeze style resources before later palette changes.

Framework64 compilation, UI interaction tests and GIF tests passed. Warm/cool renders were inspected. Added UI checks cover flat opaque cards, warm-to-cool option changes, richer tooltip saturation and neutral reset. The previously passing logic and isolated Boost tests apply to unchanged functional code. The 0.6.1 read-only media query succeeded; this update sends no external playback commands. No display settings, games, services or security configuration were changed to test the theme. Live user approval and sustained performance testing remain separate from these results.

Distribution remains a versioned folder only, with no ZIP or preview images. Previous Lucent folders are preserved.
