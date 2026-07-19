# Codex Start Prompt: Oasis Player Phase 2

Read only these Phase 2 documents before implementation unless a task requires source inspection:

1. `PHASE_02_CONTEXT.md`
2. The task document for the requested Phase 2 task

For the current checkpoint, implement only Task 02: Player Face Loading.

Respect these boundaries:

- Keep the versioned runtime manifest approach established by Task 01.
- Do not modify the Task 01 runtime Face export contract unless a genuine defect is discovered.
- Load and validate exported Face runtime manifests after cabinet instantiation.
- Resolve artwork and mask assets through a reusable loading abstraction.
- Resolve Face target meshes using the existing `OasisFace_` naming convention.
- Create/register in-memory runtime Face objects for later rendering tasks.
- Do not implement runtime Face rendering yet.
- Do not modify Unity materials.
- Do not create RenderTextures.
- Do not introduce lamp rendering, display rendering, reels, buttons, shaders, or emulation.
