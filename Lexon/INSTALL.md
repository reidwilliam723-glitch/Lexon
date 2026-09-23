# Install and start Lexon

Friends: you only need Windows. **Do not install Visual Studio or .NET.**

## 1. Install

1. Download **`Lexon.exe`** from the link you were sent.
2. Double-click it. There are no options to pick — it installs and starts Lexon for you.
3. If **Windows protected your PC** (SmartScreen) appears:
   - Click **More info**
   - Click **Run anyway**

Lexon installs for your user account only, so it does not ask for an administrator password.

## 2. Start the app

1. Lexon starts automatically right after installing. Later, use the **Lexon** shortcut on your Desktop or Start menu.
2. Windows may ask to allow the app. Allow it, or suggestions will not appear in other programs.
3. Complete the short setup wizard if it appears.
4. Look in the **system tray** (bottom-right, near the clock). You may need to click the **^** arrow to find the Lexon icon.

Lexon stays running in the tray. There is no big main window on purpose.

## 3. Confirm it works

1. Open **Notepad**.
2. Type `helo` (the misspelling). A small list should appear.
3. Press **Tab**. It should become `hello` (or similar).
4. Type `he are` and pause. You should see a grammar line like `he are → he is`. Press **Tab**.

If nothing appears, see [GUIDE.md](GUIDE.md#troubleshooting).

## 4. Stop Lexon

Right-click the tray icon → **Exit**.

Closing Notepad does not close Lexon.

## 5. Updates

Lexon checks GitHub for a newer version each time it starts. **Nothing you type is included in that check.**

If there is an update, Lexon downloads it quietly and then asks whether to restart. Nothing changes until you click **Yes**. You can also check any time: tray icon → **Check for updates…**. To stop the startup check, untick it in Settings → General.

## 6. What not to touch

- You do not need to run `Lexon.Service.exe` separately. The Lexon shortcut is enough.
- Do not delete files from the install folder. Lexon will not start without them.

## Optional: Settings and AI

Right-click the tray icon → **Settings**.

Dictionary, spelling, and grammar work **without** an API key. Cloud rewrite/AI only works if you add a key (OpenAI, Gemini, DeepSeek) or run Ollama locally. Testers can skip this.

## Uninstall

Exit Lexon from the tray, then remove it from **Settings → Apps → Installed apps → Lexon**.

Optional leftover data (safe to delete after uninstalling):

`%LocalAppData%\Lexon`
