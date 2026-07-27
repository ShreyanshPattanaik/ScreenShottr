# ScreenShottr: Product Feature Inventory and Phase Plan

Last reviewed: 2026-07-27

Reference: [Shottr](https://shottr.cc/) for macOS, including its home page, tips, FAQ,
release history, upload documentation, and URL scheme documentation.

## Product direction

Build a fast, native-feeling Windows screenshot utility for designers, developers, and
people who care about pixel-level precision. Shottr is the product benchmark, but the
Windows app should use Windows conventions: `Ctrl` rather than `Cmd`, the notification
area rather than the macOS menu bar, Windows startup settings, per-monitor DPI, Windows
share/clipboard behavior, and MSIX or another signed Windows distribution format.

The primary product promise should be:

> Capture, inspect, annotate, and share any part of your Windows screen without breaking
> your flow.

## Status legend

- **Added**: reachable from the current UI and substantially functional.
- **Partial**: some code exists, but the user flow or important behavior is incomplete.
- **Not added**: no working user-facing implementation was found.

## Current app audit

The current code builds successfully on .NET 9. The implementation is a compact WPF
prototype with one editor window and separate area/window selection overlays.

### What is already added

| Capability | Status | Current behavior and limitations |
|---|---|---|
| Area capture | **Added** | Multi-monitor overlay, drag selection, `Esc` cancel, and automatic copy after capture. Per-monitor mixed-DPI behavior still needs testing. |
| Full-screen capture | **Added** | Captures the virtual desktop and automatically copies it. DPI calculation currently assumes the editor's display scale, which can be wrong across mixed-DPI monitors. |
| Copy image | **Added** | Copies a composited bitmap to the clipboard. |
| Save image | **Added** | PNG-only Save As dialog with a timestamped name. |
| Rectangle | **Added** | Fixed red outline, fixed width, no style controls. |
| Oval | **Added** | Fixed red outline, fixed width, no style controls. |
| Straight line | **Added** | Fixed red line. |
| Arrow | **Added** | Straight red arrow with a fixed arrowhead. |
| Text | **Added** | Create and edit bold black text. |
| Freehand | **Added** | Fixed red polyline. |
| Highlighter | **Added** | Fixed translucent yellow rectangle. |
| Step counter | **Added** | Creates a numbered marker as one logical editor object so selection, movement, deletion, undo, and redo stay consistent. |
| Crop | **Added** | Drag a crop region and press `Enter`; crop is copied automatically. It clears annotations and has incomplete redo behavior. |
| Select/move/delete annotations | **Partial** | Selection, movement, double-click text editing, grouped markers, and Delete work. No resize handles, rotation, multi-select, or snapping yet. |
| Undo/redo | **Added** | Covers annotation creation, deletion, movement, text edits, and crop undo/redo. Resize history will be added when resize handles exist. |
| Annotation compositing | **Added** | Save/copy flattens the current WPF annotation canvas onto the screenshot. |
| Window capture | **Added** | Window enumeration, hover selection, mixed-DPI overlay conversion, and DWM visible-frame capture are wired into the editor, tray menu, and global shortcut. |
| Repeat and delayed capture | **Added** | The last physical area can be captured again; delayed capture takes the full virtual screen after three seconds. |
| Notification-area operation | **Added** | Capture commands, reopen, and quit are available while the editor is hidden. |
| Global capture shortcuts | **Partial** | Fixed shortcuts cover area, full screen, window, and repeat capture. Shortcut customization is not implemented yet. |
| Single-instance behavior | **Added** | A second launch signals the existing instance and brings its editor forward. |
| Scrolling-capture helpers | **Partial** | Bitmap comparison, vertical stitching, and Page Down injection exist, but there is no capture loop, overlap detection, target selection, failure handling, or UI entry point. |

### Items claimed by the README but not currently implemented

The README should be treated as a vision list rather than current functionality. No
working implementation was found for:

- Production scrolling capture
- Pixelate, blur, erase, or text-only privacy modes
- Spotlight
- Image resize, combine, overlay, rasterize, or expandable canvas
- Screen ruler, guides, magnifier, smart selection, or auto-padding
- Color picker, color formats, contrast checking, OKLCH, or APCA
- OCR or QR recognition
- Pinned screenshots or preview thumbnails
- Customizable hotkeys
- Auto-save settings, configurable filenames, or default save folder
- Opening files, loading from clipboard, drag-and-drop, or Windows “Open with”
- Sharing/upload, S3, upload management, or public links
- Preferences, themes, toolbar customization, notifications, tray behavior, or startup
- Update checking, telemetry preferences, licensing, or packaging

The project file currently references only `System.Drawing.Common`; the README's listed
SkiaSharp, Tesseract, ZXing, and CommunityToolkit dependencies are not installed.

## Complete Shottr reference feature inventory

This inventory consolidates product cards, tips, FAQs, URL schemes, and release notes.
Small bug fixes are omitted unless they reveal a user-facing capability or quality bar.

### 1. Capture

- Full-screen capture
- Area capture
- Active-window and “capture any window” modes
- Window capture presentation modes:
  - transparent background with shadow
  - trimmed shadow
  - shadow on a solid background
  - shadow over wallpaper
- Scrolling capture in either direction
- Automatic and manual scrolling capture
- Reverse scrolling capture
- Configurable maximum scrolling height
- Repeat the previously selected area
- Delayed capture with standard or custom delay
- Add/append another capture to the current canvas
- Freeze-screen workflow through full-screen capture followed by crop
- Optional experimental frozen area capture
- Capture cancellation with `Esc`
- Capture on the monitor where the action began
- Multi-display and mixed-DPI handling
- Configurable inclusion or exclusion of the mouse cursor

### 2. Post-capture routing

- Open the editor
- Show a compact preview thumbnail
- Copy automatically
- Save automatically
- Pin automatically
- Run more than one follow-up action after a capture
- Keep preview visible until the image is used or dismissed
- Open preview above full-screen applications
- Custom confirmation notifications for copy, save, OCR, and upload
- Notification-area animation confirming copy, save, or upload

### 3. Editor and canvas

- Resizable/full-screen editor
- Expandable canvas
- Multiple screenshots side by side
- Combine captures on one canvas
- Crop by selecting and pressing `Enter`
- Reset crop
- Resize image
- Zoom to fit, 100%, in/out, selected region, a corner, or a pointed location
- Smooth mouse/trackpad zoom
- Right-button or Space+drag panning
- Load a PNG/JPEG from disk
- Load an image from the clipboard
- Open by drag-and-drop
- OS “Open with” integration
- Drag the edited image into another application
- Copy only the selected image region
- Copy and paste annotation objects
- Predictable duplicate/paste placement
- Rasterize annotations, overlays, and appended captures into the base image
- Automatic PNG/JPEG choice based on image content
- Optional 1× downscaling
- Correct image DPI metadata
- Print with orientation selection

### 4. Selection and precision

- Select a region or annotation object
- Pixel-precise keyboard nudge and resize in 1 px and 10 px increments
- Expand or contract a selection from all sides
- Force a square selection with `Shift`
- Smart selection / auto-adjust to nearby edges
- Preview smart selection while drawing
- Select a monotone object
- Adjust a single selection edge
- Add padding around a selection
- Move an object while drawing by holding Space
- Duplicate an object with a modifier-drag
- Bring the selected object to the front
- Configurable object snapping
- Logical/physical pixel dimension toggle

### 5. Annotation tools

- Select/crop tool
- Rectangle
- Oval
- Straight line
- Multiple arrow styles
- Narrow and curved arrows
- Bendable/arched arrows
- Text and text labels with a pointer
- Freehand drawing with smoothing and stroke variability
- Highlighter with cap styles
- Spotlight with adjustable background darkness
- Step counter with editable values
- Magnifier callout
- Horizontal and vertical guides that can be imprinted
- Screen ruler measurements that can be imprinted
- Classic and hand-drawn styles

### 6. Annotation styling

- Custom colors
- Stroke thickness
- Line style
- Fill color
- Fill opacity
- Tool-specific sizes
- Pixelation strength
- Live size preview
- Keep the selected tool active for repeated annotations
- Object attribute controls that do not obscure tool selection

### 7. Privacy and raster editing

- Pixelate a selected area
- Blur a selected area
- Remove/erase selected objects
- Text-only blur
- Text-only erase/removal while preserving surrounding content
- Small-area pixel scrambling for stronger redaction
- Rasterize overlays before applying privacy tools

### 8. OCR and QR

- Hotkey-driven OCR area selection
- Copy recognized text to the clipboard
- QR-code recognition
- OCR language preference
- Remove line breaks on demand
- Preference to remove OCR line breaks automatically
- Whitespace cleanup

### 9. Developer and design inspection

- Pixel-level zoom/magnifier
- Copy the exact color under the pointer
- Copy the darkest color in a nearby text-sized region
- Copy the average color of a selected region
- Multiple color output formats, including HEX without `#` and OKLCH
- Contrast checking
- WCAG-style and APCA contrast modes
- Measure object width/height
- Measure distance between nearby objects
- Logical vs physical pixel measurements
- Imprint measurements and guides on the screenshot

### 10. Screenshot presentation and comparison

- Gradient or solid backdrop
- Rounded screenshot corners
- Adjustable shadow
- Padding
- Paste image overlays
- Overlay opacity for visual comparisons
- Side-by-side comparisons
- Two-frame before/after GIF export

### 11. Pinned screenshots

- Borderless always-on-top image windows
- Multiple pinned references
- Resize pinned images with the mouse wheel
- Semi-transparent pinned images without shadow for overlay comparison
- Pin as a post-capture action

### 12. Storage, clipboard, and sharing

- Configurable default screenshot folder
- Save and Save As
- Timestamp-based names that sort chronologically
- Auto-save and auto-copy
- PNG and JPEG output
- Robust interoperability with third-party clipboard managers
- Upload to Shottr Cloud and copy a public link
- Upload to any S3-compatible provider
- Manage and delete previous cloud uploads
- Clear warning that public uploads are not private storage

### 13. Preferences and system integration

- Customizable global capture shortcuts
- Shortcut for reopening the editor
- Configurable `Esc` behavior
- Default zoom/window size
- Keep editor always on top
- Hide splash screen
- Show/hide menu-bar icon (Windows equivalent: notification-area icon)
- Launch at login (Windows startup)
- Toggle telemetry
- Notifications: custom, system, or disabled
- Single-instance behavior: launching again brings the existing instance forward
- Reopen editor when the app icon is activated
- Menu commands for file, edit, draw, capture, and settings
- Update checking and update notifications
- Raycast and Alfred integrations on macOS
- URL/deep-link API for automation

### 14. Automation/deep links

Shottr exposes commands to:

- Show the app
- Capture full screen, area, repeat area, window, or scrolling content
- Reverse scrolling direction
- Start delayed capture with a chosen delay
- Append a capture
- Start OCR
- Load from clipboard or file
- Open uploads
- Open settings
- Chain capture completion actions: copy, save, edit, pin, or thumbnail

For Windows, add a custom `screenshottr://` URI only after the command model is stable.
A command-line interface is also valuable for PowerToys, AutoHotkey, scripts, and
enterprise deployment.

## UI language notes

### Product voice

Shottr's language is:

- Short, direct, and task-focused: “Crop the image,” “Pin Screenshots,” “Copy average
  color.”
- Benefit-first rather than implementation-first: “Unclutter your desktop,” “Zoom in
  on your pixels.”
- Friendly and lightly playful: “those who care about pixels,” “It's that simple!”
- Precise when discussing developer workflows: exact keys, pixel increments, color
  formats, DPI, and contrast methods.
- Honest about limitations and privacy, particularly around scrolling capture and
  public uploads.
- Written in sentence case, with compact labels and minimal jargon.

### Interaction vocabulary to retain

Use consistent verbs:

- **Capture** for taking a screenshot
- **Area**, **Window**, **Full screen**, **Scrolling**, **Repeat area**, and **Delayed**
  for capture modes
- **Copy**, **Save**, **Pin**, **Edit**, and **Preview** for completion actions
- **Select**, **Crop**, **Resize**, **Rasterize**, **Combine**, and **Add capture** for
  canvas operations
- **Pixelate**, **Blur**, and **Erase** for privacy tools
- **Recognize text** for the visible command; use “OCR” as supporting terminology
- **Copy color**, **Measure**, **Guide**, and **Magnifier** for inspection tools

Avoid “grab” in the visible Windows UI even if it is used internally. “Capture” is more
familiar in Windows screenshot tools. Prefer **Full screen** as two words. Prefer
**Settings** over “Preferences” on Windows.

### Windows shortcut language

- Display shortcuts as `Ctrl+C`, `Ctrl+S`, `Ctrl+Z`, and `Ctrl+Shift+S`.
- Use `Win+Shift+...` only for global capture shortcuts and ensure they do not collide
  with Windows Snipping Tool or accessibility shortcuts.
- Use `Alt` where Shottr documentation uses `Option`.
- Use the Windows terms **notification area**, **File Explorer**, **Start with
  Windows**, and **always on top**.

### Recommended visible UI labels

Primary capture menu:

- Capture area
- Capture window
- Capture full screen
- Capture scrolling
- Repeat last area
- Delayed capture…

Primary editor actions:

- Copy
- Save
- Share
- Pin
- Add capture
- Undo
- Redo

Tooltips should be compact and show the shortcut on a second line where applicable,
for example: `Rectangle` then `R`.

### Visual language

The Shottr website uses a warm, approachable editorial style: large expressive headings,
neutral dark-gray body text, rounded product imagery, spacious sections, and bright
feature illustrations. The observed website typography pairs a slab-style display face
with a clean geometric sans-serif body face. The app should not copy macOS chrome;
translate the same clarity into a compact Windows editor using a neutral canvas,
high-contrast icons, restrained accent color, 4–8 px spacing increments, and controls
that work well at 100–200% display scaling.

## Phased delivery plan

### Phase 0 — Make the prototype trustworthy

Goal: turn the current code into a reliable baseline before adding breadth.

- Correct the README so implemented and planned features are distinct.
- Introduce a simple document model for the base bitmap and annotation objects.
- Make compound annotations, especially the step counter and arrowhead, single logical
  objects.
- Track add, delete, move, resize, crop, and text edits in undo/redo.
- Add disposal and memory checks for repeated large captures.
- Fix mixed-DPI coordinate conversion using per-monitor physical bounds.
- Add basic tests for capture coordinate conversion, crop, compositing, and undo/redo.
- Add a lightweight error surface instead of silent no-ops.

Exit criteria:

- Existing functions behave consistently across two monitors with different scaling.
- Ten consecutive captures/edits do not leak obvious resources or corrupt undo history.
- README matches the actual UI.

### Phase 1 — Fast Windows capture MVP

Goal: make the app useful as a daily Windows capture replacement.

Status: **Implemented**, except customizable shortcut assignment, which is intentionally
deferred. Fixed shortcuts remain available for the four primary capture actions.

- Add a notification-area icon and capture menu.
- Add single-instance behavior.
- Wire window capture to the existing window picker and capture helper.
- Add repeat-area and delayed capture.
- Register configurable global hotkeys with safe defaults.
- Add post-capture routing: editor, preview, copy, and save.
- Add Settings for default folder, filename pattern, auto-copy, auto-save, and startup.
- Support PNG/JPEG and Save/Save As.
- Add a compact preview thumbnail with Copy, Save, Pin, and Edit actions.
- Add user-facing success/failure notifications.

Exit criteria:

- Area, window, full-screen, repeat, and delayed capture work from global hotkeys and
  the notification-area menu.
- A user can capture and copy without opening the full editor.
- Windows startup and settings persist correctly.

### Phase 2 — Production annotation editor

Goal: reach a polished core editor before advanced imaging features.

#### Phase 2A — Canvas foundation

Goal: make navigation and coordinate handling reliable before expanding the editor.

- Introduce a proper zoomable canvas and viewport model.
- Add zoom in/out, 100%, fit, selection zoom, and smooth mouse-wheel zoom.
- Add right-button and Space+drag panning.
- Keep capture pixels, annotations, hit testing, crop regions, and exported output in
  the same coordinate system at every zoom level.
- Add robust crop and Reset Crop.
- Add image resize with correct aspect-ratio and DPI handling.
- Extend history so canvas and image operations are undoable and redoable.

Exit criteria:

- Existing annotations behave identically from minimum to maximum zoom.
- Crop, resize, copy, and save use image coordinates rather than window coordinates.
- Canvas operations do not change exported pixels unless the user requested an edit.

#### Phase 2B — Object manipulation

Goal: make every existing annotation behave like a professional editable object.

- Add visible selection and resize handles.
- Add pixel-level keyboard movement and larger Shift increments.
- Add annotation duplication, copy, and paste.
- Add bring forward, send backward, bring to front, and send to back.
- Add multi-selection and grouping.
- Add optional snapping and alignment guides.
- Ensure compound tools remain single logical objects.
- Add undo/redo for movement, resize, duplication, grouping, and layer changes.

Exit criteria:

- Every annotation can be selected, moved, resized, duplicated, reordered, and deleted.
- Multi-object operations create one predictable history entry.
- Object manipulation remains accurate at every zoom level.

#### Phase 2C — Annotation capabilities and styling

Goal: complete tool behavior and expose a stable styling model before redesigning UI.

- Add color, stroke thickness, fill, opacity, and line-style properties.
- Add text size and appearance controls.
- Finish rectangle, oval, line, arrow, text, freehand, highlighter, spotlight, and step
  counter behavior.
- Add arrowhead choices and curved arrows.
- Add adjustable highlighter opacity and spotlight darkness.
- Add optional hand-drawn styles.
- Make style changes undoable and reusable as defaults for the next annotation.

Exit criteria:

- Every supported property updates selected objects immediately.
- New objects inherit the current tool defaults consistently.
- Copy/save output matches styled objects in the editor pixel-for-pixel.

#### Phase 2D — File and image workflows

Goal: make the editor useful with images that were not captured in the current session.

- Open PNG and JPEG files.
- Load an image from the clipboard.
- Support drag-and-drop into and out of the editor.
- Add Windows “Open with ScreenShottr” integration.
- Copy only the selected image region.
- Rasterize annotations into the base image.
- Preserve dimensions, DPI, transparency, and output format where appropriate.

Exit criteria:

- Disk, clipboard, drag-and-drop, and capture inputs enter the same editor pipeline.
- Rasterization is undoable and produces deterministic output.
- Opened images can be edited, copied, and saved without losing expected metadata.

#### Phase 2E — Editor UI and visual polish

Goal: apply the visible redesign only after canvas, object, tool, and file contracts are
stable.

- Replace text-heavy toolbar buttons with a compact icon toolbar.
- Add accessible names, tooltips, shortcut hints, and keyboard navigation.
- Add a contextual properties panel for the selected tool or object.
- Add clear selection, hover, snapping, and disabled states.
- Improve empty canvas, loading, error, and unsupported-file states.
- Make the layout adapt to window size and 100–200% display scaling.
- Apply consistent spacing, typography, colors, icons, and Windows 11 interaction
  patterns.
- Run an accessibility and contrast review.

Exit criteria:

- Every edit is undoable/redoable.
- Tools behave correctly at different zoom levels.
- Copy/save output matches the editor preview pixel-for-pixel.
- The UI remains usable by keyboard and at supported Windows display scales.

### Phase 3 — Privacy and inspection toolkit

Goal: deliver the features that differentiate the app from basic snipping tools.

- Add pixelate, blur, and erase.
- Add text-aware blur/erase after OCR infrastructure is available.
- Add pixel color picker and selectable output formats.
- Add average color and text-color sampling.
- Add screen ruler, distance measurement, guides, and auto-padding.
- Add smart/monotone selection.
- Add magnifier and magnifier callout.
- Add contrast checking, OKLCH, and APCA after the basic color workflow is stable.

Exit criteria:

- Redaction output is irreversible after export and tested on small text.
- Measurements are correct in both logical and physical pixel modes.
- Color results are verified against known test images.

### Phase 4 — OCR, QR, pinning, and multi-image workflows

Goal: complete the high-value productivity layer.

- Add Windows OCR with language selection and line-break cleanup.
- Add QR decoding.
- Add borderless always-on-top pinned images with resize and opacity.
- Add multiple pinned screenshots and pin-after-capture.
- Add combine/add capture, expandable canvas, and side-by-side layout.
- Add image overlays with opacity and alignment controls.
- Add backdrop presets with padding, rounded corners, gradients, and shadows.
- Add before/after GIF export.

Exit criteria:

- OCR and QR work offline on representative screenshots.
- Pinned images behave correctly across virtual desktops and multiple monitors.
- Multi-image projects export deterministically.

### Phase 5 — Scrolling capture

Goal: ship scrolling capture only when it is reliable enough to trust.

- Let the user pick a scrollable window or region.
- Implement automatic scrolling with overlap detection and content-aware stitching.
- Mask fixed headers/footers and detect repeated content.
- Support scrolling up and down.
- Add manual scrolling as a fallback.
- Add max-height, delay, direction, and cursor settings.
- Show clear progress, stop, failure, and partial-result states.
- Test browsers, Office apps, chat clients, File Explorer, terminals, Electron apps, and
  mixed-DPI monitor layouts.

Exit criteria:

- Common web pages stitch without duplicated or missing rows.
- Failure produces a usable partial image and a clear explanation.
- Manual mode works where automatic scrolling is blocked.

### Phase 6 — Sharing, automation, and release engineering

Goal: make the app distributable and extensible.

- Add optional S3-compatible upload and copy-link.
- Add upload history and deletion.
- Display an explicit public-link/privacy warning.
- Add a command-line interface and custom URI scheme.
- Support chained post-capture actions.
- Add update checking and signed updates.
- Package and sign the app; choose MSIX or a signed installer based on hotkey/startup
  constraints.
- Add crash reporting and opt-in, privacy-respecting telemetry.
- Add accessibility review, localization readiness, and performance profiling.
- Publish privacy policy, license terms, and support documentation.

Exit criteria:

- Installation, update, uninstall, startup, and protocol registration are tested on
  supported Windows versions.
- Network features are optional and never required for local capture/editing.
- Automation commands are documented and stable.

## Recommended implementation order

Do not implement the reference list in Shottr's historical release order. The highest
value path for this codebase is:

1. Reliability and architecture
2. Capture speed and Windows integration
3. Editor quality
4. Privacy and inspection
5. OCR/pinning/multi-image
6. Scrolling capture
7. Upload, automation, and distribution

Scrolling capture is intentionally late: the current helper methods are not close to a
robust implementation, and shipping naive Page Down plus vertical concatenation would
create visible duplicates, omit content, and fail in many Windows applications.

## Immediate next milestone

The next milestone should be **Phase 0 plus the first half of Phase 1**:

- Fix the feature claims.
- Introduce reliable editor state and undo/redo.
- Correct DPI handling.
- Wire window capture.
- Add notification-area behavior and global hotkeys.
- Add repeat and delayed capture.

That milestone converts the project from an editor demo into a credible Windows capture
utility without taking on OCR, image analysis, or scrolling-capture risk too early.
