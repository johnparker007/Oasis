# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. Rebuild Edit View from the working Game/Play View foundation.
2. Replace old WPF-heavy Edit View architecture.
3. Shared Skia renderer consumption.
4. Document-space hit testing and selection.
5. Lightweight editor overlay architecture.
6. Runtime rendering performance improvements.

Codex should prioritize this work before continuing additional runtime-driven component work or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `SKIA_EDIT_VIEW_REBUILD_FROM_GAME_VIEW_PLAN.md`
3. `SKIA_EDIT_VIEW_MIGRATION_PLAN.md`
4. `SKIA_RUNTIME_RENDERER_PLAN.md`
5. `MAME_INPUT_MAP_AND_PLAY_VIEW_PLAN.md`
6. `PANEL2D_RUNTIME_STATE_PERFORMANCE_PLAN.md`
7. `OUTPUT_LOG_ENHANCEMENT_PLAN.md`
8. `MAME_EMULATION_RUNTIME_PLAN.md`
9. `MAME_ROM_MANAGEMENT_PLAN.md`
10. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
11. `MAME_VERSION_DISCOVERY_PLAN.md`
12. `MAME_ARCHITECTURE_REDESIGN.md`
13. `CODEX_NEXT_STEPS_MAME.md`
14. `TASKS_MAME_EMULATION_PORT.md`
15. `AGENT.md`
16. `TASKS.md`

## Immediate Task

Stop patching the old Edit View internals.

Instead:

- create a new Skia Edit View from the working Game/Play View foundation;
- reuse the working pan/zoom and Skia renderer path from Game/Play View;
- rebuild editing interaction incrementally on top;
- retire the old WPF-heavy Edit View runtime rendering path once replacement is stable.

## Current Architectural Goals

- Game/Play View becomes the rendering foundation for the new Edit View.
- Skia remains the canonical machine renderer.
- Runtime machine visuals should no longer use WPF component trees.
- Editing interaction should become a lightweight overlay layer.
- Pan/zoom should work consistently everywhere.
- Document-space hit testing should replace WPF component hit testing.
- Live emulation should remain smooth inside the Edit View.
- Runtime rendering failures should never crash the editor.

## Testing Direction

Codex should add or extend unit tests around:

- viewport transform math;
- document-space hit testing;
- selection service behavior;
- selection rectangle calculations;
- document-space drag delta calculations;
- overlay bounds calculations.

Avoid heavy pixel-perfect rendering tests initially.

## Do Not Work On Yet

Do not continue trying to patch/fix the old WPF Edit View architecture.

Do not remove WPF entirely from the editor.

Do not rewrite serialization or undo/redo.

Do not introduce GPU shader/HLSL rendering yet.

## Desired Output From Codex

Codex should produce small focused changes that:

- create a new Skia Edit View based on Game/Play View;
- preserve working Game/Play View pan/zoom behavior;
- rebuild selection/edit interaction incrementally;
- remove WPF runtime rendering overhead;
- improve runtime rendering performance;
- add tests and diagnostics;
- preserve editing usability;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- Panel2D opens in the new Skia Edit View;
- pan works everywhere;
- zoom works everywhere;
- live emulation still renders smoothly;
- click-to-select works;
- selection overlay aligns correctly;
- drag-select multi-select works when implemented;
- move/resize works when reintroduced;
- old WPF runtime rendering path is no longer active;
- no unrelated editor behavior regressed.
