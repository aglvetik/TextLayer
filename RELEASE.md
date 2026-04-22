# Release Build Notes

## Build the app payload

From the repository root:

```powershell
.\publish.bat
```

This creates the canonical installer payload:

```text
dist\TextLayer
```

Do not use `src\TextLayer.App\bin\Debug` or `src\TextLayer.App\bin\Release` as installer input.

## Build the installer

Install Inno Setup 6, then run:

```powershell
.\installer\build-installer.ps1
```

The installer build validates that `dist\TextLayer` exists and contains:

- `TextLayer.exe`
- `tessdata\eng.traineddata`
- `tessdata\rus.traineddata`

Output:

```text
release\TextLayer-Setup-0.1.0.exe
```

## Release hosting

- GitHub Releases should host `TextLayer-Setup-0.1.0.exe`.
- GitHub Pages should host the static site from `site\`.
- The site currently uses a placeholder GitHub Releases URL and must be updated with the real repository owner/name before publishing.
