# Release verification

## Version 1.2.1

- 65 app checks passed, including preview bounds, cache reuse, suspended history,
  background startup, and preservation of the active document and undo/redo.
- The packaged executable passed all 65 checks on the three-monitor desktop.
- 23 installer lifecycle checks passed. The installed executable also passed
  60 headless app checks using its bundled runtime.
- The native tray popup was reviewed at 125% Windows scaling, including all four
  actions, the configured shortcut hint, and Windows 11 rounded corners. The
  selected-row renderer was also checked in the generated tray preview.
- A controlled history workload used 29 generated 1080×660 images and one narrow
  20×3600 image, followed by eight saves. Process working set after the saves was
  166 MiB before the fix and 77 MiB after. Total thumbnail pixels fell from
  7,907,600 to 707,740; the eight saves took 598 ms before and 108 ms after.

These measurements describe that synthetic history workload, not a guaranteed
memory footprint for every capture. The active document, crop undo states,
pinned images, WPF, and Windows rendering still require memory.

## Version 1.2.0

Verified on Windows on September 6, 2026.

- Release build: no warnings or errors.
- 52 app checks passed, including actual desktop capture and Windows hotkey conflicts.
- 23 installer lifecycle checks passed using an isolated test product.
- Installed executable passed 47 headless app checks with its bundled runtime;
  five checks requiring an interactive desktop were explicitly skipped.
- Generated documentation preview was visually reviewed. It renders the actual
  dark editor with a sample image and annotations; no personal captures are used.
- Dark setup destination and startup/shortcut screens were reviewed interactively.
  The running-app prompt clearly explains how to quit from the tray or editor.

Installer coverage includes a fresh install, paths containing spaces, payload
integrity, Start menu and desktop shortcuts, Installed apps registration, startup
opt-in/out, upgrade preference preservation, running-app guards, and uninstall.
Uninstall preserves user data and does not delete startup registration that now
points at a different copy of the app.

## Earlier interactive verification

The dark editor, settings, palette, ring drawing, undo/redo, clipboard round-trip,
countdown cancellation, instant capture during a countdown, and shortcut recorder
were verified interactively. Capture was tested across a monitor boundary with a
negative desktop origin, including the gap below a shorter adjacent display.

Automated coverage includes negative coordinate geometry, every annotation
renderer, opaque redaction pixels after PNG export, undo/redo branching, crop
restoration, move snapshots, pixelation, PNG/JPEG/BMP roundtrips, history retention
and path boundaries, and settings recovery/migration.

## Limits of verification

Windows sign-in/reboot startup, HDR displays, rotated monitors, remote desktop,
and every possible scaling combination have not been physically tested. Startup
registration and its lifecycle are checked without changing the user's real
startup preference. See the [user guide](docs/guide.md) for capture limitations.

Detailed local test reports are generated under `artifacts/` and are excluded
from source control. GitHub Actions publishes its own test reports as artifacts.
