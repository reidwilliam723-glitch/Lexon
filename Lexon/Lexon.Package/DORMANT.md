# Dormant Store packaging

The `Lexon.Package` MSIX project and `AppxManifest.xml` are **not** part of the
supported release path. Shipping builds use Velopack (`Settings/release.ps1`
and the `publish` folder).

The manifest has unresolved issues (undeclared namespace prefixes, IgnorableNames)
and is not validated. Do not treat it as production-ready Store packaging.

If Microsoft Store distribution is needed later, replace this project with a
validated Windows App SDK / MSIX package that matches the Velopack identity
and capabilities. Until then, ignore this folder.
