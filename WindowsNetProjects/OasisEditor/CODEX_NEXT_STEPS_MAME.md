# Codex Next Steps - MAME Redesign Alignment

Read these documents first:

- `00_CURRENT_PRIORITY.md`
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

1. Live version discovery.
2. Fallback catalog architecture.
3. Version discovery tests.
4. Runtime validation architecture.
5. Plugin deployment service.
6. Automatic background setup/provisioning.
7. Download/install state management.
8. Background progress UX.

Do not jump directly into full runtime emulation integration yet.

## Desired Version Discovery Behavior

Preferred source order:

1. mamedev.org release page.
2. GitHub MAME releases.
3. cached LocalAppData catalog.
4. compiled seed fallback.

The app should:

- discover latest MAME asynchronously;
- cache successful results;
- merge discovered versions with cached/seed versions;
- remain fully usable offline;
- never crash due to network or parse failure.

## Testing Direction

Codex should begin adding proper unit tests around:

- release-page parsing;
- GitHub release parsing;
- version normalization;
- version ordering;
- cache compatibility;
- fallback chain behavior;
- malformed/unexpected HTML.

Tests should not require live network access.

Prefer:

- fake HTML content;
- fake service implementations;
- injected abstractions around HTTP/cache/time.

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

Implement Step 1 and Step 2 from `MAME_VERSION_DISCOVERY_PLAN.md`.

Specifically:

- extract pure parsing helpers;
- add normalization helpers;
- add parser tests;
- add version ordering tests;
- extend cache metadata model;
- preserve backwards compatibility with old cache JSON.

Keep implementation incremental.

## Important Constraint

Codex cannot runtime-test WPF/MAME integration.

All changes should therefore:

- compile cleanly where possible;
- be incremental;
- include manual test steps;
- include diagnostics/logging;
- avoid giant rewrites.
