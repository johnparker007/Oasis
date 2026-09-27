# Architecture Next Phase 7 implementation

## Result and checkpoint C

**Yes.** Existing fruit-machine authoring, preview and build now pass through an explicit authored `RuntimeDefinition`, whose only concrete Phase 7 implementation is `EmulationRuntimeDefinition`. Cabinet, Face and Reel remain independently authored physical assets and contain no runtime/backend ownership.

```text
Machine
├── RuntimeDefinition
│   └── EmulationRuntimeDefinition
│       ├── Platform
│       └── strongly typed platform settings / ROM configuration
├── Cabinet
├── Faces
├── Reels
└── InputDefinitions
```

No Phase 8 Installation work was started.

## Pre-Phase-7 runtime audit and old ownership

The audit covered Machine persistence, `DocumentTabViewModel`, `MainWindowViewModel`, Details and Overview, scaffolding, FML/MFME entry points, settings editors, inputs, build/export, preview launch, Fabric/Amber adapters, normalization, Player manifests, and tests. The transitional `MachineEmulationRuntime` sat directly on `MachineDocument`; neutral consumers consequently reached through `Runtime.Platform` and `Runtime.Settings`. Details labelled only an “Emulation platform”, while Overview used the platform as the Runtime card identity. New Machines came from `MachineDocument.Create`; scaffolding and import ultimately use that default. Platform settings and ROM paths were already Machine-owned, inputs were Machine-owned, and build projected the runtime to a separate `MachineRuntimeDefinition` containing a JSON settings payload and unused `ExecutionSupportedByPlayer` flag.

Platform-specific reel reversal, Epoch/Amber normalization, offsets, input translation, and Fabric ABI code are execution/presentation adapters rather than authored ownership. They remain in their focused services. Dynamic `MachineRuntimeState` / panel state also remains a separate live-state concept.

## Authored RuntimeDefinition model

`MachineDocument.Runtime` is now the abstract, typed `RuntimeDefinition`. It exposes only `Kind`. `EmulationRuntimeDefinition` is the sole concrete runtime and owns `Platform` plus the strongly typed `PlatformSettings` object. Its factory retains the established per-platform defaults. No generic property bag, authored JSON blob, CLR type discriminator, plugin registry, or speculative Scripted/Physics/Hybrid classes were introduced.

`FruitMachinePlatformType` is a broader identifier vocabulary than the currently implemented backends. `EmulationRuntimePlatforms` therefore defines the exact authored/executable subset: `None`, `Impact`, `MPU5`, `Epoch`, `MPU3`, `MaygayM1`, and `Scorpion4`. `None` remains the unconfigured default. The Emulation factory uses an exhaustive switch for that subset and rejects every other defined or numeric enum value; unsupported identifiers such as `MPU4` are not mapped to System 6 settings.

An explicit `MachineDocument.EmulationRuntime` boundary is used by existing emulation-specific Editor workflows. Runtime-neutral graph/build entry points switch on `RuntimeDefinition`; unsupported kinds fail rather than silently becoming Emulation.

## Machine schema 4 and platform settings

The Machine schema moved directly from 3 to 4. Runtime is written as `kind`, `platform`, and `platformSettings`; settings continue to be serialized according to the selected platform's concrete settings type. Only schema 4 is accepted. Schema 3 and unknown runtime kinds are rejected, with no compatibility reader or migration.

Runtime validation checks that the only supported authored kind is Emulation, the platform enum value is valid, and the settings object's exact type matches the platform factory. Diagnostics include Machine, Runtime and Platform context. Existing ROM-required checks remain at the existing preview/backend/build boundaries rather than being duplicated.

## InputDefinitions ownership

`Machine.InputDefinitions` deliberately remains Machine-owned and runtime-neutral. Logical inputs are useful to future scripted, physics, and hybrid Machines; Emulation consumes them today but does not own them. Cabinet, Face and Reel schemas gained no runtime fields.

## Details and Overview UX

Machine Details now has a visible Runtime section with a read-only `Type: Emulation` and the existing editable `Platform` selector. No unusable future runtime choices are shown. Switching platform still replaces settings with the selected platform's defaults as one document mutation, preserving undo/redo behavior.

Both Machine Details and the active-Machine Platform Settings selector consume `EmulationRuntimePlatforms.Supported`, so historical/future enum identifiers without a current backend cannot be selected in either UI.

Overview now branches at the runtime boundary. Its Runtime card title is `Emulation` and metadata is `Platform · <platform>`; the Machine summary identifies the runtime kind rather than treating platform as runtime identity. Overview remains derived/read-only and does not mutate or dirty authored data.

## Creation and MFME/FML behavior

`MachineDocument.Create` creates an Emulation definition automatically. Project scaffolding and the existing import flow use this creation path, so fruit-machine workflows require no new runtime selection step and retain the current default platform behavior.

## Build dispatch and generated runtime manifest

`MachineRuntimeBuildService` validates and explicitly projects the authored `RuntimeDefinition` through a typed switch. The generated DTO is named `MachineRuntimeManifestDefinition` to distinguish it from authored configuration and live runtime state. Emulation produces the existing kind/platform/settings JSON contract. Cabinet/Face/Reel traversal is unchanged and remains rooted in explicit Machine references.

The generated Machine runtime schema is now version 6. `ExecutionSupportedByPlayer` had no behavioral consumer and was only a transitional declaration, so it was removed rather than made part of the abstraction. The Player DTO was renamed consistently and accepts only schema 6, `kind == Emulation`, a supported platform and object-shaped settings JSON. Unsupported kinds and obsolete manifests are rejected; no fallback was added.

## Fabric / Amber boundary

Preview remains `Machine -> EmulationRuntimeDefinition -> platform settings -> EmulationLaunchRequest -> existing Fabric/Amber backend`. Native ABI, backend selection, normalization and rendering state were not rewritten and are not serialized as runtime kinds.

## Automated coverage

Focused coverage now checks schema-4 round trips, schema-3 rejection, unknown-kind rejection, concrete settings round trips for every currently executable/default Emulation platform family, projection to the Player manifest, Overview's distinct runtime/platform presentation, and existing no-mutation behavior. Existing build and document tests continue to cover dependency closure, unrelated-asset exclusion, persistence and command-based edits. Player fixtures use schema 6 without the removed flag.

## Manual verification checklist

- [ ] 1. Open an existing/current Machine regenerated to schema 4.
- [ ] 2. Confirm Details shows Runtime Type = Emulation and the current Platform.
- [ ] 3. Switch Impact to another supported platform.
- [ ] 4. Confirm the platform-specific settings editor updates.
- [ ] 5. Undo.
- [ ] 6. Redo.
- [ ] 7. Save.
- [ ] 8. Close and reopen.
- [ ] 9. Confirm runtime kind, platform and settings persist.
- [ ] 10. Open Overview.
- [ ] 11. Confirm the Runtime card distinguishes Emulation from platform.
- [ ] 12. Change platform and confirm Overview updates live.
- [ ] 13. Import/create an MFME fruit-machine project.
- [ ] 14. Confirm Runtime defaults/imports to Emulation automatically.
- [ ] 15. Configure ROM/platform settings normally.
- [ ] 16. Preview the existing fruit-machine workflow.
- [ ] 17. Confirm Fabric/Amber execution still works.
- [ ] 18. Build an Oasis Player Machine.
- [ ] 19. Launch the generated build.
- [ ] 20. Confirm Cabinet, Faces, Reels, lamps, inputs and gameplay are unchanged.
- [ ] 21. Build/preview missing or invalid emulation configuration and confirm contextual diagnostics.
- [ ] 22. Confirm graph/details navigation does not dirty the Machine.
- [ ] 23. Confirm project-local and Library physical assets remain unaffected.
- [ ] 24. Confirm no Scripted, Physics, Hybrid, or Installation UI/implementation exists.

## Deliberately deferred

Scripted, Physics, Hybrid, video/MAME changes, scripting languages, physics, runtime discovery/registries, generalized devices/surfaces, arbitrary graph editing, and Installation assets remain deferred. Phase 7 establishes only the real Emulation seam.

## Repository-driven deviations

The generated Player manifest retains `platformSettingsJson` because Unity's current `JsonUtility` contract intentionally retains arbitrary platform settings as an opaque JSON object string and Player does not host Fabric in this path. This is a generated transport DTO, not the authored model. Existing platform-specific physical normalization remains in runtime adapters because moving it would conflate authored configuration with execution/presentation and exceed Phase 7.
