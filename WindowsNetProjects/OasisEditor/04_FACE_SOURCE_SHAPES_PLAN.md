# Face Source Shapes Plan

## Purpose

Add a proper workflow for generating rectangular Face documents from photographed/perspective Panel2D source artwork.

The old temporary workflow generated a Face from a typed rectangular region. That was useful for testing, but the real workflow needs a user-defined source shape over the photographed machine glass/artwork area.

User-facing name:

```text
Face Source Shape
```

Do not use `quad` as the user-facing term. Internally, the first supported shape can be a four-point perspective rectangle, but the name should remain general enough to allow future source shapes such as curved or semi-circular glass areas.

## Current product model

The intended document roles are:

```text
Panel2D document
- source/import document
- may contain photographed/perspective artwork
- owns Face Source Shapes that mark source artwork regions

Face document
- corrected rectangular artwork/lamp document
- generated from a Panel2D Face Source Shape
- stores assigned cabinet face target ID
- should be regeneratable from its source shape

Cabinet3D document
- references a GLB cabinet model
- detects OasisFace_* target surfaces
- stores target mapping overrides
- previews assigned Faces on the 3D cabinet
```

## Legacy reference

The legacy Unity editor had a useful partial implementation under:

```text
UnityProjects/LayoutEditor
```

Relevant files:

```text
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Layout/ViewQuad.cs
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/LayoutEditor/Panels/PanelViewQuadInspector.cs
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Layout.cs
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Graphics/OasisImage.cs
```

Important legacy concepts:

- `ViewQuad` stored four ordered points: top-left, top-right, bottom-right, bottom-left.
- The inspector let users edit the four points and trigger Add/Update View.
- `LayoutObject.OutputTransformedViewQuad` converted the selected shape to image-space and called `OasisImage.Transform`.
- `OasisImage.Transform` implemented the useful part: homography/perspective transform, target sizing from aspect ratio, and bicubic sampling.

Do not port the old Unity UI directly. Reuse the algorithmic idea in a new UI-independent .NET service.

## First supported shape

V1 supports only:

```text
Face Source Shape Type: Perspective Rectangle
```

This is a four-point shape with ordered corners:

```text
TopLeft
TopRight
BottomRight
BottomLeft
```

Store points in Panel2D/source image coordinates.

## Target-aware creation workflow

Face creation should be target-aware.

Preferred workflow:

1. User opens a Panel2D document containing the source photograph/artwork.
2. User chooses `Add Face Source Shape`.
3. Editor creates a Perspective Rectangle shape over the Panel2D canvas.
4. User drags the four corners to match the perspective glass/artwork area.
5. User chooses `Create Face from Source Shape`.
6. Creation UI asks for:
   - Face name/path
   - Cabinet target, if any Cabinet3D document with valid targets is open
7. Face generation uses the selected cabinet target aspect ratio when available.
8. Generated Face document stores the selected cabinet target ID immediately.

This avoids creating a Face first and assigning the cabinet target later.

## Output resolution and aspect ratio

V1 should use automatic output size. Do not expose width/height fields initially unless the existing UI needs a debug fallback.

Base source size estimate:

```text
sourceWidth  = max(length(top edge), length(bottom edge))
sourceHeight = max(length(left edge), length(right edge))
```

If a Cabinet3D target is selected and available, use its aspect ratio:

```text
targetAspect = cabinetTargetWidth / cabinetTargetHeight
```

Then expand one dimension, never shrink, to preserve source fidelity:

```text
widthFromHeight = sourceHeight * targetAspect
heightFromWidth = sourceWidth / targetAspect

if widthFromHeight >= sourceWidth:
    outputWidth = widthFromHeight
    outputHeight = sourceHeight
else:
    outputWidth = sourceWidth
    outputHeight = heightFromWidth
```

Round up to integer pixels and clamp to at least 1x1.

If no Cabinet3D target is selected/available:

```text
outputWidth = sourceWidth
outputHeight = sourceHeight
```

The creation UI may show read-only info such as:

```text
Output: Auto, 1420 x 860, using Top Glass aspect ratio
```

## Transform scope

The legacy implementation only transformed the background image. The new implementation must be designed to grow beyond that.

V1 may transform only the background/artwork image, but the service/API should be shaped so later tasks can transform:

- lamp positions
- lamp bounds
- lamp masks
- lamp artwork/support textures
- other Panel2D elements that belong inside the source shape

For point/rect based data, use the same homography mapping from source Panel2D coordinates to generated Face coordinates.

For images/masks, use the same perspective image warp service used for the background.

## Persistence direction

Panel2D should persist Face Source Shapes.

Suggested model direction:

```json
{
  "faceSourceShapes": [
    {
      "id": "mainGlassSource",
      "name": "Main Glass Source",
      "type": "perspectiveRectangle",
      "points": [
        [100, 80],
        [900, 120],
        [860, 620],
        [130, 580]
      ]
    }
  ]
}
```

Adapt to existing Panel2D schema and naming conventions.

The generated Face document should store enough source metadata for regeneration, for example:

```json
{
  "sourcePanel2DPath": "Panels/Imported.panel2d",
  "sourceFaceShapeId": "mainGlassSource",
  "assignedCabinetFaceTargetId": "topGlass"
}
```

Use existing Face source/regeneration fields if already present.

## UI direction

V1 UI requirements:

- Add a Face Source Shape to a Panel2D document.
- Show the shape on the Panel2D canvas.
- Allow dragging the four corners.
- Show/select Face Source Shapes in Hierarchy/Inspector if practical.
- Inspector should show name, type, and four point coordinates.
- Add command to create a Face from the selected source shape.

Use existing document command patterns. Mutations should be undoable/redoable, mark the document dirty only for real changes, and avoid broad UI rebuilds where possible.

## Rect workflow

The old typed rectangular region generation UI was temporary.

Do not make it the primary workflow. It can remain temporarily as a debug/legacy fallback while Face Source Shape generation is implemented, but the intended product workflow is source-shape based.

Once Face Source Shape generation works reliably, remove or demote the typed Rect UI.

## Non-goals for first task

Do not implement in the first task unless explicitly requested:

- arbitrary curved or semi-circular source shapes
- full lamp/mask perspective transform
- removing the old Rect workflow
- dynamic cabinet lamp simulation
- Unity runtime changes
- large unrelated refactors

## Testing direction

Codex cannot run the WPF build/tests in its environment.

John should test locally:

- solution builds
- existing Panel2D open/save still works
- Add Face Source Shape creates a visible four-corner shape
- shape corners can be dragged and/or edited in Inspector
- shape persists after save/reopen
- Create Face from Source Shape opens target-aware creation flow
- selecting a Cabinet3D target uses target aspect ratio
- no target selected uses source-shape estimated dimensions
- generated Face background is perspective-corrected
- generated Face stores assigned cabinet target ID when selected
- existing Face, Cabinet3D, and Panel2D workflows remain unaffected
