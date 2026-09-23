# Architecture Next Phase 2 implementation

## Result and ownership audit

Cabinet schema 8 is the reusable physical Cabinet definition. The audit of every authored `CabinetDocument` property is:

| Property | Classification | Phase 2 result |
|---|---|---|
| `Version` | intrinsic format metadata | Retained; advanced from 7 to 8 because the serialized shape changed. |
| `Model.Path` | intrinsic reusable Cabinet state | Retained as the Cabinet-local GLB reference. |
| `Model.Scale` | intrinsic reusable Cabinet state | Retained as physical model interpretation. |
| `Model.UpAxis` | intrinsic reusable Cabinet state | Retained as physical model interpretation. |
| `SurfaceTargetSettings` (formerly `TargetOverrides`) | intrinsic reusable Cabinet state | Renamed to state its permanent purpose. It is sparse orientation/default configuration, not a target registry and never identifies an installed Face. |
| `Preview` | editor/view state | Deleted from authored schema. Target overlays/background toggles had no active authored consumer; lamp preview mode is now transient viewer state and cannot dirty the Cabinet. |
| `ReelSpecifications` | temporary Phase-1 reel bridge | Retained only as Cabinet-family physical definitions until Phase 3. |
| `DefaultReelSpecificationId` | temporary Phase-1 reel bridge | Retained to support the current Machine logical-reel resolution flow. |
| `Reflections` | intrinsic reusable Cabinet state | Retained: receiver renderer/material slot, Cabinet-local settings/mask, and source surface-target IDs/planes. |

There are no Cabinet fields for installed Faces, logical reel assignments, a Machine identity/path, platform/runtime configuration, ROMs, or inputs. Those game-specific facts remain on Machine.

## Final schema and surface targets

Schema 8 serializes `model`, sparse `surfaceTargetSettings`, temporary `reelSpecifications`/`defaultReelSpecificationId`, and `reflections`. Readers accept schema 8 only and reject unknown/superseded properties; there is no schema-7 compatibility reader.

Valid surface-target existence and identity comes exclusively from `OasisFace_*` GLB discovery. `CabinetSurfaceTargetSettings` optionally supplies `frontSide`, rotation, and horizontal-flip defaults for a detected target. A detected target without settings receives normal/0/not-flipped defaults. Settings that name something absent from the GLB do not create a target and cannot enter the reflection source catalog.

The Cabinet does not receive a new GUID. Current Machine references already use the explicit project-relative Cabinet package path, consistent with the current asset resolver. Adding a second unused identity would not improve present reference semantics; stable global/library identity remains deferred with Library work.

## Editor-only preview and UI

Lamp preview mode is held by `CabinetModelDocumentViewModel`, defaults to Live each time the viewer is created, and is neither serialized nor routed through document commands. Changing it therefore does not dirty the Cabinet. Camera, selection, contextual mounted-Face imagery, and the active Machine are likewise viewer state.

The Cabinet view describes detected **Surface Targets**, their **Target Orientation Defaults**, reflection receivers, and the optional `Previewing Machine` context. Inspector reel groups explicitly call the embedded definitions **Temporary Physical Reel Specifications**. Machine context remains read-only: it selects mounted Face preview sources but provides no Cabinet-side assignment mutation.

Intrinsic authoring does not require a Machine. Model loading performs GLB target and reflection-receiver discovery directly. Target settings, reflection editing (including automatic planes from detected geometry), temporary physical reel editing, and Cabinet save/reopen all operate on the Cabinet document alone. A missing, unrelated, switched, or cleared Machine context only clears/replaces the mounted-Face preview and does not touch dirty state or authored content.

## Reflections

A reflection belongs to the Cabinet because it describes reusable receiver geometry/material slots, Cabinet-local render settings, an optional Cabinet-local visibility mask, and planes sourced by stable Cabinet surface-target IDs. Reflection source choices now come from the currently detected GLB targets, not sparse settings and not project Face assets. The obsolete Faces-directory watcher was removed. Machine build still resolves each source target through that Machine's surface assignment before producing the existing Player runtime contract; rendering behavior is unchanged.

If the GLB becomes unavailable or definitively fails to reload, the viewer clears transient receiver/surface discovery while preserving authored reflection definitions; persisted source IDs remain visible only as explicitly missing choices until valid geometry is loaded again.

## Reel bridge and material roles

`CabinetReelSpecification` remains deliberately temporary and contains physical Cabinet-family definitions only. Machine continues to own logical Reel-to-specification assignments, and build continues to flatten the resolved dimensions. Phase 3 will replace this bridge with first-class Reel assets; Phase 2 does not expand it.

Material roles are deferred. GLB loading exposes renderer/material slots for the concrete reflection receiver workflow, but there is no existing general Cabinet appearance editor or Machine appearance-override consumer. Adding semantic `Body`/`SideTrim`/`TopTrim`/`CoinDoor` mappings now would create unused schema and prematurely begin a later appearance system.

## Deviations and checkpoint

The target documents allow reusable preview/render defaults on Cabinet when they matter to all consumers. Repository evidence showed that `CabinetPreviewSettings` only controlled the current Editor tab, so it was removed rather than retained as an authored default. No Cabinet GUID was added for the reasons above.

From the implementation perspective Architecture Checkpoint A is satisfied: Machine contains all game-specific runtime/composition state needed for its build, while Cabinet contains reusable physical state, the explicitly temporary physical-reel bridge, and optional non-authored Machine preview context. Manual UI/Player validation below is still required on the supported Windows toolchain.

## Manual verification checklist

1. Create/open a Cabinet with no Machine active.
2. Import/load its GLB.
3. Confirm valid `OasisFace_*` surface targets are detected.
4. Edit target front side, rotation, and horizontal-flip defaults.
5. Add/edit reflection receivers and source-target planes.
6. Add/edit temporary physical `ReelSpecifications` and their default.
7. Save/reopen the Cabinet and verify all intrinsic state; verify lamp preview choice resets and is absent from `asset.cabinet3d`.
8. Open a Machine that references the Cabinet.
9. Verify mapped Faces appear in Cabinet preview and the contextual Machine name is shown.
10. Switch to a different Machine, including one using another Cabinet, then clear the active Machine.
11. Verify only mounted-Face context changes and the Cabinet remains clean.
12. Change a Machine Face assignment and verify the Cabinet preview follows it without the Cabinet becoming dirty.
13. Build and Preview the Machine in Oasis Player.
14. Verify Face orientation, reflections, and resolved temporary reels render correctly.
