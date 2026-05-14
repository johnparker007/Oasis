# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. Panel2D runtime state performance.
2. Runtime-state/document-model separation.
3. Batched MAME runtime updates.
4. Runtime visual adapters/registries.
5. Lamp rendering optimization.
6. Runtime-state tests and diagnostics.

Codex should prioritize this work before continuing additional runtime-driven component work or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `PANEL2D_RUNTIME_STATE_PERFORMANCE_PLAN.md`
3. `OUTPUT_LOG_ENHANCEMENT_PLAN.md`
4. `MAME_EMULATION_RUNTIME_PLAN.md`
5. `MAME_ROM_MANAGEMENT_PLAN.md`
6. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
7. `MAME_VERSION_DISCOVERY_PLAN.md`
8. `MAME_ARCHITECTURE_REDESIGN.md`
9. `CODEX_NEXT_STEPS_MAME.md`
10. `TASKS_MAME_EMULATION_PORT.md`
11. `AGENT.md`
12. `TASKS.md`

## Immediate Task

Implement the runtime-state/render-performance architecture incrementally.

Current desired direction:

- separate runtime lamp state from the document/edit pipeline;
- batch/coalesce MAME runtime updates;
- update existing WPF visuals in place;
- avoid canvas rebuilds during lamp flashing;
- avoid undo/redo/dirty-state updates during runtime changes;
- modernize runtime rendering architecture;
- add runtime-state tests and diagnostics.

## Current Architectural Goals

- Runtime emulation updates should remain asynchronous and non-blocking.
- Runtime state should not mutate the design-time document model.
- Lamp flashing should be lightweight enough to avoid maxing out CPU cores.
- Existing edit Panel2D pane should remain capable of showing live emulation.
- Runtime visual updates should operate on existing visual instances.
- UI-thread dispatch frequency should be coalesced/throttled.
- Runtime failures should never crash the editor.
- The architecture should support future runtime-driven components.

## Testing Direction

Codex should add or extend unit tests around:

- runtime-state coalescing;
- dirty snapshot behavior;
- runtime visual adapters;
- missing lamp mappings;
- runtime updates not marking project dirty;
- runtime updates not creating undo entries;
- batched update behavior;
- layout rebuild/runtime-state reapply behavior.

The tests should not require live MAME or heavy WPF visual integration.

Use fake runtime visual adapters and fake runtime state dependencies where practical.

## Do Not Work On Yet

Do not implement a separate Preview/Runtime pane yet.

Do not implement reels/meters/segment displays yet.

Do not redesign all Panel2D components yet.

Do not continue unrelated canvas/editor tasks unless explicitly instructed.

## Desired Output From Codex

Codex should produce small focused changes that:

- isolate runtime state from design-time state;
- modernize runtime visual update architecture;
- optimize lamp rendering performance;
- add runtime diagnostics/logging;
- add tests;
- preserve editing convenience and workflow;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- lamp flashing no longer maxes out a CPU core;
- live emulation still appears in the edit Panel2D pane;
- editor remains responsive while emulation runs;
- project is not marked dirty by runtime lamp flashing;
- undo/redo history is not polluted by runtime updates;
- layout edits still rebuild correctly;
- stopping emulation leaves the panel in a sensible state;
- no unrelated editor behavior changed.
