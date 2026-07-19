# Oasis Player Phase 2 Context: Face Runtime Integration

Phase 1 established the runtime machine build contract: the Editor produces a versioned machine manifest, exports the Cabinet3D GLB, and the Player loads the cabinet with its authored PBR materials.

Phase 2 extends that contract to Face assets referenced by the machine. The goal is to make Face data available to the Player in deterministic runtime-build output before any dynamic rendering, lamp animation, display emulation, reels, buttons, shaders, or material replacement is introduced.

## End-to-end target flow

```text
Cabinet3D Asset
    ↓
Assigned Face assets
    ↓
Editor runtime build
    ↓
Generated machine build includes cabinet + faces
    ↓
Launch Oasis Player
    ↓
Player loads runtime manifest
    ↓
Player loads Cabinet GLB
    ↓
Player loads Face runtime manifests and static textures
    ↓
Later tasks instantiate static Face surfaces
```

## Phase boundaries

- Keep the existing versioned runtime manifest approach.
- Treat the Editor-generated build folder as the contract consumed by the Player.
- Export only data needed to reconstruct the Face at runtime.
- Do not alter Unity materials during Task 01.
- Do not implement lamp rendering, display rendering, reels, buttons, shaders, emulation, or dynamic updates in Task 01.
