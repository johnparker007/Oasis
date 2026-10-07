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
`MachinePreviewLoader` rejects Oasis before creating a RuntimeMachine or emitting
MachineStarted: “Oasis Script runtime package loaded successfully; runtime host
adapter is not implemented until A8.4.” Emulation preview remains operational.

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
7. Load/parse an Oasis package through RuntimeBuildLoader, confirm canonical compiler
   output is session-ready, and confirm normal preview reports the A8.4 adapter
   boundary with no script handlers driven by A7 events or commands.
