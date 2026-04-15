# Manual QA

Recommended checks for a release candidate:

## Basic

- Open a `.png`, `.jpg`, `.jpeg`, and `.bmp`
- Drag and drop each supported format into the app
- Open a corrupted file and verify the error is user-friendly
- Open an image with no text and verify the `No text found` state

## OCR Viewer

- Open a short screenshot
- Open a long screenshot
- Open a very large screenshot
- Verify OCR runs once and the UI stays responsive
- Verify hover changes to an I-beam over recognized words
- Verify click without drag does not create a selection
- Verify click on empty space clears selection
- Verify drag selection highlights words in document order
- Verify `Ctrl+A` selects all OCR text
- Verify `Ctrl+C` copies the current selection
- Verify `Copy All Text` copies normalized document text

## Visual Cases

- Light background images
- Dark background images
- Mixed text blocks
- Dense UI screenshots
- High DPI display scaling

## Viewer Controls

- Fit to window
- Actual size
- Zoom in and out
- Reset view
- Middle-mouse pan
- `Space` + drag pan

## Side Panel

- Toggle the text panel
- Verify recognized text is readable
- Verify native text selection works inside the panel

## Lifecycle

- Enable close-to-tray and verify close hides the app
- Use tray menu actions for Open, Open Image, Settings, About, Exit
- Toggle launch-at-startup and verify no crash when saving settings
