# Machine Composition

## Definition

A Machine is one independently playable/runtime unit.

It is the primary build root for a standalone game.

## Why Machine must become first-class

Current Oasis distributes machine-specific state across:

- Project metadata (platform/ROM settings);
- Cabinet (Face assignments, reel configuration);
- Faces;
- runtime build services.

This makes Cabinet and Project act as implicit partial Machine objects.

The target architecture makes Machine the explicit composition root.

## Target conceptual model

Exact C# schema should be designed during implementation after the current-state audit, but the semantic shape is:

```text
Machine
  Identity

  RuntimeDefinition

  CabinetReference

  SurfaceAssignments[]
    CabinetSurfaceTarget -> Face/Surface asset

  DeviceResolution / DeviceOverrides[]

  CabinetAppearanceOverrides[]

  Other game-specific composition/runtime mappings
```

## Surface assignment

Cabinet declares available reusable surface targets.

Machine selects the game-specific surface asset mounted on each target.

Example:

```text
Machine: Bonanza
  Cabinet = JPM Vogue

  SurfaceAssignments
    Vogue.TopGlass    -> Bonanza Top.face
    Vogue.BottomGlass -> Bonanza Bottom.face
```

Cabinet must not contain Bonanza-specific Face references.

Face must not know about Vogue.

## Device resolution

A Face may host a logical reel at a particular position and request a compatible physical profile:

```text
Bottom.face
  Reel:0
    placement
    aperture
    requestedProfile = standard-reel
```

The selected Cabinet/family can provide defaults/compatible assets:

```text
JPM Vogue
  standard-reel -> JPM Standard Reel.reel
  small-reel    -> JPM Small Reel.reel
```

Machine performs final resolution and may support explicit overrides when needed.

The exact profile/resolution mechanism is deferred until the Reel/device phases. Do not preserve the current Cabinet reel-spec model merely because it exists today.

## Project-to-Machine state migration

Machine-specific state currently stored at Project level should move to Machine over the refactor, including fruit-machine runtime/platform and ROM configuration where appropriate.

Project should retain only workspace-level concerns.

Initially Oasis may automatically create one Machine per project to preserve a simple common workflow.

## Build root

Standalone build:

```text
Machine
  -> RuntimeDefinition
  -> Cabinet
  -> assigned Faces/Surfaces
  -> resolved Device assets
  -> transitive dependencies
```

No project-wide scanning.

## Editor UX

The common case should remain simple:

1. New Project.
2. Import/create source.
3. One Machine is created/selected automatically.
4. Choose reusable Cabinet.
5. Assign/generated surfaces.
6. Resolve devices, mostly by defaults.
7. Preview/build Machine.

Multiple Machine assets should become visible only when the user actually creates or imports more than one.

## Machine does not imply emulation

Machine can use:

- EmulationRuntimeDefinition;
- ScriptedRuntimeDefinition;
- PhysicsRuntimeDefinition;
- HybridRuntimeDefinition.

Runtime abstraction is detailed separately.
