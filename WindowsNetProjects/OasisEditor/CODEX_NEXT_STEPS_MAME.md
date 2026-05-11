# Codex Next Steps - MAME Redesign Alignment

Read these documents first:

- `00_CURRENT_PRIORITY.md`
- `MAME_ROM_MANAGEMENT_PLAN.md`
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
- ROM provisioning;
- ROM validation/state handling;
- runtime validation;
- LocalAppData runtime structure.

The user should not manually manage:

- plugin directories;
- working directories;
- install roots;
- plugin copying;
- first-run MAME setup;
- latest-version lookup;
- ROM filesystem paths.

## Plugin Source Direction

The Oasis Lua plugin files are now committed under the new editor asset tree and included in the Visual Studio project output.

Runtime source should resolve from:

```text
AppContext.BaseDirectory\\Assets\\MAME\\plugins\\oasis\\
```

Codex should no longer use Unity asset folders as runtime plugin dependencies.

## Current Priority

The next MAME work should focus on:

1. ROM project settings.
2. ROM status/provisioning UI.
3. ROM auto-download orchestration.
4. ROM validation/state architecture.
5. ROM provisioning tests.
6. Runtime validation architecture.
7. Background progress UX.

Do not jump directly into full runtime emulation integration yet.

## Desired ROM Behavior

Default behavior for projects:

- ROM auto-download enabled;
- ROM validation on project load;
- ROM validation after edit completion;
- background ROM provisioning;
- passive status/progress updates;
- managed LocalAppData ROM storage.

If auto-download is disabled:

- do not automatically download missing ROMs;
- still validate selected ROM;
- still allow manual Download action;
- still show status/errors.

## Legacy Porting Direction

The legacy Unity editor already contains working ROM download URL logic using archive.org.

Codex should:

- locate the legacy ROM download implementation under `UnityProjects/LayoutEditor`;
- port the core archive.org URL logic;
- modernize async/provisioning architecture;
- avoid porting Unity coroutine/UI patterns directly.

## Testing Direction

Codex should add or extend proper unit tests around:

- project setting migration/default behavior;
- ROM-name persistence;
- auto-download enabled behavior;
- auto-download disabled behavior;
- delayed trigger behavior after edit completion;
- project-load validation;
- existing ROM detection;
- failed download handling;
- state transitions;
- preserving working ROMs.

Tests should not require live network access.

Prefer:

- fake services;
- fake orchestrator dependencies;
- fake download providers;
- fake runtime state providers;
- fake filesystem abstractions.

## Desired Startup / Project-Load Behavior

On project load:

- validate configured ROM state in the background;
- determine whether ROM exists locally;
- determine whether ROM appears valid/readable;
- automatically provision/download ROM if missing and auto-download is enabled;
- update UI/log state without blocking the editor.

The editor should remain usable while validation/download occurs.

## Provisioning Policy

Preferred behavior:

- background validation;
- automatic download/install when required;
- visible passive progress;
- cancellable operations where safe;
- minimal interruption.

Do not attempt ROM download on every keypress while editing the ROM name.

Trigger validation/download only after edit completion:

- Enter key;
- focus loss;
- explicit Download button;
- project load;
- pre-emulation validation.

## Progress UX Direction

Prefer non-modal progress.

Use:

- status text;
- progress indicators in Project Settings;
- output-log messages;
- passive launcher/editor status indicators;
- cancellable async tasks.

Avoid modal blocking dialogs except for:

- destructive confirmation;
- unrecoverable setup failure;
- emulation requested before provisioning completes.

## Recommended Next Codex Task

Implement Step 1 and Step 2 from `MAME_ROM_MANAGEMENT_PLAN.md`.

Specifically:

- add `MameRomName` project setting;
- add `AutomaticallyDownloadMissingRoms` project setting;
- default auto-download to true;
- preserve backwards compatibility with old project files;
- add ROM textbox UI;
- add ROM status label;
- add ROM Download button;
- add ROM auto-download checkbox;
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
