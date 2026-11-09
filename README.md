<div align="center">

<img src="docs/images/social-preview.png" alt="Lucent: display, games and music in one small Windows app" width="900">

<br>

### Lucent
**Display, games and music in one small Windows app. Up to 300% vibrance.**

Free and open source (MIT) · Windows 10 / 11 · **[Download Lucent-Setup.exe](https://github.com/positted/lucent/releases/latest/download/Lucent-Setup.exe)**

<a href="https://ko-fi.com/lucent_"><img src="docs/images/kofi-banner.svg" alt="Buy me a coffee. Support Lucent on Ko-fi" width="328"></a>

</div>

## What it does

- **Vibrance up to 300%**, a red filter, screen dimming and color-blindness modes, for each display
- **Resolution and refresh rate** switching with a click or a shortcut
- **Profiles**: save your settings, or tie them to a game and Lucent switches while it runs
- **Crosshair, zoom, Dark Boost, Color pop and a sound radar**
- **Boost**: see what uses memory and close only what you pick
- **A player** with synced lyrics, a floating mini player and Discord Rich Presence
- **Colors that follow the music**: the whole app takes its colors from the album that is playing

<p align="center">
<img src="docs/images/colors-follow-music.gif" alt="The app's colors changing from one album cover to the next" width="860">
</p>

<table>
<tr>
<td align="center"><img src="docs/images/player.png" alt="The player, with the artist's picture and lyrics" width="430"><br><sub>The player</sub></td>
<td align="center"><img src="docs/images/games.png" alt="Profiles, with your games found from your launchers" width="430"><br><sub>Profiles</sub></td>
</tr>
<tr>
<td align="center"><img src="docs/images/color.png" alt="Color, with vibrance at 300%" width="430"><br><sub>Color</sub></td>
<td align="center"><img src="docs/images/boost.png" alt="Boost" width="430"><br><sub>Boost</sub></td>
</tr>
</table>

<sub>Lyrics and the PC numbers in the pictures are made up for the pictures. Album covers and game pictures belong to their artists, labels and publishers.</sub>

## Questions

<details>
<summary><b>Windows or my antivirus warns about the setup. Why?</b></summary>
<br>
Lucent is a small app by one person and is not code-signed yet, so Windows SmartScreen shows "unknown publisher" and a few antivirus engines flag it by guesswork (machine learning, not a named virus). Nothing in Lucent sends your data anywhere or touches your files. The whole source is here, so you can read it or build it yourself with <code>source/build-local.cmd</code>. If your antivirus flags it, please <a href="https://github.com/positted/lucent/issues">open an issue</a> with the detection name.
</details>

<details>
<summary><b>Will it get me banned in a game?</b></summary>
<br>
Lucent never reads or injects into a game and never touches anti-cheat such as Vanguard. It only changes your display and colors, and only when you ask.
</details>

<details>
<summary><b>What does it send over the internet?</b></summary>
<br>
Only the update check (GitHub's latest release for this repository, which you can turn off), the lyrics and artist pictures if you switch them on (lrclib.net and Deezer), and Discord Rich Presence through your own Discord app. No account, no tracking.
</details>

<details>
<summary><b>For developers</b></summary>
<br>
WPF, C# 5, .NET Framework 4.8. Build with <code>source/build-local.cmd</code>, then run <code>source/test-local.cmd</code> (logic) or <code>source/test-ui.cmd</code> (the UI in test mode). The source is in <code>source/</code> and the tests in <code>tests/</code>. See <a href="CONTRIBUTING.md">CONTRIBUTING</a> and <a href="THIRD-PARTY-NOTICES.md">THIRD-PARTY-NOTICES</a>.
</details>

## License

[MIT](LICENSE). Made by [@positted](https://github.com/positted), with [Claude](https://claude.com/claude-code) as a contributor. [Report a bug](https://github.com/positted/lucent/issues) or say hello in [Discussions](https://github.com/positted/lucent/discussions).