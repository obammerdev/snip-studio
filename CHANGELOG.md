# Changelog

## 1.2.4 — 2026-09-06

- Added consistent slim, rounded dark scrollbars to history, the canvas, dialogs,
  and menus, with full-size drag targets and subdued hover feedback.
- Fixed native minimum sizes overriding scrollbar styling and horizontal sizing.
- Refined recent-capture cards, spacing, tooltips, and keyboard focus indicators.
- Kept the tool rail comfortable at smaller window sizes, and improved settings
  and shortcut layouts with wrapping labels and sensible minimum widths.
- Added scrolling and multiline-input checks, plus compact documentation previews.

## 1.2.3 — 2026-09-06

- Replaced the capture selection renderer with a cached native preview. Dimming
  happens once; dragging repaints changed strips, the outline, and the size label.
- Removed expensive full-desktop redraws and large antialiased-outline buffers.
- Released native preview buffers as soon as capture finishes or is canceled.
- Kept selection coordinates in physical pixels across mixed-DPI monitors and
  used the mouse-release position for accurate fast drags.
- Added capture-preview pixel, geometry, repaint-region, and timing checks.

## 1.2.2 — 2026-09-06

- Defaulted to software rendering to avoid persistent GPU driver buffers in a
  small 2D editor, especially after captures across several monitors.
- Added an optional hardware acceleration setting for very large images;
  changing it takes effect after quitting and reopening the app.
- Kept bundled runtime assemblies uncompressed so Windows can map them from the
  executable instead of inflating them into private memory. Downloads remain compressed.
- Released temporary image buffers after work settles even with the editor open.
- Added a repeatable real-window capture lifecycle probe and rendering preference tests.
- Fixed the version shown in Settings to follow the actual app version.

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
