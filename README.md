# ScreenShottr

A Windows screenshot and annotation tool inspired by the speed and precision of Shottr
for macOS.

## Current features

- Area capture across the virtual desktop
- Full-screen capture
- Automatic clipboard copy after capture
- PNG Save As
- Rectangle, oval, line, arrow, text, freehand, highlighter, and step annotations
- Crop with `Enter` to confirm
- Select, move, edit, and delete annotations
- Undo and redo for annotation creation, deletion, movement, text edits, and crop
- Per-monitor-V2 DPI awareness
- Visible error messages when capture, copy, or save fails

Window selection and early scrolling-capture helper code exist, but those workflows are
not yet exposed as finished features.

## Planned features

The detailed feature inventory and phased roadmap are in
[PRODUCT_FEATURES_AND_PHASE_PLAN.md](PRODUCT_FEATURES_AND_PHASE_PLAN.md).

Near-term work includes:

- Window capture
- Notification-area controls and global hotkeys
- Repeat-area and delayed capture
- Capture preview, auto-save, and persistent settings
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
