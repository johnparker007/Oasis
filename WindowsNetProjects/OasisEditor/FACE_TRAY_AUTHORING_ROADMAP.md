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

The next major workstream is not more lighting/rendering polish. It is improving the data that feeds the renderer: explicit physical trays and lamp emitters.

UK slot machine glass panels often have irregular physical lamp trays behind the glass. A tray may contain one bulb, or several individually controllable bulbs. The current bridge derives temporary rectangular trays from `FaceLampWindowElement` instances, which is useful for proving the texture path but is not a good long-term physical model.

## Core Direction

Move from temporary generated lamp-window rectangles to authored Face-level tray/emitter data.

The editor should support this workflow:

```text
Generate Face from Panel2D
  -> auto-author rough trays and emitters from imported/generation data
  -> show tray/emitter debug overlays
  -> user corrects rough trays manually
  -> export runtime textures from authored trays/emitters
  -> CPU preview validates result
  -> future Unity renderer consumes same textures
```

The important principle is that auto-authoring is a starting guess, not a source of truth forever. The Face document should become the source of truth for physical tray layout after generation.

## Current Priority

Authored trays and emitters now exist and are persisted, visualised, and regenerated.

Before investing in smarter tray inference, polygon tracing, overlap analysis, tray merging, or manual editing tools, the authored tray/emitter model must become the source of truth for runtime export.

Current state:

```text
Authored trays/emitter data
    ↓
Overlay visualisation

Runtime export
    ↓
Temporary lamp-window bridge
```

Target state:

```text
Authored trays/emitter data
    ↓
Runtime export
    ↓
Runtime textures
    ↓
CPU preview
    ↓
Future Unity renderer
```

Once authored trays affect runtime textures directly, improvements to auto-authoring heuristics can be evaluated immediately in the CPU preview.

## Export Direction

After auto-authored tray/emitter elements exist, runtime export should use authored trays/emitters instead of temporary `FaceLampWindowElement` bridge rectangles.

Transition plan:

1. If authored trays/emitters exist, export from them.
2. If none exist, fall back to the current temporary lamp-window bridge.
3. Once authored tray generation is stable, the fallback can remain as a safety net.

For the first authored-tray export, keep the influence simple:

```text
one tray + one emitter -> weight 1.0 inside tray
```

Do not add multi-bulb falloff until authored trays are visible and editable.

## Roadmap

### Phase 4A: Auto-author rough trays and emitters with minimal overlays

Completed.

### Phase 4B: Improved debug overlays and inspection

Deferred until authored trays drive runtime export.

### Phase 4C: Manual tray editing

Deferred until authored trays drive runtime export.

### Phase 4D: Manual emitter editing

Deferred until authored trays drive runtime export.

### Phase 4E: Export from authored trays and emitters

- Make authored trays and emitters the source of truth for runtime texture generation.
- Generate `trayId.png`, `lampIds0.png`, and `lampWeights0.png` from authored tray/emitter data when present.
- Preserve the existing lamp-window bridge as fallback.
- Preserve CPU preview compatibility.
- Preserve manifest compatibility.
- Use simple weights initially.
- One tray + one emitter = weight 1.0 inside tray.
- No multi-bulb support yet.
- No falloff weighting yet.
- Authored tray corrections should immediately affect exported textures and CPU preview results.

### Phase 4F: Auto-authoring refinement

Only after authored trays drive runtime export:

- improve tray sizing heuristics
- tray merging heuristics
- overlap analysis
- connected-component tracing
- diagonal/polygon inference
- confidence scoring

### Phase 5: Multi-bulb influence maps

- Support two to five bulbs in one tray.
- Generate weighted influence maps from emitter positions.
- Add `lampIds1.png`/`lampWeights1.png` only when needed.
- Add falloff/radius tuning.

### Phase 6: Unity runtime

- Implement Unity loader and shader after editor-side authored trays, runtime textures, and CPU preview are validated.
