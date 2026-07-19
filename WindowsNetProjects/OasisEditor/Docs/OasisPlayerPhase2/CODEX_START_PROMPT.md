# Codex Start Prompt: Oasis Player Phase 2

Read only these Phase 2 documents before implementation unless a task requires source inspection:

1. `PHASE_02_CONTEXT.md`
2. The task document for the requested Phase 2 task

For the current checkpoint, implement only Task 04: Face Renderer Infrastructure.

Respect these boundaries:

- Keep the versioned runtime manifest approach established by Task 01.
- Do not modify the Task 01 runtime Face export contract unless a genuine defect is discovered.
- Preserve Task 03 static Face rendering.
- Build the durable renderer infrastructure for later dynamic Face composition.
- Do not redesign the runtime export schema unless a genuine compatibility defect is discovered.
- Do not create RenderTextures.
- Do not introduce lamp rendering, display rendering, reels, buttons, shaders, or emulation.


## Completed Checkpoint

Task 03: Static Face Rendering is implemented. The Player now binds loaded RuntimeFaces to resolved OasisFace targets using runtime-owned material instances and displays the exported artwork texture statically. The exported mask remains loaded and owned by RuntimeFace for later renderer infrastructure; Task 03 does not combine it into the static material.
