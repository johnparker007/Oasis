# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. CLI/headless automation pipeline architecture.
2. Extraction of reusable project/import/save services.
3. Automation command runner abstractions.
4. UI-independent MFME import workflow.
5. Future CLI conversion workflow support.
6. Preparing for future OasisEditor.Core / OasisEditor.Cli separation.

Codex should prioritize this work before unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `CLI_AUTOMATION_PIPELINE_PLAN.md`
3. `SKIA_RENDER_PERFORMANCE_CACHE_PLAN.md`
4. `SKIA_EDIT_VIEW_REBUILD_FROM_GAME_VIEW_PLAN.md`
5. `SKIA_EDIT_VIEW_MIGRATION_PLAN.md`
6. `SKIA_RUNTIME_RENDERER_PLAN.md`
7. `MAME_INPUT_MAP_AND_PLAY_VIEW_PLAN.md`
8. `PANEL2D_RUNTIME_STATE_PERFORMANCE_PLAN.md`
9. `OUTPUT_LOG_ENHANCEMENT_PLAN.md`
10. `MAME_EMULATION_RUNTIME_PLAN.md`
11. `MAME_ROM_MANAGEMENT_PLAN.md`
12. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
13. `MAME_VERSION_DISCOVERY_PLAN.md`
14. `MAME_ARCHITECTURE_REDESIGN.md`
15. `CODEX_NEXT_STEPS_MAME.md`
16. `TASKS_MAME_EMULATION_PORT.md`
17. `AGENT.md`
18. `TASKS.md`

## Immediate Task

Do not build an in-app terminal.

Instead:

- extract reusable project/import/save services;
- add automation command abstractions;
- make MFME import callable without WPF dialogs/views;
- build a reusable automation pipeline;
- prepare for future CLI/headless workflows.

## Current Architectural Goals

- Core project/import/export logic should become UI-independent.
- GUI workflows and CLI workflows should reuse the same services.
- Automation should not require visible WPF views.
- Existing editor UI workflows should continue working.
- Future `OasisEditor.Core` / `OasisEditor.Cli` split should become easier.
- Future command palette/debug terminal should be able to reuse the same command pipeline.
- Runtime/editor rendering work should remain stable during this extraction.

## Testing Direction

Codex should add or extend unit tests around:

- automation command runner behavior;
- command sequencing/failure behavior;
- cancellation handling;
- create project/panel services;
- MFME import automation invocation;
- save service invocation;
- CLI argument mapping where implemented.

Avoid tests requiring visible WPF windows.

## Do Not Work On Yet

Do not implement a full in-app terminal.

Do not implement scripting language parsing.

Do not perform a giant solution-wide project split yet.

Do not rewrite undo/redo or serialization.

Do not redesign Skia rendering architecture during this workstream.

## Desired Output From Codex

Codex should produce small focused changes that:

- extract reusable automation services;
- add command runner abstractions;
- reduce UI coupling;
- prepare for future CLI/headless conversion;
- preserve existing editor workflows;
- add tests and diagnostics;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- existing UI project creation still works;
- existing UI MFME import still works;
- existing UI save still works;
- automation pipeline can create/import/save without requiring editor interaction;
- logging/errors remain understandable;
- no unrelated editor behavior regressed.
