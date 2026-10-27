# Runtime Build and Oasis Player Contract

> Current Machine authored/runtime schemas are 11/13; Prefab authored/runtime schemas are 2. The [Prefab delivery plan](14_PREFAB_AND_POOL_DELIVERY_PLAN.md) records the delivered runtime tree and lifecycle work. The [playable arcade plan](15_PLAYABLE_ARCADE.md) adds saved interaction profiles and explicit multi-machine placement packages.

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

## Prefab and live object contract

The existing Object3D asset family now authors ordinary Prefabs. Machine runtime
schema 13 exports authored object trees/placements, named spawn-only Prefab
requirements, lightweight anchors and geometry surfaces. Definitions are emitted
once under `objects/<object-guid>/` with `object.runtime.json` (schema
`oasis.object3d.runtime`, version 2) and content-addressed GLB dependencies.
Conflicting resolved definitions claiming the same GUID fail the build.

Definitions retain stable inline node/component IDs and explicit Transform, Model,
Collider and Rigidbody defaults. Placement transforms are separate from intrinsic
model scale/up-axis correction. Cabinet semantic geometry remains authoritative
in the Cabinet GLB; it is not duplicated into these declarations.

Each Machine session owns its imported templates, authored/spawned object registry,
script state, events, timers and physics adapters. Script `OasisObjectRef` handles
are scoped to session/generation and become invalid on destruction or unload.
Bounded instantiate/destroy operations register atomically and deactivate destroyed
roots before deferred engine destruction. Unload clears registries, imports and
live handles. See [the scripting contract](12_OASIS_SCRIPT.md).

Anchors retain stable raw ID, display name and Machine-space position/rotation,
without assets, scale or physics. Named geometry surfaces retain an oriented
rectangle for focused spatial queries.

## Interaction and arcade composition

Machine owns a required `interaction` object: `Buttons`, or `Pool` with explicit
`cuePrefabAlias` and `playingSurfaceId`. Pool references must resolve against the
Machine's declared dependencies/surfaces. Interaction profile and runtime kind are
independent; no profile implicitly runs an emulator or changes game rules.

Arcade runtime schema 1 composes contained, explicit generated Machine dependencies
with stable placement IDs and floor position/yaw, room dimensions and visitor spawn.
One independent loader/session is owned per placement. All dependencies are resolved
before loading; measured Cabinet placement and clearance are checked before scripts
start. Failure rolls back every loaded session. One visitor controller gates input
to one engaged station, releasing held keys on leave, settings, focus loss or unload.
The current package can be authored by script; a visual layout designer is deferred.

## Native Impact hosting

Impact exports a `runtime.fabric` descriptor with explicit relative ROM roles/slots
and the Editor-generated FAB2 binary configuration. Host-selected exact Windows x64
Fabric/Amber DLL paths are separate from content. Original ROM paths are stripped
from generated settings metadata. The Player owns native timing/input/snapshot/audio
and shutdown on one worker, with main-thread device updates. The installed JPM DLL
has one board per process; additional Impact sessions are rejected before creation.
See [Player Fabric/Impact](../NativeIntegration/PLAYER_FABRIC_IMPACT.md) for the
verified boundary and the user-deferred credited-play acceptance.

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
A8.5 adds configurable development keyboard bindings; production key/device bindings
remain deferred. Input Map authors logical declarations only.

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
9, Object3D 1 and Cabinet 5. A8.5 supplies the initial single-ball Pool proving script in
`Examples/Pool/behavior.oasis`; actual asset wiring remains local.

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
4. Save invalid script or an unresolved Machine reference through Editor Save/Save As;
   reopen and confirm source/diagnostics persist and successful save clears dirty state.
   Confirm a build fails with source diagnostics and preserves the last good output.
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

## A8.5 single-ball Pool setup and development input

See [Pool setup/manual checklist](../../Examples/Pool/README.md) for exact sample
single-ball/pocket/anchor/input mappings and authored versus generated source placement.
Only ball01, rackBall01 and traySlot01 are required alongside six
coordinate-based pockets (`Pocket_XNeg_Middle`, `Pocket_XNeg_YNeg`, `Pocket_XNeg_YPos`,
`Pocket_XPos_Middle`, `Pocket_XPos_YNeg`, `Pocket_XPos_YPos`) and rerack/newGame inputs.
They match the supplied Blender screenshot names, with `OasisTrigger_` removed and
`trigger:` added in script; verify the actual exported GLB before building.
Full multi-ball collection and cue-ball handling
remain follow-up work.
No actual Pool asset package was available or modified. The canonical script is
linked into .NET test output and read directly from the repository by Unity tests.
Automated coverage has been added but suites/manual verification remain unexecuted.

Native Oasis Editor/development Machine preview creates RuntimeInputDevelopmentControls
as the sole logical keyboard dispatcher. Initialize reads the loaded package's
`inputs[].keyboardShortcut` once and creates Inspector bindings with `inputs[].id`
as identity. MachineRuntimeBuildService already exports this metadata in schema 9;
MachineInputDefinition now retains it, so no writer/schema change is needed. Player
never reads authored Editor project files. Logical registration remains independent
of keyboard conversion and includes every declaration.

RuntimeInputShortcutMapper is a focused, testable development-only converter for
Input Map/importer single-key representations (letters, D0–D9/digits, Space, arrows,
OEM punctuation names, left/right modifiers, function and keypad/navigation keys).
It mirrors Editor MfmeShortcutKeyMapper conventions without WPF dependencies:
case-insensitive names, trimmed whitespace and generic SHIFT/CTRL/ALT mapping to
left modifiers. RawMfmeShortcut is not used. Unsupported/malformed values (including
chords and numeric enum values) warn with the logical ID/shortcut and use KeyCode.None;
empty shortcuts also use None. Every input remains visible and available through A7.

Window > Input Map > Add Input declares raw IDs; its Key column supplies development
bindings after save/build/load. Temporary Inspector edits survive polling; Initialize
on replacement/reload restores authored values. Multiple keys aggregate per input;
shared keys activate every distinct input in ordinal ID order, releases before
presses, and duplicate bindings emit one transition. Focus loss, disable and explicit
Configure release held inputs; unload detaches without stale dispatch. Emulation
initializes empty bindings, preserving existing routing. Release builds exclude the
bridge/converter. Production bindings and the broader named-input redesign remain
deferred; A8.4 fault/unload semantics are unchanged. See the Pool guide for the local
R/N authoring, reset, focus-loss, override and reload checklist. Added regression
suites must be run locally; they have not been executed in Codex.
