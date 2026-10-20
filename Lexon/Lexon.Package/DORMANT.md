# Store packaging

`AppxManifest.xml` is a full-trust desktop manifest. Tiles live in `Assets`.
The GitHub installer remains the Velopack package from `Settings\release.ps1`.

Build a Store package with:

```powershell
cd Settings
.\pack-msix.ps1 `
  -PackageName "<Partner Center Identity Name>" `
  -Publisher "CN=<Partner Center publisher GUID>" `
  -PublisherDisplayName "<Publisher display name>"
```

Partner Center → Product identity supplies those three values. Store mode refuses
`CN=WGR`. Omit `-SelfSign` for upload (the Store signs the package). Use
`-SelfSign` only for local sideload testing.

## Behaviour notes

- Keyboard hook and tray run inside `Lexon.Settings.exe` (not a separate Service process).
- Packaged builds skip Velopack and all update UI.
- "Start with Windows" uses StartupTask `LexonStartup` on `Lexon.Settings.exe`.
