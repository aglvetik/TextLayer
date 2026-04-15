# Architecture Notes

## Layers

`TextLayer.App`

- WPF windows, controls, view models, tray integration, theming

`TextLayer.Application`

- use-case orchestration
- settings management
- application interfaces

`TextLayer.Domain`

- OCR document model
- selection logic
- viewport math
- pointer intent classification
- text normalization

`TextLayer.Infrastructure`

- image loading
- OCR provider
- clipboard service
- settings persistence
- startup registration
- logging

`TextLayer.Tests`

- domain and infrastructure tests

## Main Flow

1. Load an image through `IImageLoader`
2. Run OCR through `IOcrEngine`
3. Map OCR output into `RecognizedDocument`, `RecognizedLine`, and `RecognizedWord`
4. Render the image in the viewer
5. Render a custom OCR overlay from the in-memory document
6. Hit test words in image space for hover and selection
7. Normalize selected text for clipboard output

## Interaction Model

- Plain click on empty space clears selection
- Plain click on text does not create a selection
- Drag selection starts only after the pointer crosses the drag threshold and the press started over recognized text
- Pan is separate from selection
- OCR is not re-run during hover, drag, pan, or zoom

## Replaceable Interfaces

- `IOcrEngine`
- `IImageLoader`
- `IClipboardService`
- `ISettingsStore`
- `IStartupRegistrationService`
- `ILogService`

The current OCR implementation is intentionally isolated so a future Windows App SDK or Windows AI recognizer can replace it without changing the presentation or domain layers.
