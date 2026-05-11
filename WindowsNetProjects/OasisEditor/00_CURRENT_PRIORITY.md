# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. Configurable ROM download source preferences.
2. ROM URL construction architecture.
3. `.zip` / `.7z` ROM archive handling.
4. ROM provisioning integration.
5. ROM provisioning tests.

Codex should prioritize this work before continuing older canvas/performance/layout tasks from `TASKS.md` or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `MAME_ROM_MANAGEMENT_PLAN.md`
3. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
4. `MAME_VERSION_DISCOVERY_PLAN.md`
5. `MAME_ARCHITECTURE_REDESIGN.md`
6. `CODEX_NEXT_STEPS_MAME.md`
7. `TASKS_MAME_EMULATION_PORT.md`
8. `AGENT.md`
9. `TASKS.md`

## Immediate Task

Implement configurable ROM download source preferences incrementally.

Current desired direction:

- move ROM source URL configuration into Preferences -> MAME;
- support `.7z` and `.zip` ROM archive formats;
- add Reset to default behavior;
- derive URLs from base URL + ROM name + extension;
- modernize ROM URL construction architecture;
- add validation/state/progress handling;
- add tests.

## Current Architectural Goals

- ROM management should remain mostly invisible to the user.
- ROM downloads should run in the background.
- ROM validation should be automatic.
- ROM setup should never block the editor.
- ROM downloads should trigger only after edit completion, not per keypress.
- The editor should own the managed ROM storage location.
- ROM source configuration should be global editor state, not project state.
- Startup/project loading should remain resilient.

## Testing Direction

Codex should add or extend unit tests around:

- preference migration/default behavior;
- ROM URL construction;
- `.zip` and `.7z` extension handling;
- reset-to-default behavior;
- invalid URL handling;
- invalid extension handling;
- ROM-name persistence;
- auto-download enabled behavior;
- delayed trigger behavior;
- failed download handling;
- state transitions.

The tests should not require real internet downloads.

Use fake services and fake provisioning/download dependencies where practical.

## Do Not Work On Yet

Do not continue unrelated canvas, panel layout, performance, copy/paste, ordering, locking, visibility, or general editor tasks unless explicitly instructed.

Do not continue deep MAME runtime/process integration until ROM provisioning/validation is more stable.

## Desired Output From Codex

Codex should produce small focused changes that:

- add ROM source preferences;
- add ROM URL construction services;
- modernize ROM download handling;
- add tests;
- preserve existing provisioning behavior;
- include diagnostics/logging;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- global ROM download base URL persists correctly;
- `.7z` and `.zip` dropdown values persist correctly;
- Reset to default restores the archive.org example values correctly;
- constructed URLs are correct;
- auto-download still works;
- downloads do not trigger per keystroke;
- manual Download button works;
- ROMs appear in managed LocalAppData folder;
- MAME can see downloaded ROMs;
- failed downloads do not crash the editor;
- previous working ROMs remain usable;
- no unrelated editor behavior changed.
