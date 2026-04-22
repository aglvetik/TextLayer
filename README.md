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

## Normal User Run Path

TextLayer's canonical app folder is:

`dist\TextLayer`

Use that folder only for normal use. The `src\...\bin\Debug` and `src\...\bin\Release` folders are development outputs and may contain older builds.

Publish the app once from the repo root:

```powershell
.\publish.bat
```

That creates a fresh runnable build in `dist\TextLayer` with the required OCR assets bundled beside the app.

Then launch TextLayer with either:

- `.\Start-TextLayer.bat`
- or `dist\TextLayer\TextLayer.exe`

Settings persist across runs. After launch, the main workflow is `Ctrl+Shift+O` to capture a region from another app and copy text from the overlay.

The optional `Launch at startup` setting registers TextLayer for the current Windows user and starts it silently in the tray after login.

## Development Run

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

If you want the same user-facing build that a normal user should run, publish to the canonical folder instead of opening an `.exe` from `bin`:

```powershell
.\publish.bat
```

Run tests:

```powershell
dotnet test TextLayer.sln
```

## Notes

- The tray-first overlay workflow is the main product path.
- English and Russian are the supported public OCR language choices in the UI.
- English can use either `Fast` or `Accurate` OCR.
- Russian currently uses `Accurate` OCR only.
- Fast OCR for Russian is still in development and is shown as unavailable in the app UI.
- The Auto OCR language option is still visible in the app, but it is marked as being in active development.
- The older in-app image viewer remains available as a secondary feature.
- Core settings persist across restarts.
- The published `dist\TextLayer` folder contains the correct app build and the required local Tesseract OCR assets.

## Current Limitations

- OCR quality still depends on the source image, especially with very small text, gradients, and dense UI screenshots.
- Russian OCR currently relies on Accurate mode because Fast OCR for Russian is not ready for general use yet.
- Auto OCR language remains experimental.
- Region capture is single-monitor per capture.
- The first installer setup lives under `installer\` and packages only `dist\TextLayer`.

## Technical Notes

See [TECHNICAL.md](TECHNICAL.md) for the implementation overview.

See [RELEASE.md](RELEASE.md) for the installer and GitHub Pages release flow.
