# Prefabs, OasisObjects and Components — proposed architecture

Status: proposal only, 2026-10-10. No implementation has started. Delivery and prerequisites are in [the phased plan](14_PREFAB_AND_POOL_DELIVERY_PLAN.md). This extends A1–A8, rather than replacing their delivered runtime boundary.

## Evidence and current baseline

Inspected main at `828ca04fd7e4bb4498ee573482f39740d608ceee`; GitHub comparison with main returned identical (zero commits ahead/behind). This includes PR #736 trigger discoverability, #737 coordinate-based Pool pocket names and #738 authored development keyboard shortcuts. Source inspection, not executed tests, establishes the following:

| Current contract | Evidence relative to OasisEditor, unless stated otherwise |
| --- | --- |
| Reusable Object3D schema 1: stable GUID, package-local GLB, intrinsic scale/up-axis, collider and Rigidbody defaults | [Object3DDocument.cs](../../OasisEditor/Object3DDocument.cs) |
| Machine authored schema 8: stable case-sensitive instance IDs, explicit AssetReference, placement, anchors | [MachineDocument.cs](../../OasisEditor/MachineDocument.cs) |
| Machine runtime 9, Object3D runtime 1, Cabinet runtime 5; GUID deduplication and conflicting-definition rejection | [MachineRuntimeBuildService.cs](../../OasisEditor/MachineRuntimeBuildService.cs) |
| Lists/indexing, list and constant-range iteration, scalar arithmetic, Vec3 construction, nine host operations; default 10,000 instruction budget | [Semantics.cs](../../Oasis.Scripting/Package/Runtime/Semantics.cs), [session](../../Oasis.Scripting/Package/Runtime/OasisScriptSession.cs) |
| Canonical single-ball script: ball01, rackBall01, traySlot01, six Pocket_XNeg/XPos triggers, rerack/newGame, duplicate guard and velocity clearing | [Pool script](../../Examples/Pool/behavior.oasis), [setup guide](../../Examples/Pool/README.md) |
| GLB trigger inventory is transient, follows node/mesh winning semantic names, reports duplicates/invalid IDs; Machine validation uses that inventory | [inventory](../../OasisEditor/Features/CabinetEditor/Services/CabinetTriggerInventory.cs), [validator](../../OasisEditor/OasisScriptMachineValidator.cs) |

Player source inspected under `UnityProjects/OasisPlayer/Assets/_Project/Scripts/`: `Loading/Object3DRuntimeLoader.cs`, `RuntimeBuild/RuntimeFaceModels.cs`, `RuntimeBehavior.cs`, `RuntimeMachineOasisScriptHost.cs`, `RuntimeInputDevelopmentControls.cs` and `RuntimeInputShortcutMapper.cs`. One live placement root owns the Rigidbody; model/physics correction children preserve intrinsic conversions. Teleport currently writes the root local pose, updates Rigidbody world pose and synchronizes transforms; it does not clear velocities. Reset restores authored transform/scale and clears both velocities. Registration adds identity used by relays; unload disposes behaviour before clearing timers/events/registries and destroying roots/imports.

PR #738 reads generated `inputs[].keyboardShortcut` once at development preview initialization, retains logical IDs, aggregates duplicate keys and dispatches ordinal releases before presses. Focus loss/disable releases held inputs; reload restores authored bindings; Emulation gets empty bindings. Unsupported shortcuts warn and map to None. This is development-only, not production binding support. PR #736 exposes Cabinet trigger identities in Cabinet/Machine authoring; it does not create a new persisted trigger list. Actual local Pool asset wiring and suite/manual execution remain outstanding.

## Desired Pool script first

The following is **illustrative proposed syntax, not compilable current Oasis Script**. It specifies required behaviour before choosing parser/API details. Assume a spherical PoolBall Prefab whose origin is its collider centre, unit instance scale, a rackOrigin and collectionTray frame on the playing surface (local +Y normal, +Z rack direction), cueStart at centre height, and a declared playingSurface query. Numbered-ball appearance overrides are deferred; ordinary Prefabs can share one appearance or use explicitly declared alternatives.

```oasis
const ballPrefab = prefab:poolBall;
const cuePrefab = prefab:cueBall;
state balls: List<OasisObjectRef> = [];
state collected: List<OasisObjectRef> = [];
state cue: Optional<OasisObjectRef> = none;

fn rack_pose(row: Number, col: Number, diameter: Number) -> Pose {
    let spacing = diameter + 0.0005; // deliberate clearance, metres
    let p = vec3((col - row / 2) * spacing,
                 diameter / 2, row * spacing * math.sqrt(3) / 2);
    return frame.pose(anchor:rackOrigin, p, vec3(0, 0, 0));
}
fn new_game() {
    for ball in balls { oasis.destroy(ball); }
    if option.has(cue) { oasis.destroy(option.get(cue)); }
    balls = list.empty<OasisObjectRef>();
    collected = list.empty<OasisObjectRef>();
    let diameter = 2 * collider.sphere_radius(ballPrefab);
    surface.require_contains_rack(surface:playingSurface, anchor:rackOrigin, diameter, 5);
    for row in range(0, 5) {
        for col in range(0, 5) {
            if col <= row {
                let ball = oasis.instantiate(ballPrefab, rack_pose(row, col, diameter));
                balls = list.append(balls, ball);
            }
        }
    }
    cue = option.some(oasis.instantiate(cuePrefab, frame.pose(anchor:cueStart)));
}
on machine.started() { new_game(); }
on input.pressed(input:newGame) { new_game(); }
on input.pressed(input:rerack) {
    // Proposed helper resets the existing 15 in row order and cue to cueStart,
    // using the same rack_pose; activate, teleport, clear both velocities.
    rerack_existing(balls, option.get(cue), ballPrefab);
    collected = list.empty<OasisObjectRef>();
}
on trigger.entered(pocket, ball) {
    if pool.is_declared_pocket(pocket) {
        if ball == option.get(cue) {
            oasis.teleport(ball, frame.pose(anchor:cueStart));
            rigidbody.clear_motion(ball);
        } else if list.contains(balls, ball) and not list.contains(collected, ball) {
            let slot = list.count(collected);
            collected = list.append(collected, ball); // guard before movement
            oasis.teleport(ball, pool.tray_pose(anchor:collectionTray, slot, ballPrefab));
            rigidbody.clear_motion(ball);
        }
    }
}
```

`rerack_existing`, `pool.is_declared_pocket` and `pool.tray_pose` are proposed user helpers, not new magical host commands: expand them into bounded loops/indexing, six explicit trigger comparisons and a frame-transformed linear/grid tray calculation. The surface assertion similarly derives from a focused plane/rectangle query and frame math; no Pool-specific engine API is needed. Five rows produce 1+2+3+4+5=15 balls and a separate cue. No fifteen individually authored rack anchors, full rules, scoring, random placement, cue controls or multiplayer are required.

## Vocabulary and ownership

- **Prefab**: reusable asset defining a root entity, authored child hierarchy, supported components and defaults. Evolve the existing Object3D asset pipeline, package identity, discovery, documents and build traversal into this asset; never add a competing parallel asset type with duplicated resolvers/loaders.
- **OasisObject**: authored placement or dynamically instantiated entity owned by one Machine session. Child nodes are also OasisObjects with stable node identities, without a separate asset per node.
- **PrefabRef**: typed script handle to a resolved declared reusable asset, not a path.
- **OasisObjectRef**: typed live entity handle. Reserve ordinary script “object” for future data objects.
- **Component**: supported Oasis capability, with an explicit versioned contract. Unity components are adapter implementation details; one Oasis capability may use multiple Unity components or none.

Machine behaviour remains gameplay owner. No per-entity script components, arbitrary Unity type names, reflection, engine handles or filesystem APIs. Cabinet retains fixed semantic GLB geometry; Face retains batched artwork/device rendering and logical identities; Reel retains its specialised workflow. Later specialised Reel/Lamp/Display components and separate script/emulation control adapters may coexist with these workflows. Do not demand one OasisObject per logical lamp/reel/display.

## Asset and authoring contract

Proposed Prefab document is the next revision of Object3D, with the existing stable asset GUID and a versioned root/node tree. Each node has an immutable asset-local ID, name, local Transform and component entries `(componentId, kind, contractVersion, properties)`. IDs survive reorder/rename; only supported kind/version/property keys are accepted. Each node has exactly one Transform and at most one of each initial capability. References use existing Project/Library AssetReference conventions and package-local model paths; child nodes are authored inline, not nested Prefab references.

| Initial capability | Portable defaults and validation |
| --- | --- |
| Transform | Finite local position, XYZ Euler degrees, positive scale; acyclic parent tree. Root default distinct from Machine placement. |
| Model/Rendering | Safe package-local GLB, positive intrinsic uniform model scale, supported up-axis; correction remains beneath physical placement. Imported render nodes are not implicitly public OasisObjects. |
| Collider | None/Sphere/Box/Capsule/Mesh, centre and dimensions in declared model-local coordinates; positive dimensions, capsule height >= diameter, trigger role explicit. Mesh initially retains the existing single-usable-mesh rule; no decomposition. |
| Rigidbody | Disabled/static, dynamic or kinematic mode, positive mass when required, gravity; one body owner per subtree initially, no nested bodies/joints. Mode exposes supported movement, not backend configuration. |

Model may be absent for transform-only grouping nodes. Collider source frame must be explicit when no Model is present (node-local SI); no implicit scale/up-axis guess. Dynamic colliders must satisfy backend support validation. Reject unknown kinds, duplicate IDs, missing assets, invalid numbers, unsupported property combinations and illegal hierarchy; document/node/component/property context belongs in every diagnostic. Strict save/build boundaries follow existing document validation; transient Inspector edits can show errors without poisoning persisted valid state.

Prefab editor main view is the existing 3D viewport pattern with model/collider diagnostics, selectable child nodes and transient camera/filter state. Hierarchy exposes root/children and supported component structure; Inspector edits selected node transform/defaults and component properties through document-scoped commands. Add/reorder/reparent/remove must preserve IDs, reject cycles and record no-op-free undo. Incremental updates, shared selection and semantic theme resources apply.

Machine Hierarchy/Composition selects a placed OasisObject. Inspector shows read-only source Prefab GUID/reference with Open Source, placement/parent, and a distinct permitted-overrides section with Revert. Initial placement overrides are root pose/scale, active state and display name; structure, collider dimensions and body defaults belong in the source Prefab. Avoid an unrestricted JSON override bag. Source changes update derived previews without silently replacing placement. Child inspection shows inherited defaults and derived pose; child overrides are deferred. Intrinsic scale/up-axis remains distinct from placement.

## Instances, references and lifecycle

Authored root IDs preserve current Machine case-sensitive IDs; child IDs derive from `(rootId, prefabNodeId)` rather than names/array positions. Build rejects duplicate IDs and broken parent links. Live handles contain session token, entity key and generation; authored literals resolve to handles after registration. Spawned keys use a reserved internal domain and monotonic allocation, never names or script-supplied IDs. Allocation and command ordering are deterministic within a session; physics event ordering is not a cross-platform promise. No handle survives unload/reload or silently retargets a reused slot.

All entities belong to one Machine, including parented entities. Authored roots may parent to another authored entity with validated acyclic local transforms; unparented pose is Machine-local. Children inherit hierarchy active state. Parenting across Machines and dynamic-body parenting beneath moving/scaled parents are rejected initially. Spawn defaults to Machine root; optional same-Machine static parent may be added only with explicit pose-space conversion. Runtime arbitrary reparenting is deferred.

Load resolves/imports all declared Prefabs, validates components and constructs authored entities atomically before attaching the script session. Registry entries and supported queries are available for pure read queries during initialization; mutations/spawn are forbidden in global initializers. `machine.started` follows successful initialization exactly once. Failed setup/initialization cleans partial resources and fails load; event-handler faults retain completed command effects and disable dispatch, matching A8.4.

Instantiate validates dependency, pose, parent and budgets before mutation, builds a disabled subtree, installs components/identity, registers atomically and then enables it. Return one live root ref; child lookup uses explicit node ID. Partial failures release all objects/resources, not a half-visible registry entry. No script callback is delivered reentrantly during construction. Relays suppress construction/disposal callbacks; ordinary later physics events use the common typed handle model.

Destroy invalidates root/descendant handles synchronously, unregisters and disables physics immediately, then defers backend destruction safely. It is permitted for authored and spawned entities; authored entities are not recreated by `reset(ref)`. Double destroy and commands/queries on stale, wrong-session, missing or unsupported-component refs fault with a deterministic domain code; `is_alive(ref)` is the explicit non-faulting probe. Delayed callbacks targeting invalidated entities are discarded. Reset restores authored placement or spawn-time initial pose/defaults/active state, clears motion, and keeps identity. Whole-Machine reset is unload/reload with fresh script state; Pool rerack reuses live balls, newGame destroys/recreates them. Unload disposes behaviour/input adapters, clears timers/events, invalidates registries, destroys subtrees and finally releases per-session shared imports.

## Dependencies and budgets

Machine declares named Prefab dependencies, e.g. `poolBall -> AssetReference`; script literal `prefab:poolBall` resolves only against that table. Authored placements also contribute explicit dependencies. Build walks references, deduplicates canonical GUIDs and rejects conflicting contents just as today; no project scan. Package generated manifests/models under the evolved definition directory, exporting alias-to-definition IDs and placements, never Editor paths. Load preloads the complete dependency table including spawn-only Prefabs before script startup. Script cannot open Editor files, load arbitrary URLs or dynamically resolve undeclared assets.

Proposed initial ceilings: 64 declared Prefabs, 1,024 live entities including child nodes, 4,096 total component entries, 128 spawns per event, 256 active named timers and 1,024 elements per script collection. Retain the current 10,000 instructions per initialization/event as starting policy. Imported geometry/texture memory needs separate measured build/load caps; entity counts do not bound asset memory. A versioned Player profile may lower caps and reject incompatible packages before startup; scripts cannot raise limits. Confirm ceilings with coin-pusher measurements before approval. Count admission before allocation; excess, numeric overflow, missing dependency and unsupported component faults have stable codes/context and no partial command effects. Completed earlier commands are not rolled back on handler failure. No resource pooling API or arbitrary backend access is necessary.

## Minimum language and host delta

| Need | Delivered | Proposed addition |
| --- | --- | --- |
| Refs/events | ObjectRef, AnchorRef, TriggerRef and typed events | PrefabRef, OasisObjectRef session/generation semantics; authored literal binding |
| Collections | Non-empty homogeneous list literals, indexing, state replacement, list iteration | Typed empty lists, bounded append/count/contains; value semantics, no index mutation required |
| Flow | if, let, state, bounded for; range bounds must be compile-time finite integral ordered numbers | Typed functions/return, acyclic call graph (no recursion), arguments/locals, shared event instruction budget; keep constant range for five-row algorithm |
| Maths | Scalar + - * / %, comparison, finite checks; vec3 constructor | sqrt, Vec3 add/scale or explicit helpers, pose/frame conversion, focused component/query result types |
| Movement | Set active, anchor/direct-pose teleport, velocity/angular velocity, impulse, reset; timers | Instantiate returning ref, destroy/is_alive, read pose, kinematic target; no general engine API |
| Geometry | Authored primitive defaults exist; no script geometry host query | Sphere radius, supported collider local geometry and transformed frames; declared surface plane/extent |

Do not implement maps, while loops, general mutable arrays, modules, closures, exceptions or dynamic range bounds merely to rack balls. New value-returning host operations require extending the current void command/result interface and compiler signatures, not duplicating the interpreter. Host operations remain prohibited in global initialization except explicitly whitelisted read-only queries. Options and typed empty lists require actual compiler/interpreter work; the example must never be described as currently supported.

Migration decision: replace ObjectRef with OasisObjectRef, `object:<id>` with `oasis:<id>` and `object.*` with focused `oasis.*`/`rigidbody.*` operations; keep anchor/trigger/input literals. Update event signatures, validator, canonical examples and all fixtures together. No permanent aliases/dual syntax. During earlier foundation phases retain existing syntax until the dedicated reference phase can update compiler and both hosts atomically. Machine logical device reference kinds remain distinct; do not blindly rename every use of MachineObjectReference/object terminology that denotes emulation devices.

## Coordinates, geometry and physics

Portable units: metres, seconds, kilograms, linear velocity m/s, angular velocity radians/s, impulse kg*m/s; authoring Euler angles degrees with documented XYZ convention matching current adapter. Machine space is +Y up, +X right and +Z forward using the current Unity-facing orientation convention; adapters must convert their own handedness/rotation conventions. Imported GLB up-axis/uniform model scale converts model-local geometry into node-local SI once; hierarchy transforms then produce Machine-space geometry. Machine-to-world transform belongs to Player placement, not script inputs. Do not conflate cabinet Blender naming axes with canonical Machine axes.

Focused collider query returns authored shape kind, local centre/shape frame and dimensions after intrinsic conversion, not Unity `Collider.bounds` world AABB. Sphere effective radius requires uniform positive scale through the physical ancestry; reject non-uniform/sheared dynamic sphere transforms rather than quietly reporting an ellipsoid radius. Diameter is twice radius. Capsule/box queries use their explicit shape frames; mesh provides no automatic portable radius. Source-model geometry and live transformed geometry are distinct query choices with explicit spaces.

Playing surface is a Machine-owned named plane plus oriented rectangle in a declared anchor frame, authored against Cabinet geometry and validated as finite/positive. It is lightweight query data, not a duplicate collider or new Cabinet mesh manifest. Return plane normal, origin, width/length and frame, allowing rack/tray bounds checks. It does not infer a tabletop from renderer bounds or guarantee it matches an arbitrary mesh; local authoring verification establishes alignment. Surface data is optional until the query phase, which owns its schema change.

Placement is an authored/load-time pose. Teleport is a discontinuous live pose change with immediate query-visible pose and no sweep/contact-path guarantee; preserve current separate velocity clearing. Kinematic movement submits a target to the physics-step adapter (Unity MovePosition/MoveRotation initially), not repeated Transform writes. Define one target per step, latest command wins within the owning source, and bounded velocity/finite target validation. Dynamic bodies reject kinematic-target commands. Kinematic shelves use fixed-step movement against dynamic coins; timer-driven mole motion can choose teleport only if swept contact is unnecessary.

Oasis guarantees identity/lifecycle, units, validation, supported shape/body modes, command semantics, startup/fault boundaries and event payload domains. Backend controls integration, friction/contact stability, sleeping, CCD support, exact collision ordering and numerical trajectories; document backend capabilities and fail unsupported requirements. No identical cross-platform simulation promise. Keep component/physics adapter interfaces portable so a future shared physics backend can implement the same contract.

Control ownership is explicit per controllable capability: one Machine-script, emulation-adapter or declared system controller. Conflicting claims fail setup; a source without ownership cannot issue motion/device commands. Physics integration is not a competing gameplay controller. Handover, if later needed, must release old claims and pending targets atomically. Specialised Reel/Lamp/Display components may bind logical devices or batch endpoints without one entity per device; emulation migration and hybrid runtime remain separate workstreams.

## Constrained variants — later milestone

A variant is an asset with its own GUID, exactly one ordinary Prefab base reference and explicit `(nodeId, componentId, propertyId, value)` overrides. Initially no variant chains, component additions/removals, nested Prefab composition, multiple inheritance or hierarchy restructuring. Placement overrides remain separate from variant defaults. Resolve/flatten variants for runtime packaging; Player needs no inheritance engine.

Inspector shows inherited values with base provenance and visibly overridden values; Revert removes the stored override through undoable commands. Base changes propagate to unoverridden values on reload/invalidation; overridden values retain their value and are revalidated. Deleted nodes/components/property changes make overrides errors, not silent drops. Missing bases, cycles (even though direct bases must be ordinary Prefabs), kind mismatches and illegal override properties fail validation/build with reference chains. Save As creates independent GUID, not accidental shared identity. Pool foundation requires no variants; differently numbered balls can initially use ordinary assets.

## Decisions for human review

Approve terminology/syntax replacement and latest-only format policy; root-only placement overrides and single-body hierarchy restrictions; spawn-only dependency declarations/preloading; stale-handle fault versus explicit is_alive; budget ceilings and backend profile policy; playing-surface plane authoring; and direct-base-only variants. These are proposed choices, not delivered functionality. See the phased plan for review boundaries and first-phase implementation prompt.
