# Machine Composition

## Definition

Machine is one independently playable/runtime unit and the primary standalone build root.

It owns final game-specific composition.

## Current authoritative composition

Machine currently owns:

- stable Machine identity/display name;
- selected Cabinet via `AssetReference`;
- Cabinet surface/Face assignments;
- Reel asset assignments;
- Machine inputs;
- RuntimeDefinition and emulation settings.

The active dynamic-object track will add:

- Object3D instances;
- later Machine anchors;
- later behaviour/script references.

## Dependency direction

Machine references reusable/game-specific assets.

Referenced assets do not point back at the Machine.

```text
Machine
  -> Cabinet
  -> Faces
  -> Reels
  -> Object3D assets
  -> RuntimeDefinition
```

Build traversal follows these explicit references. Do not scan the project to infer Machine ownership.

## Surface assignment

Cabinet declares reusable `OasisFace_*` surface targets.

Machine assigns the game-specific Face to each target.

Cabinet must not store the current game's Face assignment, and Face must not know which Cabinet/Machine consumes it.

## Object3D instances

General 3D objects such as pool balls and moles are Machine composition rather than Cabinet contents.

Machine owns each instance's:

- stable ID;
- display name;
- Object3D asset reference;
- transform.

The reusable Object3D asset owns intrinsic model/physics defaults.

See `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.

## Editor UX

Machine should increasingly be the place where a designer understands the assembled playable unit.

The existing Overview graph remains derived/read-only. The active Object3D track will add an assembled 3D composition view in a later PR after the authored/build/runtime object model exists.

## Machine does not imply emulation

The current implemented RuntimeDefinition is Emulation.

Future scripted/physics behaviour should extend the runtime model only when concrete vertical slices require it. Do not make physical composition depend on emulation-specific ownership.
