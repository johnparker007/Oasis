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

For these MFME shared lamp sets, each lamp element may also have a per-element mask image. This MFME bulb mask is not the same thing as the Oasis generated lamp mask. In MFME, when the component `Blend` flag is enabled, these per-element masks control how each lamp-on image is blended into the shared component area.

This mask image is valuable source data for Oasis: its bright/falloff region indicates where the physical bulb likely sits inside the shared tray.

Before further polygon tray derivation, use MFME bulb mask images to place emitter centres more accurately.

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
Possible shared physical tray/cavity, but not safe to auto-merge yet.
Preserve trays for now. Add a possible-shared-tray diagnostic for later multi-emitter/shared-tray work.
```

This phase should improve obvious geometry while avoiding destructive automatic merges.

## Export Direction

Runtime export uses authored trays/emitters when present and falls back to the temporary lamp-window bridge only when authored data is absent.

For the first authored-tray export, keep the influence simple:

```text
one tray + one emitter -> weight 1.0 inside tray
```

Do not add multi-bulb falloff until authored tray geometry and emitter placement quality are good enough to evaluate.

## Roadmap

### Phase 4A: Auto-author rough trays and emitters with minimal overlays

Completed.

### Phase 4E: Export from authored trays and emitters

Completed.

### Phase 4F: MFME bulb-mask emitter placement

- Use per-lamp MFME bulb mask images to infer individual emitter centres.
- Apply this especially for MFME components with `Blend` enabled and multiple lamp elements sharing the same component bounds.
- Compute a weighted centroid from mask brightness/alpha.
- Map mask-space centroid to Panel2D/Face coordinates.
- Store placement-source diagnostics on emitters where practical.
- Derive approximate emitter radius where practical, but do not change runtime influence maps yet.
- Preserve existing centre fallback behaviour when masks are missing, invalid, or empty.
- Do not implement multi-bulb influence/falloff yet.

### Phase 4G: Conservative polygonal tray derivation

- Keep rectangular generated trays as the fallback.
- Derive non-rectangular vertices for obvious cases.
- Generate round/octagonal polygons for isolated round-ish/square-ish lamps where appropriate.
- Clip or bevel partially overlapping neighbouring trays into non-overlapping polygon shapes.
- Handle feature-trail corner cases with conservative diagonal cuts where the overlap pattern strongly suggests a turn/corner.
- Detect small-contained-in-large overlaps and preserve both trays with diagnostics.
- Detect similarly large near-identical/heavy overlaps and preserve both trays with possible-shared-tray diagnostics.
- Do not auto-merge trays.
- Do not implement shared/multi-emitter trays yet.
- Preserve deterministic output.
- Ensure overlays, export, and CPU preview use the derived polygon vertices.

### Phase 4H: Auto-authoring refinement

Only after Phase 4F/4G have been evaluated on real machines:

- tune tray sizing heuristics
- tune overlap thresholds
- improve tray merging diagnostics
- connected-component tracing
- diagonal/polygon inference improvements
- confidence scoring

### Phase 4I: Improved debug overlays and inspection

- Add proper overlay toggles/options if needed.
- Improve label styling and visibility.
- Make auto-authored tray/emitter data easier to inspect.
- Optionally show generated runtime texture debug images.
- Keep this lighter than manual editing work.

### Phase 4J: Manual tray editing

- Add basic create/edit/delete tray operations.
- Support moving vertices and editing simple polygons.
- Ensure edits go through document-scoped commands where current editor architecture requires undo/redo.
- Keep business logic out of WPF code-behind.

### Phase 4K: Manual emitter editing

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
