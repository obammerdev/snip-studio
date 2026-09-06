# Snip Studio user guide

A native Windows capture and annotation app. Runs locally with no account or cloud service.

The charcoal interface uses dark window frames and controls with a saturated annotation palette. The default is a strong red (`#E52535`) with a 5-pixel stroke. Upgrades from early versions refresh the preset palette once; custom colors, widths, shortcuts, and capture preferences are retained.

## Start

Install **[SnipStudio-Setup.exe](https://github.com/obammerdev/snip-studio/releases/latest/download/SnipStudio-Setup.exe)** and open Snip Studio from the Start menu. Or extract the portable ZIP and open `SnipStudio.exe`. Both include the runtime; no .NET installation is needed. Keep the portable executable in a permanent location before enabling startup.

**Default global shortcut: Ctrl + Alt + Shift + S.** It starts an instant region capture and ignores the selected delay. It also replaces an active countdown with an immediate region capture. The app checks Windows for shortcut conflicts. If a combination is unavailable, choose another in Settings; the existing registration remains intact.

In **Settings**, enable **Launch when I sign in to Windows** to start quietly in the system tray. Startup is off initially. Closing the main window keeps the shortcut available by default. Double-click the tray icon to reopen; right-click it to capture or quit. **Ctrl + Q** quits completely. Running the executable again brings the existing app forward.

## Capture

- **Region:** drag any rectangle, including across monitor boundaries. Shift constrains to a square.
- **Window:** click a visible window. Captures its on-screen pixels, with the extended frame excluded where Windows provides its bounds. Areas covered by other windows remain covered.
- **Screen:** click the monitor to capture.
- **All displays:** capture the entire virtual desktop at full pixel resolution.
- **Delay:** no delay, 3, 5, or 10 seconds. Applies to New snip and Ctrl + N.
- **Cancel:** Escape or right-click during selection; Escape or Cancel during countdown.
- The editor and pinned images hide during capture. Mouse pointer capture is optional in Settings.

The process declares Per-Monitor V2 DPI awareness. Desktop pixels, virtual monitor origins (including negative coordinates), selection coordinates, and output dimensions are kept separate from WPF's logical UI units. Mixed-resolution layouts and differently scaled displays are supported. Gaps between displays appear black.

## Edit

| Tool | Key | Behavior |
| --- | --- | --- |
| Select & move | V | Select an annotation, then drag. Delete removes it. Double-click text to edit. |
| Pen | B | Freehand strokes with adjustable color and width. |
| Highlighter | H | Translucent strokes. |
| Arrow / line | A / L | Shift snaps to 45-degree angles. |
| Rectangle / ring | R / E | Shift makes a square or circle. |
| Text | T | Click to add a multiline label. Ctrl + Enter applies. |
| Numbered steps | N | Click to place automatically numbered markers. |
| Pixelate | P | Drag to obscure detail with averaged pixel blocks. |
| Solid redaction | D | Drag to cover an area with opaque black. |
| Crop | C | Drag and release to crop. Undo restores the full editable document. |

Use the palette or enter a `#RRGGBB` color. Change stroke width with the size slider, and label/step size with the pixel-size dropdown. Selecting an annotation lets you update its style. Escape cancels an in-progress stroke or clears a selection.

**Ctrl + Z** undoes; **Ctrl + Shift + Z** or **Ctrl + Y** redoes. Up to 100 edit transactions are retained during the current editing session. Crop flattens existing annotations, with the prior editable state retained in undo. Selecting an annotation supports moving and deleting it; resizing a shape means undoing/redrawing it.

## Copy, save, and reuse

- **Ctrl + C:** copy the edited full-resolution image to the clipboard, including PNG data for applications that support it. Clipboard-busy errors retry briefly.
- **Ctrl + S:** export PNG, JPEG (95% quality), or BMP. JPEG uses a white background for transparency. Saving prompts before overwriting.
- **Ctrl + O / Ctrl + V:** open a file or paste an image. You can also drop an image file onto the window. PNG, JPEG, BMP, GIF (first frame), and TIFF are supported. Other formats depend on installed Windows image codecs.
- **Ctrl + P:** pin a copy above other windows. Drag to move, resize the window, right-click for options, or double-click/Escape to close it.
- **Ctrl + mouse wheel / + / −:** zoom. **Ctrl + 0** fits the image; **Ctrl + 1** displays actual pixels. Scrollbars pan larger images.
- **F1:** shortcut reference. **Ctrl + ,:** settings.

Automatic clipboard copying after capture is on initially. After editing, use Copy image again to copy the edited version.

## Local history and settings

Software rendering is the default to reduce persistent graphics memory. If
drawing or zooming unusually large images feels slow, enable **Use hardware
acceleration** under **Settings → Performance**, then quit and reopen the app.
Hardware acceleration can use substantially more memory, depending on the GPU
driver and display setup. This preference affects Snip Studio only.

Temporary capture and export buffers are collected after image work settles,
including when the editor stays open. Memory can briefly rise while capturing.

History previews are bounded in width and height and reused between edits. When
the editor is hidden or minimized for three seconds, its previews are released
and rebuilt when needed. The current image and undo/redo remain in memory, so
large captures, pinned images, and crop history can still increase memory use.
Background startup waits to load previews until the editor opens.

Settings live in `%LOCALAPPDATA%\SnipStudio\settings.json`; recent captures live in its `Captures` folder. History defaults to the latest 30 captures; choose 10, 30, or 100. Edits are saved to the current history image after a short idle interval and when switching images or closing the editor. History is stored as flattened PNGs: reopening a capture starts a fresh undo session. No image metadata or original hidden layers are included in exported files.

Solid redaction is included as opaque pixels in saved/copied images. Undo can restore the original while that editing session is open. Pixelation is a visual effect; use solid redaction for sensitive information.

Turning history off pauses new saves. Clear in the sidebar removes existing saved history after confirmation. With history off (or a failed history save), replacing an unsaved image or quitting prompts to save. Exported files are separate from history.

Startup uses only the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SnipStudio` value. Turning startup off removes that value. No administrator rights or global keyboard hook are required. The app registers only its configured hotkey through `RegisterHotKey` with repeat suppression.

## Build and verify

Requires Windows 10/11 x64 and the .NET 10 SDK to build. The packaged executable includes the runtime. See the [development guide](development.md) for installer builds and automated checks.

```powershell
.\build.ps1 -Test
.\build.ps1 -Publish
```

The script prefers a project-local SDK at `.tools\dotnet` if present. No third-party application packages are used; the UI is WPF, with native Windows APIs for capture/hotkeys and WinForms only for the tray icon. Run live capture tests in an interactive desktop session; isolated desktops without capture access cannot run that check.

For an isolated demo with a generated sample image:

```powershell
.\app\SnipStudio.exe --demo --data-dir "$PWD\artifacts\demo"
```

For the automated checks:

```powershell
.\app\SnipStudio.exe --self-test --data-dir "$PWD\artifacts\tests"
```

The process exits 0 on success and writes `self-test-results.json`. Checks cover pixel geometry, negative monitor origins, all annotation renderers, redaction pixels, undo/redo, crop restoration, file exports, local history, corrupt settings recovery, real Windows hotkey conflicts, and full-desktop capture. Demo mode exposes capture windows in the taskbar for UI verification and writes capture-coordinate diagnostics in its isolated data directory.

The app does not yet include scrolling capture, screen recording, OCR, or HDR tone mapping. Protected/secure content may not be capturable through Windows desktop capture. Extremely large images/desktops are limited to 140 million pixels to bound memory use.
