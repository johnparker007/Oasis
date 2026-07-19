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
- deterministic Editor-generated Face runtime build contract
- Face runtime manifests and texture/image export in generated machine builds

Primary implementation project:

```text
WindowsNetProjects/OasisEditor
```

## Immediate Direction

Implement only Phase 2 Task 01:

```text
Docs/OasisPlayerPhase2/TASK_01_RUNTIME_FACE_EXPORT.md
```

The Phase 1 cabinet runtime build and Player launch flow are complete and merged.

## Explicit Non-Goals

Do not implement in this priority:

- Player-side Face loading
- runtime Face rendering
- Unity material changes
- lamp rendering
- display rendering
- reels
- buttons
- shaders
- emulation integration
- Player hot reload or IPC

## Testing Direction

Prefer focused tests around:

- machine manifests listing exported Face runtime manifests
- deterministic Face output paths under generated machine builds
- copied artwork and mask images
- copied existing Face runtime metadata/textures required for later reconstruction
- clear failures for invalid or missing Face runtime source data

Do not attempt to build the WPF application in Codex. Do not claim WPF, Unity Player, or visual verification unless it was actually performed locally.
