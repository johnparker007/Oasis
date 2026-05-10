# Codex Next Steps - MAME Redesign Alignment

Read these documents first:

- `00_CURRENT_PRIORITY.md`
- `MAME_ARCHITECTURE_REDESIGN.md`
- `TASKS_MAME_EMULATION_PORT.md`

The redesign document overrides earlier assumptions around:

- editable install-root settings;
- editable Lua plugin directory settings;
- flat Preferences layout;
- Unity-based runtime plugin sourcing.

## Current Architectural Direction

The editor now owns:

- MAME install management;
- MAME version discovery;
- plugin deployment;
- runtime validation;
- LocalAppData runtime structure.

The user should not manually manage:

- plugin directories;
- working directories;
- install roots;
- plugin copying.

## Plugin Source Direction

The Oasis Lua plugin files are now committed under the new editor asset tree and included in the Visual Studio project output.

Runtime source should resolve from:

```text
AppContext.BaseDirectory\\Assets\\MAME\\plugins\\oasis\\
```

Codex should no longer use Unity asset folders as runtime plugin dependencies.

## Current Priority

The next MAME work should focus on:

1. Preferences/settings stabilization.
2. Runtime validation architecture.
3. Plugin deployment service.
4. Latest-version discovery.
5. Download/install state management.
6. Background setup/progress UX.

Do not jump directly into full runtime emulation integration yet.

## Desired Startup Behavior

On launcher/editor startup:

- validate installed/selected MAME state in the background;
- determine whether a valid MAME install exists;
- determine whether plugin deployment is valid;
- discover latest available MAME version asynchronously;
- update UI/log state without blocking the app.

The editor should remain usable while setup/validation occurs.

## Download Policy

Preferred behavior:

- background discovery;
- explicit user-triggered download/install;
- visible progress;
- cancellable operations where safe.

Do not auto-download large MAME archives during startup without explicit user action.

## Progress UX Direction

Prefer non-modal progress.

Use:

- status text;
- progress indicators in the MAME Preferences category;
- output-log messages;
- cancellable async tasks.

Avoid modal blocking dialogs except for:

- destructive confirmation;
- rare fatal errors.

## Recommended Next Codex Task

Implement a background-oriented MAME setup state architecture.

Suggested scope:

- introduce a setup/install state model;
- introduce startup validation service abstraction;
- introduce plugin deployment service abstraction;
- wire Preferences UI to display current setup state;
- add latest-version placeholder/discovery plumbing;
- add output-log diagnostics for startup validation.

Keep implementation incremental.

## Important Constraint

Codex cannot runtime-test WPF/MAME integration.

All changes should therefore:

- compile cleanly where possible;
- be incremental;
- include manual test steps;
- include diagnostics/logging;
- avoid giant rewrites.
