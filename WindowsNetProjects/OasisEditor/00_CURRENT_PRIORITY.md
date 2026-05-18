# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. Skia Edit View migration.
2. Shared Skia renderer consumption.
3. WPF overlay/editor chrome separation.
4. Removal of WPF runtime visual trees.
5. Shared pan/zoom alignment.
6. Runtime rendering performance improvements.

Codex should prioritize this work before continuing additional runtime-driven component work or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `SKIA_EDIT_VIEW_MIGRATION_PLAN.md`
3. `SKIA_RUNTIME_RENDERER_PLAN.md`
4. `MAME_INPUT_MAP_AND_PLAY_VIEW_PLAN.md`
5. `PANEL2D_RUNTIME_STATE_PERFORMANCE_PLAN.md`
6. `OUTPUT_LOG_ENHANCEMENT_PLAN.md`
7. `MAME_EMULATION_RUNTIME_PLAN.md`
8. `MAME_ROM_MANAGEMENT_PLAN.md`
9. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
10. `MAME_VERSION_DISCOVERY_PLAN.md`
11. `MAME_ARCHITECTURE_REDESIGN.md`
12. `CODEX_NEXT_STEPS_MAME.md`
13. `TASKS_MAME_EMULATION_PORT.md`
14. `AGENT.md`
15. `TASKS.md`

## Immediate Task

Migrate the Edit View to use the shared Skia renderer incrementally.

Current desired direction:

- embed the shared Skia renderer into the Edit View;
- preserve WPF editor overlays/chrome;
- remove WPF runtime rendering paths;
- align Skia rendering and WPF overlays through the shared viewport transform;
- preserve editing workflows;
- improve live emulation performance inside the Edit View;
- add tests and diagnostics.

## Current Architectural Goals

- Skia becomes the canonical machine renderer.
- WPF becomes editor overlay/chrome only.
- Runtime visuals should no longer use large WPF visual trees.
- Edit View and Play View should share renderer infrastructure.
- Selection and editing workflows should remain stable.
- Pan/zoom should remain visually aligned.
- Live emulation should remain smooth inside the Edit View.
- Runtime rendering failures should never crash the editor.

## Testing Direction

Codex should add or extend unit tests around:

- viewport transform consistency;
- overlay/document coordinate conversion;
- selection bounds conversion;
- hit-testing conversion math;
- renderer invalidation behavior.

Avoid heavy pixel-perfect rendering tests initially.

## Do Not Work On Yet

Do not remove WPF entirely from the editor.

Do not rewrite the entire editor interaction model.

Do not introduce GPU shader/HLSL rendering yet.

Do not redesign document serialization or undo/redo.

## Desired Output From Codex

Codex should produce small focused changes that:

- migrate Edit View rendering to shared Skia renderer;
- preserve WPF overlay/editor workflows;
- remove WPF runtime rendering overhead;
- improve runtime rendering performance;
- add tests and diagnostics;
- preserve editing convenience and usability;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- Edit View visually matches Play View closely enough;
- live emulation remains smooth in Edit View;
- flashing lamps no longer cause severe slowdown;
- alpha displays render smoothly;
- segment displays render smoothly;
- reels remain smooth;
- selection still works;
- multi-selection still works;
- drag/resize still works;
- pan/zoom remains aligned;
- context menus still work;
- inspector integration still works;
- no unrelated editor behavior regressed.
