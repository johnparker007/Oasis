# Runtime Build and Oasis Player Contract

## Build roots

Target build roots are:

- Machine for standalone playable units;
- Installation for linked multi-unit compositions.

Project and Cabinet are not final runtime build roots.

## Explicit dependency traversal

Machine build walks explicit references:

```text
Machine
  -> RuntimeDefinition
  -> Cabinet
  -> assigned Faces/Surfaces
  -> Device assets
  -> transitive asset dependencies
```

Installation build walks:

```text
Installation
  -> Machine instances
  -> each Machine dependency closure
  -> shared assemblies
  -> link topology
  -> physical layout
```

Never enumerate all project assets to discover what might belong to a build.

Unreferenced broken assets must not break an unrelated Machine/Installation build.

## Diagnostics

A broken referenced dependency should identify the full useful path.

Example:

```text
Machine 'Bonanza'
  Cabinet target 'BottomGlass'
  -> Face 'Assets/Faces/BottomGlass/asset.face'
  -> missing Reel profile/device ...
```

Diagnostics should describe composition context rather than low-level path exceptions alone.

## Runtime package shape

Do not lock a final package format in this architecture document.

When Machine becomes build root:

- redesign current Cabinet-rooted runtime manifests directly;
- update Editor writer and Oasis Player reader together;
- increment schema versions;
- support current format only.

When Installation is introduced, it may contain references/instances of Machine runtime packages or a flattened combined package depending on the cleanest Player implementation at that time.

## Runtime state boundaries

Runtime packages should preserve the distinction between:

- static reusable physical definitions;
- game-specific composition;
- dynamic runtime state.

Avoid baking authoring-only provenance into runtime packages unless Player needs it.

## Standalone asset builds

Face/Surface or Device authoring may still generate preview/build artifacts for Editor use.

Those are not substitutes for final Machine composition.

A standalone Face build should not need a consuming Machine merely to generate Face-owned artwork/masks/textures.

Physical resolution that depends on Machine composition should happen at Machine build/preview time.
