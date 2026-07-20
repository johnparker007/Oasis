# Face Semantic Components — Context

## Scope

This work completes the Panel2D-to-Face conversion pipeline in Oasis Editor.

A generated Face is not only flattened artwork and lamp windows. It is the complete semantic representation of the selected panel area after perspective correction.

The current implementation creates:

- perspective-corrected background artwork
- lamp windows
- generated mask/tray/emitter data

It does not currently create Face elements for other supported Panel2D components contained inside the Face Source Shape.

The missing component classes are:

- conventional reels
- 7-segment displays
- alphanumeric/segment-alpha displays
- buttons, where the source component has a meaningful input binding

This omission prevents later runtime export from discovering those components. The Player correctly reports zero runtime reel entries when the generated Face contains no `FaceReelDisplayElement` instances.

## Authoritative asset flow

Use one conversion path:

```text
Panel2D asset
    -> Face Source Shape
    -> generated/regenerated Face asset
    -> runtime export
    -> Oasis Player
```

The Face asset must become the authoritative flattened presentation of the selected Panel2D region.

Do not make machine runtime export independently scan Panel2D assets to recover components omitted from Face generation. That would create two competing conversion paths and make Face regeneration inconsistent.

## Existing architecture

Relevant code includes:

- `FaceGenerationService`
- `FaceRegenerationService`
- `FaceSourceShapeTransformService`
- `Panel2DDocumentModel`
- `FaceDocumentModel`
- `FaceReelDisplayElement`
- `FaceSevenSegmentDisplayElement`
- `FaceAlphaDisplayElement`
- `FaceButtonElement`
- Face editor rendering and serialization

The regeneration service already contains identity-preservation branches and regeneration keys for reels, 7-segment displays, alpha displays, buttons and lamps. The initial generation path is the incomplete stage.

## Inclusion rule

Use the existing semantic inclusion rule used by lamps:

- include a source component when its centre lies inside the Face Source Shape

This keeps the initial behaviour deterministic and avoids ambiguous partial-overlap rules.

The implementation should centralize this rule rather than duplicating subtly different tests for each component type.

## Coordinate conversion

Background and lamp-mask pixels are perspective corrected as raster data.

Semantic components must not be raster-warped. They should remain typed Face elements with rectangular bounds.

For each included Panel2D component:

1. take its four panel-space rectangle corners
2. transform each corner from panel space into flattened Face space using the same source-shape mapping used by the background/lamp pipeline
3. calculate the axis-aligned bounding rectangle of the transformed corners
4. clamp or validate the result against the generated Face bounds
5. create the corresponding typed Face element

This preserves alignment with the flattened artwork while avoiding perspective or skew geometry in semantic component models.

A simple translation from the source-shape bounding box is not sufficient for trapezoidal or perspective source shapes.

## Semantic preservation

Generated Face elements must preserve existing authored semantics rather than reinterpreting them.

### Conventional reels

Preserve at least:

- source Panel2D element identity
- stable linked Panel2D element ID
- linked machine object reference
- reel-band asset path
- stop count
- visible scale
- band offset
- reversed flag
- visibility
- generated flattened bounds

### 7-segment displays

Preserve at least:

- source Panel2D element identity
- stable linked Panel2D element ID
- linked machine object reference
- display type/reference fields already represented by the models
- on colour
- off colour
- decimal-point setting
- visibility
- generated flattened bounds

### Alphanumeric/segment-alpha displays

Preserve at least:

- source Panel2D element identity
- stable linked Panel2D element ID
- linked machine object reference
- segment display type/reference fields already represented by the models
- on colour
- off colour
- decimal-point setting
- comma-tail setting
- reversed flag where supported
- visibility
- generated flattened bounds

### Buttons

Preserve at least:

- source Panel2D element identity
- stable linked Panel2D element ID
- linked machine object reference
- linked input reference
- visibility
- generated flattened bounds

Only generate buttons where the source Panel2D component genuinely represents a button/input region.

## Identity and regeneration

Generated semantic elements must be correlated through `LinkedPanel2DElementId` and their typed regeneration key.

On regeneration:

- update generated geometry and source-derived properties
- preserve runtime object identity where the current regeneration system already does so
- preserve user-maintained runtime/machine bindings according to existing regeneration policy
- remove generated semantic elements whose source component is no longer included
- preserve unrelated manual Face elements
- do not duplicate elements on repeated regeneration

The initial creation and regeneration paths must use the same conversion helpers wherever practical.

## Layers and editor visibility

Generated semantic components must be visible and selectable in the Face editor.

Add or reuse clear semantic layers as appropriate. Avoid creating a separate layer for every individual subtype unless that matches the existing Face editor conventions.

At minimum, the Face element list and preview must make it obvious that reels and displays were imported from Panel2D.

## Runtime relationship

This task is primarily an Editor asset-generation task.

After it is complete, existing/follow-up runtime export should observe non-zero typed component collections when the Face contains those components.

The runtime reel implementation depends on generated `FaceReelDisplayElement` instances carrying a valid band path and machine reference.

Future Player work for 7-segment and alpha displays should consume the completed Face asset rather than reading Panel2D directly.

## Schema policy

Oasis is an early personal project with no backwards-compatibility requirement.

If the current Face serialized shape changes:

- update the current writer and reader together
- increment the current schema version
- support only the latest format
- update tests and fixtures
- delete superseded format code

Do not add migration layers, legacy DTOs, old-version loaders or fallback parsing.

## Non-goals

Do not implement in this task:

- Player rendering for 7-segment displays
- Player rendering for alpha displays
- final 3D reel placement or live reel movement
- perspective/skewed semantic component geometry
- arbitrary overlap-percentage inclusion rules
- automatic cabinet target authoring
- backwards-compatible Face formats
