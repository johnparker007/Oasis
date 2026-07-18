# Task 02: Oasis Player Startup and Cabinet Loading

## Scope

Implement the Unity Player side of the Phase 1 vertical slice in:

```text
UnityProjects/OasisPlayer
```

Read first:

1. `WindowsNetProjects/OasisEditor/Docs/OasisPlayerPhase1/PHASE_01_CONTEXT.md`
2. `WindowsNetProjects/OasisEditor/Docs/OasisPlayerPhase1/TASK_01_EDITOR_MACHINE_BUILD_EXPORT.md`
3. this file

Inspect the completed Editor export implementation and its tests before finalising Player DTOs or assumptions. The Player must consume the actual versioned build contract produced by Task 01.

## Existing Manual Unity Setup

The following assets were created manually and are already committed:

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

Do not recreate, rename, regenerate, or replace these scene assets or their `.meta` files. Do not make broad YAML edits to scenes. If a new serialized field must be wired manually in Unity, implement the code and list the exact manual Inspector step for John instead of manufacturing scene references.

## Goal

Make a built Oasis Player executable able to start in machine-preview mode, load an Editor-produced build directory from outside the Unity project, and instantiate the cabinet at `MachineSpawn`.

## Startup Options

Implement a small typed startup model and parser supporting:

```text
--mode machine-preview
--build <directory>
--windowed
--fullscreen
--width <pixels>
--height <pixels>
```

Requirements:

- raw command-line parsing happens once
- parsing produces a typed immutable or effectively immutable options object
- duplicate/conflicting options have deterministic behaviour
- malformed integers and missing values produce clear errors
- unknown modes produce a clear error
- `arcade` may be recognised as not implemented, but must not be implemented in this phase
- display settings are separate from content mode

Mode defaults:

- `machine-preview` defaults to windowed
- sensible default dimensions may be chosen when width/height are absent
- explicit `--fullscreen` or `--windowed` overrides the mode default

When running inside the Unity Editor, provide a small development fallback so the project can be tested without operating-system command-line arguments. Keep that fallback clearly separated from release argument parsing. Prefer serialized development defaults or a narrowly scoped editor-only provider rather than hard-coded paths in production code.

## Bootstrap Behaviour

`StartupController` should remain small and orchestration-focused.

It should:

1. obtain startup arguments
2. parse and validate them
3. apply display settings
4. retain the typed startup configuration for the selected runtime mode
5. load the `MachinePreview` scene asynchronously
6. hand off to a machine-preview loading component/service
7. surface fatal startup errors clearly

Do not put glTF loading, JSON parsing, or resource ownership logic directly into `StartupController`.

Use `DontDestroyOnLoad` only for the minimal application-level object/services that need to survive scene loading.

Avoid duplicate Bootstrap/application instances if the Bootstrap scene is entered more than once during development.

## Scene and Spawn Resolution

After `MachinePreview` is loaded, find the root transform named exactly:

```text
MachineSpawn
```

Fail clearly if it is missing or duplicated.

Instantiate the loaded cabinet beneath this transform using local placement. Do not hard-code the room's world position in loading code.

## Runtime Build Loading

Load and validate:

```text
<BuildDirectory>/machine.runtime.json
```

Then resolve the referenced cabinet manifest and GLB using paths relative to their containing manifests/build root.

Requirements:

- builds may live anywhere the user can read, outside `Assets` and `StreamingAssets`
- no UnityEditor APIs in runtime assemblies
- reject rooted manifest references where relative references are required
- normalise paths and reject traversal outside the selected build root
- validate schema identifiers and supported versions before loading content
- provide errors that identify the invalid file and reason
- treat the build as read-only

Keep manifest DTOs and validation separate from Unity scene orchestration.

## Runtime glTF/GLB Package

First inspect `Packages/manifest.json` and existing project code for a runtime glTF loader.

If a suitable maintained runtime package is already present, use it.

If none is present:

- choose a maintained package compatible with the committed Unity and URP versions
- prefer a package that supports runtime GLB loading from a file or byte stream and standard material import
- make the smallest package-manifest change required
- document why it was selected
- wrap package-specific calls behind a focused cabinet model loader abstraction

Do not implement a custom glTF parser.

## Materials

Phase 1 cabinet materials are ordinary glTF PBR materials rendered under URP.

The loader should preserve:

- hierarchy and node names
- mesh boundaries
- material slots
- standard base colour, normal, metallic/roughness, occlusion, emission, and alpha behaviour supported by the chosen package

If the package performs the required glTF-to-URP material conversion, use it rather than adding a second material pipeline.

Do not create Oasis-specific cabinet shaders or repack textures manually unless the selected package demonstrably cannot supply correct URP materials and the limitation is documented.

## Model Corrections

Apply scale/orientation corrections exported in `cabinet.runtime.json` consistently at a dedicated cabinet root beneath `MachineSpawn`.

Keep the raw imported hierarchy below that correction root so later Face target discovery can use stable imported node names.

Do not flatten or rename the imported hierarchy.

## Lifetime and Reload Safety

Model loading should have explicit ownership and cleanup.

Provide a way to unload the current cabinet/machine session that destroys runtime-created:

- GameObjects
- meshes, where owned by the loader
- materials
- textures
- package-specific import contexts/resources

Calling load more than once must not leave duplicate machines or leaked owned resources.

A full in-app hot-reload UI is not required.

## Error Presentation and Logging

At minimum:

- log structured, actionable diagnostics
- present fatal startup/load errors visibly in a built Player rather than leaving only a blank room

Keep the error presentation simple. A small runtime overlay is sufficient. Do not build a general UI framework.

## Tests

Add focused tests where practical for code that does not require live rendering:

- command-line parsing and precedence
- required argument validation
- manifest deserialization/version validation
- safe relative path resolution and traversal rejection
- mode-to-scene selection
- duplicate/missing `MachineSpawn` handling where testable

Do not commit generated Unity `Library`, `Temp`, `Logs`, or build outputs.

Codex should not claim successful visual verification. John will open Unity, resolve any package import, build the Player, and test the scene locally.

## Non-Goals

Do not implement:

- Arcade scene or arcade mode functionality
- multiple machines
- player movement/navigation
- Face targets or Face shaders
- lamps, reels, displays, buttons, or emulation
- remote URLs, archives, downloads, or catalogues
- Editor launch integration
- live IPC or hot reload

## Completion Report

At completion, report:

- files changed
- startup argument syntax and precedence
- classes/services introduced and their responsibilities
- runtime glTF package used and any package change
- exact expected build layout
- how `MachineSpawn` is resolved
- resource cleanup behaviour
- tests added or changed
- manual Unity Inspector or scene steps still required
- exact local verification commands and scenarios for John