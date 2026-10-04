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
  -> Object3D assets (active future track)
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

When Object3D runtime support is implemented:

- build reusable Object3D runtime packages;
- deduplicate repeated references to the same Object3D asset;
- serialize Machine instance declarations with stable IDs/transforms;
- update Editor writer and Player reader together;
- increment current runtime schema versions when the serialized shape changes;
- support only the latest version.

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
