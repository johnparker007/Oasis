# Task: Refactor Face/Cabinet ownership and reel physical configuration

## Context

We are working in `WindowsNetProjects/OasisEditor` in the connected `johnparker007/Oasis` repository.

Oasis Editor and Oasis Player are still an early personal project. There are no external users, no released runtime package format, no stable public schema, and no backwards-compatibility requirement.

When serialized formats change:

- update Editor writer and Player reader together;
- increment the current schema version when the serialized shape changes;
- support only the latest schema/version;
- update fixtures/tests to the latest format;
- allow old generated data to stop loading;
- regenerate old runtime builds with the current Editor;
- delete superseded format code;
- do not add migration layers, legacy DTOs, fallback parsers, dual formats, compatibility branches, or tests whose sole purpose is loading obsolete formats.

This task is a data-ownership cleanup discovered while fixing machine builds/Preview in Oasis Player.

## Current problems

### 1. Machine build scans every Face asset in the project

`MachineRuntimeBuildService.ExportReferencedFaces()` currently enumerates every Face manifest under the project, parses/validates each one, and only then checks `FaceDocumentModel.AssignedCabinetFaceTargetId` to decide whether to export it.

This causes an unrelated stale/broken Face asset to fail `Build Oasis Player Machine` / `Preview in Oasis Player`, even if that Face has nothing to do with the selected Cabinet.

The machine build should instead be rooted in the selected Cabinet and follow only explicitly referenced Face assets.

### 2. Face-to-Cabinet mounting ownership is backwards

The Cabinet GLB contains specially named Face target nodes/meshes. `GlbCabinetFaceTargetDetector` currently uses the prefix:

```text
OasisFace_
```

It detects the matching node/mesh, creates a stable target ID, and derives the target quad/normal/UV mapping.

Despite this being Cabinet structure, Face documents currently persist:

- `AssignedCabinetFaceTargetId`
- `AssignedCabinetAssetPath`

and the Face Inspector looks through open Cabinets to choose a Cabinet Face target.

This should be inverted: the Cabinet should own the mapping from a detected `OasisFace_*` target to a Face asset.

### 3. Reel physical-spec ownership has the same problem

The Cabinet already owns reusable physical reel specifications:

```text
CabinetReelSpecification
    Id
    Name
    DiameterMm
    WidthMm
```

For example:

```text
Small Reel    - 230 mm diameter, 70 mm width
Standard Reel - 290 mm diameter, 70 mm width
```

However each `FaceReelDisplayElement` currently persists `ReelSpecificationId`. The Face Inspector resolves its assigned Cabinet, offers that Cabinet's reel specifications in a dropdown, and runtime export uses the Face-owned specification ID to resolve physical width/radius from the Cabinet.

That makes a Face dependent on a specific Cabinet.

The Face should retain its logical machine reference, e.g. `MachineObjectReference.Reel(0)`, because that describes what machine device the Face element represents. But the physical reel mechanism used for logical Reel 0 belongs to the Cabinet/machine composition.

## Desired ownership model

Treat the selected Cabinet as the current machine-composition root.

Conceptually:

```text
Cabinet3D
  GLB model
    OasisFace_TopGlass
    OasisFace_BottomGlass

  Face assignments
    TopGlass    -> Assets/Faces/Top Glass/asset.face
    BottomGlass -> Assets/Faces/Bottom Glass/asset.face

  Reel specifications
    standard -> 290 mm x 70 mm
    small    -> 230 mm x 70 mm

  Reel assignments
    Reel:0 -> standard
    Reel:1 -> standard
    Reel:2 -> standard
    Reel:3 -> small
```

A Face should instead contain only Face-authored/semantic state, for example:

```text
Face "Bottom Glass"
  artwork
  lamps
  buttons
  reel displays
    Reel display A -> LinkedMachineObjectReference = Reel:0
    Reel display B -> LinkedMachineObjectReference = Reel:1
```

The Face must not know which Cabinet it is mounted in or which physical reel specification a Cabinet uses.

## Required model changes

Design the smallest clean latest-version model, but the intended shape is approximately:

```csharp
CabinetDocument
{
    Model
    FaceAssignments[]
    ReelSpecifications[]
    ReelAssignments[]
    TargetOverrides[]
    Reflections[]
    Preview
}
```

Add a Cabinet-owned Face assignment type, broadly:

```csharp
CabinetFaceAssignment
{
    TargetId
    FaceAssetPath // or the existing project asset reference convention if another representation is cleaner
}
```

The `TargetId` must reference a detected `OasisFace_*` Cabinet target.

Add a Cabinet-owned reel assignment type, broadly:

```csharp
CabinetReelAssignment
{
    MachineReelReference // logical Reel:N identity, using existing MachineObjectReference conventions where practical
    ReelSpecificationId
}
```

Do not introduce a new general Machine asset in this task. The Cabinet is the current composition root.

Remove from the current Face document/schema:

- `AssignedCabinetFaceTargetId`
- `AssignedCabinetAssetPath`

Remove from `FaceReelDisplayElement` / Face serialized reel data:

- `ReelSpecificationId`

Retain `LinkedMachineObjectReference` on Face elements, including reel displays.

Update the current Face and Cabinet schema versions as needed. Support only the new format.

## Cabinet Face assignment UX

Move Face mounting UI out of the Face Inspector and into Cabinet editing.

The Cabinet already discovers `OasisFace_*` targets from the GLB. For each valid detected Face target, provide a clear Face asset assignment control.

The intended authoring experience is approximately:

```text
Face Targets

Top Glass
    Target: OasisFace_TopGlass
    Face:   [ Top Glass Face ▼ ]

Bottom Glass
    Target: OasisFace_BottomGlass
    Face:   [ Bottom Glass Face ▼ ]
```

Use the existing Assets/project-reference conventions to enumerate valid Face assets. Do not require those Face documents to be open.

Preserve existing target-specific settings such as front side, Face rotation, and horizontal flip. Do not conflate Face assignment with `CabinetTargetOverride`; create an explicit assignment model rather than abusing the override collection.

Invalid/unresolved Face assignments should produce useful Cabinet/build diagnostics, but unrelated Face assets must never be opened or validated as part of building this Cabinet.

Remove the Face Inspector's current `Cabinet Assignment` section and the associated mutation/resolution paths that exist only to persist Cabinet ownership on the Face.

## Reel assignment UX

Keep Cabinet `ReelSpecifications` as reusable physical reel types.

Add a separate Cabinet section for assigning those physical types to logical machine reels, approximately:

```text
Reel Specifications

Standard Reel
    Diameter: 290 mm
    Width:     70 mm

Small Reel
    Diameter: 230 mm
    Width:     70 mm

Machine Reels

Reel 0    [ Standard Reel ▼ ]
Reel 1    [ Standard Reel ▼ ]
Reel 2    [ Standard Reel ▼ ]
Reel 3    [ Small Reel    ▼ ]
```

Use the existing logical reel identity (`MachineObjectReference.Reel(n)`) rather than inventing a second reel numbering system.

Investigate the existing project/platform reel definitions/import data and choose the cleanest source for which logical reels should appear in this Cabinet UI. Prefer current machine/platform knowledge already present in the project over deriving the list by scanning every Face asset.

If the imported/current machine configuration already exposes the reel IDs, use that. If the cleanest current implementation needs Cabinet reel-assignment rows to be created lazily when a known logical reel is encountered through existing import/composition paths, keep the implementation small and deterministic. Do not introduce speculative generic hardware architecture.

`DefaultReelSpecificationId` may remain as an authoring convenience, but its role must change: it should default/fill Cabinet reel assignments, not be copied into Face reel elements during Face generation.

For example, if the default is `standard`, newly established Cabinet mappings can initially be:

```text
Reel:0 -> standard
Reel:1 -> standard
Reel:2 -> standard
Reel:3 -> standard
```

and the artist can change Reel 3 to `small`.

## Face reel Inspector after the refactor

A Face reel must no longer have a Cabinet reel-specification dropdown.

It should continue to expose the logical machine reference and Face-authored visual/runtime properties such as reel band, stops, visible scale, band offset, reversed state, reel lamps, etc.

If the Editor happens to be displaying the Face within a Cabinet context, it is acceptable to show resolved physical reel information as read-only contextual information, e.g.:

```text
Machine Reel: Reel:3
Cabinet Physical Reel: Small Reel — 230 mm x 70 mm
```

but do not persist that Cabinet-specific result back into the Face.

Do not make standalone Face authoring depend on a Cabinet merely to edit/build Face-owned generated content.

## Runtime/build pipeline

Rewrite `MachineRuntimeBuildService` dependency discovery so a build starts from the selected Cabinet:

1. Read the selected Cabinet.
2. Read its explicit Face assignments.
3. Resolve and validate only those Face assets.
4. Export only those Face assets.
5. Resolve each Face reel's `LinkedMachineObjectReference` against the Cabinet's reel assignments.
6. Resolve the assignment's `ReelSpecificationId` against the Cabinet's `ReelSpecifications`.
7. Produce runtime Face/machine data with the resolved physical width/radius required by Oasis Player.

Delete the current project-wide Face enumeration/discovery approach from `ExportReferencedFaces()`.

A malformed Face that is not referenced by the selected Cabinet must have zero effect on Build/Preview.

A malformed Face that *is* referenced by the Cabinet should fail with a diagnostic that identifies:

- Cabinet asset;
- Face target;
- referenced Face asset;
- actual Face read/validation error.

A missing/invalid reel mapping should similarly identify:

- logical reel reference;
- Face/reel element that requested it;
- Cabinet asset;
- missing/invalid Cabinet reel assignment or reel specification.

## Face runtime export/context cleanup

Review `FaceCabinetContext`, `FaceCabinetContextResolver`, `FaceRuntimeExportService`, Face validation, generation and regeneration.

Today these paths resolve Cabinet state largely because the Face persists Cabinet assignment and `ReelSpecificationId`.

After the ownership change:

- standalone Face authoring/build should not require an assigned Cabinet merely to process Face-owned artwork/masks/runtime textures;
- machine export should pass the selected Cabinet/composition context explicitly when it needs physical reel dimensions;
- remove resolver/mutation/helper code that becomes obsolete;
- do not leave compatibility shims for the old Face-owned assignment fields.

If there is still a useful distinction between standalone Face runtime output and machine-resolved runtime output, make that distinction explicit and small rather than reintroducing a hidden Face -> Cabinet dependency.

## Reflections

Cabinet reflections already refer to source Faces by Face ID.

Update reflection editing/validation to work with the Cabinet's explicit mounted/referenced Face set. Reflection sources must resolve against Faces referenced by that Cabinet, not against all project Faces discovered by scanning.

Preserve current reflection behavior otherwise.

## Import/generation/regeneration

Review the MFME/FML import and Face generation paths.

Currently Face generation can receive a Cabinet default reel specification and copy it into generated `FaceReelDisplayElement.ReelSpecificationId`. Remove that behavior.

Generated Face reels should retain their logical machine references from the source Panel2D elements but contain no Cabinet physical reel-spec reference.

Where existing import creates/knows the Cabinet and logical reels, establish Cabinet reel assignments there using `DefaultReelSpecificationId` as appropriate.

Where existing import establishes Face/Cabinet target relationships, write the new Cabinet Face assignments rather than fields on the Face.

Only implement current-format behavior. Old Face/Cabinet assets may stop loading.

## Oasis Player/runtime schema

Review the generated runtime schemas and Oasis Player reader together.

The Editor may resolve Cabinet reel assignments to concrete physical dimensions before emitting Face runtime data, if that remains the smallest runtime contract. Alternatively, if a cleaner current runtime model is to emit the reel assignment separately and resolve it in the Player, update both sides together.

Prefer the smallest clean runtime representation. Do not preserve obsolete runtime shapes for compatibility.

Increment runtime schema versions when serialized shapes change.

## Tests

Update/add focused tests for at least:

1. Cabinet document storage round-trips explicit Face assignments.
2. Cabinet document storage round-trips logical reel -> reel-spec assignments.
3. Face storage no longer contains Cabinet assignment fields.
4. Face reel storage no longer contains `ReelSpecificationId`.
5. Face generation/regeneration preserves logical machine reel references without adding Cabinet physical-spec references.
6. Cabinet UI/mutation commands assign/unassign a Face asset to a detected `OasisFace_*` target with normal undo/redo behavior.
7. Cabinet reel assignment editing supports assigning a reel specification to a logical reel and normal undo/redo behavior.
8. Machine build exports only Faces explicitly referenced by the selected Cabinet.
9. An invalid/unreadable unrelated Face elsewhere under `Assets/Faces` does not affect the selected Cabinet build.
10. A referenced invalid Face fails with a targeted diagnostic.
11. A Face reel resolves physical width/radius via `LinkedMachineObjectReference -> CabinetReelAssignment -> CabinetReelSpecification`.
12. Missing logical reel assignment fails clearly.
13. Missing referenced reel specification fails clearly.
14. Reflections resolve only against the Cabinet's referenced Faces.
15. Oasis Player/runtime reader tests are updated for any runtime schema change.

Delete/update tests that exist solely for the obsolete Face-owned Cabinet/reel-spec relationship.

## Manual verification

After automated tests pass, manually verify:

1. Open a Cabinet whose GLB contains `OasisFace_*` target meshes/nodes.
2. Confirm the Cabinet UI lists those detected Face targets.
3. Assign Face assets to the intended targets from the Cabinet UI.
4. Confirm the Face documents themselves no longer contain/edit Cabinet assignment state.
5. Configure at least two physical reel specifications on the Cabinet, e.g. Standard and Small.
6. Assign different logical machine reels to those specifications.
7. Open the mounted Face and confirm its reel elements contain logical machine reel references but no Cabinet reel-spec dropdown/state.
8. Build/Preview in Oasis Player and confirm reel physical dimensions/rendering still behave correctly.
9. Place an intentionally stale/invalid unused Face elsewhere in the project and confirm Build/Preview still succeeds.
10. Break a Face actually assigned to the Cabinet and confirm Build/Preview fails with a precise reference diagnostic.
11. Check reflections still work for mounted Faces.
12. Check undo/redo and save/reopen behavior for the new Cabinet assignments.

## Implementation constraints

- Do not add backwards compatibility.
- Do not create migration infrastructure.
- Do not retain old Face assignment fields as optional fallbacks.
- Do not retain `FaceReelDisplayElement.ReelSpecificationId` as a fallback.
- Do not keep project-wide Face scans for compatibility.
- Do not introduce a new general Machine asset in this task.
- Prefer explicit references and one-way dependency flow from Cabinet -> Face.
- Reuse existing project asset path/reference conventions and `MachineObjectReference` conventions where practical.
- Keep the implementation focused on the current Editor/Player architecture.

## Expected result

After this work:

- a Cabinet explicitly owns which Face asset is mounted on each `OasisFace_*` target;
- a Cabinet explicitly owns which physical reel specification is used by each logical machine reel;
- Faces retain visual/semantic machine references but no Cabinet ownership/configuration;
- Build/Preview follows only the selected Cabinet's dependency graph;
- unrelated broken Face assets cannot break a machine build;
- reel physical dimensions are resolved during machine composition/build rather than being authored into the Face;
- obsolete ownership/resolver/schema code is removed rather than preserved.
