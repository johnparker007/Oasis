# MAME Emulation Runtime Plan

This document defines the next discrete MAME workstream: porting the legacy emulation menu/runtime control path into the WPF editor so the new editor can launch MAME, communicate over stdio with the Oasis Lua plugin, and drive lamp on/off state from live MAME output.

## Goal

Reach a first working runtime integration milestone:

```text
Emulation -> Start
    -> launches managed MAME install as hidden child process
    -> loads configured project ROM
    -> starts Oasis Lua plugin/stdio communication
    -> reads lamp state messages from stdout
    -> updates lamp on/off state in the editor

Emulation -> Stop
    -> terminates MAME cleanly
```

Do not try to complete full input/control support in this workstream.

Inputs, buttons, reels, meters, segment displays, and more advanced runtime components can be planned after lamp-state readback is working.

## Legacy Source Areas

Codex should inspect the legacy Unity implementation before writing runtime code.

Primary areas:

```text
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/Editor.cs
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/LayoutEditor/Components/
UnityProjects/LayoutEditor/Assets/_Project/Prefabs/MameController.prefab
UnityProjects/LayoutEditor/Emulators/MAME/mame0258/plugins/oasis/**
UnityProjects/LayoutEditor_ExternalAssets/Windows/MameLuaPlugins/oasis/**
```

Focus on:

- Emulation menu entries exposed by the legacy NativeMenu;
- Start/Stop/Pause/Resume behavior;
- MAME executable arguments;
- hidden child-process behavior;
- stdin/stdout/stderr redirection;
- Oasis Lua plugin launch/configuration;
- lamp protocol/stdout parsing;
- process cleanup behavior;
- any required startup ordering or ready messages.

Do not blindly port Unity object/coroutine architecture.

## New Editor Source Areas

Likely WPF areas to inspect/use:

```text
WindowsNetProjects/OasisEditor/OasisEditor/MainWindow.xaml
WindowsNetProjects/OasisEditor/OasisEditor/MainWindow.xaml.cs
WindowsNetProjects/OasisEditor/OasisEditor/MainWindowViewModel.cs
WindowsNetProjects/OasisEditor/OasisEditor/EditorProject.cs
WindowsNetProjects/OasisEditor/OasisEditor/DocumentModel.cs
WindowsNetProjects/OasisEditor/OasisEditor/Panel2DDocumentModel.cs
WindowsNetProjects/OasisEditor/OasisEditor/PanelRuntimeState.cs
WindowsNetProjects/OasisEditor/OasisEditor/PanelLayoutMapper.cs
WindowsNetProjects/OasisEditor/OasisEditor/Views/OutputLogView.xaml
WindowsNetProjects/OasisEditor/OasisEditor/OutputLogEntry.cs
```

Also inspect current MAME provisioning code:

```text
MameInstallService
MameVersionCatalogService
MamePluginDeploymentService
MameSetupOrchestrator
MameRomProvisioningService
MameRomValidationService
MameDownloadService
```

Actual file/class names may differ. Codex should use the current repo state.

## OS Menu Requirements

Add a top-level WPF menu:

```text
Emulation
```

Initial menu items:

```text
Emulation
  Start
  Stop
  Pause
  Resume
```

Initial behavior:

- Start should be enabled only when a project is loaded and no emulation process is running/starting.
- Stop should be enabled only when emulation is running/starting/paused.
- Pause/Resume can be stubbed or wired if legacy implementation is simple, but should not block the first Start/Stop/lamp milestone.
- Menu command enabled states should follow runtime state.
- Menu actions should log to the output log.

Suggested future menu items to leave as TODOs, if present in legacy:

- Restart
- Reset
- Snapshot
- Debug/Diagnostics

## Runtime State Model

Add an explicit emulation process state, for example:

```text
Stopped
Starting
Running
Paused
Stopping
Failed
```

This state should drive:

- menu enabled/disabled behavior;
- output-log messages;
- status labels if present;
- command guard logic.

## Process Launch Requirements

The MAME runtime service should:

- use the current selected managed MAME install;
- validate MAME exists before launch;
- validate/sync Oasis Lua plugins before launch;
- validate project ROM exists before launch;
- use the managed ROM path;
- build process arguments based on the legacy implementation;
- launch MAME as a hidden child process where appropriate;
- redirect stdin/stdout/stderr;
- read stdout asynchronously;
- read stderr asynchronously;
- expose cancellable Stop;
- clean up on project close/editor exit.

Expected ROM path direction:

```text
-rompath "%LOCALAPPDATA%\\OasisEditor\\MAME\\roms"
```

Expected plugin source/deployment direction:

```text
AppContext.BaseDirectory\\Assets\\MAME\\plugins\\oasis
    -> selected MAME install\\plugins\\oasis
```

Codex should inspect the legacy arguments before deciding final launch args.

## Stdio / Lua Communication

Initial milestone only needs:

- MAME stdout parser;
- stderr logging;
- stdin command writer scaffold;
- lifecycle hooks.

Do not implement gameplay input mapping yet.

The stdin writer should exist as an abstraction so later work can send Lua/input commands, but initial use can be minimal.

## Lamp State Milestone

Initial runtime output integration should focus on lamps only.

Codex should:

- inspect the legacy lamp stdout protocol;
- implement parser for lamp state messages;
- map parsed lamp states to editor/runtime lamp state;
- update lamp visuals on the WPF UI thread;
- log unknown/unsupported stdout lines at debug/diagnostic level without crashing;
- tolerate partial lines and unexpected output.

## Lamp Mapping

Codex should inspect current editor panel/lamp data models and identify how lamp elements are represented.

Expected outcome:

- if panel elements already expose lamp IDs/numbers, map stdout lamp state by that identifier;
- if no clear mapping exists, add the smallest non-disruptive mapping field or adapter needed;
- do not redesign all panel components in this phase.

If mapping is ambiguous, Codex should create a TODO/diagnostic fallback rather than guessing silently.

## Runtime Services

Preferred architecture:

```text
MainWindow / Menu Command
    ↓
MainWindowViewModel or EmulationViewModel
    ↓
MameEmulationService
    ↓
MameProcessRunner
    ↓
Process stdin/stdout/stderr
```

Suggested services/classes:

```text
IMameEmulationService
MameEmulationService
IMameProcessRunner
MameProcessRunner
MameProcessStartInfoBuilder
MameStdoutParser
MameLampStateParser
MameLampRuntimeAdapter
MameEmulationState
MameEmulationCommandService
```

Do not let WPF controls directly own `Process` objects.

## Output Log / Diagnostics

Log:

- Start requested;
- resolved MAME executable path;
- resolved ROM name/path;
- resolved plugin path;
- command-line arguments, with sensitive values omitted if any;
- process start success/failure;
- stdout parser errors;
- stderr lines;
- lamp state summary/first updates;
- Stop requested;
- process exit code;
- forced termination, if needed.

Diagnostics are important because Codex cannot runtime-test MAME locally.

## Error Handling

Start should fail cleanly if:

- no project is loaded;
- no ROM name is configured;
- ROM is missing and cannot be auto-provisioned;
- MAME install is missing/invalid;
- plugin deployment fails;
- process start fails;
- stdout protocol is not detected within a reasonable time, if a ready signal exists.

The editor should not crash.

## Tests

This workstream can and should have tests for the non-WPF/process-heavy parts.

Add tests for:

- command enabled-state logic;
- process start-info argument construction;
- ROM path argument inclusion;
- plugin path argument inclusion;
- stdout parser line handling;
- partial line handling;
- lamp state parsing;
- unknown stdout line behavior;
- process lifecycle state transitions using fake process runner;
- stop/cancel behavior using fake process runner;
- failure state when validation fails.

Do not add tests that launch real MAME.

Use:

- fake process runner;
- fake stdout streams/lines;
- fake provisioning services;
- fake runtime state adapter;
- fake output logger.

## Recommended Codex Steps

### Step 1 - Legacy Inventory For Runtime

Create or update a runtime inventory document summarizing:

- legacy Emulation menu items;
- legacy process launch args;
- stdio protocol shape;
- lamp stdout protocol;
- pause/resume/stop behavior;
- cleanup behavior;
- open questions.

Do not implement runtime code until this is captured.

### Step 2 - Add Emulation Menu Shell

Add WPF topbar OS menu:

```text
Emulation -> Start / Stop / Pause / Resume
```

Wire commands and enabled-state to a new runtime state model.

Stub service methods are acceptable in this step.

### Step 3 - Add Runtime Service Abstractions

Add interfaces/classes for:

- emulation lifecycle service;
- process runner abstraction;
- start-info builder;
- stdout parser;
- lamp state parser;
- lamp state adapter.

Add tests for state transitions and command enabled states.

### Step 4 - Build Process Launch Info

Port legacy MAME args into a WPF/.NET start-info builder.

Use current managed paths:

- selected MAME exe;
- ROM name;
- managed ROM folder;
- deployed Oasis plugin folder.

Add tests for generated start info/arguments.

### Step 5 - Start/Stop Hidden Process

Implement process launch with:

- hidden/no-window behavior;
- redirected stdin/stdout/stderr;
- async stdout/stderr readers;
- cancellation;
- cleanup on stop.

Add fake-process tests for lifecycle behavior.

### Step 6 - Parse Lamp Output

Port legacy lamp stdout parsing.

Add tests using sample stdout lines from legacy code or captured expected format.

Handle malformed/unknown lines safely.

### Step 7 - Wire Lamp State To Editor

Map parsed lamp state changes to current editor lamp visuals/runtime state.

Use the smallest adapter necessary.

Ensure UI updates happen on the UI thread.

### Step 8 - Manual Runtime Verification

John should manually verify:

- Emulation menu appears;
- Start launches MAME hidden/background as expected;
- Stop terminates MAME cleanly;
- output log shows launch diagnostics;
- stdout/stderr are being read;
- lamp messages are parsed;
- lamp visuals change on/off in the editor;
- failures do not crash the editor.

## Out Of Scope For This Workstream

Do not implement yet:

- keyboard/input mapping;
- button press forwarding;
- platform-specific input Lua commands;
- reels;
- meters;
- segment displays;
- audio;
- save states;
- debugger UI;
- complex pause/resume behavior if not trivial.

These will be planned after the Start/Stop + lamp readback milestone is working.
