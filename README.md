# ScreenShottr

A Windows screenshot and annotation tool inspired by the speed and precision of Shottr
for macOS.

## Current features

- Area capture across the virtual desktop
- Selected-window capture using the visible Windows frame bounds
- Full-screen capture
- Repeat-last-area capture
- Three-second delayed full-screen capture
- Notification-area capture menu and background operation
- Single-instance activation
- Compact post-capture preview with Copy, Save, Edit, and Close
- Persistent settings stored under the current Windows user profile
- Post-capture routing to preview, editor, copy-only, or save-only
- Automatic copy and automatic save controls
- Configurable default folder and filename tokens
- PNG and JPEG output
- Optional Start with Windows registration
- Global capture shortcuts:
  - `Ctrl+Shift+1` — area
  - `Ctrl+Shift+2` — full screen
  - `Ctrl+Shift+3` — window
  - `Ctrl+Shift+4` — repeat area
- Automatic clipboard copy after capture
- PNG Save As
- Rectangle, oval, line, arrow, text, freehand, highlighter, and step annotations
- Crop with `Enter` to confirm
- Select, move, edit, and delete annotations
- Undo and redo for annotation creation, deletion, movement, text edits, and crop
- Per-monitor-V2 DPI awareness
- Visible error messages when capture, copy, or save fails

Early scrolling-capture helper code exists, but that workflow is not yet exposed as a
finished feature.

## Planned features

The detailed feature inventory and phased roadmap are in
[PRODUCT_FEATURES_AND_PHASE_PLAN.md](PRODUCT_FEATURES_AND_PHASE_PLAN.md).

Near-term work includes configurable global hotkeys, which are intentionally deferred
until a later phase.
- A production-quality annotation editor

OCR, QR recognition, privacy tools, pinned screenshots, scrolling capture, sharing, and
automation are later phases and are not currently implemented.

## Technology

- C# and WPF
- .NET 9 for Windows
- `System.Drawing.Common` for the current bitmap capture and compositing layer

## Run

1. Install the .NET 9 SDK.
2. Clone the repository.
3. Run:

   ```powershell
   dotnet restore
   dotnet run
   ```

## Validation

Build the application:

```powershell
dotnet build --no-restore
```

Run the dependency-free Phase 0 geometry checks:

```powershell
dotnet run --project tests/ShottrClone.PhaseZeroChecks.csproj
```

This project is under active development.
