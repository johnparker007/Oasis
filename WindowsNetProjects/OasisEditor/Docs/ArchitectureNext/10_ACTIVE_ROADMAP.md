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
- Runtime abstraction exists with the current `Emulation` and native `Oasis` implementations.
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

Implemented: Machine builds resolve Project/Library Object3D dependencies into GUID-keyed reusable runtime packages and export Machine-owned instance declarations. Machine runtime schema 9 and Object3D runtime schema 1 are read by the Player without instantiating Unity objects.

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

Anchors have no persisted rendering or physics semantics. Details and Composition share command-backed rows; Composition adds transient oriented diagnostics, selection, and visibility. Machine authored schema 8 and runtime schema 9 are current, with lightweight Player registry values and no anchor GameObjects.

#### PR A7 — Runtime event/command boundary

Implemented: the Player now owns a Machine-session, engine-neutral runtime API that behaviour will consume. It provides typed, synchronously dispatched events; stable-ID trigger/object/input/anchor registries; logical input transitions; deterministic one-shot timers; and focused Object3D commands. Unity physics callbacks and values are adapted behind this boundary.

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

#### A8 — Oasis Script staged implementation

- **A8.1 Oasis Script core (implemented):** pure managed lexer, parser, immutable AST,
  semantic/type validation, declarative event and built-in signatures, and a validated
  in-memory program model. There is no Machine, Editor, or Player integration.
- **A8.2 Machine authoring (implemented):** one optional Machine-owned Oasis Script
  behaviour, stored as package-local `behavior.oasis`, with plain-text editing,
  unified diagnostics, Machine-reference validation, dirty/save/Save-As, and
  structural undo/redo. Invalid source may remain in the editor while typing.
- **A8.3 Runtime packaging/interpreter (implemented):** native Oasis runtime kind,
  authored schema 8/runtime schema 9, validated package-local source, canonical UPM
  sharing with Unity, and pure typed sessions with deterministic budgets and faults.
  Player loading compiles source once through the canonical package.
- **A8.4 A7 host adapter (implemented):** Player-side RuntimeMachineOasisScriptHost
  delegates every approved command with typed reference/vector conversion and host
  error results. RuntimeOasisScriptBehavior synchronously maps all eight A7 events,
  attaches before CompleteStartup, feeds back A7 timers, logs the first fault, and
  disposes on unload. Initialization faults fail/clean the load; handler faults keep
  the Machine loaded with dispatch disabled. Emulation has no script session.
  Small live-object tests cover Pool trigger and Whac-A-Mole timer flows without
  production game content. Schemas remain authored Machine 8, runtime Machine 9,
  Object3D 1 and Cabinet 5.
- **A8.5 Pool vertical slice:** implement pocket, tray, rerack, and new-game behaviour.
- **A8.6 fruit-device scripting boundary:** add only the lamp/reel/display commands
  proven by a real fruit-machine workflow.
- **A8.7 Whac-A-Mole validation:** validate the generic language/runtime boundary with
  timer-driven mole behaviour.

Stages after A8.4 remain planned. Script references use stable Oasis IDs, never
Unity/GameObject names. A8.4 completes generic runtime execution; A8.5 owns the
first production Pool behaviour, including scoring, tray selection, rerack/new-game
and cue controls when designed. Input binding UI, random and device commands are
not part of A8.4. No Emulation/OasisScript hybrid runtime is supported.

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
