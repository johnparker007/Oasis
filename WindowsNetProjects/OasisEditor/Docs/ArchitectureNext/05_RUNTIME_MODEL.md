# Runtime Model

## Principle

A Machine is playable regardless of how its behavior is implemented.

Emulation is one runtime backend.

## RuntimeDefinition

Machine should eventually reference/contain a typed RuntimeDefinition.

Conceptual variants:

- EmulationRuntimeDefinition;
- ScriptedRuntimeDefinition;
- PhysicsRuntimeDefinition;
- HybridRuntimeDefinition.

Do not require these exact type names if a cleaner implementation exists.

## Emulated examples

Fruit machine:

```text
Machine
  Runtime = MPU5/Impact/Epoch/etc emulation configuration
```

Video game:

```text
Machine
  Runtime = arcade/JAMMA/emulator configuration
```

Platform and ROM settings currently stored at Project level should move toward this Machine-owned runtime configuration.

## Scripted example

Whack-a-mole may have no dumped ROMs.

A scripted runtime can implement:

- state machine;
- scoring;
- timers;
- target activation;
- sound events;
- payout/game-over rules.

It still drives logical/physical devices.

## Physics example

Pool table may primarily contain:

- table/balls/cue physics;
- rules;
- player interaction;
- optional coin mechanism.

It does not need to pretend to be an emulated CPU/device bus.

## Hybrid example

Modern coin pusher:

- physics simulation for coins;
- moving pusher mechanism;
- sensors/switches;
- scripted bonus logic;
- display/lights;
- potentially emulated control logic later.

## Runtime-to-presentation boundary

Where practical, physical presentation should consume runtime state rather than care how it was produced.

For existing fruit-machine concepts:

```text
Runtime backend
  -> Machine runtime/logical state
  -> logical Reel/Lamp/Display/Input references
  -> physical/rendered presentation
```

Do not force physics-owned dynamic entities into this logical-device state if they do not fit naturally.

## Player contract

Runtime build schemas should be designed around the Machine/Installation build roots.

Editor and Player readers/writers must change together.

No compatibility readers for obsolete Cabinet-rooted runtime packages should be added once the new format replaces them.
