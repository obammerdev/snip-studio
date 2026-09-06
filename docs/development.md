# Build and release

## Requirements

- Windows 10/11 x64.
- .NET SDK 10.0.400 or a later .NET 10 feature band.
- PowerShell 5.1 or newer. PowerShell 7 is used in CI.
- Internet access for the initial restore and installer compiler download.

The app uses WPF, Windows Forms for its tray icon, and native Windows APIs. No
third-party application packages are required. `NuGet.Config` keeps packages in
`.tools/packages`; `build.ps1` prefers `.tools/dotnet/dotnet.exe` if present.

## Commands

```powershell
.\build.ps1 -Test
.\build.ps1 -HeadlessTests
.\build.ps1 -Publish
.\build.ps1 -Installer -Test
.\scripts\test-installer.ps1
```

`-Publish` writes a standalone app to `app/`. `-Installer` publishes to
`artifacts/publish/`, then writes these files to `release/`:

- `SnipStudio-Setup.exe`
- `SnipStudio-Portable-win-x64.zip`
- `SHA256SUMS.txt`

The installer compiler is Inno Setup 6.7.3, downloaded from its official GitHub
release. The helper verifies its pinned SHA-256 and Authenticode publisher before
extracting it into `.tools/inno`. Build tools are excluded from the repository.

Both distributions include the .NET runtime and its license notices. They are
unsigned unless a maintainer adds a signing step with a certificate they own.
If signing is added, sign the app before packaging, sign the installer afterward,
and regenerate the published checksums after all signing steps.

## Tests

The application test runner writes `self-test-results.json` and exits 0 on success.
Failures exit 1 and write `self-test-failed.txt` in the selected data directory.

```powershell
.\app\SnipStudio.exe --self-test --data-dir "$PWD\artifacts\manual-tests"
```

Tests cover annotation pixels, undo/redo, crop restoration, image codecs, history,
settings recovery and migration, and negative monitor coordinates. Full desktop
mode also verifies Windows hotkey registration/conflicts, display enumeration,
and actual capture. `--headless` explicitly skips those five desktop checks.

The installer smoke test compiles a separate test product ID, uses isolated
installation paths and startup/shortcut names, and checks fresh installation,
startup opt-in/out, upgrade preferences, and uninstall. It does not change the
installed Snip Studio product or its capture history.

## Documentation preview

```powershell
.\bin\Release\net10.0-windows\SnipStudio.exe --render-preview "$PWD\docs\images\editor.png"
```

This renders the real WPF editor using a generated sample image and isolated
temporary storage. It never captures the desktop or loads the user's images.
Icon artwork can be regenerated with the Python scripts in `scripts/` (Pillow
required); the generated assets are already committed for ordinary builds.

## Publishing a release

1. Update the project version and changelog.
2. Run the complete desktop tests and installer smoke tests locally. Verify the
   installer UI and a capture crossing monitor boundaries.
3. Push the source and confirm the Windows build workflow passes.
4. Create a version tag such as `v1.2.0` on that commit. Attach only the three
   release files above to its GitHub release, with release notes.

The workflow builds and tests each push and pull request; it does not publish
releases automatically. Screenshots, settings, credentials, and local test output
must stay out of commits and release attachments.

## Installer behavior

Setup writes to `%LOCALAPPDATA%\Programs\Snip Studio` by default. It registers a
Start menu shortcut and an entry in Windows Installed apps. Startup uses the same
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SnipStudio` value as the app,
with a quoted executable path followed by `--background`.

An existing app mutex blocks setup and uninstall until the user quits through
the tray or Ctrl+Q. Processes are never forcibly terminated. Uninstall removes the
startup value only if it still points at that installation. User settings and
captures under `%LOCALAPPDATA%\SnipStudio` are never part of the uninstall file list.

Silent installation is available for managed deployments:

```powershell
.\SnipStudio-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="startup,desktopicon"
```

Use `/TASKS=""` to turn both optional tasks off. Omitting `/TASKS` preserves the
existing startup and shortcut choices, or leaves them off for a new installation.
