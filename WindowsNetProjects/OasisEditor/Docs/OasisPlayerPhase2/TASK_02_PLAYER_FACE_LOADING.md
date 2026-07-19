# Task 02: Player Face Loading

## Objective

Load the Phase 2 Task 01 Face entries from `machine.runtime.json` and deserialize each referenced `face.runtime.json` in Oasis Player.

This task reconstructs an in-memory runtime Face model only. It must not render Faces.

## Required behaviour

After cabinet instantiation, the Player should:

1. read Face references from `machine.runtime.json`;
2. load each `face.runtime.json`;
3. validate supported Face manifest schema versions;
4. resolve referenced artwork and mask files inside the runtime build;
5. locate the cabinet target mesh using the existing `OasisFace_` naming convention;
6. create a `RuntimeFace` object; and
7. register the Face with the loaded `RuntimeMachine`.

## Validation behaviour

Non-fatal Face issues should produce warnings and continue loading remaining Faces:

- missing `face.runtime.json`
- unsupported Face schema version
- malformed Face manifest
- missing artwork
- missing mask
- duplicate Face identifiers
- duplicate cabinet target assignments
- missing target mesh

Fatal cabinet/machine build failures remain handled by the existing machine and cabinet loading path.

## Non-goals

- No RenderTextures.
- No material replacement.
- No shader work.
- No lamp/reel/display/button rendering.
- No emulation integration.
- No Unity scene changes.
