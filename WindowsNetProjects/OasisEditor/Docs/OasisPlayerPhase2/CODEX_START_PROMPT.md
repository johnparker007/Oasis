# Codex Start Prompt: Oasis Player Phase 2

Read only these Phase 2 documents before implementation unless a task requires source inspection:

1. `PHASE_02_CONTEXT.md`
2. `TASK_01_RUNTIME_FACE_EXPORT.md`
3. The task document for the requested Phase 2 task

For the current checkpoint, implement only Task 01: Runtime Face Export.

Respect these boundaries:

- Keep the versioned runtime manifest approach.
- Export referenced Face assets into the generated machine build.
- Include Face runtime manifests, artwork image, mask image, and existing reconstruction metadata.
- Do not implement Player-side loading/rendering yet.
- Do not modify Unity materials.
- Do not introduce lamp rendering, display rendering, reels, buttons, shaders, or emulation.
