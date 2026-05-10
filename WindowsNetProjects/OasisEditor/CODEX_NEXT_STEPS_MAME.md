# Codex Next Steps - MAME Redesign Alignment

Read these documents first:

- `00_CURRENT_PRIORITY.md`
- `MAME_AUTO_UPDATE_POLICY_PLAN.md`
- `MAME_VERSION_DISCOVERY_PLAN.md`
- `MAME_ARCHITECTURE_REDESIGN.md`
- `TASKS_MAME_EMULATION_PORT.md`

The redesign documents override earlier assumptions around:

- editable install-root settings;
- editable Lua plugin directory settings;
- flat Preferences layout;
- Unity-based runtime plugin sourcing;
- user-confirmed/manual-first MAME provisioning;
- hardcoded latest-version discovery.

## Current Architectural Direction

The editor now owns:

- automatic MAME provisioning;
- automatic MAME update policy;
- MAME install management;
- live MAME version discovery;
- fallback version-catalog handling;
- plugin deployment;
- runtime validation;
- LocalAppData runtime structure.

The user should not manually manage:

- plugin directories;
- working directories;
- install roots;
- plugin copying;
- first-run MAME setup;
- latest-version lookup.

## Plugin Source Direction

The Oasis Lua plugin files are now committed under the new editor asset tree and included in the Visual Studio project output.

Runtime source should resolve from:

```text
AppContext.BaseDirectory\\Assets\\MAME\\plugins\\oasis\\
```

Codex should no longer use Unity asset folders as runtime plugin dependencies.

## Current Priority

The next MAME work should focus on:

1. Auto-update preference.
2. Automatic latest-version selection policy.
3. Auto-update orchestration.
4. Auto-update tests.
5. Runtime validation architecture.
6. Plugin deployment service.
7. Background progress UX.

Do not jump directly into full runtime emulation integration yet.

## Desired Auto-Update Behavior

Default behavior for new installs:

- auto-update enabled;
- discover latest MAME in background;
- automatically install latest when needed;
- automatically select latest after successful install;
- preserve previous working versions.

If auto-update is disabled:

- do not automatically replace the selected version;
- still validate selected/current installs;
- still allow manual update/install actions;
- still allow plugin repair/sync.

## Testing Direction

Codex should add or extend proper unit tests around:

- preference migration/default behavior;
- auto-update enabled behavior;
- auto-update disabled behavior;
- latest-version selection;
- deferred update while MAME is running;
- fallback behavior during failed discovery;
- preserving previous working installs.

Tests should not require live network access.

Prefer:

- fake services;
- fake orchestrator dependencies;
- fake version catalogs;
- fake runtime state providers.

## Desired Startup Behavior

On launcher/editor startup:

- validate installed/selected MAME state in the background;
- determine whether a valid MAME install exists;
- determine whether plugin deployment is valid;
- discover latest available MAME version asynchronously;
- automatically provision latest MAME if no valid install exists;
- automatically install/select latest MAME if auto-update is enabled and a newer version exists;
- automatically repair/re-sync plugins when possible;
- update UI/log state without blocking the app.

The editor should remain usable while setup/validation occurs.

## Provisioning Policy

Preferred behavior:

- background validation;
- background latest-version discovery;
- automatic download/install when required;
- automatic latest-version selection when enabled;
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

Implement Step 1 and Step 2 from `MAME_AUTO_UPDATE_POLICY_PLAN.md`.

Specifically:

- add `KeepMameUpToDateAutomatically` preference field;
- default it to true;
- preserve backwards compatibility with old preferences;
- add MAME Preferences checkbox UI;
- add persistence/migration tests;
- add manual verification steps.

Keep implementation incremental.

## Important Constraint

Codex cannot runtime-test WPF/MAME integration.

All changes should therefore:

- compile cleanly where possible;
- be incremental;
- include manual test steps;
- include diagnostics/logging;
- avoid giant rewrites.
