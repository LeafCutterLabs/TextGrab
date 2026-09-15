# TextGrab

TextGrab is a small offline Windows utility that turns screen regions or clipboard images into editable text. Press **Ctrl+Alt+T**, drag around English or Spanish text, and correct the cleaned result in a floating window. TextGrab copies the initial result automatically; **Copy All** copies any edits you make afterward.

The V1 target is x64 Windows 10 22H2 and Windows 11. English and Spanish are the only bundled OCR languages. See [software/README.md](software/README.md) for use, build, verification, and packaging instructions.

## Download

Download the portable no-prerequisites ZIP from the [latest GitHub release](https://github.com/LeafCutterLabs/TextGrab/releases/latest), extract the complete folder, and run `TextGrab.exe`. A smaller runtime-dependent package is also available for computers that already have the x64 .NET 10 Desktop Runtime.

## Privacy behavior

TextGrab has no network access, history, saved settings, analytics, or log files. A capture or clipboard image is processed in memory. After recognition, the cleaned result is copied to the Windows clipboard automatically. Starting another OCR operation or closing the result panel clears its text and undo history. Clipboard contents are controlled thereafter by Windows and its clipboard settings.

## Repository layout

- `software/TextGrab` — WPF application and app-local OCR assets
- `software/TextGrab.Tests` — dependency-free focused test harness
- `software/publish.ps1` — verified self-contained x64 publish and ZIP creation
- `docs/manual-acceptance.md` — release acceptance checklist
- `hardware` and `media` — supporting project material
