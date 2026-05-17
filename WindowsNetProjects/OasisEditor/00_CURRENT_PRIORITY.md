# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. Input Map architecture.
2. MFME input import.
3. Play View runtime input routing.
4. Platform-specific MAME input mapping.
5. Keyboard shortcut mapping.
6. Mouse/keyboard runtime input tests.

Codex should prioritize this work before continuing additional runtime-driven component work or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `MAME_INPUT_MAP_AND_PLAY_VIEW_PLAN.md`
3. `PANEL2D_RUNTIME_STATE_PERFORMANCE_PLAN.md`
4. `OUTPUT_LOG_ENHANCEMENT_PLAN.md`
5. `MAME_EMULATION_RUNTIME_PLAN.md`
6. `MAME_ROM_MANAGEMENT_PLAN.md`
7. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
8. `MAME_VERSION_DISCOVERY_PLAN.md`
9. `MAME_ARCHITECTURE_REDESIGN.md`
10. `CODEX_NEXT_STEPS_MAME.md`
11. `TASKS_MAME_EMULATION_PORT.md`
12. `AGENT.md`
13. `TASKS.md`

## Immediate Task

Implement the Input Map and Play View architecture incrementally.

Current desired direction:

- centralize imported/runtime inputs into InputDefinitions;
- import MFME lamp/button input metadata;
- convert MFME buttons into Oasis lamps;
- add Input Map UI window;
- port shortcut key mapping logic;
- port platform-specific MAME tag/mask resolution logic;
- add non-editing Play View;
- route mouse and keyboard input through MAME stdin/Lua commands;
- add tests and diagnostics.

## Current Architectural Goals

- Input identity should be separated from visual representation.
- One InputDefinition may later drive 2D and 3D button representations.
- Existing lamp visuals should remain reusable as clickable runtime elements.
- Play View should remain separate from edit mode.
- Runtime input should not mutate the document/edit pipeline.
- Mouse/keyboard routing should be state-driven and safe.
- Runtime input failures should never crash the editor.
- The architecture should support future 3D physical buttons.

## Testing Direction

Codex should add or extend unit tests around:

- MFME input import behavior;
- InputDefinition creation;
- shortcut mapping;
- platform tag/mask resolution;
- MAME command formatting;
- keyboard repeat handling;
- focus loss releasing active inputs;
- mouse down/up command behavior;
- unresolved input handling.

The tests should not require live MAME.

Use fake command writers, fake visual targets, and fake resolver dependencies where practical.

## Do Not Work On Yet

Do not implement 3D physical buttons yet.

Do not implement edit-view Play Mode yet.

Do not redesign all Panel2D components yet.

Do not continue unrelated editor work unless explicitly instructed.

## Desired Output From Codex

Codex should produce small focused changes that:

- add InputDefinition architecture;
- modernize MFME input import behavior;
- add Input Map UI;
- add Play View runtime input routing;
- preserve current emulation/runtime work;
- add tests and diagnostics;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- Input Map window appears;
- MFME lamps/buttons import input definitions correctly;
- imported buttons are clickable in Play View;
- keyboard shortcuts work while Play View is focused;
- inputs send correct MAME commands;
- inputs release correctly on key-up/focus loss;
- runtime input does not interfere with editing mode;
- no unrelated editor behavior changed.
