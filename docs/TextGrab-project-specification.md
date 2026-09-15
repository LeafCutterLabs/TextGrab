# Text Grab Project Specification

## Overview

TextGrab is a small portable Windows utility for fast region OCR: hotkey, select a region, recognize text locally, and show selectable text. No administrator rights, installer, or cloud services are required.

V1 targets x64 Windows 10 22H2 and Windows 11, with English and Spanish OCR only. Distribution is a portable ZIP containing the application, .NET runtime, native OCR dependencies, both models, and license notices.

## Goals and Non-goals

Goals: no installed prerequisites; local-only OCR; a few-second workflow; natural mouse and keyboard text editing and selection; cleaned output that can be corrected before copying; captured images and text kept in memory.

Non-goals: full document management or annotation, cloud synchronization, and accounts.

## Requirements

- Global Ctrl+Alt+T hotkey, with clear conflict handling and a tray fallback. The tray menu can reassign it for the current run without creating a settings file.
- Rectangular region selection, Escape cancellation, and in-memory capture.
- Replaceable local OCR engine behind an interface.
- One resizable floating result panel with an editable text box, normal typing, new lines, undo, Ctrl+A, Ctrl+C, and Copy All.
- Show one cleaned copy. Cleanup normalizes line endings, trims trailing whitespace, removes every empty or whitespace-only line, and preserves spacing on lines that contain text. Copy the initial clean result to the clipboard automatically; Copy All copies the edited text exactly.
- Explicit clipboard-image OCR through the same processing pipeline.
- Physical-pixel screen coordinates, per-monitor DPI-aware overlays, negative monitor coordinates, and cross-monitor selection. Result placement stays within the active monitor's working area.
- No content files, history, telemetry, network requests, saved settings, or content logs. Release image buffers after processing and clear result references when the panel closes. Explicit Windows clipboard copies are outside the app's persistence control.
- Responsive UI, one operation at a time, and clear failures for empty/missing images, missing engine assets, failed capture, and clipboard contention.

## V1 Features

Build from scratch using C#/.NET and WPF: region capture, local English and Spanish OCR, an editable floating text panel, automatic initial copy, Copy All, OCR Clipboard Image, and a tray icon with capture, shortcut, and exit actions.

## Architecture

Use one WPF application project and a focused test project. Separate capture, OCR, cleanup, clipboard, operation gating, and UI components. `IOcrEngine` accepts an in-memory image and cancellation token and returns recognized lines. Recognition runs away from the UI thread; clipboard access remains on the STA UI thread with bounded retries. Publish a self-contained .NET 10 win-x64 folder.

Evaluate Windows OCR first, but require a documented supported unpackaged deployment path. Microsoft currently documents package identity as required, so the portable release uses bundled Tesseract with English and Spanish data and app-local native runtime DLLs.

## Development Plan

1. Clipboard OCR to selectable text and Copy All; establish portable native loading.
2. Region capture, Escape cancellation, and the global hotkey.
3. DPI and multi-monitor coordinate handling.
4. Conservative text cleanup and editable result preview.
5. Tray UI, errors, resource cleanup, tests, and ZIP packaging.

GPT-5.6 Sol is the chosen implementation model.

## Acceptance Tests

- Offline launch as a standard user on clean Windows 10/11 without .NET or VC runtime installations.
- Real OCR of known English and Spanish text, automatic initial copy, natural editing and selection, Copy All, and clipboard-image OCR.
- Capture at 100%, 150%, and 200% scaling, negative monitor coordinates, and cross-monitor rectangles.
- Cancellation, hotkey conflicts, repeated operations, missing clipboard images, empty recognition, and clipboard contention.
- Focused cleanup, geometry, editor, undo-purge, and embedded-icon tests.
- No application-created content files or network requests during normal or failure flows.
- Target three seconds or less for typical snippets up to 1920x1080, with hardware and cold/warm measurements recorded.

See `verification.md` for measured results and `manual-acceptance.md` for checks requiring additional environments.

## Out of Scope for V1

Positional text overlay, history, table reconstruction, AI rewriting, document import/annotation/management, cloud sync, additional OCR languages, ARM64, and 32-bit releases.

## Start Small

Codex: Review the specification, propose the simplest architecture, flag decisions and feasibility conflicts, then begin with the smallest proof of concept: OCR a clipboard image locally to selectable text. Use GPT-5.6 Sol for implementation. Prove portable operation, then build outward through capture, DPI and monitors, cleanup, and tray polish.
