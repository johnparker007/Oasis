# MAME Emulation Port Test Log

## Phase B (Preferences model and UI) - Codex notes

Date: 2026-05-05

### Manual test steps for maintainer

1. Launch `OasisEditor` and open the Preferences tool window.
2. Confirm new MAME fields are visible: Version, Executable Path (with Browse), Install Root, Lua Plugin Directory, and Validate Paths status.
3. Click **Browse...** and select a local `mame.exe`.
4. Click **Validate Paths**.
5. Confirm validation feedback appears in the preferences UI and output log.
6. Set an invalid executable path and invalid plugin directory, click **Validate Paths**, and confirm clear warning output without crashes.
7. Close and restart the editor; verify values entered in the MAME fields persist.

### Untested assumptions

- Preferences tool window uses `Views/PreferencesView.xaml` bindings from `MainWindowViewModel` in all runtime entry paths.
- Output log warning/info statuses for validation are surfaced as expected by existing output infrastructure.
- Existing preferences JSON files without a `Mame` object continue to load with defaults via model initialization.
