# P1 ordinary Prefab authoring — working branch

Implemented against `ce37b035760a4c23f61f6efb0755ad0ac1aaab21` (merged PR #739).
A network-enabled fetch confirmed origin/main remains at that merge commit.
Static inspection is recorded below; Windows/.NET/WPF and Unity
checks have **not** been executed. Review, local verification and merge are pending.
P2–P8 remain unstarted. No Pool gameplay/content migration is included.

## One asset family and format consequences

The canonical manifest is **`asset.object3d`**, retained intentionally. Project
packages stay `Assets/Object3D/<Name>/asset.object3d`; Library packages stay
`Object3D/<Name>/asset.object3d`. Existing AssetReference paths and GUID identity
are retained. No `asset.prefab` discovery, parallel catalog, fallback reader or
legacy conversion is added. The UI calls this asset Prefab; internal Object3D
identifiers and runtime naming remain to avoid incidental P2 work.

Authored schema **2** contains `schemaVersion`, `id`, `displayName`, and `root`.
Each inline node has `id`, `name`, `components`, `children`. Node and component
IDs share an asset-local GUID domain and remain stable on rename/reorder/reparent
and command undo/redo. Each component has `id`, `kind`, `contractVersion: 1` and
exactly one corresponding typed property payload. Transform is mandatory;
Model, Collider and Rigidbody are optional, each at most once per node. No Unity
component type names, reflection, scripts, nested Prefab references or variants.

Schema 1 authored manifests stop loading. Deliberately reauthor each affected
package, retaining its asset `id`, `displayName` and package-local GLB path. Keep
its GUID rather than replacing it with the GUID of a newly created asset:

1. Back up the package. Record schema 1 `id`, model path/scale/upAxis and physics
   defaults. Create a schema 2 root with fresh permanent node/component GUIDs.
2. Add Transform with identity position/rotation/scale. Add Model with the recorded
   model defaults; intrinsic scale/up-axis belongs here, never in Transform or
   Machine placement. The root's name may use the recorded display name.
3. For a previous Collider, add a Collider entry with the recorded dimensions,
   `sourceFrame: ModelLocal`, `isTrigger: false`. Preserve sphere radius/centre,
   box size, capsule radius/height/axis or the Mesh kind. Omit irrelevant properties.
   Collider None may be omitted or retained as an explicit None entry.
4. For an enabled body add Rigidbody with `mode: Dynamic`, `enabled: true`, the
   recorded positive mass and explicit gravity. For a disabled body omit Rigidbody,
   or add `mode: Static`, `enabled: false` without mass/gravity.
5. Write the new manifest deliberately at the same path with the original asset
   GUID. Reopen/validate in the Editor, save, and rebuild Machines. Test-generated
   ball fixtures use schema 2 while retaining their constructed GUIDs, model
   references and equivalent physics defaults.

There is no automatic user-local package rewrite. Because the canonical filename
and folder convention are unchanged, Machine references do **not** require path
updates. If a package is manually moved, explicitly update its Machine
AssetReference; no resolver fallback will find the old location.

## Validation and source frames

Local transforms require finite position/XYZ Euler degrees and positive scale.
Euler conversion uses the existing Z-X-Y application order. Exactly one Transform
per node, unique IDs, supported kind/version/payload fields and an acyclic tree
are enforced. Tree depth is bounded at 128 levels. Optional-component removal
cannot leave invalid ownership/source-frame combinations.

Collider dimensions/centre use the explicit frame. ModelLocal uses the Model
on the same node, including its intrinsic uniform scale/up-axis; NodeLocalSI
uses node-local metres and no intrinsic model correction. Model-free colliders
must use NodeLocalSI. Mesh requires ModelLocal and a same-node Model, with exactly
one usable GLB mesh occurrence; no decomposition. Imported render nodes stay
private GLB geometry and do not appear as authored nodes.

Static Rigidbody components omit mass/gravity; Dynamic/Kinematic require explicit
positive finite mass/gravity and matching enabled state. Nested body ownership
is rejected, including disabled/static Rigidbody entries. Dynamic bodies require
an owned non-trigger primitive collider, reject Mesh colliders, and cannot sit
beneath an authored scaled parent. Kinematic is an authorable default only; P1
adds no kinematic gameplay or runtime execution.

All persisted Inspector, component, hierarchy and model-import edits use a
command targeting the owning document. Invalid edits report asset/node/component/
property context and retain the prior valid document. No-op edits create no history
or dirty state. Undo to a saved content checkpoint becomes clean; selection,
camera, model/collider visibility and preview imports remain transient. Per-node
imports keep child GLBs in separate package-local locations. Save As copies all
referenced models, including child models; model-free grouping assets can save.

## Admission to runtime schema 1

`PrefabRuntimeProjection.Validate` is shared by runtime DTO construction, Editor
build admission and Machine previews. A faithful P1 export requires:

- exactly one authored root; no children, even transform-only children;
- identity authored root Transform, distinct from Machine placement;
- a Model component with valid package-local GLB/intrinsic defaults;
- absent Collider, or ModelLocal Collider with `isTrigger: false`;
- absent Rigidbody, Static or Dynamic; never Kinematic;
- all authored validation/body/mesh constraints above.

No children are flattened and no root pose, node-local source frame, trigger role
or body mode is silently discarded. Diagnostics include Machine placement/reference
and Prefab/node/component/property context. Build staging preserves an earlier
successful package when admission fails. For the admitted subset, existing runtime
model scale/up-axis, collider fields, Rigidbody fields, definition GUID deduplication,
Machine placement IDs and generated file/schema names are retained. Player code,
Machine schemas, script syntax, trigger discovery and development keyboard bindings
are unchanged. Rebuild disposable generated output after explicit reauthoring.

## Regression coverage and static checks

Added authored tree round-trip and strict-reader coverage; rename/reorder/reparent
ID stability and undo/redo; document targeting/no-op/clean checkpoint; hierarchy
cycle/duplicate/cardinality/version/payload and collider/body/frame constraints;
faithful root projection with X/Y/Z intrinsic correction; source preview owner
selection/import caching and stable shared Hierarchy rows; grouping/multi-model
save/open/Save As; and contextual build rejection preserving every prior output
file. Existing single-family Project/Library discovery/reference tests now create
schema 2 content. The previous root/model/physics fixture constructors were
explicitly reauthored; Unity runtime schema 1 fixtures remain unchanged.

Static checks: `git diff --check`, Editor XAML XML parsing, source/binding/contract
inspection, and unchanged Unity/Player and runtime version checks. These establish
neither compilation nor passing automated suites. No build/test/application was
executed in Codex, per AGENTS.md.

## Required local Windows Editor and Unity checks

1. On Windows run the Editor test project, particularly Object3DDocumentTests,
   PrefabAuthoringTests, PrefabProjectionPreviewTests, DocumentSaveServiceTests,
   MachineObject3DInstanceTests, MachineComposition3DTests and
   MachineRuntimeBuildServiceTests; run the remaining Editor regression suite.
   Compile/run the WPF application and inspect binding errors.
2. Create **Prefab**, import a ball GLB, preserve its measured intrinsic scale and
   up-axis, set root Transform identity, add Sphere Collider in ModelLocal with
   non-trigger role, add Dynamic Rigidbody/mass/gravity. Save, close and reopen;
   compare GUID/node/component IDs and all defaults. Save As and copy to Library;
   check Project/Library reference choices contain this single asset family.
3. Select root/children from shared Hierarchy, viewport geometry and Inspector.
   Rename, add/remove optional components, change primitive shapes/dimensions,
   radius/centre, source frame, body defaults and local transforms. Undo/redo each
   operation; no-op edits leave history/dirty state unchanged; invalid values show
   node/component/property context and preserve valid persisted state.
4. Add transform-only children, reorder siblings and reparent using the parent
   picker. Test a rejected cycle, nested body and removal of Model used by a
   ModelLocal Collider. Remove/undo a subtree and verify IDs, selection and model
   sources. Save/reopen a model-free group and a child with its own imported GLB.
5. Repeat the UI checks under **System, Light and Dark** themes. Check Inspector,
   hierarchy, diagnostics, collider/trigger previews and camera/filter transience.
6. Assign the representable single-root ball to an existing Machine without
   changing `ball01`, anchors, trigger IDs, script or authored R/N keyboard bindings.
   Build and load in Unity using the unchanged Object3D runtime loader. Check
   model/collider alignment, scale/up-axis (including X/Z), mass/gravity, movement,
   trigger/reset and unload/reload using existing Unity EditMode/PlayMode suites.
7. Keep a successful package. Add an authored child and build again: expect a clear
   placement/Prefab/node/children error and byte-identical prior generated output.
   Repeat with nonidentity root Transform, NodeLocalSI Collider, trigger role and
   Kinematic defaults. Do not expect Player hierarchy support until P2.

Record actual local suite and manual outcomes before declaring P1 verified or
merged. Current local Pool asset wiring/verification remains a separate pending
checkpoint; this branch does not claim P3 or later completion.
