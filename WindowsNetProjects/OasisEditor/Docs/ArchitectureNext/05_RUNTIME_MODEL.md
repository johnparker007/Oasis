# Runtime Model

## Principle

A Machine is playable regardless of how its behaviour is implemented.

Physical composition and stable Machine object identities should not depend on an emulator-specific backend.

## Current implementation

Machine owns a typed `RuntimeDefinition`.

The current variants are:

- `EmulationRuntimeDefinition`: existing Machine-owned Platform/ROM/settings.
- `OasisRuntimeDefinition`: deliberately minimal `{ "kind": "Oasis" }`; Oasis owns
  the Machine behaviour directly, including native physical games such as Pool and
  Whac-A-Mole.

Authored Machine schema **8** supports only these combinations:

| Runtime | Authored Behaviour |
| --- | --- |
| Emulation | Must be absent |
| Oasis | Required: `{ "kind": "OasisScript", "source": "behavior.oasis" }` |

Runtime identifies who owns execution; Behaviour identifies the source/language.
Unknown kinds and unsupported combinations fail validation. There is no hybrid
Emulation + Oasis Script, migration, or previous-schema reader.

Machine Details selects Emulation or Oasis. Choosing Oasis (including Add Oasis
Script Behaviour) atomically creates the canonical declaration and a default source
if needed. Platform/ROM controls are hidden for Oasis. Switching to Emulation or
Remove Behaviour asks for confirmation, removes the declaration through one
undoable document mutation, and preserves the in-memory source for undo. Saving
removes the authored sidecar; a later reopened Emulation Machine has no script.

The pure scripting host is an engine-neutral interface. A8.3 packages and compiles
source and implements the session interpreter; A8.4 connects it to A7 in Player.
`RuntimeMachineOasisScriptHost` implements `IOasisScriptHost` using only
`RuntimeMachine.Commands`. `RuntimeOasisScriptBehavior` owns one host/session and
subscribes to the Machine event stream. The dependency remains Player ->
Oasis.Scripting; the canonical script package has no Machine or Unity dependency.

Both runtime kinds use the same preview lifecycle. After Cabinet/Object3D loading,
anchor/input/trigger registration and Face/device/driver initialization, Oasis
requires `ResolvedRuntimeBuild.ScriptProgram` and attaches the behaviour before
`CompleteStartup()`. Only A7 emits `MachineStarted`, once. Emulation attaches no
script session. A generic `IDisposable` attachment on RuntimeMachine owns cleanup;
RuntimeMachine does not implement the script host or know interpreter types.

Initialization faults report one structured diagnostic and fail the load, which
cleans partial scene/assets. Any handler fault (including `machine.started`) leaves
the Machine loaded for inspection, disables further script dispatch, and logs once.
Unload disposes/unsubscribes the attachment before clearing events/timers and
destroying objects. Reload creates fresh script state. Schema versions remain
Machine authored 8/runtime 9, Object3D runtime 1 and Cabinet runtime 5.

## Runtime-to-presentation boundary

Where practical, presentation consumes stable Oasis runtime state/references rather than knowing which backend produced them.

Existing examples include logical Lamp/Reel/Display/Input references.

The Object3D track extends this principle to designer-addressable physical objects and triggers.

## Script boundary

Scripts call approved Oasis APIs against stable, typed references.

Do not expose arbitrary Unity/`.NET` APIs or use raw `GameObject.Find` names as the authored contract.

The implemented A7 boundary is scoped to one loaded `RuntimeMachine`. Its typed event stream publishes `MachineStarted`, input pressed/released, trigger entered/exited, Object3D collision entered/exited, and timer elapsed events. Payloads contain only stable logical IDs; Unity `GameObject`, `Transform`, `Collider`, `Collision`, and `Rigidbody` values remain implementation details. Subscriber delivery is synchronous in subscription order, unsubscribe is explicit, and subscriber exceptions propagate rather than being hidden.

Cabinet semantics map the winning classified `OasisTrigger_<id>` node-or-mesh name to the sole canonical `trigger:<id>` domain. Initial collision events are directional Object3D-to-Object3D notifications: each registered object's Unity callback may publish its own `(objectId, otherObjectId)` event. Static Cabinet collisions still affect physics but emit no invented collider identity.

Declared `input:<id>` values become Machine-session runtime input state. `SetInputState(id, pressed)` publishes only false-to-true and true-to-false transitions. The command service resolves stable IDs for `SetActive`, anchor/direct-pose `Teleport`, linear/angular velocity, impulse, reset, and named timer operations. `RuntimeVector3` and `RuntimePose` keep command inputs Unity-independent.

Direct poses and anchors use Machine composition space at the authoritative Object3D root; intrinsic model scale/up-axis correction is not reapplied. Teleport preserves velocity. Reset reactivates the existing instance, restores authored position/rotation/scale, and clears both velocities without reloading its definition. Timers are named, one-shot, replace-on-start values advanced deterministically by `Advance`; the Unity driver supplies scaled `Time.deltaTime`. Unload clears timers, subscribers, input state, runtime registries, and callback reachability.

See `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.
