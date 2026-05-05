# MAME Emulation Port Test Log

## Phase C (Download/cache service) - Codex notes

Date: 2026-05-05

### Manual test steps for maintainer

1. Launch `OasisEditor` and open the Preferences tool window.
2. Set **MAME Install Root** to a writable local directory.
3. Click **Refresh Versions** and confirm known versions are written to output log.
4. Set **MAME Version** (for example `0267`) and verify **MAME Release Source** points to the MAME GitHub releases base URL.
5. Click **Download Selected** and verify progress log entries for download and extraction appear.
6. Confirm `mame.exe` is extracted under `<install-root>/mame####/mame.exe`, and that **MAME Executable** is auto-filled.
7. Click **Open Install Folder** and verify Explorer opens at install root.
8. Click **Remove Cached Version** and verify the version folder is deleted and a corresponding log entry is produced.
9. Try download with an invalid URL or unwritable install root and confirm failure is logged clearly without editor crash.

### Untested assumptions

- Download archive format `mame####b_64bit.zip` / `mame####b_x64.zip` remains valid for target versions and GitHub release layout.
- `ZipFile.ExtractToDirectory` can extract archives produced by MAME releases without additional staging logic.
- `explorer.exe` launch is acceptable for target Windows environments.
