# TECHNICAL

## Overview

TextLayer is a .NET 9 WPF desktop application with a layered structure:

- `src/TextLayer.App`
- `src/TextLayer.Application`
- `src/TextLayer.Domain`
- `src/TextLayer.Infrastructure`
- `src/TextLayer.Tests`

The current primary workflow is tray-first OCR capture over external applications. The older in-app viewer is still present as a secondary path.

## Architecture

### `TextLayer.App`

WPF windows, controls, tray integration, view models, theme resources, global hotkey handling, region selection, and overlay window orchestration.

Important app-side pieces:

- `App.xaml.cs`: startup composition root
- `MainWindow`: control center and secondary viewer host
- `TrayIconService`: tray menu and tray notifications
- `GlobalHotkeyService`: `Ctrl+Shift+O`
- `RegionSelectionService`: full-screen capture-region picker
- `OverlayWindowManager`: processing window + live overlay lifecycle
- `UiTextService`: runtime UI text localization for English and Russian

### `TextLayer.Application`

Application models and use cases:

- `ImageDocumentUseCase`
- `SettingsManager`
- settings models and OCR request models

### `TextLayer.Domain`

Pure OCR-related and interaction-related models:

- recognized document / line / word models
- selection model
- viewport and geometry types
- selection engine
- pointer interaction classification
- overlay coordinate mapping

### `TextLayer.Infrastructure`

Replaceable implementation details:

- Windows OCR provider
- Tesseract OCR provider
- OCR scoring / post-processing / filtering
- image loading
- clipboard
- settings persistence
- logging
- startup registration

## OCR Pipeline

The OCR path keeps the original image for display and uses OCR-only preprocessing for recognition.

Typical flow:

1. capture or open an image
2. load bitmap safely
3. analyze screenshot characteristics
4. choose OCR engine and OCR language profile
5. run OCR
6. post-process results
7. map output into `RecognizedDocument`, `RecognizedLine`, and `RecognizedWord`
8. display the overlay and/or side text panel

### OCR engines

- `WindowsMediaOcrEngine`: fast local Windows OCR
- `TesseractOcrEngine`: slower but generally more accurate local OCR
- `CompositeOcrEngine`: engine/language orchestration

### OCR language behavior

The public UI currently exposes English and Russian as supported OCR language choices. Auto language remains visible but disabled because it is still under active development.

Internally, the OCR layer still supports:

- English
- Russian
- mixed English + Russian evaluation paths
- retry/fallback scoring in composite OCR mode

### Preprocessing

The OCR preprocessing path includes:

- safe scaling for small-text screenshots
- dark-UI handling
- low-contrast handling
- accent-aware grayscale generation for colored foreground text

The preprocessing output is used only for OCR. The original image is preserved for display and overlay alignment.

### Post-processing and filtering

Important components:

- `ScriptAwareOcrPostProcessor`
- `RecognizedDocumentNoiseFilter`
- `RecognizedDocumentScoreCalculator`

These are responsible for:

- word-level and character-level script recovery
- mixed Latin/Cyrillic cleanup
- merging nearby OCR fragments into more complete selectable words
- rejecting icon/emoji/avatar/badge noise
- preserving real short text fragments when they connect to real neighboring text

## Overlay Workflow

Primary runtime flow:

1. app starts hidden/tray-first
2. user presses `Ctrl+Shift+O`
3. region selection windows appear per monitor
4. selected screen region is captured to a temporary image
5. OCR runs locally
6. a transparent overlay window is positioned over the captured screen rectangle
7. user hovers, selects, copies, or closes

Related windows:

- `ProcessingOverlayWindow`
- `RegionSelectionWindow`
- `ScreenOverlayWindow`

The overlay is topmost only while active. It closes on `Esc`, via its action bar, after copy if configured, or when the source window context becomes invalid.

## Shutdown Model

TextLayer uses an explicit application shutdown path instead of relying on the main window close event for full termination.

Current behavior:

- clicking `X` on the control center minimizes to tray when that setting is enabled
- tray `Exit` always performs a full application shutdown
- full shutdown cancels active capture work, closes overlay/process windows, persists current shell state, closes remaining WPF windows, disposes the tray icon, unregisters global hotkeys, and then shuts down the WPF application

This avoids the earlier deadlock-prone path where window closing could synchronously wait on async settings persistence.

## Windows Startup Registration

The `Launch at startup` setting uses the current-user Registry Run key:

- key: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- value name: `TextLayer`
- command shape: `"path\to\TextLayer.exe" --startup`

The `--startup` argument is significant. It tells `App.xaml.cs` that Windows launched TextLayer at login, so the app initializes tray/hotkey services silently without showing the control center or a startup balloon.

Startup registration is per-user and does not require administrator rights. When TextLayer is running from a development build under `src\...\bin\...`, registration prefers the canonical published executable at `dist\TextLayer\TextLayer.exe` if that file exists. This prevents a developer or user from accidentally registering an old Debug/Release build path. If TextLayer is already running from a normal published folder, the current executable path is used.

On settings load, an existing stale `TextLayer` Run value is repaired to the expected command. On settings save, the Run key is updated before the JSON settings file is written so the checkbox reflects the real Windows registration state.

TextLayer also uses a local named mutex to avoid duplicate app instances. A second launch exits immediately; the already-running tray instance remains the active app.

## Selection and Hit Testing

The overlay and the secondary viewer both rely on the same OCR document model.

Key points:

- OCR word bounds are stored in image-space coordinates
- screen-space / viewport-space input is transformed back into the same coordinate system
- selection is word-based, not character-based
- the drag logic preserves click-vs-drag threshold behavior
- post-processing can merge adjacent OCR fragments before selection so the overlay misses fewer broken half-words

## Settings, Tray, and Control Center

Settings are stored per-user under Local AppData as JSON.

Stored settings include:

- launch at startup
- close-to-tray behavior
- overlay enable/disable
- OCR engine mode
- OCR language mode
- UI language
- theme
- side panel visibility
- debug bounds
- close overlay after copy
- window placement

The control center is the visible management window. The tray menu remains the primary access point when the app is running in the background.

Settings persistence is handled through `SettingsManager` + `JsonSettingsStore`. Core user-facing settings are restored on startup, including OCR mode, OCR language, UI language, overlay enablement, close-to-tray behavior, theme, and window placement.

## Localization

UI localization is implemented in the app layer through `UiTextService`.

Current supported UI languages:

- English
- Russian

The service exposes runtime text values used by:

- control center
- settings window
- about window
- tray menu
- region selection / processing overlays
- overlay action bar

Language choice is stored in settings and applied on startup. Main-window text updates after save; newly opened auxiliary windows use the selected language immediately.

## Launch and Runtime Assets

The project builds as a normal WPF `WinExe`, so the published output can be launched by double-clicking `TextLayer.exe` without a console window.

### Canonical user-facing output

The canonical publish/output folder is:

- `dist\TextLayer`

Root-level helpers:

- `publish.ps1`: clears and republishes the app into `dist\TextLayer`
- `publish.bat`: simple Windows wrapper around `publish.ps1`
- `Start-TextLayer.bat`: launches `dist\TextLayer\TextLayer.exe` if it exists, otherwise tells the user to publish first

This keeps the user-facing app separate from development outputs such as:

- `src\TextLayer.App\bin\Debug\...`
- `src\TextLayer.App\bin\Release\...`

Those `bin` folders remain normal .NET build outputs for development only and should not be treated as the canonical runnable app location.

Tesseract assets are repository-owned and ship with the app:

- source assets: `src/TextLayer.App/Assets/tessdata`
- runtime lookup: `AppContext.BaseDirectory\\tessdata`
- canonical published runtime location: `dist\TextLayer\tessdata`

`TesseractDataPathResolver` checks predictable project-owned locations first and only consults `TESSDATA_PREFIX` as a fallback. The release/publish output copies the required `eng` and `rus` traineddata files automatically.

## Current OCR Limitations

- Small colored text on noisy backgrounds can still fail or fragment.
- Mixed-script OCR is better than earlier versions, but still not perfect on dense chat screenshots.
- Auto OCR language remains experimental and is intentionally disabled in the public UI.
- OCR quality still depends on source image scale, compression, and UI styling.

## Tests

The test project covers practical non-UI logic, including:

- settings serialization
- OCR engine selection heuristics
- preprocessing planning
- document scoring
- script-aware OCR correction
- noise filtering
- selection and geometry behavior
