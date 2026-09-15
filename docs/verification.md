# TextGrab verification

This file separates measured checks from release checks that need additional hardware or operating systems.

## Version 1.2.1 — automatic copy, shortcut reassignment, Spanish OCR, and compact multiline text

- Region and clipboard OCR now copy the initial cleaned result to the Windows clipboard automatically. The result remains editable, and Copy All recopies the current editor contents exactly.
- Clean output removes every empty or whitespace-only line, including gaps within multiline OCR, while preserving spacing on lines that contain text.
- The tray menu now opens a keyboard-shortcut dialog. It supports Ctrl, Alt, and Shift combinations with A-Z, 0-9, and F1-F11, reports registration conflicts, and restores the prior shortcut after a failed change. Changes last until exit so the app creates no settings file.
- The release bundles only the English and Spanish `tessdata_fast` models. Unused x86 native OCR assets and non-English framework satellite resources were removed from both x64 packages.
- Release build passed with zero warnings/errors, and the focused harness passed 15/15 checks, including removal of internal blank lines and the shortcut dialog's modifiers and supported-key list. Self-contained and compact published smoke tests recognized `TEXTGRAB 12345 ESPANOL 67890`, loaded native modules from their own publish directories, and completed in 278 ms and 347 ms respectively.
- The self-contained ZIP is 83,224,739 bytes (79.37 MiB), down 8,053,483 bytes from the 1.1 ZIP.
- The optional framework-dependent folder is 40,159,911 bytes (38.30 MiB, 31 files). Its ZIP is 13,415,427 bytes (12.79 MiB) and requires the x64 .NET 10 Desktop Runtime to be installed.

## Delivered builds (1.2.1)

- Portable no-prerequisites ZIP: `software/artifacts/TextGrab-v1.2.1-win-x64.zip`.
- SHA-256: `4E1EBA55838817F6C0CBA17378E6C3AF4BBA250528BCF931D45C1DFA6CF225C8`.
- Compact runtime-dependent ZIP: `software/artifacts/TextGrab-v1.2.1-compact-requires-dotnet10-win-x64.zip`.
- SHA-256: `5E6AB9616A209663FF042A537A6B0465C6D178142001BDC52C5476F569BFA7D8`.
- Smoke reports: `software/artifacts/smoke-report.json` and `software/artifacts/smoke-report-compact.json`.
- Physical mixed-DPI, clipboard-contention, and clean-machine Windows 10/11 checks remain external release gates.

## Version 1.1.0 — editable preview and custom icon

- Cleaned OCR output is editable. Raw/Clean controls are removed; Copy All reads the current editor text without applying cleanup again.
- New results and closing the panel purge the editor's text and undo history. Further edits clear the Copied status.
- The executable, result-window title/taskbar, and system tray use the custom TextGrab capture-bracket icon. All nine ICO sizes decoded successfully; the icon extracted from the published executable was visually verified.
- Release build passed with zero warnings/errors. The focused harness passed 12/12 checks, including editing, absence of mode controls, undo purge, and WPF/tray resource loading. A synthetic render of the result editor was visually checked.
- Published OCR smoke passed in 312 ms. The delivered ZIP was extracted into a path containing spaces and launched from C:\\Windows; OCR passed with exit code 0 in 122 ms, with all OCR/native modules loading from the extracted folder.
- Publishing now creates a versioned ZIP through a unique staging file and reports packaging failures. The complete corrected script passed using an output path containing spaces.
- These checks do not replace the outstanding physical mixed-DPI and clean-machine checks below.

## Environment

- Windows 10 Pro x64, version 10.0.19045 (22H2).
- AMD Ryzen 7 3700X 8-Core Processor.
- Non-elevated session.
- One 2560 × 1440 display, working area 2560 × 1400.
- No pre-existing `dotnet` command on PATH. Development uses the workspace-local SDK 10.0.401, downloaded from Microsoft and checked against the published SHA-512.
- SDK runtime version 10.0.12. The release must include its own runtime and OCR files.
- No Windows Sandbox, Hyper-V PowerShell module, or Docker available in this environment.

## Portability evidence

- Microsoft documents Windows.Media.Ocr as requiring package identity. This fails the supported unpackaged deployment gate. No MSIX registration, installer, or Windows OCR dependency is introduced.
- English `tessdata_fast` model and release VC143 x64 runtime DLLs are bundled with checksums and third-party notices.
- PE dependency inspection confirms Tesseract depends on bundled Leptonica, MSVCP140, VCRUNTIME140, and VCRUNTIME140_1 plus Windows system/UCRT libraries. Leptonica needs VCRUNTIME140 and Windows system/UCRT libraries.
- A successful run on this developer machine alone cannot prove absence of hidden prerequisites on a clean machine. Published-process module paths must also be inspected, and clean Windows 10/11 checks remain a release gate.

## Checks performed

- The Release test harness passed 10/10 cases: cleanup, negative coordinates, monitor intersection, empty intersections, exclusive operation gating, and idempotent gate release.
- The final packaged executable recognized `TEXTGRAB 12345` from a synthetic 720x150 image in 280 ms. An independent run of the extracted ZIP from `C:\\Windows`, with the executable in a folder containing spaces, passed with exit code 0 and the same recognized text in 120 ms. Native module paths all resolved inside the extracted application folder. These are small-fixture measurements, not a completed 1920x1080 performance benchmark.
- The synthetic fixture now uses CPU rasterization. This fixed intermittent blank fixture rendering seen with WPF RenderTargetBitmap in a hidden test session; the production OCR pipeline was unchanged.
- Loaded Tesseract, Leptonica, MSVCP140, VCRUNTIME140, and VCRUNTIME140_1 module paths were inside the published application folder, including after extraction into a folder whose name contains spaces.
- A source scan found no normal application file-writing or network code. The only explicit file write is the opt-in synthetic `--smoke-test` diagnostic report. This is static evidence, not a substitute for OS-level tracing.
- The Windows UI verification helper could read accessibility trees but failed screenshot capture with `SetIsBorderRequired failed: No such interface supported (0x80004002)`; coordinate input then failed with `coordinate input geometry is unavailable`. Full interactive region/clipboard/result validation was therefore not completed and is not claimed.

## Delivered build (1.1.0)

- ZIP: `software/artifacts/TextGrab-v1.1.0-win-x64.zip` (91,278,222 bytes).
- SHA-256: `B1A7C0F90054C8A095E05F60FAF5EA5A316B4D9995B9F7CCA17DDDFDA0A7A5F3`.
- Build smoke report: `software/artifacts/smoke-report.json`.
- Independent extracted-ZIP report: `.tools/verification/v1.1-relocated-smoke.json`.
- Implementation and the final fixes were performed by GPT-5.6 Sol coding agents, with independent setup/review/verification in the main task.

## Remaining external acceptance checks

- Extract and run offline as a standard user on clean Windows 10 22H2 and Windows 11 x64 machines without .NET or Visual C++ Redistributable installations.
- Physical monitors at 100%, 150%, and 200% scaling, including monitors left/above the primary display and cross-monitor rectangles.
- Move the result panel between differently scaled monitors; confirm all controls and text remain usable and the initial panel is inside the selected monitor's working area.
- Trace file and network activity across launch, successful OCR, blank OCR, clipboard contention, missing assets, cancellation, and exit. Normal application behavior must not create content files or network requests. OS paging, crash handling, and explicit Windows clipboard copies are outside the application's persistence control.

## References

- https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr?view=winrt-26100
- https://learn.microsoft.com/en-us/dotnet/core/deploying/
- https://learn.microsoft.com/en-us/cpp/windows/choosing-a-deployment-method?view=msvc-170
