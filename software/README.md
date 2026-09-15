# TextGrab 1.2.1 software

## Use

Run `TextGrab.exe`. The app lives in the Windows notification area.

- Press **Ctrl+Alt+T**, then drag a rectangle around text. Press **Esc** to cancel.
- Right-click the tray icon for **Capture Region**, **OCR Clipboard Image**, **Keyboard shortcut**, or **Exit**. Shortcut changes last for the current run and create no settings file.
- English and Spanish are the only OCR languages. Both models run locally and are bundled with the app.
- The result panel shows one cleaned, editable copy. Cleanup normalizes line endings to CRLF, trims spaces and tabs at the end of each line, and removes every empty or whitespace-only line. It preserves leading and internal spacing on lines that contain text.
- TextGrab automatically copies that initial cleaned result to the clipboard. Type, add new lines, select text, and use normal undo, Ctrl+A, and Ctrl+C while the current result is open. **Copy All** copies the editor text exactly, including your changes.

If Ctrl+Alt+T belongs to another app, TextGrab shows a conflict message and remains usable from the tray menu. Only one capture or OCR operation runs at once. Errors for a busy clipboard, missing clipboard image, missing OCR assets, an oversized image, native load failure, capture failure, and an empty OCR result are presented in the UI.

## Build and test

The app requires the .NET 10 SDK for development. The published ZIP is self-contained and does not require .NET to be installed.

```powershell
& ..\.tools\dotnet\dotnet.exe build .\TextGrab.slnx -c Release
& ..\.tools\dotnet\dotnet.exe run --project .\TextGrab.Tests\TextGrab.Tests.csproj -c Release
```

The checked-in NuGet lock files pin resolved dependencies. OCR assets are expected here:

- `TextGrab/tessdata/eng.traineddata`
- `TextGrab/tessdata/spa.traineddata`
- `TextGrab/native/x64/*.dll` for the app-local Microsoft VC143 runtime
- `TextGrab/ThirdPartyNotices/*` for dependency licenses and provenance

The wrapper package contributes its Tesseract and Leptonica native libraries under the published `x64` directory. VC runtime DLLs are copied to the application root so the native dependency loader resolves them app-locally.

## Publish

From the repository root:

```powershell
.\software\publish.ps1
```

The script reads the project version and creates `software/artifacts/TextGrab-v<version>-win-x64.zip` plus a SHA-256 file. It builds the archive at a unique temporary path and moves the completed ZIP into place, so a packaging failure cannot be mistaken for a valid final archive. Before creating the ZIP it launches the published executable in `--smoke-test` mode. That mode renders known English and Spanish text in memory, performs real OCR without reading or changing the clipboard, checks the recognized text, records OCR duration, and lists loaded OCR/VC native module paths in `software/artifacts/smoke-report.json`. A failed smoke test stops packaging.

The default ZIP includes .NET and is the portable no-prerequisites release. If the target computer already has the x64 .NET 10 Desktop Runtime, you can create a much smaller package:

```powershell
.\software\publish.ps1 -Compact
```

This creates `TextGrab-v<version>-compact-requires-dotnet10-win-x64.zip` and `smoke-report-compact.json`. The compact build is not the no-prerequisites release.

The normal app never writes this report; it exists only when the explicit diagnostic switch is used.

## OCR engine decision

V1 uses Tesseract 5.2.0 with the English and Spanish `tessdata_fast` models. Windows.Media.Ocr was evaluated and rejected for this unpackaged desktop build because Microsoft documents that this API requires package identity for desktop use. That makes it an unsupported basis for this portable ZIP. See Microsoft’s [Windows.Media.Ocr API requirements](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr?view=winrt-26100).

## Design boundaries

Capture, OCR, cleanup, clipboard access, operation gating, and UI live in separate components. Screen capture uses Win32 physical-pixel coordinates and a top-down in-memory DIB. A translucent overlay is created for every monitor; cursor polling during the drag allows a selection to span monitors, negative desktop coordinates, and DPI boundaries. The app rejects images above 50 megapixels to bound transient memory use.

OCR runs on a worker thread so native recognition does not freeze the UI. Cancellation is checked before conversion, before native recognition, and after recognition; the Tesseract call itself cannot be interrupted safely once entered. Pixel and BMP byte arrays are zeroed in `finally` blocks. Managed immutable strings remain subject to normal .NET lifetime rules. Starting a new region or clipboard OCR operation, closing the result panel, or exiting clears the editor text and undo history so an earlier capture cannot be restored with Undo.
