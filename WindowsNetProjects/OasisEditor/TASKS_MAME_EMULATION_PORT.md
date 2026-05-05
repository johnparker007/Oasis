# MAME Emulation Port Tasks

Objective: port the legacy Unity LayoutEditor MAME integration into the WPF/.NET `WindowsNetProjects/OasisEditor` editor so loaded fruit machine projects can be launched, driven, and observed through MAME.

This plan is written for Codex. Codex should make small, reviewable changes, but should not claim runtime validation of the WPF/.NET app. The maintainer will sync, build, run, test MAME locally, and report compile/runtime issues back to Codex.

## Source areas to inspect first

Legacy Unity editor:

- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Editor.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Import/Importer.cs`
- `UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/LayoutEditor/Components/*`
- `UnityProjects/LayoutEditor/Assets/_Project/Prefabs/MameController.prefab`
- `UnityProjects/LayoutEditor/Emulators/MAME/mame0258/plugins/oasis/**`
- `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/**`

New WPF/.NET editor:

- `WindowsNetProjects/OasisEditor/OasisEditor.sln`
- `WindowsNetProjects/OasisEditor/OasisEditor/EditorPreferencesStore.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/PreferencesWindow.xaml`
- `WindowsNetProjects/OasisEditor/OasisEditor/PreferencesWindow.xaml.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/EditorProject.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/DocumentModel.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/Panel2DDocumentModel.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/Panel2DDocumentStorage.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/MainWindow.xaml`
- `WindowsNetProjects/OasisEditor/OasisEditor/MainWindow.xaml.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/MainWindowViewModel.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/PanelRuntimeState.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/OutputLogEntry.cs`
- `WindowsNetProjects/OasisEditor/OasisEditor/Views/OutputLogView.xaml`

## Non-negotiable implementation rules

- Do not port Unity concepts directly into WPF. Extract the platform-independent MAME behavior into .NET services/models and keep WPF UI in thin view/view-model layers.
- Avoid blocking the UI thread. Process start, stdout/stderr reads, MAME downloads, and Lua command writes must be async/cancellable.
- Keep the MAME process wrapper testable without launching MAME by abstracting process creation, filesystem, HTTP download, and clock/timer behavior where practical.
- Do not commit downloaded MAME binaries.
- Keep platform-specific assumptions contained. This editor is Windows-focused, but path handling and process configuration should still be explicit and discoverable.
- Add log output to the existing output-log infrastructure rather than writing only to console/debug output.
- Prefer incremental vertical slices: settings persistence first, then process launch, then Lua communication, then live lamp/input integration.

## Phase A - Legacy behavior inventory

- [ ] Read the legacy MAME source and make an implementation inventory in `MameEmulationPort.Inventory.md`.
- [ ] Identify how legacy preferences store MAME executable/version/download location.
- [ ] Identify how legacy project settings store fruit machine platform type.
- [ ] Identify how `MameController` builds process arguments, working directory, plugin paths, and environment.
- [ ] Identify stdout protocol lines used for lamps, reels, meters, status messages, errors, and any startup-ready signal.
- [ ] Identify stdin/Lua command format and escaping requirements.
- [ ] Identify all platform-specific keyboard/input mappings and the exact Lua scripts they call.
- [ ] Identify which files under `MameLuaPlugins/oasis` must be copied into the new editor output or user data folder.
- [ ] Record any uncertain behavior as questions for the maintainer instead of guessing.

Deliverable: `MameEmulationPort.Inventory.md` with source-path references and a checklist of behavior to preserve.

## Phase B - Preferences model and UI

- [ ] Extend the new editor preferences model/store with MAME configuration:
  - selected MAME version string;
  - MAME executable path;
  - MAME install root/cache directory;
  - download URL or release source metadata;
  - default Lua plugin path/location;
  - optional command-line overrides for debugging.
- [ ] Add fields to the WPF Preferences window for the current MAME executable/version.
- [ ] Add browse/select support for an existing `mame.exe`.
- [ ] Add validation that reports missing executable, missing plugin directory, invalid paths, and unsupported version state without crashing.
- [ ] Persist preferences through `EditorPreferencesStore` using the existing JSON/settings pattern.
- [ ] Surface preference validation in the output log and in the preferences UI.

Acceptance for maintainer testing:

- Preferences survive editor restart.
- Selecting a local `mame.exe` works.
- Invalid paths show clear errors and do not crash.

## Phase C - MAME download/cache service

- [ ] Create a service for discovering/downloading MAME versions based on the legacy behavior.
- [ ] Keep binary downloads outside git, preferably under the editor/user data cache.
- [ ] Support cancellation, progress reporting, checksum/size sanity checks when available, and safe extraction.
- [ ] Never overwrite an active MAME install while an emulation process is running.
- [ ] Add UI commands in Preferences: refresh available versions, download selected version, open install folder, remove cached version.
- [ ] Log all download/extract actions in the output log.

Acceptance for maintainer testing:

- Downloaded version appears in preferences.
- Download cancellation leaves no corrupt active install.
- Downloaded `mame.exe` can be selected and used by later phases.

## Phase D - Project settings: fruit machine platform type

- [ ] Add a project-level enum/model for fruit machine platform type.
- [ ] Port the legacy list of platform types and map them to the correct input/Lua behavior.
- [ ] Add the field to the project settings UI using the existing WPF project/document patterns.
- [ ] Persist the value in the project file/document schema with backwards-compatible defaults.
- [ ] Ensure missing/unknown platform values load as a safe default and emit a warning.

Acceptance for maintainer testing:

- Platform type can be edited and saved.
- Reopening a project preserves the value.
- Existing projects without the value still load.

## Phase E - Lua plugin asset staging

- [ ] Decide the runtime plugin asset location for the new editor.
- [ ] Copy or port `UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/**` into an appropriate new-editor asset folder if licensing/size is acceptable.
- [ ] If assets should not be copied into the WPF project, add a service that locates them from the repo or user-configured path.
- [ ] Add startup validation that confirms required Lua plugin files exist before launching MAME.
- [ ] Add a single source of truth for plugin paths used by process launch and diagnostics.

Acceptance for maintainer testing:

- The editor can report exactly which plugin path it will give to MAME.
- Missing plugin files are reported before process launch.

## Phase F - MAME process controller service

- [ ] Add a .NET service equivalent to the legacy `MameController`, separated into:
  - process configuration builder;
  - process lifetime manager;
  - stdout/stderr protocol parser;
  - stdin/Lua command writer;
  - runtime state publisher.
- [ ] Start MAME with redirected stdin/stdout/stderr.
- [ ] Provide cancellable start/stop/restart APIs.
- [ ] Ensure process cleanup on editor exit, project close, and failed startup.
- [ ] Emit structured output-log entries for launch args, ready/error lines, process exit, and parse failures.
- [ ] Do not expose raw process APIs directly to WPF controls.

Acceptance for maintainer testing:

- Start launches MAME for a configured project.
- Stop terminates MAME cleanly.
- Failed launch gives actionable logs.

## Phase G - Protocol parsing and runtime state

- [ ] Port stdout parsing for lamps, reels, meters, segment displays, status, and errors from the legacy controller.
- [ ] Update `PanelRuntimeState` or introduce a dedicated `MameRuntimeState` that can drive existing panel visuals.
- [ ] Keep parsing robust against partial lines, unexpected lines, and version differences.
- [ ] Add parser-only tests where possible using captured sample output strings.
- [ ] Log unknown protocol lines at debug/trace level without spamming normal output.

Acceptance for maintainer testing:

- Lamp state changes from MAME update editor visuals.
- Unknown output does not crash the editor.

## Phase H - Lua command and input mapping

- [ ] Port Lua command sending from the legacy controller.
- [ ] Implement safe escaping/quoting for Lua command payloads.
- [ ] Add platform-type-specific input mapping for keystrokes/buttons.
- [ ] Route WPF keyboard/button events through the platform mapper into Lua/MAME commands.
- [ ] Avoid sending input when no MAME process is running or before MAME is ready.
- [ ] Log command send failures with context.

Acceptance for maintainer testing:

- Pressing mapped editor controls sends the expected MAME input.
- Different fruit machine platform types use their correct mappings.

## Phase I - Main editor integration

- [ ] Add editor commands/buttons/menu entries for emulation start, stop, restart, and focus/send input mode.
- [ ] Reflect emulation state in the UI: stopped, starting, running, stopping, failed.
- [ ] Disable invalid actions based on state.
- [ ] Use the existing output log for lifecycle messages.
- [ ] Ensure project close prompts/stops emulation safely.
- [ ] Ensure editor shutdown stops child processes.

Acceptance for maintainer testing:

- User can launch/stop from the main editor.
- UI state remains consistent after failed launch or process exit.

## Phase J - Diagnostics and maintainer test loop

- [ ] Add a diagnostics command that prints effective MAME configuration:
  - mame executable path;
  - version;
  - working directory;
  - plugin path;
  - project platform type;
  - generated command-line args;
  - project/game identifiers used for launch.
- [ ] Add a `MameEmulationPort.TestLog.md` file where Codex records untested assumptions and the maintainer records manual test results.
- [ ] Every Codex change should include:
  - files changed;
  - expected manual test steps;
  - risks/unknowns;
  - exact build/runtime errors reported by maintainer, if any.

## Suggested first Codex prompt

Use this prompt after syncing this file:

```text
Implement Phase A from WindowsNetProjects/OasisEditor/TASKS_MAME_EMULATION_PORT.md. Inspect the legacy MAME controller, preferences, project settings, and Lua plugin asset paths. Create MameEmulationPort.Inventory.md in the OasisEditor root with concrete source-path references, behavior inventory, and open questions. Do not change runtime code yet.
```

## Suggested follow-up Codex prompt after Phase A

```text
Using MameEmulationPort.Inventory.md and TASKS_MAME_EMULATION_PORT.md, implement Phase B only. Add MAME preferences model/store fields and Preferences window UI for selecting a local mame.exe and validating the configured paths. Keep changes small, do not implement launching MAME yet, and document manual test steps in MameEmulationPort.TestLog.md.
```
