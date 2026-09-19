# Architecture Next: Current-State Audit

> **Phase 0 only.** This document records the repository state inspected on 2026-09-19. It does not define a compatibility plan and does not make the current transitional ownership authoritative. The Architecture Next documents are the target. Per repository policy, later phases should change writer and reader together, update fixtures, support latest only, and delete superseded code.

## A. Executive summary

### Project

`EditorProject` is both the workspace record and, unintentionally, part of the current machine model. The `.oasisproj` schema is version 8. Workspace paths (`Assets`, `Machines`, `Generated`) sit beside the selected `FruitMachinePlatform`, every supported platform's ROM/hardware settings (System 6/Impact, MPU5, Epoch, MPU3, Maygay M1, and Scorpion 4), and project-wide input definitions. `ProjectScaffolder` creates all three roots plus typed asset folders and `Generated/Build`/`Generated/Preview`; the build service actually writes `Generated/Builds/<cabinet-name>`, so even the scaffolded output naming is not the build service's current convention.

Project Settings is implemented through `MainWindowViewModel`, `ProjectSettingsWindow`/`Views/ProjectSettingsView`, and platform-specific settings view models. Changes are written directly back into `project_settings`. The selected platform is also copied into every open document's transient `MachineRuntimeState`; Editor emulation, reel normalization, Play View input, displays and lamps consume that transient state while native launch requests take the platform-specific settings from the loaded project. Consequently two independently configured playable machines cannot currently coexist in one project.

### Machine

There is no authoring Machine model, storage service, schema, inspector, specialized view, or package convention. `EditorDocumentType.Machine`, `.machine` recognition/save filtering, `CreateMachineStub`, a `Machines/` directory and generic tab/type-label support are dormant shell scaffolding. A `.machine` file is opened as arbitrary text and displayed as the generic summary; save writes the tab summary rather than a machine composition. No runtime build depends on it.

The word “Machine” is nevertheless used for two real but different concepts:

* transient Editor runtime state and logical `MachineObjectReference` identities (`Lamp:n`, `Reel:n`, displays and inputs); and
* generated `machine.runtime.json`, which is synthesized from a Cabinet and project name and is not authored from a `.machine` asset.

### Cabinet

Cabinet authoring is real and mature, but schema 6 mixes reusable Cabinet state with the temporary game composition root:

* **intrinsically reusable Cabinet state:** GLB path/scale/up-axis, detected `OasisFace_*` target geometry (derived from the GLB), per-target front-side/rotation/flip defaults/overrides, reflection receiver definitions/material slots/settings/masks, and arguably compatible/default profile hints once redesigned;
* **transitional Machine-specific composition:** `FaceAssignments`, `ReelAssignments`, embedded `ReelSpecifications`, `DefaultReelSpecificationId`, and reflections' concrete source Face IDs where those select the installed game's Faces;
* **runtime/editor-only:** `Preview` overlay/background/lamp-preview settings; these drive Cabinet editor visualization and are not exported to Player;
* **obsolete/suspicious:** embedded physical reel definitions as a long-term representation, the requirement for a target override merely because a Face is assigned when any override exists, and reflection sources bound directly to Face IDs inside a reusable Cabinet.

The recent Cabinet/Face ownership refactor correctly inverted Face mounting and reel physical resolution away from Face. Cabinet now explicitly references only mounted Faces, build traversal no longer scans all Face assets, and unrelated invalid Faces do not break a build. This is a good one-way dependency shape but its root is intentionally temporary.

### Face

Face schema 23 is a game-specific surface asset. It owns Panel2D provenance/source shape and source region; an artwork recipe and derived/generated paths; processing/build state; mask/tray/lamp-emitter data; semantic artwork, lamp, reel, seven-segment, alpha-display and button elements; Face placement; and logical machine references. Reels contain visual placement, band asset, stops, visible scale, band offset, reversal, reel-lamp data and transmission mask—not physical reel specification IDs.

The old persisted Face-to-Cabinet fields (`AssignedCabinetFaceTargetId`, `AssignedCabinetAssetPath`) and Face-owned `ReelSpecificationId` are absent from the current Face model and storage DTOs. They are absent from persisted schemas, but stale transient generation names remain: `FaceGenerationWorkItem.AssignedCabinetAssetPath` and `GenerateFromPanelFaceSourceShape` parameters `assignedCabinetFaceTargetId`, `assignedCabinetAssetPath`, and `cabinetDocument`. Those arguments are no longer consumed (only the Cabinet-derived target aspect ratio still affects output sizing). The remaining `FaceCabinetContext` API is deliberately used when machine export must resolve physical reel dimensions, but its naming and `FaceCabinetContextResolver.ResolveForGeneration(selectedCabinetFaceTargetId)`/dead resolution helpers still encode the transitional Cabinet-rooted mental model. Standalone Face build validates and generates Face-owned content without a Cabinet; a runtime manifest containing reels requires composition context to emit physical dimensions.

### Panel2D and MFME/FML import

The actual chain is:

```text
MFME v20.1 .fml
  -> decoder Layout + extracted images (temporary diagnostic staging)
  -> FmlToOasisMapper / LayoutImportAssetCopier
  -> Panel2D elements + copied project-relative assets + project InputDefinitions
  -> user-authored Panel Face Source Shapes
  -> FaceGenerationService / FaceSemanticElementConversionService
  -> persisted Face provenance + semantic elements
```

FML import mutates the active Panel2D through an undoable command; it does not create or select a Cabinet or Machine. Per-element FML provenance (`format`, optional reference, source component/element indices and shared-source metadata) is persisted in Panel2D. Panel2D does **not** persist a document-level source FML path. Panel2D-to-Face is persisted in the Face (`SourcePanel2DDocumentId`, project-relative path, source-shape ID/region, artwork source); Panel2D itself does not point forward to Faces. Logical references are inferred deterministically during Face conversion from Panel element kind plus `DisplayNumber`, or from project `InputDefinitions` for buttons.

The decoder can read the MFME version and rich component/layout content, but the current import result exposes elements, assets, input definitions and diagnostics—not fruit-machine platform, ROM set, game identity or emulator configuration. There is therefore no existing import-derived runtime definition ready to move; Phase 1 must move current project settings first and may add import population only if actual decoded fields are proven.

### Runtime/build and Player

“Build Oasis Player Machine” and “Preview in Oasis Player” are enabled only for a saved Cabinet3D tab. `MachineRuntimeBuildService.BuildFromCabinetDocument` treats that Cabinet as root, follows its explicit Face assignments, resolves logical Face reels through Cabinet reel assignments/specifications, exports resolved Face packages, copies Cabinet/Face dependencies into a staging directory, validates reflections against the mounted Face set, writes runtime manifests, then atomically replaces the final build directory. Preview invokes the same build and starts Player with that build root.

The generated root manifest is named and identified with `project.Name`, while its output folder is named after the Cabinet. It contains no platform, ROM, emulation/runtime-definition or input configuration. Oasis Player loads machine schema 3, Cabinet schema 4 and Face runtime schema 9, then loads a single Cabinet GLB, mounted Faces, reflections and reels. Player is currently a visual runtime package consumer; it does not launch the Editor's native emulation configuration. This is a major distinction: “Preview in Oasis Player” is not the same pipeline as Editor Play View/native emulation.

### Largest ownership mismatches with Architecture Next

1. Project owns all platform/ROM/hardware/input configuration, so Project is an implicit single fruit machine.
2. Cabinet owns installed Face mappings, installed reel mappings and embedded physical reel definitions, so reusable Cabinet is the current composition/build root.
3. No authored Machine exists; generated “machine” identity is synthesized from Project/Cabinet.
4. Cabinet reflections identify installed Face IDs, coupling reusable reflection configuration to a game composition.
5. Runtime output omits runtime/emulation configuration entirely and therefore does not yet represent a complete independently playable Machine.
6. Cross-asset references are project-relative paths with assumptions about one project root, which constrains later Library reuse.

## B. Current authoritative ownership table

| Datum | Current owner | Current serialized location | Current editor/UI | Runtime consumer | Architecture Next target owner |
| --- | --- | --- | --- | --- | --- |
| Workspace identity/layout | Project | `.oasisproj` v8: `name`, `layout.assets/machines/generated` | New/Open Project, Project Overview | Path/build services | Project |
| Fruit-machine platform | Project | `.oasisproj/project_settings/FruitMachine_Platform` | Project Settings platform selector | Editor emulation factory, Play View adapters; copied to document `MachineRuntimeState`; **not Player package** | Machine `RuntimeDefinition` |
| System 6/Impact ROM and hardware config | Project | `System6NativeRoms` (program/sound ROMs, flash/percent, reel optos, coins/EDC) | Project Settings ROMS/Reels/Coins/options | Fabric Amber launch/configuration | Machine emulation runtime definition |
| MPU5 ROM and hardware config | Project | `Mpu5NativeRoms` (ROMs, reels, coins, DIPs/stake/prize/percentage/characteriser/PIC/SEC/hopper/jumpers) | MPU5 Project Settings | Fabric Amber launch/configuration | Machine emulation runtime definition |
| Epoch ROM and hardware config | Project | `EpochNativeRoms` (ROMs/flash/reels/coins/options) | Epoch Project Settings | Fabric Amber launch/configuration | Machine emulation runtime definition |
| MPU3/M1/Scorpion 4 runtime config | Project | `Mpu3Settings`, `M1Settings`, `Scorpion4Settings` | Platform settings view models/tabs | Fabric Amber launch/configuration | Machine emulation runtime definition |
| Logical inputs | Project | `.oasisproj/input_definitions` | import plus Project/Inspector-related input editing/routing | Editor input dispatcher and Face button links; absent from Player build | Machine/runtime definition (exact split to confirm) |
| Cabinet GLB reference | Cabinet | `Assets/Cabinet3D/<name>/asset.cabinet3d` v6: `model.path/scale/upAxis` | Cabinet document/model viewer | build copies `cabinet.glb`; Player GLB loader | Cabinet |
| Face target geometry | Cabinet GLB, derived | not duplicated in Cabinet JSON; `OasisFace_*` node/mesh discovery, with authored `targetOverrides` keyed by stable target ID | Cabinet viewport/target rows | Player finds target transforms by prefix; machine Face references carry target ID | Cabinet |
| Target orientation/front-side/rotation/flip | Cabinet | Cabinet `targetOverrides[]` | Cabinet target UI, undoable mutations | flattened into machine runtime Face reference | Cabinet default/intrinsic orientation, with Machine appearance/assignment overrides only where game-specific |
| Installed Face assignment | Cabinet (transitional) | Cabinet `faceAssignments[] { targetId, faceAssetPath }` | Cabinet Face Targets assignment controls/commands | build traversal; machine runtime `faces[]` | Machine `SurfaceAssignments` |
| Face placement/visual semantic state | Face | Face v23 `elements[]` including x/y/width/height and type-specific fields | Face Workspace, hierarchy and Inspector | Face runtime manifest/renderers | Face/Surface host |
| Logical reel identity | Face reel | `linkedMachineObjectReference: "Reel:n"` | Face reel Inspector/generation | build resolution, Face runtime `machineReference`, Player reel state | Face mount references Machine logical device |
| Physical reel selection | Cabinet (transitional) | `reelAssignments[] { machineReelReference, reelSpecificationId }` | Cabinet Machine Reels controls | Face export dimension resolution | Machine device resolution/override |
| Physical reel dimensions | Cabinet embedded spec (transitional) | `reelSpecifications[] { id,name,diameterMm,widthMm }` | Cabinet Reel Specifications controls | flattened to Face runtime `physicalWidth/physicalRadius` | reusable Reel asset |
| Reel platform normalization | Project platform + transient runtime | project platform and platform-specific reel settings; code in `MachineReelRuntimeAdapter`/`FaceRuntimeStateResolver` | Project Settings/Play View | Editor preview/emulation; Player consumes already-normalized runtime position state interfaces and authored reversal/offset | Machine RuntimeDefinition/backend, not Reel asset placement |
| Face provenance | Face | Face v23 source Panel2D ID/path/shape/region, artwork source and per-element Panel link | Face generation/regeneration/artwork UI | authoring/build invalidation, not composition | Face provenance (unchanged) |
| Reflections | Cabinet, but sources name mounted Face IDs | Cabinet `reflections[]` and optional package-local visibility masks | Cabinet reflection editor/catalog | Cabinet runtime schema 4 and Player reflection renderer | Cabinet owns receiver/material geometry; Machine must resolve installed-surface sources or bind stable surface slots |
| Cabinet preview settings | Cabinet | `preview { showTargetOverlays, showFaceBackgrounds, lampPreviewMode }` | Cabinet viewer | Editor only | Editor-only state (whether persisted beside asset remains a UX decision) |
| Authored build root | selected saved Cabinet | no Machine asset; command passes Cabinet path/model | Build/Preview command on Cabinet tab | `MachineRuntimeBuildService` | Machine |
| Generated runtime “machine” identity | Project name + Cabinet name/path | `machine.runtime.json` v3 uses project name; directory uses Cabinet asset name | none | Player startup/load | Machine asset identity |

## C. Current serialized schemas

### Project (`.oasisproj`, schema 8)

Authoritative code: `EditorProject`, `ProjectScaffolder`, and the read/write helpers embedded in `MainWindowViewModel`.

```text
{
  name, createdUtc, version: 8,
  layout: { assets: "Assets", machines: "Machines", generated: "Generated" },
  project_settings: {
    FruitMachine_Platform,
    System6NativeRoms,
    Mpu5NativeRoms,
    EpochNativeRoms,
    Mpu3Settings,
    M1Settings,
    Scorpion4Settings
  },
  input_definitions: [ ... ]
}
```

The loader rejects non-v8 projects. System 6 has custom JSON writing; other platform objects are mostly serialized as models. `Mpu5NativeRoms` and System 6 can fall back to defaults if missing, while Epoch/MPU3/M1/Scorpion4 are required by current v8 loading paths. Runtime settings include much more than ROM paths: reel steps/optos, coin communication/channels, DIPs, stake/prize/percentage and platform-specific options/hoppers.

### Panel2D (`asset.panel2d`, current schema 3)

Folder package: `Assets/Panel2D/<AssetName>/asset.panel2d`, with imported images copied under the project's Assets tree. Shape includes title, summary, `elements[]`, and `faceSourceShapes[]`. Elements include stable object ID, kind, geometry, primary/secondary assets, display/lamp number, display properties, reel band/stops/scale/offset/reversal/lamps/transmission mask, transform/visibility flags and FML source metadata.

Storage still contains a schema-1 migration/lenient deserialize path and may select an older schema for simple documents. This predates the Architecture Next latest-only policy and is cleanup-worthy when this schema is next changed, but Phase 1 need not touch Panel2D format.

### Face (`asset.face`, schema 23)

Folder package: `Assets/Faces/<AssetName>/asset.face`. Important groups:

* identity/title/summary;
* persisted Panel2D provenance: source document ID/path, source-shape ID/region and regeneration time/settings;
* `provenance`, product `buildState`, artwork recipe/source/geometry/pipeline/override and generated output paths;
* runtime render asset paths and dimensions (generated output references);
* mask layer/contributions, trays, lamp emitters, layers;
* polymorphic semantic elements with logical machine and linked Panel IDs.

No current DTO/model property persists assigned Cabinet target/path or reel specification ID. Face has direct image or Panel2D-source artwork modes. Generated paths commonly point into `Generated/Faces/<FaceName>/...`; authored masks/overrides remain in the Face package/Assets.

### Cabinet (`asset.cabinet3d`, schema/version 6)

Folder package: `Assets/Cabinet3D/<AssetName>/asset.cabinet3d`; GLB and reflection mask references are resolved relative to the Cabinet package.

```text
version: 6
model: { path, scale, upAxis }
targetOverrides: [{ targetId, frontSide, faceRotation, faceFlipHorizontal }]
preview: { showTargetOverlays, showFaceBackgrounds, lampPreviewMode }
reelSpecifications: [{ id, name, diameterMm, widthMm }]
defaultReelSpecificationId
reflections: [{ id, targetId, materialSlot, sources[{faceId, plane, planeSource}], settings, visibilityMask }]
faceAssignments: [{ targetId, faceAssetPath }]
reelAssignments: [{ machineReelReference, reelSpecificationId }]
```

The GLB remains authoritative for face-target quads/normals/UVs and reflection receiver geometry. `GlbCabinetFaceTargetDetector` discovers names prefixed `OasisFace_`; JSON stores keys and overrides, not copied target geometry.

### Runtime package

Current normal build layout:

```text
Generated/Builds/<CabinetAssetName>/
  machine.runtime.json                 # oasis.machine.runtime v3
  cabinet/
    cabinet.runtime.json               # oasis.cabinet.runtime v4
    cabinet.glb
    reflection-masks/...               # when configured
  faces/<FaceAssetName>/
    face.runtime.json                  # Face runtime schema 9
    artwork.png, mask.png
    tray/lamp lookup textures and debug textures
    reels/<band and optional transmission-mask images>
```

Machine v3: `machineId`, `displayName`, `cabinetManifest`, and `faces[]` carrying Face ID/name, Cabinet target, front side, rotation, flip and Face manifest path. Cabinet v4: Cabinet ID, GLB, scale/up-axis and resolved reflection definitions. Face runtime v9: dimensions/textures plus lamps/trays/reels/displays/buttons. Reel entries carry logical `machineReference`, band/stops/reversal/offset, concrete `physicalWidth`/`physicalRadius`, aperture geometry and reel lamps.

All runtime paths are normalized relative paths and Player containment-checks them against the build root. Player hard-rejects machine versions other than 3, Cabinet versions other than 4, and Face versions other than 9.

### Machine authoring schema

None exists. `.machine` is an extension discriminator and generic file-save option only. `asset.machine` is recognized by `AssetInspectorDetailsBuilder` as a label but is not an `EditorAssetType`, package manifest constant, storage format or build input.

## D. Current dependency graph

### Authoring graph

```text
.oasisproj v8
  |-- workspace layout --------------------------------------+
  |-- platform + all ROM/hardware settings ---> Editor emulation/Play View
  `-- input_definitions -----------------------+             |
                                               |             |
MFME/FML --decode/map/copy--> Panel2D v3       |             |
                             | elements -------+             |
                             | face source shapes             |
                             `--[generation provenance]------> Face v23
                                                               | artwork/masks
                                                               | semantic elements
                                                               ` logical refs (Reel:n etc.)

Cabinet GLB --detect OasisFace_* targets-------> Cabinet v6
                                                 | GLB/target overrides
                                                 | Face assignments -------> Face assets (explicit paths)
                                                 | Reel assignments --+
                                                 | Reel specifications +--> physical reel resolution
                                                 ` Reflections -----------> mounted Face IDs

.machine stub ----------------------------------X (no model/storage/build edge)
```

Persisted edges are Cabinet-to-Face paths, Face-to-Panel2D provenance/path/shape IDs, semantic logical references, and Cabinet logical-reel-to-spec IDs. Panel2D-to-Face is not a forward edge; generation finds/receives a selected source Panel2D/shape. Cabinet target geometry is inferred from its referenced GLB. FML-to-Panel2D document provenance is mostly per element, not a document-level source relationship.

### Build/Player graph

```text
selected saved Cabinet v6 (actual build root)
  + .oasisproj name/workspace paths
  |
  |-- model.path ------------------------> copied cabinet.glb
  |-- faceAssignments[] ----------------> read only referenced Face v23 assets
  |                                        ` export Face runtime v9 + textures/bands
  |-- reelAssignments[] + specs --------> flatten physical dimensions into Face reels
  |-- targetOverrides[] ----------------> flatten orientation into machine faces[]
  `-- reflections[] --------------------> validate against mounted Faces; Cabinet runtime v4

MachineRuntimeBuildService
  -> machine.runtime.json v3 (synthetic project-name identity)
  -> staged/atomically replaced Generated/Builds/<Cabinet>/
  -> Oasis Player RuntimeBuildLoader
       -> Cabinet GLB
       -> mounted Face manifests/textures
       -> reflection bindings
       -> reel meshes/materials/state bindings
```

There is no edge from the generated Player package to project platform/ROM/hardware settings. There is also no project-wide Face scan in the current builder; this was removed by the recent transitional refactor.

## E. Current build traversal

1. The command is available only when the selected document is a saved Cabinet3D asset. The Machine stub cannot build.
2. `BuildFromCabinetDocument` validates the Cabinet manifest/package shape and requires `Assets/Cabinet3D/<name>/asset.cabinet3d`.
3. It resolves `model.path` relative to the Cabinet manifest and verifies the GLB.
4. It chooses `Generated/Builds/<sanitized Cabinet asset name>` as final output and `<final>.staging` as temporary output; existing staging is cleared.
5. It copies the GLB to `cabinet/cabinet.glb`.
6. It redetects GLB Face targets and verifies each Cabinet assignment names a valid detected target (unless detector returns no targets).
7. For each `CabinetDocument.FaceAssignments` row, in declaration order, it resolves a rooted or project-relative path (a directory implies `asset.face`), reads only that Face, validates schema 23, and requires package placement under `Assets/Faces/<name>/asset.face`.
8. It resolves target orientation. With zero target overrides a default is allowed; if any overrides exist, each assigned target must have one—a transitional validation quirk.
9. It passes the selected Cabinet explicitly as `FaceCabinetContext` to `FaceRuntimeExportService`. For each Face reel, export requires a logical `Reel:n`, exactly one matching Cabinet `ReelAssignment`, exactly one valid Cabinet spec, then emits width and radius. Face-owned artwork/masks/lookup textures and bands are produced under `Generated/Faces/.../runtime` and copied into staging.
10. Errors are wrapped with Cabinet asset, target and referenced Face path. Unreferenced Faces are never opened.
11. Reflections are validated against discovered receiver targets and the exported mounted Face list. Optional masks are containment-checked relative to the Cabinet package and copied.
12. It writes Cabinet runtime v4, then machine runtime v3. The generated machine ID/display name are both `project.Name`; the manifest points at the one Cabinet and flattened Face placements.
13. Cancellation is checked between major steps, assignments and recursive copies. Progress is hierarchical (preparation, Cabinet copy, child Face-export interval, reflections/manifests/finalization).
14. Finalization deletes the prior final directory and moves staging into place. The `finally` block removes abandoned staging. This is staged but not transactional across a failed delete/move of the prior output.
15. Preview first validates Player launch settings, performs the identical build, then launches the configured executable with the build root and window/fullscreen arguments.

A “normal fruit-machine build” is therefore Cabinet-rooted visual composition; it is not a package of the selected native emulator backend or ROMs.

## F. Transitional architecture remnants

* **Cabinet `FaceAssignments`:** successful inversion from old Face ownership and correct explicit traversal, but belongs on Machine in the target.
* **Cabinet `ReelSpecifications`:** currently the only authoritative physical width/diameter source. It should be replaced, not promoted, by one reusable Reel asset per physical implementation in Phase 3.
* **Cabinet `ReelAssignments`:** maps logical `Reel:n` to the embedded spec and belongs to Machine device resolution.
* **`DefaultReelSpecificationId`:** authoring convenience that fills assignments; transitional with embedded specs.
* **Face runtime physical dimensions:** cleanly resolved during composition, but flattened into Face runtime v9. This contract will change when reusable Reel assets and Machine runtime composition become authoritative.
* **Stale Face generation context:** `DocumentWorkspaceViewModel` still discovers a selected target from any open Cabinet, carries `AssignedTargetId`/`AssignedCabinetAssetPath`, and passes three now-unused Cabinet-named arguments into `FaceGenerationService`; only `targetAspectRatio` is used. Remove these misleading APIs while preserving an optional generic target aspect input.
* **`FaceCabinetContext`:** useful short-term explicit composition input; rename/generalize to Machine build context when Machine is introduced.
* **`FaceCabinetContextResolver`:** `ResolveForFace` now always returns missing assignment; `ResolveForGeneration` still searches open Cabinets by selected target; `ResolveByAssetPath` is retained despite Face no longer persisting Cabinet paths. These are stale remnants to remove or replace rather than extend.
* **Cabinet preview:** Face preview refresh resolves Cabinet assignments and often depends on matching *open* Face documents. It is editor visualization, not authoritative traversal, and Composition View should reuse rendering while sourcing assignments from Machine.
* **Cabinet reflection sources:** the catalog correctly limits sources to explicitly mounted Faces, but the persisted Face IDs make reusable Cabinet definitions game-specific.
* **Dormant Machine shell:** enum/extension/stub/type label/menu/save dialog/`MachinesDirectory` without a schema or specialized view. The useful parts are document routing/tab/command infrastructure and logical machine-reference types; stub content and generic save behavior should be replaced.
* **Synthetic runtime Machine:** real runtime DTO/Player loader named Machine, but its authored source is Cabinet and its identity is Project. It is a useful output shell, not evidence of a first-class authoring asset.
* **Project `Machines/` versus package assets:** scaffolding creates a separate root while Architecture Next describes Machine as an asset and current `ProjectAssetPathService` supports only Panel2D/Face/Cabinet3D. Phase 1 must choose one convention and remove the other; do not support both.
* **Project-wide runtime state:** every document has its own transient `MachineRuntimeState`, while platform selection is globally pushed to all open documents. This is convenient for previews but hides the absence of selected Machine context.
* **Panel2D legacy migration:** schema-1 migration conflicts with the new latest-only policy but is unrelated to Phase 1 unless Panel2D is changed.

## G. Target-architecture conflicts

| Conflict | Current behavior | Target behavior | Files/subsystems affected | Difficulty/risk |
| --- | --- | --- | --- | --- |
| Project is an implicit machine | Platform, six backend settings families and inputs live in v8 Project and feed Editor emulation globally | Project retains workspace metadata; Machine owns a typed runtime definition and game mappings | `EditorProject`, scaffolder/load/save, Project Settings, platform VMs, emulation launch/factory, Play View, tests/fixtures | **High.** Broad UI/runtime coupling; do not merely copy fields and leave project fallbacks |
| No real Machine asset | `.machine` is generic stub text; no schema/service/view | First-class package asset, explicit Cabinet/Faces/runtime refs and identity | `DocumentModel`, workspace/tab/save/open, asset paths/browser/inspector, new model/storage/view/commands | **Medium-high.** Infrastructure is reusable but generic Machine save behavior is misleading |
| Cabinet is composition root | Cabinet owns installed Faces/reels and build action | Cabinet reusable; Machine owns assignment/resolution/build | Cabinet schema/view/commands/tests, build/preview services, Face export context | **High.** Recent behavior/tests deliberately enforce current transitional ownership |
| Physical reel is embedded | Cabinet spec stores dimensions and Machine mapping | independent reusable Reel asset; Face owns mount/placement; Machine resolves asset/default | Cabinet/Face export/Player reels/tests; new asset package/editor in later phase | **High but defer to Phase 3.** Phase 1 needs a deliberate temporary representation without cementing it |
| Reflection source is installed Face ID | reusable Cabinet definition directly identifies current game Faces | Cabinet owns receiver geometry/material roles; composition resolves installed surfaces | Cabinet reflection model/catalog/editor, build validation/export, Player reflection contract | **Medium-high.** Target docs do not fully specify stable source-slot binding |
| Build identity/root mismatch | output folder from Cabinet; runtime ID/name from Project; Cabinet selected | Machine identity/path selected and transitive closure followed | `MachineRuntimeBuildService`, commands, Preview service, runtime DTO writer/Player reader/tests | **High.** Must avoid keeping Cabinet overload as hidden alternate root |
| Runtime package incomplete | visual Cabinet/Faces/reels/reflections only; no emulator/runtime definition | Machine runtime manifest includes runtime definition/config needed to run independently | Editor contracts/build, Player startup/runtime host, ROM packaging/reference policy | **High / scope-sensitive.** Player currently has no emulation launcher; Phase 1 contract can establish metadata without inventing all Phase 7 abstractions |
| Cross-asset refs assume project | Cabinet paths and Face provenance resolve under `project.ProjectDirectory`; browser watches one `Assets` root | assets may later live in reusable Libraries with stable explicit references | path service, validators, Cabinet catalogs/build, asset browser | **Medium now, high later.** Avoid baking project-relative strings into new Machine APIs without a reference boundary |
| One Machine runtime context | global Project Settings and selected platform update every document state | document/preview resolves through selected Machine | `MainWindowViewModel`, `DocumentTabViewModel`, adapters, Inspector/Play View | **High.** Selection/context behavior must be explicit to avoid cross-Machine state leakage |
| Composition visualization is Cabinet-local | Cabinet viewer renders mounted Faces from Cabinet assignments/open docs | derived Composition View reads Machine graph, never writes duplicate graph state | Cabinet viewer/render helpers, document templates, hierarchy/inspector | **Medium in Phase 6.** Rendering is reusable; assignment authority is not |

## H. Phase ordering constraints

1. **Split Phase 1 into an authoring-root slice and a runtime-definition migration slice, but finish both before declaring Phase 1 complete.** A Machine that only wraps Cabinet would leave the largest Project ownership violation intact; moving all platform settings before Machine selection/document context exists would break Editor emulation. Add Machine identity/storage/selection first within the phase, then move Project runtime configuration onto it, then redirect consumers.
2. **Move Cabinet Face assignments in Phase 1 as planned.** The existing builder already follows explicit references, so transferring the collection to Machine is lower risk than retaining a Cabinet fallback. Update reflection source resolution at the same boundary; otherwise reusable Cabinet still depends on game Faces.
3. **Do not attempt full Reel assets in Phase 1.** The builder/Player require concrete dimensions. Until Phase 3, keep a clearly temporary Machine-owned snapshot/embedded composition structure or reference the selected Cabinet's existing specs only as a short-lived implementation detail. The master plan should explicitly require deletion in Phase 3. Moving `ReelAssignments` to Machine while leaving specs on Cabinet preserves behavior and improves game-specific ownership; moving specs to Machine would make later extraction mechanically simpler but temporarily duplicates non-target ownership. This is a genuine decision (see J).
4. **Change the build command/root only after Machine references and save/load are functional.** Remove Cabinet-rooted public entry points in the same phase—no compatibility overload/fallback—then update Preview/build tests and Player contract together.
5. **Decide Machine package location before implementing it.** Current `Machines/` is outside `Assets`, while reusable asset tooling is package-oriented and `EditorAssetType` omits Machine. Supporting both would harm Phase 5 Libraries. Recommended: `Assets/Machines/<name>/asset.machine`, update scaffold/path service, and delete the standalone `Machines/` convention in the new schema.
6. **Account for two runtime loops.** Editor native emulation currently reads Project settings; Oasis Player runtime package has no emulator config. Phase 1 must redirect Editor launch to Machine. It should also revise the runtime manifest ownership/identity, but adding a working Player emulation backend is beyond current Player capability and aligns with later RuntimeDefinition work. Define exactly what Player must consume now versus preserve as contract data.
7. **Phase 2 Cabinet cleanup depends on Phase 1 reflection and preview decisions.** `FaceAssignments`/`ReelAssignments` can be removed only after Cabinet viewer and reflection catalog receive explicit Machine composition context. Do not leave hidden open-document inference.
8. **Phase 3 Reel assets must update Face runtime/Player together.** Current Player geometry and lamps depend on concrete width/radius already flattened into Face v9; reusable Reel authoring cannot be editor-only.
9. **Phase 5 Library work should not precede reference abstraction cleanup.** Current absolute/project-relative resolution and single Assets watcher are constraints. Phase 1 should avoid solving Libraries but should centralize new Machine asset references so Phase 5 can replace resolution without rewriting every consumer.
10. **The master plan's broad order remains sound.** The key refinement is making Phase 1 an explicit sequence (Machine package/document → composition ownership → project runtime settings/context → build/Player root) and treating reflection source rebinding plus temporary reel handling as Phase 1 exit criteria.

## I. Recommended Phase 1 scope

### Add

* `MachineDocument`/typed runtime-definition model and `MachineDocumentStorage`, latest-only schema 1.
* Machine asset package support in `EditorAssetType`/`ProjectAssetPathService`, recommended at `Assets/Machines/<AssetName>/asset.machine`.
* Explicit stable references for one Cabinet and assigned Faces/surfaces. Use normalized asset references through a small resolver boundary rather than scattered `Path.Combine(project.ProjectDirectory, ...)` calls; do not implement Libraries yet.
* Machine-owned runtime settings sufficient to represent every current `EditorProject` platform configuration without loss: platform discriminator, selected backend's settings, and logical input definitions. Prefer a discriminated/typed shape so irrelevant settings for all platforms are not authoritative simultaneously.
* Machine-owned surface assignments and logical-reel physical-resolution mappings.
* Machine document view model/view/Inspector sections and undoable mutations, reusing document command/history/dirty-state patterns.
* simple new-project behavior: create/select one Machine automatically or provide a deterministic first-run creation path, consistent with target UX.

### Change/move from Project

* Move `FruitMachinePlatform`, System6/MPU5/Epoch/MPU3/M1/Scorpion4 configurations and machine input definitions out of `.oasisproj`/`EditorProject` into Machine.
* Increment Project schema directly and retain only workspace identity/layout. Update `ProjectScaffolder`, loader/writer, fixtures and Project Settings binding; delete old project settings read/write helpers rather than falling back.
* Rebind Project Settings (or rename it Machine Runtime Settings) to the selected Machine. Platform changes must update only that Machine's preview/emulation context, not every open document globally.
* Redirect `EmulationLaunchRequest`, backend setup, reel-step lookup, Play View input routing and runtime adapters to selected Machine state.

### Change/move from Cabinet

* Move `FaceAssignments` to Machine surface assignments and remove them from Cabinet v7 (or the chosen next direct version).
* Move `ReelAssignments` to Machine.
* Remove Cabinet reflection references to concrete installed Face IDs or move the composition-specific source binding to Machine while Cabinet retains receiver/material geometry and intrinsic settings.
* Keep GLB/model, detected target identity/geometry, intrinsic target orientation, receiver geometry and Cabinet-hosted facts on Cabinet.
* Decide and document the temporary handling of `ReelSpecifications`; do not create full Reel assets in Phase 1 and do not imply the temporary shape is final.
* Adapt Cabinet viewer to accept an explicit Machine composition context when showing assigned Faces; standalone Cabinet editing must remain possible without a game.

### Build root and runtime/Player

* Replace `BuildFromCabinetDocument` with Machine-rooted build entry points. Do not retain the Cabinet overload as compatibility infrastructure.
* Resolve the selected Machine, then its Cabinet, surfaces, temporary reel resolution and transitive dependencies. Preserve the current explicit-only traversal, targeted diagnostics, staging, cancellation and progress behavior.
* Name the output directory and machine manifest identity from Machine, never Project/Cabinet.
* Update machine runtime schema and Player reader together. At minimum the manifest must become an honest projection of the authored Machine root. Decide whether Phase 1 embeds the current emulation runtime settings for future consumption or whether Player execution is explicitly still visual-only; do not silently omit required state while calling the package independently playable.
* Preserve current resolved Face v9 physical dimensions until Phase 3 unless Phase 1's temporary reel representation requires a coordinated schema bump.
* Keep Cabinet runtime concerned with the GLB/intrinsic/reflection receiver result, not installed Face selection.

### Editor document/UI infrastructure

* Replace `CreateMachineStub` with a typed Machine document factory; add storage payload fields to `DocumentTabViewModel` or introduce a focused Machine document VM following Face/Cabinet patterns.
* Add a Machine-specific template to `DocumentEditorView`; the current generic summary is insufficient.
* Teach `DocumentWorkspaceViewModel.BuildOpenDocumentData`, save service, save dialog, asset browser, inspector details and navigation/open-by-path about the package manifest.
* Reuse tab selection, open-by-path de-duplication, command history, dirty/save lifecycle, hierarchy/Inspector primitives and Cabinet/Face preview renderers.
* For future Phase 6, derive Composition View from the Machine model and reuse Cabinet target geometry/preview rendering and existing workspace selection/navigation. It must not persist a second graph.

### Delete/replace

* Generic `.machine` text-save behavior, stub summary and standalone `Machines/` root if the package recommendation is accepted.
* Project-owned platform/runtime settings and global propagation paths after consumers are redirected.
* Cabinet `FaceAssignments`/`ReelAssignments` and their Cabinet mutation/UI rows after equivalent Machine editing exists.
* obsolete `FaceCabinetContextResolver` generation/asset-path resolution and rename the remaining export context to Machine composition terminology.
* Cabinet-root build/preview entry points and tests.
* No migration readers, legacy DTOs, dual paths or fallback ownership.

### Tests requiring updates/additions (Phases 1–3)

* **Project ownership:** `ProjectAndPanelCreationServicesTests`, `ProjectSettingsViewXamlTests`, MPU5/MPU3/platform settings VM tests, emulation backend/launch/stability tests, input tests and project fixtures.
* **Machine document:** new storage latest-version round trip/rejection, asset package paths, open/save/tab/template, command undo/redo, multiple Machines with isolated settings.
* **Cabinet composition:** `CabinetReelSpecificationTests`, `CabinetReflectionFaceCatalogTests`, Cabinet viewer/lifecycle tests and mutation/UI tests must stop treating assignments as Cabinet authority.
* **Build/Preview:** `MachineRuntimeBuildServiceTests` and `OasisPlayerPreviewServiceTests` must build from Machine, verify explicit closure and multiple Machines per Project, preserve unrelated-invalid-asset isolation and targeted diagnostics.
* **Face/provenance:** `FaceDocumentRoundTripTests`, `FaceGenerationServiceTests`, `FaceBuildServiceTests`, `FaceRuntimeExportServiceTests`, `FaceValidationServiceTests` and regeneration tests should preserve Panel provenance/logical refs and accept explicit Machine composition only for resolved export.
* **Runtime manifests/Player:** Unity `RuntimeBuildLoaderManifestTests`, Face/reel/reflection loading/rendering tests and Editor manifest assertions must advance in lockstep.
* **Phase 3:** Cabinet embedded-spec tests and Player reel physical-dimension tests require the largest second rewrite when Reel assets replace the temporary source.

## J. Open design questions

Only the following decisions are not settled by current documents plus repository evidence and materially affect Phase 1.

### 1. Where exactly is a Machine package stored?

* **Recommended:** `Assets/Machines/<Name>/asset.machine`. It aligns with folder-as-asset tooling, browser watching, explicit references and future Libraries; delete the separate `Machines/` root.
* **Alternative:** `Machines/<Name>/asset.machine`. It preserves scaffold intent but requires a second asset root throughout browser/path/library code and weakens the “Machine is an asset” rule.

This must be decided before schema/path work; dual support is explicitly not recommended.

### 2. What temporary physical-reel shape is allowed between Phase 1 and Phase 3?

* **Machine assignment references Cabinet embedded specs:** minimal immediate change and preserves runtime export, but Cabinet remains partly device-authoritative and Machine references an implementation detail scheduled for deletion.
* **Move embedded specs and mappings wholesale to Machine:** makes Cabinet reusable immediately and Phase 3 extraction straightforward, but temporarily places reusable physical facts on Machine, also contrary to target.
* **Pull Reel assets into Phase 1:** reaches target directly but violates scoped phasing and significantly expands editor/runtime work.

Recommended practical choice: move mappings to Machine, retain Cabinet specs for only the shortest Phase 1→3 interval with an explicit deletion checkpoint; do not expose this as a stable abstraction.

### 3. How should reusable Cabinet reflection definitions identify source surfaces?

* **Stable Cabinet surface target IDs:** Cabinet reflection receiver refers to intrinsic source target slots; Machine assignments determine which Face supplies them. This is reusable and likely smallest.
* **Machine-owned reflection source bindings:** Cabinet defines receivers/settings only; Machine binds one or more installed surfaces. More flexible but increases Machine schema/UI.

Concrete existing data uses Face IDs and up to four source planes, while target docs establish ownership principles but not this binding contract. Decide before removing Cabinet `FaceAssignments`.

### 4. What must Player do with Machine runtime/emulation configuration in Phase 1?

* **Serialize and validate it now, without launching emulation:** makes the package contract complete enough for later Player integration while limiting behavior change.
* **Implement Player launch now:** produces a truly independently playable package but substantially expands Phase 1 into the later RuntimeDefinition/backend phase.
* **Omit it:** smallest change, but contradicts Machine as a complete runtime root and repeats the current split truth.

Recommended: serialize a narrow current emulation runtime definition in Machine runtime schema, update Player DTO/validation, but defer actual Player emulation hosting unless the Phase 1 acceptance criteria explicitly require it.

### 5. Which Machine is the active context for Face/Panel standalone preview and Project Settings?

* **Explicit active Machine selection at workspace level:** clear for multi-Machine projects and permits a Face to be previewed in a chosen composition.
* **Owning/open Machine tab only:** avoids global state but standalone Face documents need an explicit “preview in Machine” context.
* **First/only Machine implicit fallback:** simple for one-Machine projects but becomes ambiguous and risks hidden inference.

Recommended: explicit active Machine with an automatic selection only when exactly one exists; never infer ownership by scanning all Machines. The UX details are needed before redirecting global `MachineRuntimeState` consumers.
