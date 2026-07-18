# Codex Start Prompt: Oasis Player Phase 1 Completion

Work in the connected repository:

```text
johnparker007/Oasis
```

Primary implementation project:

```text
WindowsNetProjects/OasisEditor
```

The existing command-line contract should be checked under:

```text
UnityProjects/OasisPlayer
```

## Required Read Order

Read only these planning files first:

1. `WindowsNetProjects/OasisEditor/AGENTS.md`
2. `WindowsNetProjects/OasisEditor/00_CURRENT_PRIORITY.md`
3. `WindowsNetProjects/OasisEditor/Docs/OasisPlayerPhase1/PHASE_01_CONTEXT.md`
4. `WindowsNetProjects/OasisEditor/Docs/OasisPlayerPhase1/TASK_03_EDITOR_PLAYER_LAUNCH_INTEGRATION.md`

Then inspect only source files directly relevant to:

- existing Preferences architecture and persistence
- existing browse-dialog abstractions
- `File > Build Oasis Player Machine`
- `MachineRuntimeBuildService`
- status/output reporting
- Player startup argument parsing

Do not broadly scan archived Markdown, generated outputs, Unity `Library`, or unrelated systems.

## Current State

The earlier Phase 1 checkpoints are merged.

The Editor can create a deterministic machine build through:

```text
File > Build Oasis Player Machine
```

The Player can consume that build through command-line arguments equivalent to:

```text
OasisPlayer.exe --mode machine-preview --build <path> --windowed --width 1280 --height 800
```

The missing part is the Editor-side configuration and launch bridge.

## Implement This Task

Complete:

```text
TASK_03_EDITOR_PLAYER_LAUNCH_INTEGRATION.md
```

The target user flow is:

```text
Preferences > Player
    -> configure OasisPlayer.exe

File > Preview in Oasis Player
    -> build selected saved Cabinet3D asset
    -> launch configured executable
    -> pass machine-preview arguments
```

Keep the existing build-only command.

Use the existing settings, Preferences, dialog, ViewModel, command, theme, and output patterns. Do not introduce an unrelated settings subsystem or put process-launch logic in WPF code-behind.

Prefer a focused, testable launch service with an injectable process-start boundary. Pass command arguments separately rather than constructing a manually quoted string.

## Scope Discipline

This task is only the MVP build-and-launch bridge.

Do not implement:

- automatic Unity builds
- Unity installation discovery
- live IPC
- hot reload
- existing-process reuse or management
- arcade mode
- multiple machines
- Face rendering
- emulation
- archive/download flows

Do not make broad Unity changes unless a small correction is genuinely necessary for compatibility with the already-merged command-line contract.

Any Unity-side code must use C# 9-compatible syntax and block-scoped namespaces, as required by `AGENTS.md`.

## Environment and Verification

The Codex environment may not be able to build or run the Windows WPF application or launch a Unity-built executable.

Add focused automated tests where practical, especially around settings persistence, validation, argument construction, paths containing spaces, build-before-launch orchestration, and process-start failures.

Do not start a real Player process from automated tests.

Finish with:

- files changed
- design summary
- settings storage used
- exact generated arguments
- automated tests added or changed
- assumptions
- exact local verification steps for John

Do not claim a build, process launch, or visual test passed unless it was actually run successfully in an appropriate environment.
