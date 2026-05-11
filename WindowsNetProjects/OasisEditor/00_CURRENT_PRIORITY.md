# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. MAME ROM management.
2. Project-level ROM settings.
3. ROM auto-download orchestration.
4. ROM validation/state management.
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

Implement the MAME ROM management system incrementally.

Current desired direction:

- add ROM name project setting;
- add ROM status UI;
- add ROM download button;
- add project-level auto-download checkbox;
- port archive.org ROM URL logic from the Unity project;
- modernize the provisioning/download architecture;
- add validation/state/progress handling;
- add tests.

## Current Architectural Goals

- ROM management should remain mostly invisible to the user.
- ROM downloads should run in the background.
- ROM validation should be automatic.
- ROM setup should never block the editor.
- ROM downloads should trigger only after edit completion, not per keypress.
- The editor should own the managed ROM storage location.
- Startup/project loading should remain resilient.

## Testing Direction

Codex should add or extend unit tests around:

- project setting migration/default behavior;
- ROM-name persistence;
- auto-download enabled behavior;
- auto-download disabled behavior;
- delayed trigger behavior;
- project-load validation;
- existing ROM detection;
- failed download handling;
- state transitions;
- preserving working ROMs.

The tests should not require real internet downloads.

Use fake services and fake provisioning/download dependencies where practical.

## Do Not Work On Yet

Do not continue unrelated canvas, panel layout, performance, copy/paste, ordering, locking, visibility, or general editor tasks unless explicitly instructed.

Do not continue deep MAME runtime/process integration until ROM provisioning/validation is more stable.

## Desired Output From Codex

Codex should produce small focused changes that:

- add ROM project settings;
- add ROM provisioning architecture;
- modernize ROM download handling;
- add tests;
- preserve existing provisioning behavior;
- include diagnostics/logging;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- ROM name persists correctly;
- ROM status updates correctly;
- auto-download works on project load;
- auto-download works after edit completion;
- downloads do not trigger per keystroke;
- manual Download button works;
- ROMs appear in managed LocalAppData folder;
- MAME can see downloaded ROMs;
- failed downloads do not crash the editor;
- previous working ROMs remain usable;
- no unrelated editor behavior changed.
