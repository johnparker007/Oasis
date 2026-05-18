# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. Shared Skia runtime renderer.
2. Skia-based Play View.
3. Shared viewport transform architecture.
4. Shared text layout engine.
5. Runtime rendering performance.
6. Future Edit View renderer migration.

Codex should prioritize this work before continuing additional runtime-driven component work or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `SKIA_RUNTIME_RENDERER_PLAN.md`
3. `MAME_INPUT_MAP_AND_PLAY_VIEW_PLAN.md`
4. `PANEL2D_RUNTIME_STATE_PERFORMANCE_PLAN.md`
5. `OUTPUT_LOG_ENHANCEMENT_PLAN.md`
6. `MAME_EMULATION_RUNTIME_PLAN.md`
7. `MAME_ROM_MANAGEMENT_PLAN.md`
8. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
9. `MAME_VERSION_DISCOVERY_PLAN.md`
10. `MAME_ARCHITECTURE_REDESIGN.md`
11. `CODEX_NEXT_STEPS_MAME.md`
12. `TASKS_MAME_EMULATION_PORT.md`
13. `AGENT.md`
14. `TASKS.md`

## Immediate Task

Implement the Skia runtime renderer incrementally.

Current desired direction:

- introduce SkiaSharp rendering for Play View;
- centralize runtime rendering through shared renderer services;
- add shared viewport transform model;
- add deterministic shared text layout;
- render runtime machine visuals through Skia;
- preserve existing WPF edit workflows initially;
- prepare for later Edit View migration to shared Skia renderer plus WPF overlay;
- add tests and diagnostics.

## Current Architectural Goals

- Skia becomes the canonical machine renderer.
- WPF remains responsible for editor UI/chrome.
- Runtime rendering should scale to many rapidly updating components.
- Existing edit workflows should remain stable during migration.
- Play View should avoid WPF control trees for runtime visuals.
- Shared text layout should become deterministic and reusable.
- Pan/zoom should use one shared viewport transform.
- Runtime rendering failures should never crash the editor.

## Testing Direction

Codex should add or extend unit tests around:

- viewport transform math;
- text wrapping/alignment calculations;
- screen/document coordinate conversion;
- runtime renderer dispatch;
- runtime hit-testing math;
- runtime-state consumption.

The tests should not require live MAME.

Avoid heavy pixel-perfect rendering tests initially.

## Do Not Work On Yet

Do not rewrite the entire editor interaction model yet.

Do not remove WPF from the editor.

Do not introduce GPU shader/HLSL rendering yet.

Do not migrate the Edit View to Skia until the Play View renderer is stable.

## Desired Output From Codex

Codex should produce small focused changes that:

- add Skia renderer infrastructure;
- modernize runtime rendering architecture;
- improve runtime rendering performance;
- preserve existing editing workflows;
- add tests and diagnostics;
- prepare for future Edit View migration;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- Play View renders live machine visuals smoothly;
- lamp flashing no longer causes severe CPU spikes;
- alpha displays render smoothly;
- pan/zoom behaves correctly;
- clickable inputs still work;
- runtime visuals visually match existing edit view closely enough;
- no unrelated editor behavior changed.
