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

Move from generated rectangular tray guesses toward authored Face-level polygon tray/emitter data and smooth per-emitter influence maps.

The editor should support this workflow:

```text
Generate Face from Panel2D
  -> import all MFME lamp elements, including shared lamp sets
  -> use MFME bulb masks to place emitters when available
  -> group known MFME shared lamp-set elements into one authored tray
  -> generate smooth per-emitter influence within shared trays
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

MFME shared lamp-set grouping now creates one tray with multiple emitters for known shared MFME lamp components.

This fixes the structural problem, but exposes the next rendering issue: the current runtime influence texture generation still writes hard/binary weights for every emitter inside the tray.

Current behaviour for a shared tray with multiple emitters:

```text
for every pixel inside tray:
  lampIds0.r/g/b = emitter lamp IDs
  lampWeights0.r/g/b = 255 for every emitter
```

That makes all emitters contribute fully across the entire shared tray. Where two or more lamps are on, lighting can spike or form visible hard rectangular/blocked bright regions instead of smoothly blending.

The next priority is to generate smooth per-emitter influence weights inside multi-emitter trays.

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

For MFME shared sets, create one authored tray for the group and one emitter per lamp window. Preserve individual lamp IDs and emitter centres.

## Multi-Emitter Influence Maps

Shared trays need smooth influence maps, not binary full-tray weights.

Recommended first runtime export behaviour:

```text
single-emitter tray:
  one lamp ID
  weight = 1.0 inside tray

multi-emitter tray:
  for each pixel inside tray:
    compute distance from pixel to each emitter centre
    convert distances to smooth raw weights
    keep the strongest 1-4 emitters supported by lampIds0/lampWeights0
    normalize retained weights so total influence remains stable
```

Suggested first weighting formula:

```text
rawWeight = 1 / (distanceSquared + softness)
```

or:

```text
rawWeight = saturate(1 - distance / radius) ^ falloff
```

Start with a simple deterministic formula. Use emitter radius if available, otherwise derive a reasonable radius from tray bounds and emitter count.

The key visual requirement is that two bulbs in the same tray should blend smoothly without hard rectangular bright blocks. Total intensity should not spike sharply just because two influence regions overlap.

Do not add `lampIds1.png`/`lampWeights1.png` in the first smoothing pass unless required. The current practical target is two to three emitters in one shared tray.

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

For authored shared trays, runtime export should write per-pixel influence weights from emitter positions.

## Roadmap

### Phase 4A: Auto-author rough trays and emitters with minimal overlays

Completed.

### Phase 4E: Export from authored trays and emitters

Completed.

### Phase 4F: MFME bulb-mask emitter placement

Completed.

### Phase 4G: MFME shared lamp-set tray grouping

Completed.

### Phase 5A: Smooth multi-emitter influence maps

- Keep single-emitter trays using binary full-tray weight for now.
- For multi-emitter trays, generate smooth per-pixel influence weights from emitter centres.
- Preserve lampIds0/lampWeights0 format.
- Support the current RGB-channel practical limit for up to three emitters in a tray unless the existing alpha-channel handling is revised.
- Normalize retained weights so total light remains stable.
- Use deterministic distance-based weighting.
- Use emitter radius when available, otherwise derive a conservative radius from tray bounds.
- Update lampWeights_debug.png so the influence distribution can be inspected.
- Do not add lampIds1/lampWeights1 yet unless explicitly required.
- Do not implement Unity code.

### Phase 5B: Conservative polygonal tray derivation

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

### Phase 5C: Auto-authoring refinement

Only after Phase 5A/5B have been evaluated on real machines:

- tune tray sizing heuristics
- tune overlap thresholds
- improve tray merging diagnostics
- connected-component tracing
- diagonal/polygon inference improvements
- confidence scoring

### Phase 5D: Additional multi-bulb influence capacity

- Add `lampIds1.png`/`lampWeights1.png` only when real data requires more than the current lampIds0 channel capacity.
- Support four or more bulbs in one tray when needed.
- Ensure CPU preview and future Unity runtime remain aligned.

### Phase 5E: Improved debug overlays and inspection

- Add proper overlay toggles/options if needed.
- Improve label styling and visibility.
- Make auto-authored tray/emitter data easier to inspect.
- Optionally show generated runtime debug texture views.
- Keep this lighter than manual editing work.

### Phase 5F: Manual tray editing

- Add basic create/edit/delete tray operations.
- Support moving vertices and editing simple polygons.
- Ensure edits go through document-scoped commands where current editor architecture requires undo/redo.
- Keep business logic out of WPF code-behind.

### Phase 5G: Manual emitter editing

- Add basic emitter movement and tray assignment.
- Allow lamp ID/reference correction where needed.
- Support multiple emitters in a tray.

### Phase 6: Unity runtime

- Implement Unity loader and shader after editor-side authored trays, runtime textures, and CPU preview are validated.
