# Oasis Architecture Next — Active Roadmap

## Purpose

This file replaces the completed historical refactor phase plan. It lists only architecture work that is still active or intentionally deferred.

The durable ownership rules live in the numbered architecture documents in this directory. Historical phase/audit documents are intentionally removed once their implementation is merged so future Codex tasks do not consume obsolete transitional context.

## Current baseline

The following foundations are already implemented and should be treated as current architecture, not future work:

- Machine is the standalone composition/build root.
- Cabinet is a reusable physical asset.
- Face is a game-specific surface assembly mounted by Machine onto Cabinet surface targets.
- Reel is a reusable physical asset resolved by Machine.
- Machine owns runtime/emulation configuration and inputs.
- Project/Library assets use explicit `AssetReference` values.
- Machine has a derived Composition/Overview view.
- Runtime abstraction exists with the current `Emulation` implementation.
- Cabinet GLB semantic geometry uses:
  - `OasisFace_*`
  - `OasisCollider_*`
  - `OasisTrigger_*`
- Machine build preserves Cabinet GLB semantic geometry.
- Oasis Player creates Unity MeshColliders for Cabinet collider/trigger semantics.

Do not recreate transitional Cabinet/Project ownership removed by those implementations.

## Active track A — Dynamic Object3D composition

The immediate next architecture track is described in:

- `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`

The proving cases are:

1. Pool table:
   - reusable PoolBall Object3D assets;
   - Machine-owned ball instances;
   - dynamic Rigidbody/SphereCollider runtime objects;
   - Cabinet pocket triggers and table colliders;
   - later scripted rerack/pocket/tray behaviour.
2. Whac-A-Mole:
   - reusable Mole Object3D;
   - Machine-owned instances;
   - script-controlled motion;
   - hit/input/timer behaviour.

### Planned PR sequence

#### PR A1 — Object3D asset foundation

Add a reusable `Object3D` authored asset with:

- stable identity/display name;
- GLB model reference;
- model scale/up-axis as required by current conventions;
- minimal physics defaults;
- collider kind: None/Sphere/Box/Capsule/Mesh;
- Rigidbody enabled flag;
- mass/useGravity where Rigidbody is enabled;
- Project and Library discovery/reference support;
- basic Object3D editor/Inspector.

No Machine instances or Player instantiation in this PR.

#### PR A2 — Machine Object3D instances

Add Machine-owned object instances:

- stable instance ID;
- display name;
- Object3D `AssetReference`;
- position;
- rotation;
- scale.

Add `MachineObjectKind.Object` and `object:<id>` references.

Expose basic Machine Details/Hierarchy editing. Keep the authoritative state on Machine. Do not create script APIs yet.

#### PR A3 — Runtime build contract

Implemented: Machine builds resolve Project/Library Object3D dependencies into GUID-keyed reusable runtime packages and export Machine-owned instance declarations. Machine runtime schema 7 and Object3D runtime schema 1 are read by the Player without instantiating Unity objects.

Repeated references are deduplicated by authored Object3D GUID; identity collisions with conflicting definitions fail the build.

#### PR A4 — Player Object3D instantiation

Implemented: Player loads each referenced Object3D definition once per Machine session and instantiates Machine object instances with authored placement transforms, separate intrinsic scale/up-axis conversion, configured physics, and stable runtime registration.

Primitive/Mesh colliders and Rigidbody defaults are configured from Object3D runtime schema 1. Mesh collision requires exactly one usable model mesh.

Each live instance is registered in `RuntimeMachine` under its stable `object:<id>` identity.

Pool milestone: 16 authored balls can appear physically on the loaded pool table with working Unity physics, without scripting.

#### PR A5 — Assembled Machine 3D composition view

Implemented: the Machine Composition tab projects authored assets directly into a Helix viewport showing:

- Cabinet;
- mounted Faces where practical;
- Object3D instances;
- optional Cabinet collider/trigger diagnostics.

It supports instance-ID selection, shared Details/Composition numeric transform editing, transient semantic preview filters, missing-reference diagnostics, assembled bounds camera reset, and per-document Object3D definition/model reuse. It reuses the Machine command history and does not create a second authoritative scene representation.

Translation/rotation/scale manipulators, Object3D collider diagnostics, and mounted Face rendering remain focused follow-ups; numeric editing is the A5 transform interaction.

#### PR A6 — Machine anchors

Implemented: generic Machine-owned spatial anchors provide:

- stable ID;
- display name;
- position;
- rotation.

Runtime references use `anchor:<id>` or an equivalently explicit typed form.

Examples:

- pool rack positions;
- cue-ball start;
- tray slots;
- mole up/down positions;
- spawn/drop locations.

Anchors have no persisted rendering or physics semantics. Details and Composition share command-backed rows; Composition adds transient oriented diagnostics, selection, and visibility. Machine authored schema 6 and runtime schema 8 are current, with lightweight Player registry values and no anchor GameObjects.

#### PR A7 — Runtime event/command boundary

Before choosing a scripting language, define and implement the engine-neutral runtime API that behaviour will consume.

Initial events:

- machine started;
- input pressed/released;
- trigger entered/exited;
- collision entered/exited;
- timer elapsed.

Initial object commands:

- set/teleport transform;
- set active;
- set/clear velocity;
- set/clear angular velocity;
- apply impulse;
- reset to authored state;
- start/stop timer.

Do not expose arbitrary Unity APIs.

#### PR A8 — Behaviour/scripting implementation

Choose the first scripting/behaviour implementation only after A7 is proven.

Script references use stable Oasis IDs, never Unity/GameObject names.

Initial pool behaviour should cover:

- pocket entry;
- moving collected balls to tray positions;
- reracking;
- new-game input.

Then validate the same runtime API with Whac-A-Mole.

## Active track B — Installation assets

Installation remains a future composition layer above independently playable Machines:

- Machine instances;
- transforms/layout;
- link topology;
- shared assemblies where required.

Do not let Installation work complicate the standalone Machine workflow.

## Active track C — First video-machine vertical slice

After the dynamic-object track is stable, use a concrete video/JAMMA cabinet to decide whether Face should generalize into a broader Surface type.

Do not generalize Face from theory alone.

## Deliberately deferred

Do not implement until a real workflow requires them:

- generic asset inheritance/variants;
- online package marketplace;
- universal Device schema;
- arbitrary graph rewiring;
- compound/automatic collider decomposition;
- generic Unity-component scripting access;
- pinball-specific architecture;
- backwards-compatibility loaders/migrations.

## Task guidance

Future Codex tasks should normally read:

1. `00_OVERVIEW.md`;
2. the one or two specialist architecture docs relevant to the task;
3. this active roadmap only when sequencing matters.

Do not ask Codex to read deleted historical phase documents or reconstruct obsolete migration paths.
