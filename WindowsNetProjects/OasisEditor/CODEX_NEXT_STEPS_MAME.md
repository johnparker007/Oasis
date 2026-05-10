# Codex Next Steps - MAME Redesign Alignment

Read these documents first:

- `TASKS_MAME_EMULATION_PORT.md`
- `MAME_ARCHITECTURE_REDESIGN.md`

The redesign document overrides earlier assumptions around:

- editable install-root settings;
- editable Lua plugin directory settings;
- flat Preferences layout.

## Immediate Priority

Before continuing MAME implementation work:

1. Refactor the Preferences window into a category-based layout.
2. Move Theme settings into an Appearance category.
3. Move MAME settings into a MAME category.
4. Remove editable MAME Install Root UI.
5. Remove editable Lua Plugin Directory UI.
6. Introduce automatic LocalAppData-based runtime folder management.
7. Transition toward editor-managed plugin deployment.

## Desired Preferences UX

Left category list:

- Appearance
- MAME

Right content area:

- category-specific settings.

## Desired MAME UX

The user should think in terms of:

- installed versions;
- selected version;
- download/remove version;
- validation;
- diagnostics.

The user should NOT normally manage:

- runtime paths;
- plugin paths;
- working directories.

## Runtime Folder Direction

Use:

```text
%LOCALAPPDATA%\\OasisEditor\\MAME\\
```

Per-version installs:

```text
versions\\mame0258\\
versions\\mame0260\\
```

## Lua Plugin Direction

The editor owns the canonical plugin source.

The editor deploys/syncs plugins into installed MAME versions automatically.

## Important Constraint

Codex cannot runtime-test WPF/MAME integration.

All changes should therefore:

- compile cleanly where possible;
- be incremental;
- include manual test steps;
- include diagnostics/logging;
- avoid giant rewrites.

## Recommended Next Task

Implement ONLY the Preferences-window redesign and settings-model cleanup.

Do not continue process-launch/runtime integration work until the settings architecture is stabilized.
