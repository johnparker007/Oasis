# Face Authoring Overhaul Plan

## Purpose

This plan turns the target architecture in `Docs/face-authoring-workspace-architecture.md` into incremental implementation phases. Each phase should leave the Editor usable and testable. Do not attempt the entire overhaul in one PR.

The current system already has working Face documents, Hierarchy/Inspector integration, Face rendering/editing, Panel2D-derived Face generation, Artwork Calibration, high-quality perspective rectification, post-warp sharpening, mask/tray generation and runtime-lighting related code. Preserve working behaviour while migrating UX and ownership semantics into the new workspace.

## Phase 1 - Face workspace shell and navigation

Goal: establish the new Face authoring mental model without changing core generation behaviour.

Implement:

- persistent Face workspace header/breadcrumb in the central Face document pane
- Face Overview as the default Face presentation
- top-level cards/sections for:
  - Artwork
  - Components
  - Illumination
- subsystem detail navigation:
  - `Face > Artwork`
  - `Face > Components`
  - `Face > Illumination`
- ability to return to the current visual Face editing viewport through an explicit focused workspace route
- initial cards populated from the current Face model using read-only summaries/status; do not yet redesign persistence or generation
- keep Hierarchy object-centric and Inspector selection-centric
- preserve all existing Face editing capabilities, preferably by hosting/reusing the current Face viewport rather than replacing it

Do not yet implement:

- new source types
- dependency-aware Build/Rebuild engine
- artwork override
- native lamp/component authoring changes
- new schema solely for card navigation

Success criteria:

- opening a `.face` lands on a clear Overview rather than an ambiguous editing mode
- cards communicate the three major subsystems
- breadcrumb/navigation can enter subsystem detail and existing visual editing mode, then return safely
- no existing authored data is lost

## Phase 2 - Workspace modes and command consolidation

Goal: make Face editing modes explicit and predictable.

Implement a clear document-local workspace/navigation state for focused tools such as:

- normal Face/component editing
- Artwork Calibration marker editing
- future Artwork Geometry registration editing
- illumination/lamp editing routes where existing tools already exist

Move or surface relevant actions from scattered Face menus/Inspector entry points into the appropriate cards/detail pages while retaining menu shortcuts where useful.

Make the breadcrumb/title clearly indicate the active Face tool/mode.

Success criteria:

- no important Face viewport interaction depends on an invisible mode
- users can always navigate back to Overview
- existing calibration workflow is reachable naturally from Artwork > Correction

## Phase 3 - Provenance and build-state foundation

Goal: add the non-visual backbone for Authored / Derived / Generated and stale/current state.

Design and implement the smallest clean current-format model/service needed to represent:

- subsystem provenance
- local divergence where required
- generated-node stale/current/error/not-configured state
- dependency relationships between generated products and authored/derived inputs

Keep card UI driven by this central state rather than bespoke conditionals.

Introduce conceptual commands/services:

- Build Face - incremental/stale only
- Rebuild Face - force regeneration of generated outputs while preserving authored state

Do not introduce backwards compatibility. If serialized Face shape changes, increment schema and support only the new format.

Initially map existing generation/regeneration paths into the graph rather than rewriting all algorithms.

Success criteria:

- document dirty and build stale are distinct
- changing a known input marks correct downstream products stale
- Build Face only rebuilds stale outputs
- Rebuild Face rebuilds generated outputs but does not reset authored state

## Phase 4 - Artwork pipeline normalization

Goal: make Artwork explicitly follow Source -> Geometry -> Correction -> Base -> Output using current Panel2D workflow first.

Refactor current generated-artwork ownership so stages have unambiguous semantics. Target generated files:

- `base.png` - generated canonical pre-override artwork from source + geometry + correction
- `artwork.png` - final output

If an intermediate `original.png` remains useful for existing processing semantics, define its role explicitly and avoid ambiguous naming. Prefer a clean model where each file corresponds to a documented pipeline stage.

Move current perspective rectification, post-warp sharpening and Artwork Calibration into the appropriate conceptual stages without regressing quality.

Success criteria:

- current Panel2D -> Face Source Shape workflow behaves as before visually
- artwork cards show actual stage status
- generated stage ownership is clear
- external/user-authored files are never stored under Generated

## Phase 5 - Independent image Artwork Source + Geometry registration

Goal: support creating/replacing Face artwork directly from a photograph/image without requiring Panel2D.

Implement image Artwork Source as authored project material under `Assets`.

Add Geometry registration editing in the Face workspace:

- raw source image displayed in central Face viewport
- four semantic corners: TopLeft, TopRight, BottomRight, BottomLeft
- draggable handles and connected quad
- normalized source-image coordinates
- one undo entry per completed drag, not per mouse move
- explicit Apply/Build rather than expensive rebuild on every drag
- use the existing high-quality shared perspective rasterizer
- preserve useful high source resolution

Changing Artwork source must not alter Components or Illumination provenance/state.

Success criteria:

- an MFME-derived Face can replace only its artwork with a better photograph while retaining imported components/lighting
- a native Face can start from an image without Panel2D

## Phase 6 - Components provenance and native Face component authoring

Goal: make Components fully Face-owned/authored when desired while preserving Panel2D derivation workflow.

Clarify/standardize Face-local editing of reels, buttons, seven-segment displays, alpha displays and other supported component types.

For Panel2D-derived components:

- derivation is one-way
- local Face edits mark Components locally modified/diverged
- normal Build never overwrites local changes
- deliberate `Rebuild From Source...` may replace them with clear destructive warning

For native Face:

- add/place/edit components directly in the Face viewport without Panel2D

Success criteria:

- Components card accurately reports Authored vs Derived vs Locally Modified
- native Face can be built without Panel2D for supported component types

## Phase 7 - Illumination provenance and native authoring

Goal: make illumination usable for both MFME-derived and native Face workflows.

Lamps:

- derived from Panel2D where applicable
- authorable directly on Face

Lamp Mask:

- derived from imported lamp artwork where applicable
- authored image under `Assets` for native workflow

Trays:

- support current auto-generation
- expose tray settings within Illumination > Trays
- define simple authored transition for manually edited generated trays rather than complex merge behaviour

Runtime Lighting:

- generated-only node in dependency graph

Success criteria:

- native designer can place lamps, supply a lamp-mask image and generate/edit trays without Panel2D
- MFME workflow still auto-populates the same subsystem

## Phase 8 - External Artwork Override/remaster workflow

Goal: support external AI upscale, Photoshop/GIMP restoration and targeted repair without replacing source/provenance architecture.

Implement optional authored Artwork Override:

- Create From Base copies the current generated base into an authored location under `Assets`
- Import/Replace authored override
- Reload invalidates image caches
- normalized Face-space X/Y/Width/Height alignment
- alignment editing in central Face viewport
- Base reference underneath Override
- adjustable preview opacity for eyeballing alignment
- high-quality resampling/compositing
- preserve useful override resolution

Typical use cases:

- upscale current Oasis-generated artwork externally
- rectify/calibrate a photograph, then externally repair it
- use a partial RGBA repair overlay in future if useful

Success criteria:

- external authored image is never overwritten by Build/Rebuild
- alignment is persisted/undoable
- final output updates correctly

## Phase 9 - New Face creation UX

Goal: make standalone/native Face creation first-class.

Keep `Create Face From Source Shape` from Panel2D as a convenient contextual workflow.

Add/clean up standalone New Face creation with a small initializer:

- Name
- Blank or Image starting artwork
- optional image chooser

Do not create a large wizard. The Face Overview cards guide subsequent authoring.

Success criteria:

- user can create a functional `.face` without ever creating a `.panel2d`

## Phase 10 - Cleanup and legacy UX removal

After the new Face workspace covers old flows:

- remove superseded Face menu/dialog entry points that duplicate card workflows
- remove obsolete state/models/services rather than keeping compatibility branches
- consolidate tests around the new architecture
- update documentation

Do not retain old workflow code merely for hypothetical compatibility.

## Future, explicitly out of scope for this overhaul unless separately requested

- lens/fisheye correction
- curved-edge registration
- fine registration mesh
- thin-plate spline/freeform deformation
- automatic feature matching/corner detection
- direct AI integration
- filesystem watchers for external editors
- multiple artwork overrides/layer stack

The architecture should allow these to be inserted later, especially in Artwork > Geometry, without redesigning the Face workspace.

## Implementation loop

For each phase:

1. Review current `main` and this architecture/plan.
2. Implement only the requested phase and necessary supporting refactors.
3. Keep the Editor usable after the phase.
4. Add/update focused tests.
5. Report changed files, model changes, UX changes, tests, and manual verification steps.
6. User tests locally.
7. Review the PR/result before proceeding to the next phase.
