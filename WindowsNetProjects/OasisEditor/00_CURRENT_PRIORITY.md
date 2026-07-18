# Current Priority for Codex

## Read First

Read only:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`
3. `Docs/OasisPlayerPhase1/CODEX_START_PROMPT.md`

Do not scan all Markdown files in this directory.

Open additional Phase 1 task documents only as directed by `Docs/OasisPlayerPhase1/CODEX_START_PROMPT.md` or when directly relevant to the requested work.

## Current Focus

Priority workstream:

- Oasis Editor Player preferences
- persisted Oasis Player executable path
- preview window/fullscreen settings
- build-then-launch workflow
- `File > Preview in Oasis Player`
- safe command-line argument construction
- clear launch validation and errors

Primary implementation project:

```text
WindowsNetProjects/OasisEditor
```

Existing Player command-line implementation to inspect for compatibility:

```text
UnityProjects/OasisPlayer
```

## Immediate Direction

Complete the Phase 1 launch-integration task described in:

```text
Docs/OasisPlayerPhase1/CODEX_START_PROMPT.md
Docs/OasisPlayerPhase1/TASK_03_EDITOR_PLAYER_LAUNCH_INTEGRATION.md
```

The earlier machine-build and Player cabinet-loading checkpoints are already merged.

Core target flow:

```text
Preferences > Player
    -> configure OasisPlayer.exe

File > Preview in Oasis Player
    -> build selected saved Cabinet3D asset
    -> launch OasisPlayer.exe
       --mode machine-preview
       --build <absolute generated build path>
       --windowed or --fullscreen
       --width <pixels>
       --height <pixels>
```

Keep the existing command:

```text
File > Build Oasis Player Machine
```

## Architectural Boundaries

- Store the Player executable path as a user/machine preference, not project or asset data.
- Extend the existing Preferences and settings architecture rather than adding a second settings system.
- Reuse `MachineRuntimeBuildService`; do not duplicate build logic.
- Keep process-launch logic out of WPF code-behind.
- Prefer a focused testable launch service and injectable process-start boundary.
- Pass process arguments separately; do not manually quote a single argument string.
- Do not assume the Player executable lives at a repository-relative path.
- Keep the launch fire-and-forget for this MVP.

## Explicit Non-Goals

Do not implement in this priority:

- automatic Unity Player builds
- Unity installation discovery
- launching the Unity Editor
- live Editor-to-Player IPC or hot reload
- reuse, shutdown, or monitoring of an already-running Player
- arcade mode or multiple machines
- Face shaders or material replacement
- lamps, reels, displays, buttons, or emulation
- remote downloads or archive packaging

## Testing Direction

Prefer focused tests around:

- Player preference defaults and persistence
- missing/invalid executable validation
- paths containing spaces
- windowed and fullscreen arguments
- width and height propagation
- build failure preventing launch
- successful build passing the exact returned build root
- process-start failure reporting

Do not launch the real Player from automated tests.

Do not attempt to build the WPF application in Codex. Do not claim process-launch or visual verification unless it was actually performed. John will run builds, tests, Unity Player builds, and end-to-end checks locally.
