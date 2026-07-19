# Current Priority for Codex

## Read First

Read only:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`
3. `Docs/OasisPlayerPhase2/CODEX_START_PROMPT.md`

Do not scan all Markdown files in this directory.

Open additional Phase 2 task documents only as directed by `Docs/OasisPlayerPhase2/CODEX_START_PROMPT.md` or when directly relevant to the requested work.

## Current Focus

Priority workstream:

- Oasis Player Phase 2: Face Runtime Integration
- Player-side loading and validation of exported Face runtime assets
- in-memory RuntimeFace model registration after cabinet instantiation

Primary implementation project:

```text
UnityProjects/OasisPlayer
```

Related contract producer:

```text
WindowsNetProjects/OasisEditor
```

## Immediate Direction

Implement only Phase 2 Task 02:

```text
Docs/OasisPlayerPhase2/TASK_02_PLAYER_FACE_LOADING.md
```

Phase 2 Task 01 is complete. Do not modify the runtime Face export contract unless a genuine defect is discovered.

## Explicit Non-Goals

Do not implement in this priority:

- runtime Face rendering
- Unity material changes
- RenderTextures
- mesh replacement or mesh modification
- lamp rendering
- display rendering
- reels
- buttons
- shaders
- emulation integration
- Player hot reload or IPC

## Testing Direction

Prefer focused tests around:

- valid machine manifests with multiple Faces
- missing `face.runtime.json`
- unsupported Face manifest schema version
- missing artwork or mask image files
- duplicate Face identifiers
- duplicate cabinet target assignments
- missing cabinet target mesh warnings
- successful RuntimeFace registration
- continued loading after invalid Face entries

Do not claim WPF, Unity Player, or visual verification unless it was actually performed locally.
