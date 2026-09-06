# Changelog

## 1.2.1 — 2026-09-06

- Rebuilt the tray menu with flat dark rows, subdued hover states, icons, a live
  shortcut hint, and native rounded corners on Windows 11.
- Bounded history thumbnails in both dimensions and prevented small or narrow
  captures from being enlarged into large previews.
- Reused unchanged thumbnails and sidebar items during autosave.
- Deferred history loading on background startup and released previews after
  three seconds hidden or minimized, preserving the active image and undo/redo.
- Avoided full-image render copies for unedited images and detached the automatic
  clipboard image from its full-desktop capture source.
- Added regression checks for preview caching, idle cleanup, and undo preservation.

## 1.2.0 — 2026-09-06

First public GitHub release.

- Dark Windows installer with optional startup and desktop shortcut choices.
- Installation for the current user without administrator access.
- Upgrades preserve settings, captures, and the app's current startup preference.
- Uninstaller removes the app and shortcuts while retaining saved captures.
- Standalone portable package, runtime license notices, and SHA-256 checksums.
- Public documentation, generated editor preview, and automated Windows builds.
- Headless test mode for CI, with native desktop checks explicitly reported as skipped.

## 1.1.0 — 2026-09-06

- Charcoal editor, dialogs, controls, tray menus, and native window frames.
- Strong red annotations and a saturated preset palette.
- One-time migration of earlier preset colors without resetting custom preferences.

## 1.0.0 — 2026-09-06

- Region, window, screen, and virtual desktop capture.
- Instant configurable hotkey, optional countdowns, and tray startup.
- Drawing, shapes, labels, numbered steps, redaction, pixelation, and crop.
- Undo/redo, image clipboard, export, local history, and pinned images.
