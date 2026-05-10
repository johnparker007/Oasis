# Codex Next Steps - MAME Redesign Alignment

Read these documents first:

- `00_CURRENT_PRIORITY.md`
- `MAME_ARCHITECTURE_REDESIGN.md`
- `TASKS_MAME_EMULATION_PORT.md`

The redesign document overrides earlier assumptions around:

- editable install-root settings;
- editable Lua plugin directory settings;
- flat Preferences layout;
- Unity-based runtime plugin sourcing;
- user-confirmed/manual-first MAME provisioning.

## Current Architectural Direction

The editor now owns:

- automatic MAME provisioning;
- MAME install management;
- MAME version discovery;
- plugin deployment;
- runtime validation;
- LocalAppData runtime structure.

The user should not manually manage:

- plugin directories;
- working directories;
- install roots;
- plugin copying;
- first-run MAME setup.

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
5. Automatic background setup/provisioning.
6. Download/install state management.
7. Background progress UX.

Do not jump directly into full runtime emulation integration yet.

## Desired Startup Behavior

On launcher/editor startup:

- validate installed/selected MAME state in the background;
- determine whether a valid MAME install exists;
- determine whether plugin deployment is valid;
- discover latest available MAME version asynchronously;
- automatically provision latest MAME if no valid install exists;
- automatically repair/re-sync plugins when possible;
- update UI/log state without blocking the app.

The editor should remain usable while setup/validation occurs.

## Provisioning Policy

Preferred behavior:

- background validation;
- background latest-version discovery;
- automatic download/install when no valid install exists;
- visible passive progress;
- cancellable operations where safe;
- minimal interruption.

Do not show a first-run modal dialog just to approve downloading MAME.

## Progress UX Direction

Prefer non-modal progress.

Use:

- status text;
- progress indicators in the MAME Preferences category;
- output-log messages;
- passive launcher/editor status indicators;
- cancellable async tasks.

Avoid modal blocking dialogs except for:

- destructive confirmation;
- unrecoverable setup failure;
- emulation requested before setup completes.

## Recommended Next Codex Task

Implement the background-oriented MAME setup/provisioning architecture.

Suggested scope:

- introduce a setup/install state model;
- introduce startup validation service abstraction;
- introduce plugin deployment service abstraction;
- introduce setup orchestration abstraction;
- wire Preferences UI to display current setup/provisioning state;
- add latest-version discovery plumbing;
- add output-log diagnostics for startup validation/setup;
- add background progress state plumbing.

Keep implementation incremental.

## Important Constraint

Codex cannot runtime-test WPF/MAME integration.

All changes should therefore:

- compile cleanly where possible;
- be incremental;
- include manual test steps;
- include diagnostics/logging;
- avoid giant rewrites.
