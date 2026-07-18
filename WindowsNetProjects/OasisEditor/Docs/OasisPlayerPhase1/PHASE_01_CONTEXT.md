# Oasis Player Phase 1 Context

## Purpose

Phase 1 establishes the first complete vertical slice between Oasis Editor and Oasis Player:

1. Oasis Editor exports a deterministic machine build containing one cabinet model and versioned runtime manifests.
2. Oasis Player starts in machine preview mode.
3. Oasis Player loads the external build directory at runtime.
4. The cabinet is instantiated at the `MachineSpawn` transform in the existing preview room.
5. The cabinet renders with ordinary glTF PBR materials under Unity URP dynamic lighting.

This phase deliberately stops before dynamic Face rendering, lamps, reels, displays, emulation, or a navigable arcade.

## Repository Locations

Editor:

```text
WindowsNetProjects/OasisEditor
```

Player:

```text
UnityProjects/OasisPlayer
```

Unity scenes and startup script already created manually:

```text
UnityProjects/OasisPlayer/Assets/_Project/Scenes/Bootstrap.unity
UnityProjects/OasisPlayer/Assets/_Project/Scenes/MachinePreview.unity
UnityProjects/OasisPlayer/Assets/_Project/Scripts/StartupController.cs
```

The Bootstrap scene contains an `OasisApplication` GameObject with `StartupController` attached.

The Machine Preview scene contains a root transform named:

```text
MachineSpawn
```

Do not recreate, rename, or regenerate these Unity assets. Preserve their existing GUIDs and scene references.

## Long-Term Architecture

Oasis Player is one application with explicit runtime modes rather than separate executables.

Initial mode:

```text
machine-preview
```

Future mode:

```text
arcade
```

Display mode is independent from content mode. Machine preview should default to windowed and arcade should eventually default to fullscreen, but explicit command-line options must be able to override those defaults.

Conceptual startup flow:

```text
Bootstrap
    -> parse startup options
    -> validate startup options
    -> retain typed startup configuration
    -> select content scene
        -> MachinePreview
        -> Arcade (future)
```

The Bootstrap scene is the application composition root. It must not contain machine-specific room content.

## Cabinet and Face Separation

Cabinet content should remain standard:

- GLB/glTF model
- standard PBR materials
- ordinary Unity URP-compatible runtime materials
- preserved model hierarchy and material slots

Face content is specialised Oasis runtime content and will be implemented in later phases using generated artwork, masks, lamp influence textures, and custom shaders.

Do not invent an Oasis-specific cabinet material format in Phase 1.

Preserve imported node names, hierarchy, and material-slot boundaries so later phases can discover designated Face targets and replace selected materials without reworking the cabinet loader.

## Proposed Build Layout

Use a deterministic, versioned build layout. The implementation may refine exact field names where existing code conventions strongly suggest a better choice, but it should retain this separation:

```text
<BuildDirectory>/
    machine.runtime.json
    cabinet/
        cabinet.runtime.json
        cabinet.glb
```

The machine manifest is the entry point. It should identify its schema/version and reference the cabinet manifest using a path relative to the build root.

The cabinet manifest should identify its schema/version and reference the GLB using a path relative to the cabinet manifest.

All runtime paths stored in manifests should use a portable canonical representation. Resolve and validate them safely at runtime.

## Command-Line Direction

The initial supported launch shape should be equivalent to:

```text
OasisPlayer.exe --mode machine-preview --build "C:\path\to\build" --windowed --width 1280 --height 800
```

Required Phase 1 concepts:

- `--mode machine-preview`
- `--build <directory>`
- `--windowed`
- `--fullscreen`
- optional `--width <pixels>`
- optional `--height <pixels>`

The parser must produce one typed startup-options object. Other systems should not repeatedly inspect raw command-line strings.

Recommended precedence:

1. explicit command-line options
2. build or content manifest settings, where applicable later
3. saved user preferences, where applicable later
4. mode defaults

Phase 1 does not need to implement saved preferences.

## Runtime Loading Constraints

The Player must load builds from outside the Unity project's `Assets` directory and outside `StreamingAssets`.

The build directory is read-only from the Player's perspective.

Runtime loading must:

- provide actionable errors for missing or invalid files
- avoid path traversal outside the selected build root
- support unloading or replacing the loaded machine cleanly
- release generated GameObjects, meshes, materials, and textures owned by the load session
- avoid relying on Unity editor-only APIs

Use an established runtime glTF/GLB loading package already present in the Unity project if one exists. If none exists, select a suitable maintained package compatible with the project's Unity/URP version, document the choice, and keep the integration behind a small loader abstraction.

Do not hand-write a complete glTF parser.

## Scale, Axis, and Placement

The cabinet must be instantiated as a child of the existing `MachineSpawn` transform.

Keep placement responsibilities separated:

- the preview scene controls where the machine stands
- the cabinet manifest controls model-specific scale/orientation corrections if required
- the loader applies those corrections consistently

Do not hard-code preview-room world coordinates in the cabinet loader.

## Phase 1 Non-Goals

Do not implement any of the following in this workstream:

- Face target discovery or binding
- Face material replacement
- custom Face shaders
- lamp texture upload or lamp animation
- reels
- alpha displays
- seven-segment displays
- buttons or input mapping
- emulator integration
- arcade layout loading
- multiple machines
- player navigation
- remote downloading, archives, or online asset catalogues
- hot reload or live Editor-to-Player IPC

Design interfaces so these can be added later, but do not implement speculative frameworks for them now.

## Completion Definition

Phase 1 is complete when John can:

1. Open an Oasis Editor project containing a Cabinet3D asset and machine reference.
2. invoke the new build/export operation.
3. obtain the deterministic machine build directory and manifests.
4. launch a built Oasis Player with machine-preview arguments.
5. see the cabinet loaded at `MachineSpawn` in the preview room.
6. see its ordinary PBR materials rendered under URP dynamic lighting.
7. receive a clear error for an invalid build path or malformed manifest.
8. unload/reload or relaunch without leaked duplicate runtime objects.

John will build and test the WPF and Unity applications locally.