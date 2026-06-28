# OasisFace Target Detection Task

## Read first

Read only:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`
3. `01_CABINET_MODEL_VIEWER_PLAN.md`
4. this file

Do not scan all Markdown files in this directory.

## Goal

Implement the next Cabinet Model Viewer milestone: detect specially named face target meshes in loaded `.glb` cabinet models and expose them to the editor so Face documents can later be assigned to them.

The naming convention is:

```text
OasisFace_<TargetName>
```

Examples:

```text
OasisFace_TopGlass
OasisFace_BottomGlass
OasisFace_ButtonPanel
```

## Current baseline

Recent work has added:

- Cabinet3D document/viewer support for `.glb` files.
- In-editor model rendering.
- Camera orbit/pan/zoom/reset.
- Grid/orientation helpers.
- Improved material rendering.

Do not replace this architecture. Extend it.

## Design direction

The `.glb` should contain optional Blender-authored target meshes. These are not Oasis metadata by themselves; they are named model geometry that Oasis can detect and use as target candidates.

Oasis metadata should remain separate from the `.glb`.

The first implementation should detect and display the targets. Do not implement full `.cabinet3d` persistence unless already necessary for existing document flow.

## Required functionality

### 1. Detect target nodes/meshes

Scan the loaded glTF scene for nodes/objects/meshes whose effective name starts with:

```text
OasisFace_
```

Use the source name as the stable source identity.

Create a UI display name from the suffix, for example:

```text
OasisFace_TopGlass -> Top Glass
OasisFace_BottomGlass -> Bottom Glass
```

Follow existing naming/style helpers if available.

### 2. Extract geometry

For each detected target, extract enough geometry to represent it as a cabinet face target.

Preferred first target shape:

- a flat quad object exported from Blender
- usually two triangles
- four unique corner points after applying node/world transforms

The extracted target model should include:

- stable id derived from the source name
- source name
- display name
- transformed corner positions
- center
- derived normal
- validity/error state if extraction failed

A suggested model shape:

```csharp
public sealed record CabinetFaceTarget(
    string Id,
    string SourceName,
    string DisplayName,
    IReadOnlyList<Point3D> Corners,
    Vector3D Normal,
    Point3D Center,
    bool IsValid,
    string? ErrorMessage);
```

Adapt this to existing conventions. Keep WPF types out of domain/core models if the existing architecture requires that.

### 3. Validate geometry safely

Treat valid targets as simple quads.

For the first version, a valid target should have four usable corner points. If the source object is not a simple quad, mark it invalid and show a clear message rather than crashing.

Do not try to implement complex polygon fitting in this task.

### 4. Preserve existing model rendering

Detected `OasisFace_` meshes may still appear in the normal rendered model depending on how the GLB is authored. For this task, do not overcomplicate hiding/removing them unless it is straightforward with the current loader.

The important first step is detection and display. Later work can add rules such as hiding target meshes from the normal render pass and rendering them as editor overlays.

### 5. Add editor UI visibility

Expose detected targets in the Cabinet Model Viewer UI.

Minimum acceptable UI:

- a small list/section showing detected targets
- target display name
- source name
- valid/invalid state
- warning when no targets are found

Optional but useful:

- target count in load status
- selection in list highlights an overlay in the viewport
- simple wireframe/tint overlay for valid targets
- normal arrow indicator

Keep the UI theme-compliant. Do not hard-code colors.

### 6. Prepare for Face document assignment

Do not implement the full assignment workflow unless it is trivial and well aligned with existing patterns.

However, structure the detected target data so a Face document can later choose one of the loaded cabinet targets.

The future assignment concept is:

```json
{
  "faceDocumentPath": "Faces/TopGlass.face",
  "targetId": "topGlass"
}
```

The target id should be stable across loads if the source name does not change.

### 7. Simple preview guidance

Do not implement full dynamic lamp rendering on the 3D cabinet in this task.

A later task should render the Face document's static/background image onto the selected target. Full lamp flashing in the 3D Editor viewport should be deferred until the Unity/runtime rendering path is more mature.

## Non-goals

Do not implement:

- full surface editing tools
- create/move/rotate/scale target quads in the editor
- full `.cabinet3d` persistence format unless already required
- dynamic lamp simulation on the 3D cabinet
- Unity runtime export
- PBR parity with Unity
- unrelated refactors

## Suggested files/classes

Use existing CabinetEditor patterns from recent commits. Possible additions:

```text
Features/CabinetEditor/Models/CabinetFaceTarget.cs
Features/CabinetEditor/Services/ICabinetFaceTargetDetector.cs
Features/CabinetEditor/Services/SharpGltfCabinetFaceTargetDetector.cs
Features/CabinetEditor/ViewModels/CabinetFaceTargetViewModel.cs
```

If the current model loader already walks the SharpGLTF scene, consider returning detected targets as part of the existing load result, but keep responsibilities clear.

Possible expanded load result:

```csharp
public sealed class CabinetModelLoadResult
{
    public Model3DGroup? Model { get; }
    public Rect3D Bounds { get; }
    public IReadOnlyList<CabinetFaceTarget> FaceTargets { get; }
}
```

Adapt to the current codebase.

## Geometry extraction notes

Important details:

- Apply glTF node/world transforms before storing target corners.
- Preserve the same coordinate convention as the rendered cabinet model.
- Avoid assuming the model is centered at origin.
- Avoid assuming the target mesh has a specific material.
- Use source object/node names, not material names.
- If both node and mesh names exist, prefer the node/object name because Blender object names are likely to export there.

## Testing checklist for John

After implementation, John should test locally:

- Build the solution.
- Launch the editor.
- Open a project.
- Open a `.glb` with no `OasisFace_` objects; viewer should still work and show no targets found.
- Open a `.glb` with `OasisFace_TopGlass` and `OasisFace_BottomGlass`; viewer should detect both.
- Confirm target names display clearly.
- Confirm invalid/non-quad target geometry does not crash the viewer.
- Confirm existing textured/material model rendering still works.
- Confirm orbit/pan/zoom/reset still work.
- Confirm existing Panel2D editing remains unaffected.

## Codex environment constraint

Do not run builds or tests in Codex. The environment does not have the required Windows/.NET/WPF toolchain. Summarize changes and provide the local test checklist instead.
