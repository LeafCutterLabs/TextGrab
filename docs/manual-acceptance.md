# TextGrab 1.3.0 manual acceptance

Record the OS build, display arrangement/scaling, app build or ZIP hash, tester, date, and result for each run. Automated builds and smoke tests do not replace the hardware-dependent checks below.

## Available development machine

- Launch the published `TextGrab.exe`; verify no console window appears and one tray icon is present.
- Press Ctrl+Alt+T. Verify all displays dim, the pointer becomes a crosshair, and Esc closes every overlay without changing the prior result.
- Drag around sharp, high-contrast English text. Verify the rectangle follows the physical cursor, overlays disappear before capture, the UI remains responsive during OCR, and a resizable result panel appears within the cursor monitor working area. Repeat with Spanish text, including accented characters.
- Select a region with no text. Verify a clear “No text was found” message appears.
- Copy an image, choose **OCR Clipboard Image**, and verify it uses the same result path. Repeat with text-only/empty clipboard data and verify the missing-image error.
- After region and clipboard OCR, paste into Notepad and confirm the initial cleaned text was copied automatically. This intentionally replaces a clipboard source image after clipboard OCR.
- In the result panel verify the cleaned text is editable. Check typing, new lines, undo, mouse selection, Ctrl+A, Ctrl+C, resizing, wrapping, and scrolling. Confirm Copy All matches the edited text exactly and that the copy status clears after another edit.
- Verify cleanup with mixed line endings, trailing spaces/tabs, leading indentation, and blank lines before, between, and after text. Confirm every blank line is removed while spacing on nonblank lines is retained.
- Start a slow OCR operation and immediately request another from the hotkey or tray. Verify the second request reports that TextGrab is busy.
- Temporarily reserve Ctrl+Alt+T in another process, launch TextGrab, and verify both the conflict message and working tray-menu fallback.
- Right-click the tray icon, choose **Keyboard shortcuts**, assign another Ctrl/Alt/Shift plus letter, number, or F-key combination, and verify the old shortcut stops working and the new one captures. Try a shortcut already reserved by another app and verify TextGrab keeps the prior shortcuts. Restart TextGrab and verify the Ctrl+Alt+T and Ctrl+Alt+V defaults return.
- Create at least seven region and clipboard-image OCR results, editing several before moving on. Press Ctrl+Alt+V and verify Snip History opens within the cursor monitor, shows newest-first thumbnails/text/ages, and displays several items without resizing. Verify Up/Down, Home/End, mouse wheel, click, Enter, double-click, Delete, Clear All, Escape, and close-on-deactivation behavior.
- Select an edited history entry, press Enter, then paste into Notepad with Ctrl+V. Confirm the exact latest edited text is restored, no automatic keystroke is injected, and a clipboard-contention failure leaves the picker open with a friendly error.
- Fill history past 25 successful results and verify only the newest 25 remain. Confirm cancelled selections, empty OCR failures, and other errors add no entry. Exit and relaunch TextGrab and verify history is empty.
- In **Keyboard shortcuts**, change capture and history shortcuts together, verify identical combinations are rejected, verify an externally reserved combination restores the previous pair, and confirm both tray commands remain available after conflicts.
- Start a new OCR operation after editing, and separately close the result panel and reopen it with another OCR operation. In both cases verify the old text is absent, Undo cannot restore it, and only one reusable result panel exists.
- Exit from the tray menu. Verify overlays/result close and the tray icon disappears.
- Run `TextGrab.exe --smoke-test <absolute-report-path>` from a process that waits for completion. Verify exit code 0, expected text `TEXTGRAB 12345` and `ESPANOL 67890`, and Tesseract, Leptonica, and VC runtime module paths under the extracted app directory.

## External release gates

These checks require machines/display setups that may be unavailable in the development environment. Mark the release candidate unverified rather than assuming a pass.

- Clean Windows 10 22H2 x64 machine without .NET SDK/runtime or Visual C++ redistributable installed: extract the complete ZIP, run smoke mode, then exercise region and clipboard OCR.
- Clean supported Windows 11 x64 machine with the same prerequisites absent: repeat the full launch/smoke/region/clipboard flow.
- Physical mixed-DPI setup, including 100% and at least one higher scaling value: capture within each display, cross the DPI seam in both directions, and verify the rectangle and captured pixels align with the drag.
- Physical layout with a monitor left of and/or above the primary display: verify negative-coordinate capture within that monitor and across the primary boundary.
- At least two resolutions and taskbar positions: verify result placement remains fully inside the cursor monitor working area.
- On each mixed-DPI and negative-coordinate arrangement, open Snip History near every screen edge and verify it remains fully inside the cursor monitor's working area with crisp thumbnails and usable keyboard focus.
- Windows clipboard held open by another process: verify bounded retries end with the friendly busy message and that the UI recovers for the next operation.

No release claim should state that clean-machine or physical mixed-monitor compatibility passed until those rows have recorded evidence.
