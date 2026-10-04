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

See `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.
