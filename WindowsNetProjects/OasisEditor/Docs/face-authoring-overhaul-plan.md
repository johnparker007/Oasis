# Face Authoring Overhaul — Completion Record

## Status

Phases 1–10 are complete. This file records the delivered migration; it is no longer an implementation proposal. The implemented architecture is documented in `Docs/face-authoring-workspace-architecture.md`.

| Phase | Delivered result |
|---|---|
| 1 | Face Overview, subsystem cards, breadcrumb navigation, and explicit whole-Face viewport route. |
| 2 | Document-local focused modes for Geometry, Calibration, Components, Lamps, Override, and return navigation. |
| 3 | Independent subsystem provenance plus central dirty/stale/current/error/not-configured build state. |
| 4 | Artwork normalized to `correction-input.png`, `base.png`, and `artwork.png`. |
| 5 | Authored Image source and separate perspective registration, independent of component/illumination provenance. |
| 6 | Native component authoring and explicit destructive Components rebuild from Panel2D. |
| 7 | Native/derived illumination, authored mask import, trays, and runtime build nodes. |
| 8 | Authored external Artwork Override with reload, enablement, alignment, and final compositing. |
| 9 | First-class New Face (Blank or Image) flow. |
| 10 | Legacy menu/apply UX removed, generic Face Editor renamed Layout View, settings surfaced contextually, action visibility tightened, tests and documentation consolidated. |

## Final cleanup decisions

- Kept contextual Panel2D **Add Face Source Shape** and **Create Face from Selected Panel2D Source Shape** actions with command enablement tied to the selected document/shape.
- Kept whole-Face validation because it provides structural/runtime diagnostics and accepts native Faces.
- Removed the normal-menu whole-Face **Regenerate from Source Shape** entry; subsystem rebuild and Build/Rebuild are the normal operations.
- Removed the global **Generation Settings** menu. The shared settings implementation opens from Correction and Illumination cards.
- Removed Inspector **Apply Changes** and its legacy command wrapper. Artwork output is a build product.
- Renamed the useful all-object viewport from **Face Editor** to **Layout View**.
- No schema increment was required for Phase 10 because serialized current-format fields did not change.

## Scope differences from the early proposal

A single focused settings dialog remains behind contextual entry points rather than being split into three duplicated models/dialogs. The whole-Face viewport remains because it uniquely supports reviewing and selecting all Face objects together. Runtime output is labelled **Runtime Assets**, matching the generated implementation.

## Explicit future work

Lens/fisheye correction, curved-edge/fine registration, multiple Overrides, AI integration, file watching, automatic detection, source-history management, and a large tray polygon editor are not part of this overhaul.
