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

The Player reader validates and retains these typed definitions and instances, but Object3D GLB loading, Unity GameObject/physics creation, and live-object registration remain the next runtime phase.

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
