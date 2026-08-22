# Face Authoring Workspace Architecture

## Status

This document defines the target architecture for the Oasis Editor Face authoring overhaul. It is intentionally broader than any one implementation PR. Individual phases should move toward this model while keeping the Editor usable after each step.

The project is early-stage. There is no backwards-compatibility requirement for internal Face schemas or generated formats. When serialized shapes change, update writer and reader together, increment the current schema version, update tests/fixtures, support only the latest version, and remove superseded format code.

## Core document model

Keep the existing document concept.

- `*.panel2d` remains an independent editable document.
- `*.face` remains an independent editable document.
- A Face may reference a Panel2D, image assets, cabinet assets, or other project material, but a Panel2D is not required for a Face to exist.
- A Face must be fully authorable directly, without first creating a Panel2D.

Long-term meaning:

- **Panel2D**: a flat 2D machine/layout authoring document. It is especially useful for MFME-imported layouts and for workflows where a complete flat layout is convenient before deriving cabinet Faces.
- **Face**: a physical cabinet-surface authoring document. It owns Face-local artwork, component placement, illumination authoring, and generated runtime outputs.

Panel2D-to-Face derivation is one-way. Manual changes made on a Face are not back-propagated to the source Panel2D.

## Face workspace responsibilities

When a `*.face` document is open, preserve the normal Oasis Editor layout:

- **Hierarchy** on the left: object-centric; shows actual Face elements/objects such as artwork, lamps, reels, displays, buttons, trays, etc.
- **Central Face document pane**: workflow-centric; contains breadcrumb navigation, overview/detail cards, and focused visual editing modes.
- **Inspector** on the right: property-centric; shows properties for the current selection or focused tool context.

Do not mirror the workflow cards into the Hierarchy. The two have different responsibilities.

## Central Face workspace navigation

The central Face document pane should support three presentation levels:

1. **Face Overview**: card dashboard covering Artwork, Components, and Illumination.
2. **Subsystem detail**: e.g. `Face > Artwork` or `Face > Illumination`, showing that subsystem's cards in more detail.
3. **Focused editing mode**: e.g. `Face > Artwork > Geometry`, where the card view is replaced by the large visual editing viewport/tool for that stage.

A persistent breadcrumb/navigation header should appear across the top of the Face document pane, for example:

`Upper Glass > Artwork > Geometry`

Each breadcrumb segment is clickable. Focused editing modes should also expose an obvious `Back to Overview` / `Done` route so the user is never trapped in an invisible mode.

Cards and the viewport share the same central document area; they are not shown permanently side-by-side.

## Top-level Face subsystems

The Face workspace has three major areas:

### Artwork

A mostly linear image pipeline:

`Source -> Geometry -> Correction -> Base Artwork -> Override -> Output`

### Components

A Face-local collection rather than a pipeline. Typical component types include:

- reels
- buttons
- seven-segment displays
- alpha displays
- other current/future Face components

The card summarizes counts and provenance. Individual objects remain in the Hierarchy and are edited in the Face viewport.

### Illumination

A mostly dependent authoring/build pipeline:

`Lamps -> Lamp Mask -> Trays -> Runtime Lighting`

The exact dependency edges are implementation details, but the UI should communicate the logical progression and stale/build state.

## Artwork cards

### Source

Answers: **Where does the visual artwork originate?**

Initial supported source concepts:

- Panel2D + Face Source Shape
- Image asset
- None/blank where needed for native authoring

For a Panel2D source, show the source Panel2D and Face Source Shape, with actions such as `Edit Source` and `Open Panel2D`.

For an image source, the image is authored project material under `Assets`; show project-relative path/resolution and actions such as Replace/Reveal.

Artwork source is independent from component and illumination provenance. A Face may use high-resolution image artwork while components and illumination remain derived from an MFME-imported Panel2D.

### Geometry

Answers: **How does source imagery map into rectangular Face space?**

Initial implementation is four-corner perspective registration/rectification.

The architecture must not equate the entire Geometry subsystem with one perspective quad. Future geometry tools may include lens/fisheye correction or fine registration. Those future transforms should ideally compose into one high-quality sampling operation rather than repeatedly resampling intermediate images.

Do not implement hypothetical lens-correction fields/classes until that feature is requested, but keep the UI/model boundary broad enough that Geometry can gain additional operations later.

### Correction

Photometric/source correction after geometry reconstruction.

Current/relevant tools include:

- Artwork Calibration using authored markers/groups for lighting, black/white and colour correction
- post-warp sharpening settings

Focused editing opens existing marker-placement/calibration tooling in the central Face viewport.

### Base Artwork

Generated/read-only canonical artwork produced from Source + Geometry + Correction before optional external override/remastering.

Target generated asset concept:

`Generated/Faces/<Face>/Artwork/base.png`

`base.png` is the stable hand-off point for external AI upscaling, Photoshop/GIMP restoration, repainting, etc.

### Override

Optional authored external improvement/remaster aligned in Face space.

Target authored model concept:

- enabled
- project-relative asset path under `Assets`
- normalized Face-space alignment transform (initially translation + scale / X,Y,Width,Height)

Typical actions:

- Create From Base
- Import/Replace
- Reload
- Edit Alignment
- Disable

Alignment editing should show Base underneath and Override overlaid with adjustable preview opacity. The opacity is an editing aid, not necessarily part of final compositing.

Do not implement this stage until its dedicated phase.

### Output

Generated/read-only final Face artwork consumed by the rest of Oasis/runtime packaging.

Target concept:

`Generated/Faces/<Face>/Artwork/artwork.png`

## Components subsystem

Components are Face-local objects. Prefer one summary card rather than one pipeline card per component type because the Hierarchy already represents individual objects and categories.

Example card content:

- 4 reels
- 7 buttons
- 2 seven-segment displays
- 1 alpha display
- provenance: Authored or Derived from `<panel2d>`
- locally modified status where relevant

`Edit Components` opens the normal Face visual component-editing workspace. The Hierarchy remains the primary list/select mechanism for individual objects.

### Derived components

For MFME/Panel2D workflows, Face components may initially be derived from a Panel2D.

Derivation is one-way:

`Panel2D -> Face`

If the user manually edits derived Face components, mark the subsystem as locally modified/diverged. Normal Build must never silently replace those edits.

A deliberate `Rebuild From Source...` action may replace the current Face component layout from the source Panel2D, but it must clearly warn that local component edits will be replaced.

No backpropagation to Panel2D.

## Illumination subsystem

### Lamps

May be:

- Derived from a Panel2D/imported layout
- Authored directly on the Face

Native Face workflow should allow the designer to place/select lamps directly in the Face viewport without using Panel2D.

### Lamp Mask

Support different provenance/modes rather than treating MFME-derived masks as fundamental:

- Derived from imported lamp artwork/images
- Authored image under `Assets`

A native workflow can therefore use a hand-drawn/Photoshop lamp mask while an MFME workflow can retain the current derivation path.

### Trays

May be auto-generated or authored/manual.

A simple long-term rule is preferred: if an auto-generated tray is manually edited, it can become authored rather than requiring complex merge/reconciliation with later auto-generation.

Tray generation settings should conceptually live with the Trays card/tool rather than as mysterious global Face settings.

### Runtime Lighting

Pure generated output. No meaningful authored state should live here. It depends on the current lamps, mask, trays/emitters and other required illumination inputs.

## Provenance and ownership

Use consistent concepts across the Face workspace:

### Authored

Designer-owned state/assets. Build/Rebuild must never silently destroy it.

Examples:

- imported source image under `Assets`
- artwork calibration markers
- manually authored Face components
- authored lamp mask image
- manually authored trays
- external override/remaster image

### Derived

State initially obtained from another document/source, such as Panel2D/MFME-derived components or masks.

Derived state may later become locally modified/diverged.

### Generated

Disposable build output that Oasis may recreate.

Examples:

- base artwork
- final artwork
- generated masks where applicable
- runtime lighting textures/manifests

Orthogonal status flags include:

- locally modified
- stale / needs build
- current
- error
- not configured

## Save versus Build

Keep document persistence and build state separate.

### Save

Persists authored Face state. A Face can be saved while generated outputs are stale.

### Build Face

Dependency-aware incremental build. Rebuild only generated products that are stale because of authored/derived input changes.

### Rebuild Face

Force all generated products to be recreated from the current authored/derived recipe state.

Rebuild must not mean reset. It must preserve authored assets/settings/components/trays/calibration/registration/override state.

## Dependency graph

The cards are a presentation of the dependency graph, not the implementation of it.

Conceptual Artwork dependencies:

`Artwork Source -> Geometry Recipe -> Correction Recipe -> Base Artwork -> Override Alignment/Asset -> Final Artwork`

Conceptual Components dependencies:

`Panel2D Component Source -> Face Components`

or direct authored Face Components.

Conceptual Illumination dependencies:

- Lamp Definitions feed auto tray generation and runtime lighting.
- Lamp Mask feeds runtime lighting.
- Face Trays feed runtime lighting.
- Runtime Lighting is generated.

The implementation may refine the exact graph, but dependency/stale behaviour should be declared centrally rather than hard-coded independently into each card.

## Dirty versus stale

These are separate states.

- **Document dirty**: authored state has unsaved changes.
- **Build stale**: generated outputs do not match the current recipe.

Example:

1. Move a calibration marker -> document dirty; Base/Output stale.
2. Save -> document clean; Base/Output still stale.
3. Build Face -> generated outputs current.

The UI should communicate these states clearly.

## Face overview example

The exact visual treatment is not locked down, but the default Face view should quickly answer:

- What is this Face made from?
- Which parts are authored vs derived vs generated?
- Is anything stale?
- What should I edit next?

Conceptual overview:

```text
ARTWORK                                      Current
[Source] -> [Geometry] -> [Correction]
                            |
                          [Base]
                            |
                        [Override]
                            |
                         [Output]

COMPONENTS                                   Current
[18 Components]
4 Reels | 8 Buttons | 6 Displays
Derived from Main.panel2d

ILLUMINATION                                 Build required
[42 Lamps] -> [Lamp Mask] -> [42 Trays] -> [Runtime]
                               stale

                                      [Build Face]
```

## Creation workflows

### Create Face From Source Shape

Retain the convenient contextual Panel2D workflow:

`Panel2D -> Face Source Shape -> Create Face From Source Shape`

This creates a normal `.face` document and prepopulates Artwork, Components, and Illumination from the Panel2D where applicable.

### New Face

A standalone `New Face` flow creates a normal `.face` without requiring Panel2D.

Keep the initial wizard small. Initial options can be:

- Blank
- Image

The Face card workspace then guides the rest of authoring.

## Representative workflows

### MFME-derived Face

- Artwork: Derived from Panel2D
- Components: Derived from Panel2D
- Illumination: Derived from Panel2D

### MFME Face upgraded with better photograph

- Artwork: Authored high-resolution image + registration/correction
- Components: still Derived from Panel2D
- Illumination: still Derived from Panel2D

Only the Artwork subsystem changes provenance.

### Fully native Face

- Artwork: Authored image/blank native artwork
- Components: Authored directly in Face
- Illumination: Authored directly in Face; lamp mask may be an authored image; trays may be auto-generated then authored/edited

No Panel2D is required.

## Future geometry quality

Quality is a priority. If future lens/fisheye correction or fine registration is introduced, prefer composing geometric mappings and sampling the raw source once with the high-quality perspective rasterizer rather than performing successive destructive warps.

Potential future Geometry contents:

- Perspective Registration
- Lens Correction
- Fine Registration

These are future capabilities, not current implementation requirements.

## Stable architectural decisions

Treat these as the target contract unless deliberately revisited:

- `.face` and `.panel2d` remain independent documents.
- Panel2D is optional for Face authoring.
- Face has Artwork, Components, and Illumination subsystems.
- Each subsystem can have independent provenance.
- Cards provide workflow/status/navigation, not object hierarchy.
- Hierarchy remains object-centric.
- Inspector remains property-centric.
- Focused visual editing happens in the central Face document pane.
- Breadcrumbs navigate Face overview, subsystem detail, and focused modes.
- Authored/Derived/Generated have explicit semantics.
- Build is dependency-aware and incremental.
- Rebuild regenerates generated state without destroying authored state.
- Panel2D derivation is one-way.
- Geometry remains conceptually extensible and should minimise repeated resampling.

## Deliberately flexible details

These may evolve without changing the architecture:

- exact card visuals/layout
- status iconography
- exact naming such as `Correction` vs `Source Correction`
- whether Base/Output are root-level overview cards or only Artwork detail cards
- future lens/fine-warp implementation
- future final-processing stages
- detailed tray authored/generated transition UX
