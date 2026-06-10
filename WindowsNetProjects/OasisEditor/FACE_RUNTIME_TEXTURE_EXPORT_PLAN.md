# Face Runtime Texture Export and Preview Plan

## Purpose

This plan captures the intended direction for rendering UK slot machine glass panels from Oasis Editor into a future Unity-based player.

The immediate implementation should happen inside the WPF editor first. Unity should consume the same exported data later, once the editor-side data model, texture generation, and CPU preview are working.

## Key Decision

Build and validate the runtime face rendering data inside the editor first:

1. Define the Face runtime export format.
2. Generate artwork, mask, tray, lamp ID, and lamp weight textures from Face data.
3. Render a CPU preview in the WPF Face Play view using those generated textures.
4. Validate tray ownership, multi-bulb trays, lamp state mapping, mask clipping, art transparency, displays, and reels in the editor. Phase 3a bridge-tray overlaps are diagnostics only; authored physical tray overlaps introduced later receive warning/error validation.
5. After the data format is stable, implement the equivalent Unity URP shader/runtime loader.

Unity remains the authoritative final player renderer, but the editor must become the authoritative authoring and validation environment.

## Schema Compatibility Policy

This area is still early prototype work and John is currently the only developer using it.

Do not spend implementation effort preserving compatibility with older `.face` files unless a task explicitly asks for it. It is acceptable to change the Face schema aggressively while this system is being designed.

The required guarantee is only that a Face document created/saved by the latest code can be closed, reopened, and rendered/exported again by the latest code.

## Existing Architecture Context

Panel2D is currently the source/layout canvas. It stores rectangular visual elements such as backgrounds, lamps, reels, seven segment displays, alpha displays, and metadata such as asset paths, display properties, visibility, lock state, and machine object references.

Face is already closer to the runtime-facing model. A Face document is generated from a selected Panel2D region and stores:

- source Panel2D document/region metadata
- generated mask layer metadata
- face layers
- artwork elements
- lamp window elements
- reel display elements
- seven segment display elements
- alpha display elements
- button elements

The current Face renderer draws:

1. artwork
2. lamp illumination clipped through the generated mask
3. reels/displays/buttons

The current lamp illumination is a SkiaSharp CPU approximation: one radial gradient per lamp window, clipped by the mask image. This should remain available as a simple fallback, but the next implementation should add a texture-driven preview path matching the planned Unity data model.

## Rendering Model

Separate these concepts explicitly:

- Artwork: the visible printed glass artwork, including transparent holes for reels, displays, and other behind-glass elements.
- Mask: where light can pass through the reverse glass mask.
- Tray: physical lamp tray / compartment containment area. Trays may be rectangular, circular, triangular, or irregular polygons.
- Lamp emitter: an individually controllable bulb position inside a tray.
- Lamp influence: per-pixel mapping from pixels to the lamp emitters that affect them.

The runtime equation should be deliberately simple and shared conceptually between WPF and Unity:

```text
visibleLight = mask * sum(lampState[id] * weight)
finalRgb = artworkRgb * ambient + artworkRgb * visibleLight * emission
finalAlpha = artworkAlpha
```

The shader/code itself will not be shared between WPF and Unity. The shared contract is the exported textures, manifest, lamp state values, and lighting equation.

## Exported Runtime Package

Generate a package under the project Generated directory, for example:

```text
Generated/Faces/{faceId}/runtime/
  face.runtime.json
  artwork.png
  mask.png
  trayId.png
  lampIds0.png
  lampWeights0.png
  lampIds1.png       optional
  lampWeights1.png   optional
  trayId_debug.png        optional/debug
  lampWeights_debug.png   optional/debug
```

### artwork.png

A flattened transparent PNG of visible Face artwork at Face runtime resolution.

Requirements:

- Preserve alpha from the source artwork.
- Transparent artwork areas must remain transparent so reels, seven segment displays, alpha displays, and other behind-glass features can show through.
- Use the existing source-region/provenance logic when an artwork element references a cropped region from a larger Panel2D source asset.

### mask.png

A grayscale or alpha-style mask texture representing where lamp light is visible through the reverse glass mask.

For the first implementation, reuse the existing generated Face mask layer asset where possible.

Longer term, prefer explicit Face mask authoring over extracting masks from lamp-on/lamp-off Panel2D assets.

Suggested Unity import settings later:

- sRGB off
- compression none
- mipmaps off
- clamp wrap mode

### trayId.png

A single-channel 8-bit ID texture where each pixel identifies the physical tray that owns that pixel.

```text
0 = no tray
1..255 = tray ID
```

This is not the same as lamp ID. A large tray may contain multiple individually controllable lamps.

For the current known target range of roughly 15 to 130 lamps, 8-bit ID storage is enough. If trays or lamps later exceed 255 IDs, introduce a 16-bit format or explicit encoded ID texture.

Suggested import/settings later:

- single-channel or grayscale
- sRGB off
- compression none
- mipmaps off
- point filtering
- clamp wrap mode

### lampIds0.png and lampWeights0.png

Per-pixel lamp influence textures.

For each pixel, store up to four lamp IDs and four corresponding weights:

```text
lampIds0.r = first influencing lamp ID
lampIds0.g = second influencing lamp ID
lampIds0.b = third influencing lamp ID
lampIds0.a = fourth influencing lamp ID

lampWeights0.r = weight for first lamp
lampWeights0.g = weight for second lamp
lampWeights0.b = weight for third lamp
lampWeights0.a = weight for fourth lamp
```

If a tray contains five or more bulbs, add `lampIds1.png` and `lampWeights1.png` for the additional four slots.

ID channels should be read exactly, so keep ID textures uncompressed, non-sRGB, no mipmaps, point filtered. Weight textures may be bilinear filtered if useful, but for exact CPU/Unity parity it is acceptable to start with point sampling for both.

### Debug textures

Generate optional debug textures during editor export while this system is being developed:

- `trayId_debug.png`: colourised tray ownership, useful for checking tray mapping and overlap diagnostics.
- `lampWeights_debug.png`: visualises stored influence weights, useful for checking whether lamps affect the expected pixels.

These debug textures are editor/export diagnostics. They are not required by the future Unity runtime.

## Face Model Additions

Do not make Panel2D responsible for these runtime textures. Extend the Face model because Face is the physical presentation/runtime document.

Suggested model additions:

```csharp
public sealed class FaceRuntimeRenderAssetsModel
{
    public string? ArtworkPath { get; init; }
    public string? MaskPath { get; init; }
    public string? TrayIdPath { get; init; }
    public string? LampIds0Path { get; init; }
    public string? LampWeights0Path { get; init; }
    public string? LampIds1Path { get; init; }
    public string? LampWeights1Path { get; init; }
    public string? TrayIdDebugPath { get; init; }
    public string? LampWeightsDebugPath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTime GeneratedUtc { get; init; }
}
```

Suggested tray model:

```csharp
public sealed class FaceLampTrayElement : FaceElementModel
{
    public int TrayId { get; init; }
    public IReadOnlyList<FacePointModel> Polygon { get; init; } = [];
}
```

Suggested emitter model:

```csharp
public sealed class FaceLampEmitterElement : FaceElementModel
{
    public int LampId { get; init; }
    public int TrayId { get; init; }
    public double BulbX { get; init; }
    public double BulbY { get; init; }
    public double Radius { get; init; }
    public double Falloff { get; init; }
}
```

Use UI-agnostic point models, not WPF `Point`, in persisted/domain models.

The existing `FaceLampWindowElement` may remain as a temporary/fallback concept, but the new tray/emitter model should become the preferred runtime-lighting representation.

## Runtime Texture Generation Subsystem

Add a dedicated texture generation subsystem rather than growing all logic inside `FaceRuntimeExportService`.

Suggested responsibilities:

```text
FaceRuntimeExportService
  - coordinates package export
  - writes manifest
  - records generated asset metadata

FaceRuntimeTextureGenerator
  - coordinates runtime texture generation
  - owns shared validation/result models

ArtworkRuntimeTextureGenerator
  - flattens artwork into artwork.png

TrayIdTextureGenerator
  - writes trayId.png
  - writes trayId_debug.png

LampInfluenceTextureGenerator
  - writes lampIds0.png
  - writes lampWeights0.png
  - writes lampWeights_debug.png
```

Use the repository's actual naming/style conventions if these names do not fit.

The goal is to avoid reworking Phase 3 code when tray polygons and multi-bulb trays are introduced later.

## Manifest

Create `face.runtime.json` alongside the generated textures. It should describe the texture package and the runtime objects Unity will load.

Initial shape:

```json
{
  "schemaVersion": 1,
  "faceId": "...",
  "width": 1024,
  "height": 768,
  "artwork": "artwork.png",
  "mask": "mask.png",
  "trayId": "trayId.png",
  "lampIds0": "lampIds0.png",
  "lampWeights0": "lampWeights0.png",
  "lampIds1": null,
  "lampWeights1": null,
  "debug": {
    "trayId": "trayId_debug.png",
    "lampWeights": "lampWeights_debug.png"
  },
  "lamps": [
    {
      "lampId": 24,
      "machineReference": "Lamp:24",
      "name": "Jackpot Left",
      "trayId": 7,
      "x": 123.5,
      "y": 456.0
    }
  ],
  "emitters": [
    {
      "lampId": 24,
      "trayId": 7,
      "x": 123.5,
      "y": 456.0,
      "radius": 40.0,
      "falloff": 1.0
    }
  ],
  "trays": [],
  "reels": [],
  "sevenSegmentDisplays": [],
  "alphaDisplays": [],
  "buttons": []
}
```

Use relative paths inside the manifest so the package can be moved or loaded by Unity without depending on the original project root.

## Texture Generation Algorithm

### Artwork

Render all visible Face artwork elements to a transparent SkiaSharp bitmap at Face runtime resolution.

Respect each artwork element's destination bounds and source crop/provenance.

### Mask

For the first implementation:

- use the existing `FaceMaskLayerModel.AssetPath` if available
- copy it into the runtime package as `mask.png`
- if unavailable, generate a default black/no-light mask or report a validation error

### Phase 3 rectangular bridge behaviour

Before polygon trays are authored, derive temporary tray/emitter data from existing visible `FaceLampWindowElement` rectangles:

- each visible lamp window becomes one tray
- each tray has one emitter
- emitter position is the centre of the lamp window
- emitter lamp ID is resolved from the existing lamp window's machine reference where possible
- tray ID and lamp ID values must be stable and deterministic
- weight is `1.0` everywhere inside that temporary rectangular tray
- all remaining lamp ID and weight channels are empty/zero

Temporary bridge trays are not authored physical trays. They may overlap because MFME-derived lamp rectangles and current Face lamp windows are only coarse source data, not tray-compartment boundaries. For Phase 3a bridge trays:

- allow overlap
- record overlap diagnostics for debugging/export inspection
- use deterministic first-tray ownership for overlapping pixels so generated `trayId.png`, `lampIds0.png`, and `lampWeights0.png` remain internally consistent
- do not block save/export on bridge-tray overlap

This bridge exists only to prove the exported texture pipeline and CPU preview. It is not the final physical tray model.

### Tray ID

For each Face lamp tray polygon in the final model:

1. Rasterise the polygon into the output texture.
2. Write the tray ID into every covered pixel.
3. Use `0` outside all trays.

For the Phase 3 bridge, rasterise the temporary trays derived from `FaceLampWindowElement` data. These bridge trays may overlap; record diagnostics and resolve overlapping pixels with stable first-tray ownership rather than blocking save/export.

Overlap errors apply only to future authored physical tray geometry, not Phase 3a bridge trays. When authored `FaceLampTrayElement` polygons are introduced, detect overlap between authored trays and surface a warning or error depending on severity. Severe authored-tray overlaps should be treated as validation/export errors because they indicate ambiguous physical tray ownership.

### Lamp Influence

For each tray:

1. Gather all lamp emitters assigned to that tray.
2. For each pixel inside the tray polygon, calculate distance from the pixel to each emitter.
3. Convert distance to influence weight.
4. Keep the top four weights in `lampIds0/lampWeights0`.
5. If more than four emitters are meaningfully influential, keep slots five to eight in `lampIds1/lampWeights1`.
6. Normalise the retained weights so total influence is stable.

Suggested starting formula for the final polygon/multi-bulb implementation:

```text
rawWeight = 1 / (distanceSquared + softness)
```

or:

```text
rawWeight = saturate(1 - distance / radius) ^ falloff
```

For the Phase 3 bridge, do not implement distance falloff yet. Use weight `1.0` inside the temporary tray and `0.0` outside.

## WPF CPU Preview

Add a texture-driven Face preview path to the current Face Play rendering pipeline.

The CPU preview should load:

- artwork
- mask
- lamp IDs
- lamp weights
- current runtime lamp states

Then render a preview bitmap with the shared equation:

```text
visibleLight = mask * sum(lampState[id] * weight)
finalRgb = artworkRgb * ambient + artworkRgb * visibleLight * emission
finalAlpha = artworkAlpha
```

This does not need to match Unity bloom/post-processing. It should validate:

- correct lamp IDs
- correct tray ownership
- multi-bulb trays
- mask clipping
- transparent artwork holes
- display/reel visibility behind transparent art
- basic falloff and attract-mode sequencing

Keep the existing radial-gradient Skia preview as fallback while the texture path is being introduced.

## Unity Plan Later

Only after the editor export and CPU preview are working:

1. Create a Unity runtime loader for `face.runtime.json`.
2. Load the generated textures.
3. Implement equivalent URP/HLSL sampling logic.
4. Add emission and URP bloom for final visual quality.
5. Add hot-reload from the WPF editor/export folder.

Do not start Unity shader work until the texture package is proven by the editor preview.

## Implementation Phases

### Phase 1: Planning and Schema

- Add the Face runtime export plan document.
- Add model/file records for runtime render assets.
- Bump or replace the Face document schema as needed.
- Do not implement migrations for older `.face` files unless explicitly requested.
- Ensure Face documents created by the latest code can be saved, closed, reopened, and exported again by the latest code.

### Phase 2: Runtime Export Service

- Add `FaceRuntimeExportService`.
- Export `face.runtime.json`.
- Export flattened `artwork.png`.
- Copy/reuse existing `mask.png`.
- Store generated asset references back into the Face document model where appropriate.

### Phase 3a: Runtime Texture Generation Foundation

- Add a dedicated runtime texture generation subsystem.
- Introduce `FaceLampEmitterElement` now, because the runtime renderer ultimately needs emitters rather than only lamp windows.
- Auto-generate temporary emitters from existing visible `FaceLampWindowElement` instances.
- Generate `trayId.png`.
- Generate `lampIds0.png`.
- Generate `lampWeights0.png`.
- Generate optional debug visualisations: `trayId_debug.png` and `lampWeights_debug.png`.
- Bridge rules: one lamp window = one tray, one tray = one emitter, emitter at lamp window centre, weight `1.0` inside the tray. Temporary bridge tray overlaps are allowed, recorded as diagnostics, and resolved with deterministic first-tray ownership; they must not block save/export.
- Do not implement polygon tray authoring yet.
- Do not implement multi-bulb tray support yet.
- Do not implement distance falloff yet.

### Phase 3b: Texture-Driven CPU Preview Prototype

- Add a texture-driven CPU preview path to Face rendering using the generated runtime textures.
- Use current `MachineRuntimeState` lamp values.
- Add settings/constants for ambient, emission, and mask strength.
- Keep current radial-gradient renderer fallback.
- Validate the generated texture contract before investing heavily in polygon tray authoring.

### Phase 4: Tray and Emitter Authoring Model

- Add explicit Face tray and emitter authoring model.
- Add persistence support for the latest schema only.
- Add validation.
- Add hierarchy/inspector support only as much as needed for initial authoring.
- Ensure models use UI-agnostic data types.

### Phase 5: Polygon Rasterisation and Multi-Bulb Support

- Generate tray/influence textures from tray polygons and authored emitters.
- Detect overlap between authored physical tray polygons and surface a warning or error depending on severity; severe authored-tray overlaps should block export.
- Support 2 to 5 bulbs in a larger tray.
- Add optional `lampIds1/lampWeights1` when more than four emitters influence a pixel.
- Add real falloff weighting once multi-bulb trays exist.

### Phase 6: Unity Runtime

- Implement Unity loader and shader after the editor texture pipeline and CPU preview are validated.

## Testing Guidance

Codex cannot run the WPF/.NET build in its environment. After implementation, John should build and test locally.

Suggested local tests:

- generated Face from Panel2D still works
- a Face saved by the latest code can be closed and reopened by the latest code
- mask generation still writes the current mask asset
- runtime export creates all expected files
- transparent artwork areas stay transparent
- `trayId.png` marks expected temporary rectangular trays
- `lampIds0.png` stores expected deterministic lamp IDs
- `lampWeights0.png` stores weight `1.0` inside temporary trays and `0.0` outside
- `trayId_debug.png` is visually useful for checking ownership
- `lampWeights_debug.png` is visually useful for checking weight coverage
- CPU preview lights the correct lamp areas
- sequential lamp test lights lamps in numerical order
- random lamp test does not crash or light unmapped areas
- multi-bulb tray test shows independent lamps contributing to the same tray once Phase 5 exists
- displays/reels remain visible through transparent artwork holes

Add automated tests where practical for model serialization, manifest generation, and pure texture-generation logic. Avoid tests that require visible WPF windows.

## Non-Goals for the First Implementation

- Do not embed Unity in WPF.
- Do not build the Unity shader first.
- Do not attempt physically accurate ray tracing.
- Do not make Panel2D the final runtime render asset owner.
- Do not over-polish bloom or post-processing in the WPF CPU preview.
- Do not refactor unrelated editor systems while adding this pipeline.
