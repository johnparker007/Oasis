# Runtime Model

## Principle

A Machine is playable regardless of how its behaviour is implemented.

Physical composition and stable Machine object identities should not depend on an emulator-specific backend.

## Current implementation

Machine owns a typed `RuntimeDefinition`.

The currently implemented runtime kind is:

- `EmulationRuntimeDefinition`.

Platform/ROM settings are Machine-owned and edited from the Machine rather than Project Settings.

## Future behaviour

Physical games such as pool and Whac-A-Mole require scripted/physics behaviour.

Do not introduce `ScriptedRuntimeDefinition`, `PhysicsRuntimeDefinition` or `HybridRuntimeDefinition` merely to satisfy names predicted by old plans.

First establish:

- Object3D assets and Machine instances;
- Player runtime object registry;
- trigger/object/input identities;
- runtime event API;
- runtime command API.

Then choose the smallest runtime/behaviour representation that the concrete pool and Whac-A-Mole workflows require.

## Runtime-to-presentation boundary

Where practical, presentation consumes stable Oasis runtime state/references rather than knowing which backend produced them.

Existing examples include logical Lamp/Reel/Display/Input references.

The Object3D track extends this principle to designer-addressable physical objects and triggers.

## Script boundary

Future scripts must call approved Oasis APIs against stable references.

Do not expose arbitrary Unity/`.NET` APIs or use raw `GameObject.Find` names as the authored contract.

The implemented A7 boundary is scoped to one loaded `RuntimeMachine`. Its typed event stream publishes `MachineStarted`, input pressed/released, trigger entered/exited, Object3D collision entered/exited, and timer elapsed events. Payloads contain only stable logical IDs; Unity `GameObject`, `Transform`, `Collider`, `Collision`, and `Rigidbody` values remain implementation details. Subscriber delivery is synchronous in subscription order, unsubscribe is explicit, and subscriber exceptions propagate rather than being hidden.

Cabinet semantics map the winning classified `OasisTrigger_<id>` node-or-mesh name to the sole canonical `trigger:<id>` domain. Initial collision events are directional Object3D-to-Object3D notifications: each registered object's Unity callback may publish its own `(objectId, otherObjectId)` event. Static Cabinet collisions still affect physics but emit no invented collider identity.

Declared `input:<id>` values become Machine-session runtime input state. `SetInputState(id, pressed)` publishes only false-to-true and true-to-false transitions. The command service resolves stable IDs for `SetActive`, anchor/direct-pose `Teleport`, linear/angular velocity, impulse, reset, and named timer operations. `RuntimeVector3` and `RuntimePose` keep command inputs Unity-independent.

Direct poses and anchors use Machine composition space at the authoritative Object3D root; intrinsic model scale/up-axis correction is not reapplied. Teleport preserves velocity. Reset reactivates the existing instance, restores authored position/rotation/scale, and clears both velocities without reloading its definition. Timers are named, one-shot, replace-on-start values advanced deterministically by `Advance`; the Unity driver supplies scaled `Time.deltaTime`. Unload clears timers, subscribers, input state, runtime registries, and callback reachability.

See `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.
