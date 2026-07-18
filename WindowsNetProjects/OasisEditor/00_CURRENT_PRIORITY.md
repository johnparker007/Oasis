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

- Oasis Editor machine build/export
- versioned runtime machine and cabinet manifests
- external cabinet GLB packaging
- Oasis Player command-line startup
- windowed single-machine preview mode
- runtime GLB loading into the existing Unity preview room

This work spans:

```text
WindowsNetProjects/OasisEditor
UnityProjects/OasisPlayer
```

## Immediate Direction

Implement the two Phase 1 checkpoints described in:

```text
Docs/OasisPlayerPhase1/CODEX_START_PROMPT.md
```

Required order:

1. establish the Editor's deterministic machine-build contract
2. implement Player startup and runtime cabinet loading against that contract

Core target flow:

```text
Oasis Editor project
    -> generated machine build directory
    -> OasisPlayer.exe --mode machine-preview --build <path>
    -> Bootstrap scene
    -> MachinePreview scene
    -> cabinet instantiated beneath MachineSpawn
```

## Architectural Boundaries

- Cabinet content remains standard GLB/glTF with ordinary PBR materials.
- Oasis-specific Face rendering is a later phase.
- Preserve imported node hierarchy, node names, mesh boundaries, and material slots for future Face target binding.
- Keep display mode independent from content mode.
- Treat build output as disposable generated content, not authored `Assets/` content.
- Treat the build directory as read-only in Oasis Player.
- Do not recreate or regenerate the manually committed Unity scenes, `.meta` files, GUIDs, `StartupController` script asset, or `MachineSpawn` transform.

## Explicit Non-Goals

Do not implement in this priority:

- Face shaders or Face material replacement
- lamps, reels, displays, buttons, or emulation
- arcade layout loading or multiple machines
- player navigation
- remote downloads or archive packaging
- live Editor-to-Player IPC or hot reload

## Testing Direction

Prefer focused tests around:

- deterministic Editor build paths and output replacement
- versioned manifest serialization and validation
- relative path safety
- Cabinet3D GLB copying and model corrections
- command-line parsing and option precedence
- Player manifest loading and traversal rejection
- mode-to-scene selection
- runtime resource ownership and cleanup boundaries

Do not attempt to build the WPF application in Codex. Do not claim Unity visual verification unless it was actually performed. John will run builds, tests, Unity package imports, and visual checks locally.