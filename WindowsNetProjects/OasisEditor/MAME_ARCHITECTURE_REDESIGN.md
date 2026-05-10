# MAME Architecture Redesign

This document supersedes earlier assumptions in `TASKS_MAME_EMULATION_PORT.md` relating to user-configurable MAME filesystem paths.

## Goals

- Minimize end-user configuration.
- Keep MAME installs isolated per version.
- Make Lua plugin deployment automatic.
- Reduce invalid setup states.
- Keep the Preferences UI scalable as more settings categories are added.
- Separate editor-owned assets from downloaded runtime assets.

## Preferences Window Redesign

The Preferences window should move to a two-pane category layout.

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

- Installed MAME versions list
- Selected active version
- Download version
- Remove version
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
%LOCALAPPDATA%\\OasisEditor\\MAME\\versions\\mame0258\\
%LOCALAPPDATA%\\OasisEditor\\MAME\\versions\\mame0260\\
```

Each version should be isolated.

The selected version determines:

- which `mame.exe` is launched;
- which plugins folder is used;
- which runtime working directory is used.

## Lua Plugin Source Layout

Lua plugin source files should live inside the editor project/repository.

Suggested location:

```text
WindowsNetProjects/OasisEditor/OasisEditor/Assets/MAME/plugins/oasis/
```

Alternative acceptable location:

```text
WindowsNetProjects/OasisEditor/Assets/MAME/plugins/oasis/
```

The exact location is less important than:

- being editor-owned;
- version-controlled;
- available at build/runtime;
- not depending on Unity asset locations.

## Lua Plugin Deployment Model

The editor should automatically copy/sync the Oasis Lua plugin files into each installed MAME version.

Example deployed location:

```text
%LOCALAPPDATA%\\OasisEditor\\MAME\\versions\\mame0258\\plugins\\oasis\\
```

The user should not manually manage this.

## Plugin Sync Rules

Plugin sync should occur:

- after MAME download/install;
- before MAME launch if validation fails;
- when editor plugin assets change;
- when the user explicitly clicks Re-sync Lua plugins.

Sync should:

- create missing folders;
- overwrite outdated plugin files;
- avoid deleting unrelated user files.

## Validation Rules

Validation should confirm:

- selected MAME version exists;
- `mame.exe` exists;
- required Lua plugin files exist;
- plugin deployment matches expected version;
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
- diagnostics.

The user owns:

- choosing which MAME version to use;
- initiating downloads/removal;
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

- refactor the Preferences window layout before adding more MAME controls;
- remove editable install-root/plugin-directory concepts;
- transition toward automatic managed runtime folders;
- implement MAME state/actions rather than raw path editing;
- keep changes incremental and reviewable.

Codex should not:

- hardcode absolute machine-specific paths;
- require manual Lua plugin copying;
- expose unnecessary low-level filesystem configuration.
