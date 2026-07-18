# Task 03: Editor-to-Player Launch Integration

## Goal

Complete the first usable Editor-to-Player preview workflow:

```text
File > Preview in Oasis Player
    -> build the selected saved Cabinet3D asset
    -> launch the configured OasisPlayer executable
    -> pass machine-preview arguments
    -> Player loads the generated build in MachinePreview
```

The previous Phase 1 work already provides:

- `File > Build Oasis Player Machine`
- deterministic runtime build output
- versioned machine and cabinet manifests
- Player command-line parsing
- Player machine-preview startup
- runtime cabinet GLB loading beneath `MachineSpawn`

This task adds the missing Editor-side configuration and process-launch bridge.

## Project Scope

Primary work is in:

```text
WindowsNetProjects/OasisEditor
```

The existing Player command-line contract under:

```text
UnityProjects/OasisPlayer
```

should be inspected for compatibility, but should not require broad modification.

## Required User Experience

### Preferences

Add a `Player` section or tab to the existing Preferences UI, following its current architecture and styling.

At minimum provide:

- Oasis Player executable path
- Browse button using the Editor's existing file-dialog abstractions/patterns

Also provide preview display settings unless the existing Preferences architecture makes that disproportionately invasive:

- Windowed/fullscreen selection
- Preview width
- Preview height

Recommended defaults:

```text
Windowed
1280 x 800
```

The executable path is a machine/user preference. Do not store it in an Oasis project or authored asset manifest.

Persist the settings through the Editor's existing Preferences/settings storage. Do not introduce an unrelated second settings system.

### File Commands

Keep the existing command:

```text
File > Build Oasis Player Machine
```

Add:

```text
File > Preview in Oasis Player
```

The preview command must:

1. require an open project
2. require a selected saved Cabinet3D asset, matching the current build command's Phase 1 scope
3. validate the configured Player executable
4. build through the existing `MachineRuntimeBuildService`
5. stop and report the build error if building fails
6. launch the configured executable with the generated build root
7. report the launch and build path through the existing status/output systems

Do not duplicate the build implementation inside the preview command.

## Launch Contract

Launch the Player with arguments equivalent to:

```text
OasisPlayer.exe
    --mode machine-preview
    --build <absolute generated build directory>
    --windowed
    --width 1280
    --height 800
```

For fullscreen preferences, pass `--fullscreen` instead of `--windowed`.

Use `ProcessStartInfo.ArgumentList` or an equivalent API that passes each argument separately. Do not construct a fragile manually quoted argument string.

Use an explicit absolute executable path and absolute build-directory path.

The initial launch is fire-and-forget. Do not add IPC, process monitoring, automatic shutdown, hot reload, or reuse of an already-running Player process.

## Recommended Design

Keep process launching outside the ViewModel's business logic where practical.

A focused service could expose concepts such as:

```text
OasisPlayerLaunchService
OasisPlayerLaunchRequest
OasisPlayerLaunchResult
```

Responsibilities should include:

- validating launch settings
- constructing a deterministic `ProcessStartInfo`
- starting the process through an injectable abstraction or overridable boundary
- converting launch exceptions into clear user-facing failures

The ViewModel should orchestrate:

```text
validate selection -> build -> launch -> report
```

Do not put process-start logic in WPF code-behind.

## Validation and Errors

Provide clear errors for at least:

- Player executable path not configured
- configured path does not exist
- configured path points to a directory
- configured path is not an executable file on Windows
- width or height is invalid
- build failure
- process launch failure, including permission or OS errors

Suggested missing-path guidance:

> The Oasis Player executable has not been configured. Set it under Preferences > Player.

Do not require the Player executable to live inside the repository or at a fixed relative path.

## Preferences Details

Follow the existing Preferences patterns for:

- tab/section creation
- ViewModel bindings
- persistence
- validation
- browse dialogs
- semantic theme resources

Do not hard-code colours.

Do not redesign unrelated Preferences pages.

It is acceptable for the browse dialog to filter for `*.exe` on Windows while still validating the selected path independently.

## Tests

Add focused tests where practical for:

- persisted Player settings defaults and round-trip behavior
- missing and invalid executable validation
- generated argument order and values
- paths containing spaces
- windowed arguments
- fullscreen arguments
- width and height propagation
- build failure prevents launch
- successful build uses the exact returned build root
- process start failure becomes a clear result/error

Avoid tests that start the real Oasis Player executable.

Prefer an injectable process-start boundary so tests can capture the intended `ProcessStartInfo` without launching a process.

## C# and Platform Constraints

The Editor can continue using its existing language version and conventions.

Any Unity-side changes must obey the Unity compatibility guidance in `AGENTS.md`:

- C# 9 syntax only
- block-scoped namespaces
- no file-scoped namespaces
- no global using directives
- no `required` members
- no C# 10+ syntax

Do not change Unity scripting runtime, API compatibility, or .NET settings to accommodate generated code.

## Explicit Non-Goals

Do not implement:

- automatic Unity Player builds
- locating Unity installations
- launching the Unity Editor
- live IPC
- hot reload
- reusing or controlling an existing Player process
- multiple-machine preview
- arcade launch mode
- Face rendering
- emulation
- zip packaging or downloads

## Completion Criteria

The task is complete when a user can:

1. build `OasisPlayer.exe` manually from Unity
2. configure its path in Editor Preferences
3. select a saved Cabinet3D asset
4. choose `File > Preview in Oasis Player`
5. have the Editor create/replace the runtime build
6. have the Editor launch the Player with the correct arguments
7. see the Player attempt to load that build in the MachinePreview scene

Finish with exact local testing steps, including:

- where to configure the executable
- how to build the Unity Player manually
- which Editor command to invoke
- expected command-line behavior
- expected generated build location
- negative cases to test
