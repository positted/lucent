# Contributing to Lucent

Thanks for helping. Lucent is a small Windows app (WPF, C# 5, .NET Framework 4.8), made by one person and Claude, so a short note is enough.

## Ways to help

- **Report a bug or idea:** open an [issue](https://github.com/getlucent/lucent/issues). Say your Windows version, your Lucent version (Settings → About) and what you expected.
- **Send a fix:** keep it small and focused, one change per pull request.
- **Support it:** star the repository, or [Ko-fi](https://ko-fi.com/lucent_) if you want to (never required).

## Build and test

Build only with `source/build-local.cmd`, then run `source/test-local.cmd`. The full list of test scripts is in the README. Please run the logic tests before sending a change, and say what you did and did not test.

## Ground rules

- Keep the existing architecture and dependencies. Do not add network calls, services, drivers or scheduled tasks.
- Lucent must never apply a display mode on start, touch a running game, or touch Vanguard, Windows or security components.
- Lyrics, artist pictures and Discord Rich Presence stay opt-in.

By contributing you agree your work is released under the MIT license.
