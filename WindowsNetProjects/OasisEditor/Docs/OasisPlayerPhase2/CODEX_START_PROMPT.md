# Codex Start Prompt: Oasis Player Phase 2

Read only these Phase 2 documents before implementation unless a task requires source inspection:

1. `PHASE_02_CONTEXT.md`
2. The task document for the requested Phase 2 task

Current checkpoint complete: Task 04 Face Renderer Infrastructure. Do not automatically begin lamp rendering; the next likely checkpoint is narrowly scoped dynamic lamp rendering design/implementation.

Respect these boundaries:

- Keep the versioned runtime manifest approach established by Task 01.
- Do not modify the Task 01 runtime Face export contract unless a genuine defect is discovered.
- Preserve Task 03 static Face rendering.
- Build the durable renderer infrastructure for later dynamic Face composition.
- Do not redesign the runtime export schema unless a genuine compatibility defect is discovered.
- Do not create RenderTextures.
- Do not introduce lamp rendering, display rendering, reels, buttons, shaders, or emulation.


## Completed Checkpoint

Task 04: Face Renderer Infrastructure is implemented. The Player now uses a dedicated Oasis/Face shader and runtime-owned per-Face material instances, binds artwork/mask/production lookup textures, preserves static artwork output, and prepares a minimal dynamic-state update seam without implementing lamp rendering.
