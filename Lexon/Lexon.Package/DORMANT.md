# Store packaging

`AppxManifest.xml` is a full-trust desktop manifest for Lexon 1.10.7. The files in `Assets` are real PNG tiles. This folder is not built by `Settings\release.ps1`. The GitHub installer remains the Velopack package.

Still required before a Partner Center upload:

- Set `Identity Publisher` to the exact publisher value from Partner Center. `CN=WGR` is only a stand-in.
- Reserve the package name `WGR.Lexon`, or change `Identity Name` to the name you reserved.
- Host `documents\PRIVACY_POLICY.md` at a public `https` address and paste that URL into the submission.
- Pack the published `win-x64` app with this manifest. Do not turn Lexon into a sandboxed Store app. It has to type into other programs, so the package needs `runFullTrust`.
- Leave the GitHub updater out of the Store build. The Store delivers those updates.
