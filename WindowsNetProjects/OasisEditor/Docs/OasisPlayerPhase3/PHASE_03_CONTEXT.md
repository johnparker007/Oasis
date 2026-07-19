# Oasis Player Phase 3 Context: Dynamic Face Lamps

Phase 2 is complete: the Player loads cabinet GLB content, loads Face runtime manifests and production textures, resolves `OasisFace_` targets, binds static Face artwork through the dedicated `Oasis/Face` shader, and owns/cleans up Face materials and textures explicitly.

Phase 3 adds dynamic Face lamps without changing the Phase 2 runtime export contract. The production textures remain `artwork.png`, `mask.png`, `trayId.png`, `lampIds0.png`, and `lampWeights0.png`.

## Authoritative runtime texture contract discovered in Editor code

Source of truth:

- `FaceRuntimeTextureGenerator` writes `trayId.png`, `lampIds0.png`, and `lampWeights0.png`.
- `FaceTexturePreviewRenderer` consumes those textures for the Editor texture-driven preview.

Semantics:

- `trayId.png`: RGBA PNG. Red stores the tray ID as an integer byte. Green and blue are zero. Alpha is 255 for tray-owned pixels and 0 for unowned transparent pixels. Tray IDs are one-based in generated bridge trays; ownership prevents later overlapping trays from overwriting earlier tray pixels. The current Player lamp lookup does not bank lamp states by tray; tray is metadata/ownership for the exported pixel region.
- `lampIds0.png`: RGBA PNG. RGB store up to three lamp IDs affecting that pixel. Alpha is 255 for tray-owned pixels and 0 outside authored tray pixels, but alpha is not a fourth lamp contribution in the current production writer. Lamp ID 0 is the invalid/unassigned sentinel. Valid exported lamp IDs are 1 through 255.
- `lampWeights0.png`: RGBA PNG. RGB store the matching contribution weights for the lamp IDs in `lampIds0.png`, quantized as `round(rawWeight * 255)` and decoded as byte/255. Alpha is 255 for tray-owned pixels and 0 outside authored tray pixels; the current production writer preserves RGB data channels plus opaque alpha and supports up to three emitters per tray.
- `mask.png`: RGBA image. The lamp mask value is `max(R,G,B) * alpha * maskStrength`, using normalized channel values. It gates illumination, not artwork opacity.
- Multiple contributions: the current production contract supports up to three contributions per pixel through the RGB channels of `lampIds0.png` and `lampWeights0.png`. Additional `lampIds1`/`lampWeights1` fields exist in manifests but the current export sets them to null and the production writer does not generate them.
- Illumination formula: the Editor texture preview computes `visibleLight = sum(lampBrightness[lampId] * weightByte / 255)`, `light = maskValue * visibleLight * emissionStrength`, then outputs `artworkRgb * (ambientStrength + light)` clamped to 0..255 while preserving artwork alpha.

## Phase boundaries

- Task 01 proves dynamic lamp rendering without emulator integration.
- Task 02 will bridge emulation output into the runtime lamp-state model.
- Task 03 will validate and tune rendering behaviour visually and with more robust render tests.
