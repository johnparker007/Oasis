# Dynamic Object3D, Behaviour and Scripting Architecture

## Goal

Support physical arcade games authored entirely through Oasis Editor without turning Cabinet GLBs or Unity GameObjects into the gameplay API.

The first proving case is a pool table. The second is Whac-A-Mole.

The architecture separates:

- reusable physical object definition;
- Machine-owned object instances and spatial composition;
- Player runtime instances;
- generic events/commands;
- later scripting/behaviour syntax.

## Editor composition projection

The Machine **Composition** tab is an authoring-time projection of the selected Cabinet and Machine-owned Object3D instances. It resolves Project/Library manifests and package-local GLBs directly, caches repeated Object3D definitions for that Machine view, and applies the same intrinsic up-axis/model-scale correction beneath the authoritative Machine placement as the Player.

Viewport camera, selection, derived bounds, diagnostics, and Cabinet semantic visibility filters are transient. Numeric transform editing uses the existing Machine instance command path, so Details, dirty state, and undo/redo remain synchronized. No viewport scene representation is persisted.

Scripting is deliberately last. It consumes a stable Oasis runtime object model rather than defining that model.

## Authored behaviour (A8.2)

A Machine now owns zero or one Oasis Script behaviour. The authored schema 7 manifest
contains only `{ "kind": "OasisScript", "source": "behavior.oasis" }`; source is a
separate canonical file in the Machine package and is never an external or reusable
Project/Library asset. The Machine Behaviour tab permits temporary syntax/type/reference
errors, reports their source line and column, and folds source changes into the Machine's
normal dirty/save/close lifecycle. Add and Remove are document commands; ordinary typing
uses the text control's local undo rather than flooding global history.

Machine-aware reference validation is an Editor/domain concern layered after successful
pure-language compilation. It walks the complete validated syntax tree and resolves
Object3D instances, anchors, Cabinet semantic triggers, Machine inputs, and existing
numeric lamp/reel/alpha/seven-segment identities through Machine composition. It does not
treat event bindings as authored references. Player packaging and execution remain A8.3;
runtime builds currently fail explicitly rather than silently discarding behaviour.

## Ownership model

### Cabinet

Cabinet owns reusable fixed physical structure:

- visible cabinet/table geometry;
- `OasisFace_*` targets;
- `OasisCollider_*` fixed collision geometry;
- `OasisTrigger_*` fixed trigger geometry;
- reusable Cabinet intrinsic settings.

Pool examples:

- cloth/table body;
- cushions;
- playing-surface collision;
- pocket trigger volumes.

Cabinet does not own the 16 current-game ball instances.

### Object3D asset

Object3D is a reusable physical 3D object definition.

Examples:

- pool ball;
- mole;
- puck;
- pinball;
- prize ball;
- movable door/lever where a reusable physical asset is useful.

Object3D owns intrinsic facts that normally remain true wherever the object is used:

- model GLB;
- model scale/up-axis where required;
- collider shape/defaults;
- Rigidbody default;
- mass;
- gravity setting.

Do not put Machine placement or gameplay identity on Object3D.

### Machine Object3D instance

Machine owns the fact that a particular reusable object exists in this playable composition.

Each instance owns:

- stable instance ID;
- display name;
- Object3D asset reference;
- position;
- rotation;
- scale;
- later: narrowly justified per-instance overrides.

Examples:

- `object:cueBall`;
- `object:ball01`;
- `object:ball15`;
- `object:mole3`.

### Machine anchor

A Machine anchor is a named transform with no rendering/physics behaviour.

It owns:

- stable ID;
- display name;
- position;
- rotation.

Examples:

- `anchor:rackCueBall`;
- `anchor:rackBall01`;
- `anchor:traySlot01`;
- `anchor:mole1Up`;
- `anchor:mole1Down`.

Anchors keep spatial authoring in the Editor instead of forcing scripts to contain layout mathematics.

The settled A6 contract is Machine-owned, case-sensitive within the anchor namespace, and uses the same conservative letters/digits/underscore/hyphen ID syntax as Object3D instances. Its canonical typed reference is only `anchor:<id>`. Position and XYZ Euler rotation are expressed directly in Machine composition space; there is no scale and no Object3D `modelScale`/`upAxis` correction. The generated schema-8 declaration is registered as lightweight `RuntimeAnchor` data in `RuntimeMachine`; no Unity GameObject or Transform is created merely to represent an anchor.

### Behaviour/script

Machine behaviour owns game rules and state transitions.

It refers only to stable Oasis identities such as:

- `object:ball08`;
- `trigger:PocketLeftMiddle`;
- `input:rerack`;
- `anchor:traySlot03`.

It must not depend on Unity scene paths or imported names such as `Sphere.017`.

## Proposed authored Object3D schema

Initial shape:

```json
{
  "schemaVersion": 1,
  "id": "<guid>",
  "displayName": "Pool Ball 8",
  "model": {
    "path": "ball8.glb",
    "scale": 1.0,
    "upAxis": "Y"
  },
  "physics": {
    "collider": {
      "kind": "Sphere",
      "radius": 0.028575,
      "center": [0, 0, 0]
    },
    "rigidbody": {
      "enabled": true,
      "mass": 0.17,
      "useGravity": true
    }
  }
}
```

This is illustrative; implementation should follow current Oasis serialization conventions.

Initial collider kinds:

- `None`;
- `Sphere`;
- `Box`;
- `Capsule`;
- `Mesh`.

Do not add compound colliders, joints, constraints, physics materials or automatic decomposition in the first Object3D schema.

For a pool ball, use a SphereCollider rather than a MeshCollider.

## Proposed Machine authored shape

Machine gains:

```text
ObjectInstances[]
  Id
  DisplayName
  ObjectAsset : AssetReference
  Transform
    Position
    Rotation
    Scale

Anchors[]
  Id
  DisplayName
  Position
  Rotation
```

Example:

```text
Machine: Pool

Cabinet
  PoolTable

Objects
  cueBall -> PoolBallCue
  ball01  -> PoolBall01
  ...
  ball15  -> PoolBall15

Anchors
  rackCueBall
  rackBall01
  ...
  traySlot01
  ...
```

Object instance IDs and anchor IDs must be unique within a Machine.

## MachineObjectReference

Extend the current typed reference system rather than creating a parallel string-ID mechanism.

Add at least:

```text
MachineObjectKind.Object
object:<id>
```

When trigger scripting is introduced, provide an explicit typed trigger reference, for example:

```text
trigger:<id>
```

The implementation may use `MachineObjectReference` or a small adjacent typed reference if that produces a cleaner ownership model, but there must be one canonical authored/runtime identity for each concept.

Do not expose raw GLB node names as general-purpose script object references.

## Cabinet trigger identity

Cabinet GLB already provides semantic triggers through:

```text
OasisTrigger_<name>
```

At runtime these should be registered under a stable Oasis trigger identity derived deterministically from the semantic name.

Example:

```text
OasisTrigger_PocketLeftMiddle
    -> trigger:PocketLeftMiddle
```

The Cabinet loader still owns the Unity Collider. The runtime registry supplies the logical identity used by behaviour.

Do not duplicate trigger mesh geometry into Machine manifests.

## Generated runtime package

Proposed package shape after Object3D runtime export:

```text
machine.runtime.json

cabinet/
  cabinet.runtime.json
  cabinet.glb

objects/
  <object-asset-id-or-stable-package-name>/
    object.runtime.json
    object.glb

faces/
  ...
```

Repeated Machine instances referencing one Object3D asset should not require duplicate packaged asset definitions.

The Machine runtime manifest contains instance declarations because placement and instance identity belong to Machine, not to the reusable Object3D GLB.

Generated instance (Machine runtime schema 8) preserves authoring coordinates without conversion; rotation remains XYZ Euler degrees for the documented Unity conversion in A4:

```json
{
  "id": "ball08",
  "definitionId": "<object-guid>",
  "definitionManifest": "objects/<object-guid>/object.runtime.json",
  "transform": {
    "position": { "x": 0.1, "y": 0.8, "z": -0.2 },
    "rotationEulerDegrees": { "x": 0, "y": 0, "z": 0 },
    "scale": { "x": 1, "y": 1, "z": 1 }
  }
}
```

Definitions use `oasis.object3d.runtime` schema version 1 and contain the generated model filename, intrinsic model scale/up-axis, shape-specific collider values, and enabled/mass/useGravity Rigidbody values. The Player reader supports only Machine runtime schema 8 and retains the resolved definitions for A4.

## Player runtime model

Introduce a small runtime representation such as:

```text
RuntimeObjectInstance
  Id
  Root GameObject
  Collider
  Rigidbody?
  AuthoredInitialTransform
```

`RuntimeMachine` owns a registry keyed by stable Object identity.

Required operations should include:

- register;
- resolve by ID/reference;
- enumerate when required;
- reset/unload cleanly.

Do not make external behaviour code use `GameObject.Find`.

Object loading flow:

```text
Machine build loaded
  -> Cabinet instantiated
  -> Cabinet semantic colliders/triggers configured and triggers registered
  -> Object3D runtime assets resolved
  -> Machine object instances instantiated
  -> collider/Rigidbody defaults configured
  -> instances registered in RuntimeMachine
  -> Faces/devices/renderers initialized
  -> behaviour starts
```

The exact order can follow current Player requirements as long as behaviour starts only after referenced objects/triggers/inputs are available.

## Physics policy

Object3D physics is authored as reusable defaults.

Initial pool ball:

- SphereCollider;
- dynamic Rigidbody;
- gravity enabled;
- authored mass.

Static Cabinet collision remains Cabinet semantic geometry.

Do not add Rigidbody components to Cabinet merely because dynamic objects exist.

Script-controlled objects such as Whac-A-Mole may use:

- no Rigidbody;
- kinematic Rigidbody;
- or later a more explicit motion mode if a real requirement proves necessary.

Do not predesign a universal physics-body taxonomy in Object3D v1.

## Machine assembled viewport

Long-term, Machine is the natural assembled 3D composition view.

It should show:

- selected Cabinet;
- Object3D instances;
- mounted/rendered Machine content where practical;
- optional semantic Collider/Trigger overlays.

The authoritative data remains the Machine/Cabinet/Object3D assets, not the viewport scene.

Initial object editing should support:

- hierarchy/list selection;
- Object3D asset choice;
- numeric position/rotation/scale;
- simple 3D selection/gizmo when practical.

Do not block the Object3D runtime pipeline on a sophisticated scene editor.

## Runtime event boundary

Define the runtime event API before choosing a scripting language.

Initial generic events:

```text
MachineStarted

InputPressed(input)
InputReleased(input)

TriggerEntered(trigger, object)
TriggerExited(trigger, object)

CollisionEntered(object, other)
CollisionExited(object, other)

TimerElapsed(timer)
```

Events should carry stable Oasis references/IDs.

Do not expose Unity `Collider`, `Rigidbody` or `GameObject` as the authored script contract.

### Settled A7 event contract

The event types are `RuntimeMachineStartedEvent`, `RuntimeInputPressedEvent`, `RuntimeInputReleasedEvent`, `RuntimeTriggerEnteredEvent`, `RuntimeTriggerExitedEvent`, `RuntimeCollisionEnteredEvent`, `RuntimeCollisionExitedEvent`, and `RuntimeTimerElapsedEvent`. `RuntimeMachine.Events` delivers them synchronously in subscription order. Subscriptions can be removed, are cleared at unload, and exceptions propagate to the future behaviour host.

`MachineStarted` is emitted once, after Cabinet triggers, Object3D instances, anchors, declared inputs, Faces/devices, renderers, and runtime drivers have been initialized. Trigger payloads contain the raw IDs represented canonically by `trigger:<id>` and `object:<id>`. Classification retains node-name precedence over mesh name, and the semantic name that wins classification supplies the trigger ID.

Collision events initially cover only registered Object3D-to-Object3D interactions and are directional: Unity may publish one event from each participant's perspective. Unregistered objects and static Cabinet geometry publish no collision event and no fake `collider:` identity.

## Runtime command boundary

Initial generic commands:

```text
SetActive(object, bool)
Teleport(object, anchor-or-transform)
SetVelocity(object, vector)
SetAngularVelocity(object, vector)
ApplyImpulse(object, vector)
ResetObject(object)
StartTimer(id, duration)
StopTimer(id)
```

`Teleport` and `ResetObject` should own correct Unity physics synchronization rather than requiring scripts to manipulate Transform and Rigidbody independently.

The API can expand when real machines require additional operations.

### Settled A7 command and state contract

`RuntimeMachine.Commands` implements `SetActive`, `Teleport(object, anchor)`, `Teleport(object, RuntimePose)`, `SetVelocity`, `SetAngularVelocity`, `ApplyImpulse`, `ResetObject`, `StartTimer`, and `StopTimer`. `RuntimeVector3` and `RuntimePose` are the behaviour-facing values; conversion to Unity vectors/quaternions is internal.

Teleport targets the authoritative root in Machine space, moves its root Rigidbody when present, synchronizes transforms, and deliberately preserves both velocities. It never reapplies Object3D model scale or up-axis correction. Velocity commands require the root Rigidbody; impulse uses Unity impulse mode in Machine/Unity units. Reset reuses the same registered instance and definition, reactivates it, restores authored position/rotation/scale, and clears linear/angular velocity.

The runtime registers existing Machine input declarations by the ID in `input:<id>`. `SetInputState` emits only actual pressed/released transitions and rejects unknown IDs. Named timers use the same conservative letters/digits/underscore/hyphen ID rule, reject non-finite or negative duration, replace an existing timer on start, are harmless to stop when absent, publish once and remove themselves on elapsed, and are cleared on unload. Timer logic exposes deterministic `Advance(deltaSeconds)`; the play-mode adapter uses scaled `Time.deltaTime`.

No authored behaviour schema, scripting language, arbitrary component/property mutation, or Unity API access is part of A7. Those choices remain deferred to A8.

## Pool vertical slice

### Composition

```text
Cabinet:
  PoolTable

Objects:
  cueBall
  ball01..ball15

Cabinet triggers:
  six pocket triggers

Machine inputs:
  rerack
  newGame
```

### Initial state

Machine-authored positions can initially be the rack/start positions.

Later anchors allow alternate rack/tray/spawn positions without embedding coordinates in script.

### Pocket behaviour

Conceptually:

```text
TriggerEntered(pocket, ball)
  -> choose next tray slot
  -> Teleport(ball, traySlot)
  -> clear velocity/angular velocity
```

### Rerack

Conceptually:

```text
InputPressed(rerack)
  -> Teleport each ball to rack anchor
  -> reset velocity/angular velocity
  -> activate all balls
```

The runtime should not contain pool-specific APIs. Pool is authored behaviour using generic object/trigger/input/anchor capabilities.

## Whac-A-Mole validation

Use Whac-A-Mole after the pool object/runtime foundation to verify the model is not physics-ball-specific.

Example:

- one reusable Mole Object3D;
- several Machine instances;
- up/down anchors;
- hit/click/input events;
- timers;
- score/state in behaviour;
- script-controlled movement.

If Whac-A-Mole requires a motion primitive not covered by pool, add the smallest generic runtime capability justified by that implementation.

## Scripting language boundary

Do not choose or implement a scripting syntax before the runtime object/event/command API exists.

Potential scripting implementations can be evaluated later against these requirements:

- sandboxed;
- deterministic enough for arcade-machine behaviour;
- clear errors with Machine object IDs;
- easy Editor integration;
- serializable source/assets;
- able to subscribe to Oasis events and call only approved Oasis commands;
- no arbitrary Unity/.NET access by default.

The first script implementation should be deliberately small and prove pool + Whac-A-Mole workflows before broader language features are added.

## Explicit non-goals for the initial track

Do not add:

- raw Unity API scripting;
- generic scene graph asset;
- Object3D inheritance/variants;
- automatic mesh simplification/decomposition;
- networking/multiplayer;
- save-game framework;
- generic ECS;
- universal Device/Object superclass;
- pinball-specific systems;
- backward-compatible readers for earlier experimental schemas.

## PR boundaries

Implement in separate reviewable PRs:

1. Object3D asset foundation.
2. Machine Object3D instances and stable references.
3. Runtime build/export contract.
4. Player Object3D instantiation and registry.
5. Assembled Machine viewport.
6. Machine anchors.
7. Runtime event/command API.
8. Minimal behaviour/scripting implementation.
9. Pool behaviour vertical slice and Whac-A-Mole validation as separate follow-ups where useful.

Each PR should remove superseded code rather than preserving transitional formats.
