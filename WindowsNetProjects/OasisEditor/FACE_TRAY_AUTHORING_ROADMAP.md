# Face Tray Authoring Roadmap

## Purpose

The Face runtime texture export and CPU preview now prove the core rendering contract:

```text
Face document
  -> runtime package
  -> artwork.png
  -> mask.png
  -> trayId.png
  -> lampIds0.png
  -> lampWeights0.png
  -> texture-driven CPU preview
  -> future Unity renderer
```

The next major workstream is improving the data that feeds the renderer: explicit physical trays and lamp emitters.

UK slot machine glass panels often have irregular physical lamp trays behind the glass. A tray may contain one bulb, or several individually controllable bulbs. Authored trays and emitters now exist and drive runtime export, so tray/emitter improvements are directly visible in exported textures and CPU preview.

## Core Direction

Move from generated rectangular tray guesses toward authored Face-level polygon tray/emitter data.

The editor should support this workflow:

```text
Generate Face from Panel2D
  -> import all MFME lamp elements, including shared lamp sets
  -> use MFME bulb masks to place emitters when available
  -> group known MFME shared lamp-set elements into one authored tray
  -> auto-author rough trays and emitters from imported/generation data
  -> derive conservative polygon tray shapes where obvious
  -> show tray/emitter debug overlays
  -> export runtime textures from authored trays/emitters
  -> CPU preview validates result
  -> user corrects remaining rough trays manually later
  -> future Unity renderer consumes same textures
```

The important principle is that auto-authoring is a starting guess, not a source of truth forever. The Face document should become the source of truth for physical tray layout after generation.

## Current Priority

MFME multi-lamp component import is now pulling multiple lamp elements into Oasis Editor. Some MFME lamp components contain several individually controllable lamp elements sharing the same x/y/width/height. These are commonly used for large logos, jackpot values, or other large illuminated areas.

Per-element MFME bulb mask centroiding now places emitters well for blended multi-lamp components. The next safe improvement is tray grouping for known shared MFME lamp sets.

Current likely behaviour in tray auto-authoring:

```text
MFME shared lamp component
  -> imported as multiple Oasis/Panel2D lamp elements with the same source bounds/shared-set provenance
  -> Face generation creates multiple FaceLampWindowElement instances
  -> tray auto-authoring creates one tray per FaceLampWindowElement
  -> result is multiple overlapping authored trays for one physical MFME shared lamp area
```

For MFME lamp elements that are known to come from the same source lamp component/shared set, especially when `Blend` is true, this should usually become:

```text
one authored tray
  -> multiple authored emitters
```

This is safer and more valuable than generic overlap merging because the shared-set provenance tells us the overlap is intentional source data, not just a coincidental geometric overlap.

## MFME Bulb Mask Emitter Placement

When an imported MFME lamp element has an associated mask image, derive the Oasis emitter centre from that image instead of placing every emitter at the centre of the shared component rectangle.

Recommended first heuristic:

```text
if source component Blend == true
and lamp element has BmpMaskImageFilename
and mask image loads
and mask has enough meaningful pixels

    weight = alpha * luminance
    centerX = sum(x * weight) / sum(weight)
    centerY = sum(y * weight) / sum(weight)
    map mask-space centre into Panel2D/Face coordinates
    set emitter centre from that mapped point

else
    fall back to current centre placement
```

Alpha may be opaque in some source images. If so, luminance-only weighting is acceptable.

The derived emitter should record diagnostic/source metadata such as:

```text
EmitterPlacementSource = MfmeBulbMaskCentroid
EmitterPlacementSource = ComponentCentreFallback
EmitterPlacementSource = LampWindowCentreFallback
```

If practical, also derive an approximate emitter radius from the meaningful mask area or mask bounds. This is not required for current binary weight maps, but will be useful later for multi-bulb influence falloff.

Do not confuse these concepts:

```text
MFME bulb mask image
  -> used to infer individual emitter centre/radius

Oasis generated lamp mask
  -> used to decide visible lit artwork/mask contribution
```

## MFME Shared Lamp Set Tray Grouping

When multiple imported lamp windows are known to originate from the same MFME lamp component/shared lamp set, they should usually share one authored Face tray.

This is different from generic overlap merging:

```text
known shared MFME source set
  -> group into one tray with multiple emitters

unknown geometric overlap
  -> keep conservative diagnostics/clip heuristics
```

Recommended first heuristic:

```text
if two or more visible FaceLampWindowElement instances have the same non-empty shared MFME lamp set/source component identifier
and their source bounds are equivalent or near-equivalent
and the source component indicates Blend == true or has multiple valid lamp elements

    create one authored tray for the group
    derive the tray bounds from the shared component bounds or union of contribution bounds
    create one emitter per lamp window
    assign all emitters to the shared tray
    preserve individual lamp IDs and emitter centres
    record diagnostics/source metadata showing the tray was grouped from an MFME shared lamp set

else
    keep existing one-lamp-window-to-one-tray behaviour
```

For the first implementation, keep the tray shape simple: rectangular bounds or existing derived polygon. The main goal is to avoid multiple fully overlapping trays for a known shared MFME lamp component.

Do not use this as a generic solution for all overlapping trays. Small-contained-in-large overlaps and heavy overlaps without shared-source provenance remain ambiguous and should stay conservative.

## Conservative Overlap Interpretation

Do not treat all overlaps the same.

Partial overlap between similarly sized neighbouring trays:

```text
Likely adjacent physical trays.
Generate non-overlapping polygons, usually by clipping or diagonal/bevel splitting.
```

Small tray mostly contained inside a larger tray:

```text
Ambiguous, but often a valid isolated small cut-out near a larger lit area.
Preserve both trays. Do not auto-merge. Add a containment diagnostic.
```

Two or more similarly large trays with heavy/near-full overlap:

```text
Possible shared physical tray/cavity, but not safe to auto-merge yet unless there is known shared MFME lamp-set provenance.
Preserve trays for now. Add a possible-shared-tray diagnostic for later multi-emitter/shared-tray work.
```

This phase should improve obvious geometry while avoiding destructive automatic merges.

## Export Direction

Runtime export uses authored trays/emitters when present and falls back to the temporary lamp-window bridge only when authored data is absent.

For the first authored-tray export, keep the influence simple:

```text
one tray + one or more emitters -> each emitter weight 1.0 inside tray for its lamp slot/channels
```

Do not add distance-based multi-bulb falloff until authored tray geometry and emitter placement quality are good enough to evaluate.

## Roadmap

### Phase 4A: Auto-author rough trays and emitters with minimal overlays

Completed.

### Phase 4E: Export from authored trays and emitters

Completed.

### Phase 4F: MFME bulb-mask emitter placement

Completed.

### Phase 4G: MFME shared lamp-set tray grouping

- Detect Face lamp windows imported from the same MFME lamp component/shared lamp set.
- For known shared MFME lamp sets, create one authored tray for the group rather than one tray per lamp element.
- Assign all lamp emitters in that shared set to the single tray.
- Preserve individual lamp IDs and emitter centres.
- Prefer Blend-enabled multi-lamp components, but preserve safe fallback behaviour.
- Derive tray bounds from shared component bounds or union of mask contribution bounds.
- Store tray/emitter diagnostics/source metadata where practical.
- Keep existing one-lamp-window-to-one-tray behaviour for lamps without shared-set provenance.
- Do not perform generic overlap merging.
- Do not implement distance falloff yet.

### Phase 4H: Conservative polygonal tray derivation

- Keep rectangular generated trays as the fallback.
- Derive non-rectangular vertices for obvious cases.
- Generate round/octagonal polygons for isolated round-ish/square-ish lamps where appropriate.
- Clip or bevel partially overlapping neighbouring trays into non-overlapping polygon shapes.
- Handle feature-trail corner cases with conservative diagonal cuts where the overlap pattern strongly suggests a turn/corner.
- Detect small-contained-in-large overlaps and preserve both trays with diagnostics.
- Detect similarly large near-identical/heavy overlaps and preserve both trays with possible-shared-tray diagnostics unless known shared MFME lamp-set grouping already applies.
- Do not auto-merge trays without shared-source provenance.
- Do not implement additional shared/multi-emitter inference yet.
- Preserve deterministic output.
- Ensure overlays, export, and CPU preview use the derived polygon vertices.

### Phase 4I: Auto-authoring refinement

Only after Phase 4G/4H have been evaluated on real machines:

- tune tray sizing heuristics
- tune overlap thresholds
- improve tray merging diagnostics
- connected-component tracing
- diagonal/polygon inference improvements
- confidence scoring

### Phase 4J: Improved debug overlays and inspection

- Add proper overlay toggles/options if needed.
- Improve label styling and visibility.
- Make auto-authored tray/emitter data easier to inspect.
- Optionally show generated runtime texture debug images.
- Keep this lighter than manual editing work.

### Phase 4K: Manual tray editing

- Add basic create/edit/delete tray operations.
- Support moving vertices and editing simple polygons.
- Ensure edits go through document-scoped commands where current editor architecture requires undo/redo.
- Keep business logic out of WPF code-behind.

### Phase 4L: Manual emitter editing

- Add basic emitter movement and tray assignment.
- Allow lamp ID/reference correction where needed.
- Support multiple emitters in a tray.

### Phase 5: Multi-bulb influence maps

- Support two to five bulbs in one tray.
- Generate weighted influence maps from emitter positions.
- Add `lampIds1.png`/`lampWeights1.png` only when needed.
- Add falloff/radius tuning.

### Phase 6: Unity runtime

- Implement Unity loader and shader after editor-side authored trays, runtime textures, and CPU preview are validated.
