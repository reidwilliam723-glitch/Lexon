# Lexon 1.0.2

First tester build under the new name — renamed from LexFlow, with two new writing features and a handful of UI fixes.

## New

- **Next-word predictions** — after you finish a word, up to 3 likely next words appear as chips near the caret, learned from your own writing. Accept one with `1`/`2`/`3` or a click.
- **Auto-correct for known typos** — common misspellings (e.g. "teh" → "the") now fix themselves the moment you finish typing them, with a brief flash so it's never silent. Toggle it off anytime in Settings → "Auto-correct known typos." It never touches a word you've already taught Lexon to recognize as yours.
- **Live theme preview during setup** — picking a theme in first-run setup now applies it immediately instead of waiting until you finish the wizard.

## Fixed

- Scrolling the Settings window no longer silently changes whichever combo box the cursor happens to pass over.
- Switching themes now repaints the whole window in one frame instead of visibly cascading top to bottom.
- A combo box could get stuck looking highlighted/selected after closing; fixed.
- Accepting a grammar fix that arrived late (after you'd already kept typing past it) could overwrite the wrong words; Lexon now declines the fix instead of guessing.

## Renamed: LexFlow → Lexon

This is a clean break, not an upgrade. If you have LexFlow 1.0.1 installed:

- It will **not** auto-update to Lexon.
- Your settings and learned vocabulary will **not** carry over.
- Uninstall LexFlow first, then install Lexon fresh below.

## Install

Download **`LexonSetup.exe`** from the assets below and run it. You do not need Visual Studio or .NET installed.
