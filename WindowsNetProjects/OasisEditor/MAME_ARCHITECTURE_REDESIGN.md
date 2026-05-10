# MAME Architecture Redesign

This document supersedes earlier assumptions in `TASKS_MAME_EMULATION_PORT.md` relating to user-configurable MAME filesystem paths.

## Goals

- Minimize end-user configuration.
- Keep MAME installs isolated per version.
- Make Lua plugin deployment automatic.
- Reduce invalid setup states.
- Keep the Preferences UI scalable as more settings categories are added.
- Separate editor-owned assets from downloaded runtime assets.
- Allow the editor to self-provision a working MAME install when possible.

## Preferences Window Redesign

The Preferences window should use a two-pane category layout.

## Layout

Left side:

- Appearance
- MAME

Right side:

- Displays the selected category settings.

This structure should be reusable for future categories.

Examples of future categories:

- Input
- Audio
- Rendering
- Emulator
- Diagnostics
- Experimental

## Appearance Category

The Appearance category should initially contain:

- Theme dropdown:
  - Light
  - Dark
  - System

Future appearance settings can later be added here.

## MAME Category

The MAME category should expose high-level state and actions rather than raw filesystem configuration.

The UI should avoid exposing internal runtime directories unless an advanced/debug mode is added later.

## Preferred MAME UI

- Setup status summary
- Installed MAME versions list
- Selected active version
- Latest available version, if known
- Download latest version
- Download specific version, optional advanced action
- Remove selected version
- Open install folder
- Re-sync Lua plugins
- Validate setup
- Diagnostics output

The current executable path may be displayed read-only for diagnostics.

## Remove User-Configured Install Root

The earlier design exposed:

- MAME Install Root
- Lua Plugin Directory

These should no longer be user-editable settings.

The editor should instead manage these paths automatically.

## Standard MAME Working Directory

Use LocalAppData for all runtime/downloaded MAME content.

Recommended root:

```text
%LOCALAPPDATA%\\OasisEditor\\MAME\\
```

Example structure:

```text
%LOCALAPPDATA%\\OasisEditor\\MAME\\downloads\\
%LOCALAPPDATA%\\OasisEditor\\MAME\\versions\\mame0258\\
%LOCALAPPDATA%\\OasisEditor\\MAME\\versions\\mame0260\\
%LOCALAPPDATA%\\OasisEditor\\MAME\\state\\
```

Each version should be isolated.

The selected version determines:

- which `mame.exe` is launched;
- which plugins folder is used;
- which runtime working directory is used.

## Editor Asset Source Layout

The Oasis MAME Lua plugin files are now expected to be included in the Visual Studio project as content assets.

Canonical source at runtime should be resolved from the built application output, not from the Unity project.

Expected project layout:

```text
WindowsNetProjects/OasisEditor/Assets/MAME/plugins/oasis/
```

Expected `.csproj` behavior:

```xml
<Content Include="..\\Assets\\**\\*.*">
  <Link>Assets\\%(RecursiveDir)%(Filename)%(Extension)</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

Expected build output layout:

```text
<app output>\\Assets\\MAME\\plugins\\oasis\\
```

The new editor should not use `UnityProjects/LayoutEditor_ExternalAssets` as its runtime plugin source.

The Unity plugin folders remain useful as legacy reference material only.

## Lua Plugin Deployment Model

The editor should automatically copy/sync the built Oasis Lua plugin files into each installed MAME version.

Source:

```text
AppContext.BaseDirectory\\Assets\\MAME\\plugins\\oasis\\
```

Destination example:

```text
%LOCALAPPDATA%\\OasisEditor\\MAME\\versions\\mame0258\\plugins\\oasis\\
```

The user should not manually manage this.

## Plugin Sync Rules

Plugin sync should occur:

- after MAME download/install;
- during startup validation if a selected/installed MAME version exists but plugin deployment is missing or stale;
- before MAME launch if validation fails;
- when editor plugin assets change;
- when the user explicitly clicks Re-sync Lua plugins.

Sync should:

- create missing folders;
- overwrite outdated plugin files;
- avoid deleting unrelated user files;
- preserve recursive folder structure;
- log source and destination paths;
- fail gracefully if the built asset source is missing.

## MAME Version Discovery

The editor should move away from requiring users to type or know a MAME version number.

Preferred behavior:

- discover the latest official MAME Windows command-line/binary release automatically;
- show the discovered latest version in Preferences;
- provide a Download Latest action;
- allow a specific version override only as an advanced/debug option.

Implementation guidance:

- add a `MameVersionCatalogService` that resolves available/latest versions separately from install/download logic;
- cache catalog results in LocalAppData so the editor can still start if the network is unavailable;
- make catalog refresh async and cancellable;
- handle website/network parsing failures without crashing;
- retain the existing version-to-URL construction as a fallback once a version number is known.

Codex should prefer small, testable steps:

1. introduce catalog service abstraction and data model;
2. implement latest-version lookup;
3. cache latest-version metadata;
4. wire Preferences to display latest/installed/selected state;
5. then wire Download Latest.

## Startup MAME Setup Policy

On editor/launcher startup, the app should check MAME setup in the background.

Startup validation should determine:

- whether a selected MAME version exists;
- whether any installed MAME version exists;
- whether the selected/installed version has a valid `mame.exe`;
- whether the Oasis Lua plugin has been deployed into that version;
- whether an incomplete download/extraction can be resumed, repaired, or discarded.

Preferred policy:

1. Do not block the Launcher or Editor just because MAME is missing.
2. Show MAME setup status in the MAME Preferences page and output log.
3. If no valid MAME install exists, automatically start discovering the latest version in the background.
4. Do not automatically download large MAME binaries without a clear user opt-in yet.
5. Once the user clicks Download Latest, run download/extract/plugin-sync with visible progress.
6. After download completes, select the new version automatically if no valid version was already selected.

Rationale:

- MAME setup may be slow or network-dependent.
- Users should still be able to use non-emulation editor features.
- Automatic background discovery is safe; automatic large downloads should wait for explicit user action until UX is more mature.

Future option:

- add a first-run prompt: `MAME is required for emulation. Download latest MAME now?`

## Progress UX Policy

Use non-blocking background progress by default.

Recommended UI:

- MAME Preferences page contains current operation/status/progress.
- Main launcher/editor may show a small status line or output-log entries.
- Long-running operations expose Cancel where safe.
- Disable only MAME-specific actions while a MAME operation is running.
- Do not modal-block the whole editor except for critical confirmations or destructive actions.

Use modal dialogs only for:

- confirming removal of an installed MAME version;
- first-run optional prompt, if later implemented;
- fatal errors requiring user acknowledgement, which should be rare.

## Download / Install State Machine

A MAME install should have explicit states, for example:

- NotInstalled
- DiscoveringLatest
- DownloadAvailable
- Downloading
- Downloaded
- Extracting
- InstallingPlugins
- Installed
- Invalid
- Failed

The UI and logs should be driven by this state rather than ad-hoc booleans.

## Validation Rules

Validation should confirm:

- selected MAME version exists;
- `mame.exe` exists;
- required Lua plugin source assets exist in app output;
- required Lua plugin files exist in the selected MAME install;
- plugin deployment matches expected source files;
- required runtime folders exist.

Validation errors should:

- appear in the output log;
- appear in the Preferences UI;
- never crash the editor.

## Runtime Ownership Model

The editor owns:

- downloaded MAME installs;
- plugin deployment;
- runtime folder structure;
- diagnostics;
- latest-version discovery/cache.

The user owns:

- choosing whether to download MAME;
- choosing which installed MAME version to use, if multiple exist;
- project configuration.

## Architectural Direction

Do not directly port Unity editor architecture.

Preferred architecture:

```text
WPF UI
    ↓
ViewModels
    ↓
MAME Services
    ↓
Filesystem / Process / HTTP
```

Suggested services:

- `MameInstallService`
- `MameVersionCatalogService`
- `MamePluginDeploymentService`
- `MameStartupValidationService`
- `MameProcessService`
- `MameRuntimeStateService`
- `MameDiagnosticsService`

## Preferences UI Architecture

The Preferences window should become category-driven.

Suggested structure:

```text
PreferencesWindow
    ├── PreferencesCategoryViewModel
    ├── AppearancePreferencesView
    ├── MamePreferencesView
    └── Shared Preferences Models
```

Avoid one giant XAML/settings view.

## Codex Guidance

Codex should:

- treat `AppContext.BaseDirectory\\Assets\\MAME\\plugins\\oasis` as the packaged source plugin directory;
- deploy plugins automatically into each installed MAME version;
- build latest-version discovery as a service separate from download/install;
- perform startup validation in the background;
- avoid modal-blocking the editor for MAME setup;
- keep changes incremental and reviewable.

Codex should not:

- hardcode absolute machine-specific paths;
- require manual Lua plugin copying;
- expose unnecessary low-level filesystem configuration;
- auto-download large MAME archives on startup without user opt-in;
- continue using Unity asset folders as runtime dependencies.
