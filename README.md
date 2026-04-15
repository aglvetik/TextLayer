# TextLayer

TextLayer is a local Windows OCR utility that lives in the tray and lets you copy text from images shown inside other apps.

It works by capturing a region of the screen, running OCR locally, and placing a selectable text overlay over the captured area. No cloud OCR, no browser plugin, no screen-scanning loop.

## Main Workflow

1. Run TextLayer.
2. Open an image in another app such as Discord, Telegram, a browser, or an image viewer.
3. Press `Ctrl+Shift+O`.
4. Drag a region on screen.
5. Select text directly from the overlay and press `Ctrl+C`.

`Ctrl+A` selects all text in the active overlay. `Esc` closes it.

## What It Uses

- C#
- .NET 9
- WPF
- Local Windows OCR
- Local Tesseract OCR

OCR runs locally on your machine. TextLayer does not send captured images over the network.

The control center and settings UI can be switched between English and Russian.

## Run From Source

Requirements:

- Windows 11 recommended
- .NET 9 SDK

Build from the repo root:

```powershell
dotnet build TextLayer.sln
```

Run:

```powershell
dotnet run --project .\src\TextLayer.App\TextLayer.App.csproj
```

Or build once and launch it like a normal Windows app:

```powershell
dotnet publish .\src\TextLayer.App\TextLayer.App.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\publish\TextLayer
```

Then open:

`artifacts\publish\TextLayer\TextLayer.exe`

You can double-click `TextLayer.exe` directly or create a normal Windows shortcut to it.

Run tests:

```powershell
dotnet test TextLayer.sln
```

## Notes

- The tray-first overlay workflow is the main product path.
- English and Russian are the supported public OCR language choices in the UI.
- The Auto OCR language option is still visible in the app, but it is marked as being in active development.
- The older in-app image viewer remains available as a secondary feature.
- Core settings persist across restarts.
- Local Tesseract OCR assets are included with the project and are copied automatically into the publish output.

## Current Limitations

- OCR quality still depends on the source image, especially with very small text, gradients, and dense UI screenshots.
- Auto OCR language remains experimental.
- Region capture is single-monitor per capture.
- No installer is included yet; the repo currently targets a clean unpackaged build first.

## Technical Notes

See [TECHNICAL.md](TECHNICAL.md) for the implementation overview.
