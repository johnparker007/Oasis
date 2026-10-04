# Cabinet Semantic Geometry — Stage 2 Runtime Build Contract

## Authority and build path

The authored Cabinet GLB remains the sole authority for Cabinet geometry semantics. A Machine build resolves the selected Cabinet package and its model path, validates the source GLB, and then copies that file directly to `cabinet/cabinet.glb` in the staging build. Finalisation renames the completed staging directory into the generated Machine build. There is no import, scene reconstruction, or GLB export step, so the bytes—and therefore node names, mesh names, hierarchy, transforms, and geometry—are preserved.

`cabinet.runtime.json` and `machine.runtime.json` do not repeat Collider or Trigger names, transforms, or geometry. Their serialized shapes are unchanged, so the Cabinet runtime schema remains version 5 and the Machine runtime schema remains version 6.

## Build validation

The Editor and Machine build share `CabinetSemanticGeometry` classification and `GlbCabinetSemanticGeometryValidator`. Classification continues to give a semantic node name precedence and otherwise uses the mesh name.

- `OasisFace_*` retains the existing Face-target detector contract: a valid planar quad, POSITION and TEXCOORD_0 geometry, and the existing assignment-to-detected-target check.
- Every instantiated `OasisCollider_*` and `OasisTrigger_*` object must reference a mesh with at least one primitive. Every primitive must have a non-empty, readable POSITION accessor, and its triangle indices must resolve within those positions.
- Physics semantic geometry does not require TEXCOORD_0 or a material.
- Ordinary visual geometry and legacy `COL_*` / `TRG_*` names are not physics semantics and are not subjected to this validation.

An explicitly authored Collider or Trigger that cannot fulfil its role fails the Machine build rather than being silently omitted. Diagnostics identify the Cabinet reference, semantic kind and name, and the invalid geometry reason where the GLB can be read far enough to recover that context.

## Stage 3 boundary

Stage 3 will load the preserved `cabinet.glb` in Oasis Player, locate `OasisCollider_*` and `OasisTrigger_*` instances by their retained names, and create the appropriate Unity physics components. MeshCollider creation, trigger flags, rigid bodies, collision layers, physics materials, convexity policy, and gameplay interpretation are deliberately not part of Stage 2.
