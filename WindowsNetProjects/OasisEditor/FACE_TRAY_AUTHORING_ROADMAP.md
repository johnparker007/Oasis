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

## Why Auto-Authoring Matters

Imported MFME/Panel2D data may have many lamps. Hand-tracing every tray from scratch would make each machine too expensive to prepare.

When a user generates a Face from a Panel2D region, the editor should create a first rough tray/emitter layout using available data:

- `FaceLampWindowElement` bounds
- `FaceMaskLayer.Contributions`
- `LinkedMachineObjectReference`
- `LinkedPanel2DElementId`
- source Panel2D lamp bounds
- generated mask contribution bounds

This initial guess will often be imperfect, but it should be good enough for visual debugging and manual correction.

## Target Concepts

### Physical tray

A tray is the physical light compartment behind the glass. It constrains where light from one or more bulbs can spread.

Eventually trays should support irregular polygons, but the first authored model can start with rectangles generated from mask contribution bounds.

Suggested persisted model shape:

```csharp
public sealed class FaceLampTrayElement : FaceElementModel
{
    public int TrayId { get; init; }
    public IReadOnlyList<FacePointModel> Polygon { get; init; } = [];
    public bool IsAutoAuthored { get; init; }
    public string? Source { get; init; }
}
```

Use UI-agnostic point models, not WPF `Point`, in persisted/domain models.

### Lamp emitter

An emitter is an individually controllable bulb inside a tray.

Suggested persisted model shape:

```csharp
public sealed class FaceLampEmitterElement : FaceElementModel
{
    public string SourceLampWindowObjectId { get; init; } = string.Empty;
    public int TrayId { get; init; }
    public int? LampId { get; init; }
    public double CenterX { get; init; }
    public double CenterY { get; init; }
    public double Radius { get; init; }
    public double Falloff { get; init; }
    public bool IsAutoAuthored { get; init; }
}
```

The current `FaceLampEmitterElement` from the texture bridge may already contain some of this. Extend or reshape it as needed; schema compatibility with older `.face` files is not required during this prototype phase.

## Auto-Authoring Strategy

### First implementation

When generating or regenerating a Face from a Panel2D region:

1. For each lamp window, find the best matching mask contribution.
2. Create one rough tray from the contribution bounds when available.
3. If no contribution is available, create a rough tray from the lamp window bounds.
4. Create one emitter at the lamp window centre.
5. Assign the emitter to the generated tray.
6. Resolve lamp ID/machine reference from the existing lamp window metadata where possible.
7. Mark generated trays/emitters as auto-authored.
8. Keep IDs stable and deterministic.

For the first implementation, rough trays may be rectangular polygons. Do not attempt complex polygon tracing yet.

### Later improvements

Improve the initial guess over time:

- merge nearby or overlapping contributions that likely share one physical tray
- group multiple emitters into a single larger tray where appropriate
- trace connected components from mask pixels
- expand tray bounds slightly to approximate physical spill space
- simplify traced polygons
- expose confidence/debug information to the user

Do not try to solve all of this in the first PR.

## Debug Overlays

Basic tray/emitter visualisation must be available as soon as auto-authoring exists. Otherwise evaluating generated tray quality requires inspecting JSON/model data, which is too slow.

Phase 4A should include a minimal overlay subset whose only goal is immediate visual verification:

- draw tray boundaries as simple outlines
- draw emitter positions as small markers
- show tray IDs where practical
- show lamp IDs where practical
- visually distinguish auto-authored elements
- provide enough visibility to judge whether generated trays are plausible

Keep this first overlay implementation deliberately lightweight:

- no editing interactions
- no vertex manipulation
- no new tool modes
- no broad UI refactor
- no complex overlay settings UI

A later Phase 4B can improve the overlay experience with toggles, labels, styling options, inspector integration, and generated runtime debug texture views.

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

- Add Face-level tray model if not already present.
- Ensure emitter model is suitable for persistence and later authoring.
- During Face generation/regeneration, create rough tray/emitter elements from existing lamp windows and mask contributions.
- Use rectangular polygons from mask contribution bounds for the first pass.
- Mark generated trays/emitters as auto-authored.
- Save/load latest-schema Face documents with generated tray/emitter elements.
- Add minimal non-interactive debug visualisation of generated trays and emitters.
- Show tray outlines, emitter markers, and IDs where practical.
- Do not add manual editing UI yet.
- Do not add Unity code.
- Do not add multi-bulb falloff.

### Phase 4B: Improved debug overlays and inspection

- Add proper overlay toggles/options if needed.
- Improve label styling and visibility.
- Make auto-authored tray/emitter data easier to inspect.
- Optionally show generated runtime texture debug images.
- Keep this lighter than manual editing work.

### Phase 4C: Manual tray editing

- Add basic create/edit/delete tray operations.
- Support moving vertices and editing simple polygons.
- Ensure edits go through document-scoped commands where current editor architecture requires undo/redo.
- Keep business logic out of WPF code-behind.

### Phase 4D: Manual emitter editing

- Add basic emitter movement and tray assignment.
- Allow lamp ID/reference correction where needed.
- Support multiple emitters in a tray.

### Phase 4E: Export from authored trays

- Generate `trayId.png`, `lampIds0.png`, and `lampWeights0.png` from authored trays/emitters.
- Use bridge fallback only when authored data is absent.
- Keep weights simple initially.

### Phase 5: Multi-bulb influence maps

- Support two to five bulbs in one tray.
- Generate weighted influence maps from emitter positions.
- Add `lampIds1.png`/`lampWeights1.png` only when needed.
- Add falloff/radius tuning.

### Phase 6: Unity runtime

- Implement Unity loader and shader after editor-side authored trays, runtime textures, and CPU preview are validated.

## Testing Guidance

Codex cannot run the WPF/.NET build in its environment. John will build and test locally.

Suggested tests for Phase 4A:

- generated Face contains tray elements
- generated Face contains emitter elements
- tray/emitter IDs are deterministic
- mask contribution bounds are preferred over lamp window bounds when available
- lamp window bounds are used as fallback when no contribution exists
- generated Face saves, closes, reloads, and preserves trays/emitters
- runtime export still works
- existing temporary bridge fallback still works when authored trays are absent
- overlay renderer/service can render tray outlines and emitter markers without requiring visible WPF windows where practical

Avoid tests that require visible WPF windows.

## Non-Goals for Phase 4A

- No Unity implementation.
- No manual tray editor.
- No manual emitter editor.
- No interactive overlay tooling.
- No complex connected-component tracing.
- No automatic multi-bulb tray grouping.
- No physically perfect tray inference.
- No schema migration for older `.face` files unless explicitly requested.
