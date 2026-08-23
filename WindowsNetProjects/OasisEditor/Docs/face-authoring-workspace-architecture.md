# Face Authoring Workspace Architecture

## Implemented status

The ten-phase Face authoring overhaul is complete. The Face workspace is the authoritative workflow UI; the Hierarchy remains object-centric and the Inspector remains property/tool-centric.

A `.face` is an independent document. Panel2D is optional. Panel2D derivation is a supported one-way import workflow, not a prerequisite and never a synchronization contract.

## Workspace

The central pane has a fixed breadcrumb and scrollable Overview, Artwork, Components, and Illumination pages. Focused routes host Geometry registration, Artwork Calibration, Override alignment, component editing, lamp editing, and the whole-Face **Layout View**. Overview owns the shared **Build Face** and **Rebuild Face** actions.

The final information architecture is:

- **Artwork:** Artwork Source → Geometry → Correction → Base Artwork → Override → Output.
- **Components:** Face-local reels, buttons, seven-segment and alpha displays.
- **Illumination:** Lamps → Lamp Mask → Trays → Runtime Assets.

Contextual generation settings are opened from Correction and Illumination rather than from a generic Face menu. One settings model remains shared because sharpening, mask extraction, and tray generation are inputs to the same build graph.

## Ownership and provenance

- **Authored** state and assets are designer-owned and Build/Rebuild never replace them: image sources, registration, calibration, native components and lamps, authored masks, and Override assets/alignment.
- **Derived** state is copied one-way from Panel2D. Artwork, Components, and Illumination have independent provenance. Local component or illumination edits can diverge without changing another subsystem.
- **Generated** products are disposable build outputs. They are controlled only by `FaceBuildStateModel` and `FaceBuildService`.

The retained Face-wide Panel2D identifiers exist only so explicitly derived operations can locate their source. Native Faces do not emit missing-Panel2D warnings during normal editing or building.

## Artwork pipeline and files

Source is either Panel2D/Face Source Shape or an authored Image. Geometry is a separate four-corner perspective registration recipe. Correction contains calibration and post-warp sharpening. Override is an optional authored external remaster aligned in normalized Face space; it is not another Source.

Generated artwork uses exactly:

- `Generated/Faces/<Face>/Artwork/correction-input.png` — rectified source supplied to Correction;
- `Generated/Faces/<Face>/Artwork/base.png` — corrected canonical pre-Override artwork;
- `Generated/Faces/<Face>/Artwork/artwork.png` — final composited output.

`original.png` has no current-format meaning. User assets belong under `Assets`, never `Generated`.

Logical Face coordinates describe component and lamp layout and remain independent of source/output raster resolution. Registration and build map source pixels into that logical surface while retaining useful raster resolution.

## Components and illumination

Components can be authored natively or derived. **Rebuild From Source** is explicit and warns that it replaces component edits; it does not affect Artwork or Illumination.

Lamps and masks can likewise be authored or derived. Trays and Runtime Assets are build products where configured. Authored masks remain under `Assets`. Optional nodes report `NotConfigured`; notably runtime generation without Cabinet context is not an error.

## Save, Build, and Rebuild

Save persists authored recipes and may leave outputs stale. **Build Face** executes stale configured nodes in dependency order. **Rebuild Face** forces configured generated nodes but preserves authored and derived recipe state. Neither operation resets a Face.

Mutations invalidate graph inputs, and the graph is the sole freshness authority. Generated file existence is not freshness. Focused artwork tests/actions call the same build-node implementation rather than a separate “Apply Changes” path.

## Supported workflows

### MFME-derived

Panel2D → Face Source Shape → Create Face initializes independently derived Artwork, Components, and Illumination. Switching Artwork to Image changes only Artwork. Component and supported illumination rebuilds remain deliberate source-derived operations.

### Native

New Face → Blank or Image creates a Face without Panel2D. The designer can register an image, author components and lamps, import a mask, generate trays, build, and create/align an Override without any Panel2D dependency.

## Deliberately deferred extensions

Lens/fisheye correction, curved-edge geometry, multiple Overrides, direct AI integration, filesystem watching, automatic component/lamp detection, source history, and a large tray polygon editor remain future work.
