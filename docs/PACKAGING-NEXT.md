# Packaging Next

Recommended next steps after the unpackaged build:

## Short Term

- Add release configuration publishing instructions
- Replace the placeholder icon with a final product icon
- Add version stamping for release builds
- Smoke-test on a clean Windows 11 machine

## Installer Path

- Keep the current app layout installer-friendly
- Add an installer after the unpackaged build is stable
- Prefer an installer flow that creates shortcuts, uninstall entries, and optional startup registration cleanly

## Before Public Release

- Verify Local AppData settings and log paths
- Verify tray icon and exit behavior
- Verify OCR works with the target Windows language packs
- Confirm no sensitive text is logged
- Run the manual QA checklist on Windows 11 and best-effort on Windows 10
