# Codex Start Prompt: Oasis Player Phase 1

Work in the connected repository:

```text
johnparker007/Oasis
```

This work spans both projects:

```text
WindowsNetProjects/OasisEditor
UnityProjects/OasisPlayer
```

## Required Read Order

Read only these planning files first:

1. `WindowsNetProjects/OasisEditor/AGENTS.md`
2. `WindowsNetProjects/OasisEditor/00_CURRENT_PRIORITY.md`
3. `WindowsNetProjects/OasisEditor/Docs/OasisPlayerPhase1/PHASE_01_CONTEXT.md`
4. `WindowsNetProjects/OasisEditor/Docs/OasisPlayerPhase1/TASK_01_EDITOR_MACHINE_BUILD_EXPORT.md`
5. `WindowsNetProjects/OasisEditor/Docs/OasisPlayerPhase1/TASK_02_PLAYER_STARTUP_AND_CABINET_LOADING.md`

Then inspect only source files directly relevant to the current task. Do not broadly scan archived Markdown, generated outputs, Unity `Library`, or unrelated systems.

## Execution Order

Implement the work as two sequential checkpoints.

### Checkpoint A: Editor Export

Complete `TASK_01_EDITOR_MACHINE_BUILD_EXPORT.md` first.

Before changing Player DTOs, establish and test in source the versioned runtime build contract produced by Oasis Editor.

Stop after the Editor checkpoint and provide a concise report including local tests John should run. Do not continue into broad Player implementation if the Editor build contract remains ambiguous or incomplete.

### Checkpoint B: Player Startup and Loading

After the Editor contract is coherent, implement `TASK_02_PLAYER_STARTUP_AND_CABINET_LOADING.md` against that actual contract.

Preserve the manually created Unity scenes, GameObjects, script asset, `.meta` files, and GUIDs. Prefer asking John to perform a small Inspector assignment over generating risky scene YAML references.

## Scope Discipline

This phase is only the first cabinet-loading vertical slice:

```text
Editor build -> command-line Player startup -> MachinePreview scene -> GLB cabinet at MachineSpawn
```

Do not implement Face shaders, lamps, reels, displays, emulation, arcade navigation, multiple machines, downloads, archives, or live IPC.

Keep cabinet loading standard glTF/PBR/URP and preserve hierarchy/material slots for later Face target replacement.

## Environment and Verification

The Codex environment may not be able to build the Windows WPF application or perform trustworthy Unity visual verification.

Do not create build-attempt diary Markdown files.

Add focused automated tests where practical, inspect changes carefully, and finish each checkpoint with exact local verification steps for John.

Do not claim a build or visual test passed unless it was actually run successfully in an appropriate environment.