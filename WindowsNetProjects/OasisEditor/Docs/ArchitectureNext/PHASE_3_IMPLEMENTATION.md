# Architecture Next Phase 3 implementation

## Result

Phase 3 introduces the first reusable typed physical device without introducing a generic Device framework. A Reel is a package asset at `Assets/Reels/<Name>/asset.reel`. Machine uses direct project-relative Reel references (Option A); no profile layer was justified by the current workflow because Faces already provide stable logical `Reel:n` identities and the existing Machine UI explicitly assigned each logical reel.

## Current reel-flow audit and ownership

Before this change MFME imported Panel2D reel presentation and logical reel identity; Face generation retained the reel rectangle/aperture, crop/artwork/band data and `MachineObjectReference.Reel(n)`. Machine mapped that identity to a Cabinet specification ID. Cabinet schema 8 embedded the specification name, diameter and width plus a default ID. Machine build supplied the Cabinet and assignments to Face export, which flattened width and radius into the Face runtime manifest. Player consumed only those flattened values. Inspector edited the embedded specifications and Machine displayed a temporary specification selector.

The final classification is:

- **Face-owned placement/presentation:** x/y/width/height, aperture and visible crop, artwork/band association, reel lamps, opacity and the logical `Reel:n` reference.
- **Reel-owned physical definition:** stable ID, display name, physical diameter and band width.
- **Machine-owned composition:** the project-relative mapping from each required logical reel to an authored Reel package.
- **Platform/runtime behavior:** reversal, Epoch normalization, stop offsets and Amber/Fabric translation remain in their existing runtime adapters and platform services. No such field was added to Reel.

MFME import continues to create logical reel identities/layout only. It does not guess or seed physical Reel asset assignments.

## Authored schemas

### Reel version 1

`asset.reel` contains `version`, GUID `id`, `displayName`, `diameterMm`, and `widthMm`. The reader is strict/latest-only; the name is required and both dimensions must be positive finite millimetre values. New Reels use neutral valid dimensions solely so the editor can create a valid document immediately.

### Machine schema 2

`MachineReelAssignment` now contains `machineReelReference` and normalized project-relative `reelAssetPath`. Cabinet specification IDs and duplicated dimensions are absent. Multiple logical reels can reference the same Reel asset.

### Cabinet schema 9

`reelSpecifications` and `defaultReelSpecificationId` were deleted. The strict schema-9 reader rejects schema 8 and unknown legacy properties. Cabinet continues to contain only its model, intrinsic surface-target settings and reflections.

Face schema did not change and contains no Cabinet, Machine or final Reel-asset reference.

## Editor and build behavior

The Assets/document infrastructure recognizes `asset.reel`, provides `File -> New -> Reel`, package-aware open/save and a dedicated editor for display name, diameter and width. Saving creates `Assets/Reels/<Name>/asset.reel`, and new projects pre-create the `Assets/Reels` package root consistently with the other authored asset roots. Changes use document commands and participate in dirty/save/undo/redo. Reel values are also exposed in Inspector.

Machine reel choices are valid Reel manifests discovered from the current project's `Assets/Reels` catalog and selected by stable project-relative path. Missing authored selections remain visible. Catalog refresh reconciles choices without changing the Machine. Rows are the union of authored mappings and logical reel references found by traversing only Faces explicitly assigned to the Machine; unrelated project Faces are never scanned.

During Machine build each assigned Face supplies its required logical reels. Only those references are followed. Every reference must have exactly one assignment; the selected package must exist at the canonical path and parse as a valid version-1 Reel. Diagnostics retain Machine, logical reel and Reel path context. Invalid unused Reel packages and unused assignments are not traversed. Face runtime export continues to flatten Reel width and radius, so Player's runtime schema/rendering contract is unchanged and Cabinet runtime has no reel definitions.

Reel resolution crosses the authoring/runtime boundary through `FaceRuntimeCompositionContext`, which contains only the Machine's reel assignments and the Reel documents resolved for the current Face. The dependency is therefore `Face logical reference -> Machine composition context -> Reel asset`; Cabinet is not present in the Reel resolution API. Standalone Face export supplies no composition context and deliberately leaves physical width/radius unresolved.

## Removed bridge

`CabinetReelSpecification`, Cabinet specification/default fields, Cabinet Inspector actions, Cabinet mutation commands, temporary Machine selector terminology and Cabinet-based dimension resolution were removed rather than deprecated. There is no migration, fallback or dual-format reader.

## Deferred work

Motor models, lamps as reusable assemblies, stops, 3D mechanism GLBs, inertia, default symbol strips, profiles, generic Device/mount infrastructure and external libraries remain deferred. Phase 4 has not begun.

## Manual verification checklist

1. Create/open a Cabinet and verify reel-specification UI is absent.
2. Create `JPM Standard Reel.reel`; set diameter 290 mm and width 70 mm.
3. Create `JPM Small Reel.reel`; set diameter 230 mm and width 70 mm.
4. Open a Machine whose assigned Faces contain `Reel:0` through `Reel:3`.
5. Verify those rows are discovered from assigned Faces only.
6. Assign Standard to reels 0/1/2 and Small to reel 3.
7. Save/reopen the Machine and verify all assignments persist.
8. Open the Face and verify placement is unchanged and no final Reel asset is stored there.
9. Build the Machine and preview it in Oasis Player.
10. Verify reel geometry/scale and existing platform-specific direction, normalization and offsets are unchanged.
11. Add an unused malformed Reel package and verify the Machine still builds.
12. Delete a referenced Reel package and verify the build diagnostic names the Machine, logical reel and missing path.
