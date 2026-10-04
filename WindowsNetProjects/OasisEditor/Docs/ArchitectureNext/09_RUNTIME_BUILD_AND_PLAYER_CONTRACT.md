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

Machine runtime schema 7 contains Machine-owned Object3D instance declarations. Each declaration preserves its authored ID/display name and position, Euler rotation in degrees, and placement scale, and points at a generated definition manifest rather than an authoring path.

Reusable definitions are emitted once under `objects/<object-guid>/` with `object.runtime.json` (schema `oasis.object3d.runtime`, version 1) and `object.glb`. The lower-case canonical authored Object3D GUID is the deterministic package/definition identity. Definitions retain model scale/up-axis and the minimal collider/Rigidbody contract; generated paths are package-relative. Conflicting resolved definitions claiming the same GUID fail the build.

The Player validates the typed definitions, loads each referenced GLB definition once per Machine session, and instantiates every Machine occurrence from that shared import. A live instance has an authoritative physical placement root under `RuntimeMachine/Objects`; `RuntimeObjectInstance.Root.transform` is both the current live transform and the transform Unity physics moves. When enabled, the Rigidbody is attached directly to that root. Intrinsic model scale and up-axis conversion live on a `ModelCorrection` child and therefore cannot become a second independently moving physical root. Y-up is unchanged, Z-up rotates -90 degrees around X, and X-up rotates +90 degrees around Z.

Object3D primitive collider coordinates are authored in source-model coordinates. A `PhysicsCorrection` child receives exactly the same uniform model scale and up-axis rotation as `ModelCorrection`, so primitive center, dimensions and orientation remain aligned without baking or approximating the conversion. Its collider is part of the ancestor root Rigidbody's compound body. A Mesh collider remains on the sole usable rendered mesh, preserving the imported node transforms, and fails clearly when the model has zero or multiple usable meshes. A Mesh collider is made convex when its definition also enables a dynamic Rigidbody, as required by Unity physics; Oasis does not perform convex decomposition. Colliders may therefore be on the root or descendants, but a live Object3D Rigidbody is always on the registered root.

`RuntimeMachine` registers live `RuntimeObjectInstance` values by raw Machine instance ID (the Object3D domain represented canonically as `object:<id>`). Each value retains identity/display name, its resolved definition, authoritative live root GameObject, optional Collider/root Rigidbody, and authored initial-transform data that physics does not mutate. Unload destroys all instance roots, clears the registry, and releases the per-load glTF imports. Object3D GLBs remain visible and are not processed using Cabinet semantic-name conventions.

See `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.

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
