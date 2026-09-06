# Release verification

## Version 1.2.4

- 81 app checks passed, including scrollbar sizing, synchronization on both axes,
  scrolling to content edges, automatic visibility, and multiline text input.
- 23 installer lifecycle checks passed. The installed binary passed 76 headless
  app checks with its bundled runtime, covering the new scrolling behavior too.
- Normal and compact editor layouts and compact preferences were rendered from
  the actual WPF controls and visually reviewed using generated sample captures.
- Native capture behavior and the memory optimizations from 1.2.3 are unchanged.

## Version 1.2.3

- 76 app checks passed, including native preview color preservation, dimming,
  changed-region repainting, reverse square drags, and physical pixel bounds.
- 23 installer lifecycle checks passed; the installed binary passed 71 headless
  app checks using its bundled runtime.
- The capture workload exercised a 6400×1440 virtual desktop across three
  monitors, growing and shrinking the selection through 120 updates per cycle.
  Native update-to-paint time averaged 4.2 ms on the first cycle and 2.5 ms on
  subsequent cycles. The 95th percentile was 11.8 ms initially and 4.5 ms warmed.
  These measure completed application painting, not display scanout latency.
- Private committed memory settled at 40–52 MiB after the capture windows closed.
  The editor's visible history and undo/redo remained intact through idle cleanup.
- A real mouse drag crossed the negative-origin monitor boundary and returned
  a 1531×337 image to the editor at the correct physical selection bounds.
- Interactive window and screen selections returned the expected physical
  dimensions; Escape canceled a capture while preserving the current image.

## Version 1.2.2

- 68 app checks and 23 installer lifecycle checks passed. The installed binary
  passed 63 headless checks using its bundled runtime.
- A real-window lifecycle workload reproduced high memory using the previous
  rendering and packaging settings. It displayed generated 6400×1440 desktop
  pixels across three monitors, selected 800×450 captures, and hid/reopened the
  editor three times. The production idle timer ran without extra collections.
- After the third capture settled in the tray, private committed memory was
  517 MiB with compressed assemblies and GPU rendering, 441 MiB with uncompressed
  assemblies and GPU rendering, and 46 MiB in the final 1.2.2 build. Total working
  set fell from 469 MiB to 132 MiB; this includes shared pages.
- With the editor still open, the final build settled at 51 MiB of private
  committed memory after three seconds. The active document and visible history
  stayed available. Undo/redo survived background cleanup.
- Interactive checks covered real captures, ring drawing, Ctrl+Z/Ctrl+Y, and the
  Performance setting at 125% display scaling. During small-image editing the
  new build used about 56 MiB of private working set, compared with 343 MiB in the
  previously running 1.2.1 process. These were different live editing sessions.

These are observations on this GPU/driver and monitor setup, using small snips.
They do not guarantee a fixed footprint for large images, pins, or crop history.
Software rendering trades some drawing CPU work for lower persistent graphics
memory. Hardware acceleration remains an explicit preference requiring a restart.

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
