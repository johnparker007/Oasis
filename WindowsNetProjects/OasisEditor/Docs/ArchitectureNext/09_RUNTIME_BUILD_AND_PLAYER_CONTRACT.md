# Runtime Build and Oasis Player Contract

## Build root

Machine is the standalone playable build root.

Future Installation builds may compose multiple Machines, but standalone Machine must remain independently buildable.

## Explicit dependency traversal

Machine build walks explicit references such as:

```text
Machine
  -> RuntimeDefinition
  -> Cabinet
  -> assigned Faces
  -> Reel assets
  -> Object3D assets
```

Never enumerate all project assets to guess what belongs to a Machine.

## Current Cabinet contract

Generated builds contain the Cabinet runtime manifest and Cabinet GLB.

Cabinet semantic geometry remains authoritative in the GLB:

- Face targets;
- fixed Colliders;
- fixed Triggers.

Do not duplicate semantic mesh geometry into runtime manifests.

## Dynamic Object3D contract

Object3D differs from Cabinet semantic geometry because Machine instance identity and placement are Machine-owned.

Machine runtime schema 9 contains Machine-owned Object3D instance declarations. Each declaration preserves its authored ID/display name and position, Euler rotation in degrees, and placement scale, and points at a generated definition manifest rather than an authoring path.

Reusable definitions are emitted once under `objects/<object-guid>/` with `object.runtime.json` (schema `oasis.object3d.runtime`, version 1) and `object.glb`. The lower-case canonical authored Object3D GUID is the deterministic package/definition identity. Definitions retain model scale/up-axis and the minimal collider/Rigidbody contract; generated paths are package-relative. Conflicting resolved definitions claiming the same GUID fail the build.

The Player validates the typed definitions, loads each referenced GLB definition once per Machine session, and instantiates every Machine occurrence from that shared import. A live instance has an authoritative physical placement root under `RuntimeMachine/Objects`; `RuntimeObjectInstance.Root.transform` is both the current live transform and the transform Unity physics moves. When enabled, the Rigidbody is attached directly to that root. Intrinsic model scale and up-axis conversion live on a `ModelCorrection` child and therefore cannot become a second independently moving physical root. Y-up is unchanged, Z-up rotates -90 degrees around X, and X-up rotates +90 degrees around Z.

Object3D primitive collider coordinates are authored in source-model coordinates. A `PhysicsCorrection` child receives exactly the same uniform model scale and up-axis rotation as `ModelCorrection`, so primitive center, dimensions and orientation remain aligned without baking or approximating the conversion. Its collider is part of the ancestor root Rigidbody's compound body. A Mesh collider remains on the sole usable rendered mesh, preserving the imported node transforms, and fails clearly when the model has zero or multiple usable meshes. A Mesh collider is made convex when its definition also enables a dynamic Rigidbody, as required by Unity physics; Oasis does not perform convex decomposition. Colliders may therefore be on the root or descendants, but a live Object3D Rigidbody is always on the registered root.

`RuntimeMachine` registers live `RuntimeObjectInstance` values by raw Machine instance ID (the Object3D domain represented canonically as `object:<id>`). Each value retains identity/display name, its resolved definition, authoritative live root GameObject, optional Collider/root Rigidbody, and authored initial-transform data that physics does not mutate. Unload destroys all instance roots, clears the registry, and releases the per-load glTF imports. Object3D GLBs remain visible and are not processed using Cabinet semantic-name conventions.

See `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.

Machine runtime schema 9 also exports lightweight `anchors[]` declarations with raw stable ID, display name, Machine-space position, and `rotationEulerDegrees`. Anchors have no scale, asset, physics, or Editor-only viewport state. The Player validates and registers them as `RuntimeAnchor` values before content setup; it does not create Unity GameObjects to store them.

## Diagnostics

Broken referenced dependencies should identify composition context rather than only low-level filesystem errors.

## Runtime state boundary

Runtime packages preserve the distinction between:

- reusable physical definitions;
- Machine composition;
- dynamic live state.

Authoring-only provenance is omitted unless Player genuinely requires it.

## Compatibility

There is no backwards-compatibility requirement.

When runtime shapes change, update current writer/reader/tests and delete superseded format code.

## Native Oasis runtime and source package

Current versions are Machine authored **8**, Machine runtime **9**, Cabinet runtime
**5**, and Object3D runtime **1**. Writer and reader support latest only.

Schema 9 projects runtime as an explicit union, without irrelevant Emulation fields
for Oasis:

```json
{"runtime":{"kind":"Emulation","platform":"MPU5","platformSettingsJson":"..."}}
```

```json
{"runtime":{"kind":"Oasis","behavior":{"kind":"OasisScript","source":"behavior/behavior.oasis"}}}
```

The generated Machine package contains `machine.runtime.json`, existing `cabinet/`,
`objects/`, and `faces/` content, and exactly one `behavior/behavior.oasis` for Oasis.
Source is UTF-8 text, never embedded JSON source, bytecode, or a serialized AST.
Emulation packages contain no behaviour directory. No authored Project/package
absolute paths enter the generated runtime definition.

Build/preview entry points read the persisted Machine manifest. Before copying the
persisted `behavior.oasis`, the build validates the Runtime/Behaviour combination,
requires the sidecar, runs `OasisScriptCompiler`, reconstructs the current composition
reference index, and runs `OasisScriptMachineValidator`. Every error fails the build
with Machine/source context and code/line/column when available. The same source
string that passed validation is written into staging. Existing staging/final
replacement removes stale script output when rebuilding as Emulation; failed builds
retain the last successful final package.

`RuntimeBuildLoader` accepts only schema 9 and validates explicit runtime kind.
Oasis requires the canonical package-relative source, lexical package containment,
file existence, and successful canonical compiler output. `ResolvedRuntimeBuild.ScriptProgram`
is compiled once at Machine load and ready for `OasisScriptSession(program, host)`.
The loader creates no host, subscriptions, gameplay state or command calls.
`MachinePreviewLoader` supports both Oasis and Emulation through the same content
load/unload path. Every load first unloads the previous session. For Oasis, after
Cabinet and Object3Ds are loaded, anchors/inputs/semantic triggers registered, and
Faces/devices/renderers/drivers initialized, it attaches `RuntimeOasisScriptBehavior`
with the required `ScriptProgram`. The adapter subscribes before the sole
`RuntimeMachine.CompleteStartup()` call. Emulation creates no script session.

## Player execution adapter (A8.4 implemented)

`RuntimeMachineOasisScriptHost` delegates the nine approved host operations to A7:
SetActive, anchor Teleport, direct-pose Teleport, SetVelocity, SetAngularVelocity,
ApplyImpulse, ResetObject, StartTimer and StopTimer. It checks ObjectRef/AnchorRef
domains using the value's Type and extracts raw IDs, converts finite Vec3 doubles
to `RuntimeVector3` floats and builds Machine-space `RuntimePose` values with XYZ
Euler degrees. Numeric overflow and malformed/null values return host failures.
Normal A7 registry/command failures retain their domain messages; unexpected
exceptions include command and exception context. The host manipulates no Unity
object or physics component and does no composition-aware revalidation.

`RuntimeOasisScriptBehavior` translates all eight A7 events to the corresponding
canonical factories: MachineStarted, InputPressed/Released (InputRef),
TriggerEntered/Exited (TriggerRef, ObjectRef), CollisionEntered/Exited (ObjectRef,
ObjectRef in A7 directional order), and TimerElapsed (String). Dispatch completes
synchronously before the A7 publisher continues; matching handlers retain source
order. Existing trigger/collision relays are reused without duplicate components.

`timer.start/stop` use A7 named one-shot timers. `RuntimeBehaviorDriver` advances
them with scaled `Time.deltaTime`; `RuntimeTimerElapsedEvent` flows back through
the adapter into `timer.elapsed`. There is no second timer service or event queue.
Logical `RuntimeMachine.SetInputState` drives pressed/released handlers directly;
A8.5 adds configurable development keyboard bindings; production input authoring remains deferred.

Global initialization faults abort startup before CompleteStartup, report once,
and enter the loader's normal cleanup path. A handler fault, including a fault in
machine.started, leaves the Machine loaded but disables further script dispatch.
The first diagnostic is logged with Machine name (ID fallback), source path,
code, line, column, event name and message. Completed command effects are retained.
`MachinePreviewLoader.HasOasisBehavior` and `OasisBehaviorFault` provide read-only
inspection without exposing the interpreter session.

RuntimeMachine owns a generic disposable behaviour attachment. Unload disposes it
before clearing timers/events and destroying assets. The adapter unsubscribes,
detaches its host and drops session/host/Machine/reporter references; already
snapshotted callbacks also skip disposed sessions. Repeated loads create fresh
state. No schema or serialized shapes change: authored Machine 8, runtime Machine
9, Object3D 1 and Cabinet 5. A8.5 supplies reusable Pool gameplay in `Examples/Pool/behavior.oasis`; actual asset wiring remains local.

## Canonical scripting assembly

`WindowsNetProjects/OasisEditor/Oasis.Scripting/Package/Runtime/` is the one canonical
compiler/interpreter source tree. The existing `Oasis.Scripting.csproj` compiles it
recursively as `netstandard2.1`, C# 9. Unity 6 consumes `Package/` through the local UPM
entry `com.oasis.scripting` at
`file:../../../WindowsNetProjects/OasisEditor/Oasis.Scripting/Package`, relative to
Player's `Packages/manifest.json`. `Runtime/Oasis.Scripting.asmdef` names the same
assembly and sets `noEngineReferences: true`. The package has no .NET build output
or Editor tests; `bin`/`obj` stay outside its root. Source changes are versioned in
this repository and immediately consumed by both projects, with no copy/generation
step and no second parser. Player/EditMode verification asserts the resolved package
path, assembly identity, and absence of copied compiler/session source under Assets.

## Manual verification

1. Open an existing current-format Machine and select Oasis runtime; confirm
   Platform/ROM controls disappear and Behaviour owns `behavior.oasis`.
2. Edit source, save, reopen, and confirm runtime/source preservation. Switch to
   Emulation, cancel/confirm removal, and exercise undo/redo before saving.
3. Build the saved Oasis Machine. Inspect `machine.runtime.json`: schema 9, kind
   Oasis, source `behavior/behavior.oasis`, no absolute Project path. Confirm exactly
   one script file and unchanged Cabinet/Object3D/Face packages.
4. Save invalid script or an unresolved Machine reference externally in the package;
   confirm a build fails with source diagnostics and preserves the last good output.
   Remove the sidecar and confirm the explicit missing-source error.
5. Build an ordinary Emulation Machine and confirm existing settings, schema 9,
   and no behaviour source. Rebuild a previously scripted package as Emulation and
   confirm stale generated behaviour disappears.
6. Run full `Oasis.Scripting.Tests` and `OasisEditor.Tests` on the Windows/.NET 9
   toolchain. Open Player in its configured Unity 6 editor and run all EditMode tests.
7. Open the Pool Machine, select Oasis, and add a simple `on machine.started()`
   handler that moves one declared object to an existing anchor. Build and load in
   Player; confirm the temporary A8.4 guard is gone and startup executes once.
8. Add a temporary input/trigger handler to move one ball and clear both velocities.
   Drive logical `SetInputState` or an existing Cabinet trigger; inspect the live
   authoritative root/Rigidbody. No production Pool script is supplied by A8.4.
9. Start a script timer and confirm `timer.elapsed` fires through the A7 driver.
10. In a temporary generated package, deliberately use a missing runtime object
    (normal Editor/build validation rejects this). Confirm one source/code/line/
    column/event diagnostic and no repeated errors on further events; Machine stays
    loaded. A global `1 / 0` initialization fault should instead fail/clean the load.
11. Reload and confirm script state resets; then load an Emulation Machine and
    confirm existing behaviour and absence of a script attachment.

## A8.5 Pool setup and development input

See [Pool setup/manual checklist](../../Examples/Pool/README.md) for exact sample
object/pocket/anchor/input mappings and authored versus generated source placement.
No actual Pool asset package was available or modified. The canonical script is
linked into .NET test output and read directly from the repository by Unity tests.
Automated coverage has been added but suites/manual verification remain unexecuted.

Editor/development Machine preview creates RuntimeInputDevelopmentControls with
empty Inspector bindings. Set R/rerack and N/newGame on the loaded session root.
Held-key transitions use A7 SetInputState; multiple keys aggregate per input.
Disable, focus loss and binding replacement release held inputs; inactive/unloaded
Machines are detached without dispatch. Reload destroys session settings, so reapply
bindings. No serialized Machine format, production input UI or hard-coded game IDs
are introduced. Existing A8.4 adapter and fault/unload semantics are unchanged.
