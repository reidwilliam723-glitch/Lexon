# Lexon

Lexon is a **Windows typing assistant**. It sits in the system tray and suggests words, spelling fixes, and grammar fixes while you type in other apps (Notepad, Word, browsers, and most text fields).

This copy is meant for **trying the app**, not for developing it.

## What to open

| Item | What it is |
| --- | --- |
| **[INSTALL.md](INSTALL.md)** | How to start Lexon (no extra software) |
| **[GUIDE.md](GUIDE.md)** | How to use suggestions, grammar, rewrite, and Settings |
| **`Run`** | Double-click **`Lexon.exe`** here |
| **`Settings`** | Support files and project source. **Do not edit or delete.** |

You do **not** need Visual Studio, the .NET SDK, or an installer.

## What it does (short)

- Completes words as you type. **Tab** accepts, **Esc** dismisses.
- Suggests grammar fixes automatically (for example `he are` → `he is`). **Tab** accepts.
- Optional rewrite: select text, then use the small **Aa** chip or the rewrite shortcut.
- Learns locally. Cloud AI is **off** unless someone puts an API key in Settings.
- Skips password fields and blocked apps.

## Requirements

- Windows 10 or 11 (64-bit)
- Permission to run an app that is not signed with a company certificate (SmartScreen may warn once)

## Privacy (for testers)

Typing stays on the PC unless you turn on a cloud AI provider in Settings and use rewrite/AI features. See `PRIVACY_POLICY.md` in this folder.

## For Liam (releasing to testers)

Testers now install once from **`Lexon.exe`** and the app updates itself after that. To cut a release, bump `<Version>` in `Lexon.Settings\Lexon.Settings.csproj`, then from this folder:

```bat
powershell -ExecutionPolicy Bypass -File Settings\release.ps1 -Upload
```

That publishes, packs with Velopack, and creates the GitHub release. Requires `dotnet tool install -g vpk` once, and `GITHUB_TOKEN` set. Drop `-Upload` to pack into `releases\` without publishing.

For a quick local run without the installer:

```bat
powershell -ExecutionPolicy Bypass -File Settings\publish-portable.ps1
```

That refreshes **`Run\Lexon.exe`**. Portable builds do **not** auto-update. Do not ask testers to install .NET.
