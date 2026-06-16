# Emulation Backend Architecture Context

## Purpose

This document describes the planned emulator backend architecture for Oasis Editor.

Oasis currently has a MAME-based emulation path. A new native DLL-core path is being introduced for arcade/f fruit-machine emulator cores such as System6 and Epoch.

The goal is to support both backend types cleanly without compromising the existing MAME implementation.

## Current MAME Implementation

The existing MAME path is process and plugin based.

MAME is launched as an external process. Oasis communicates with it using redirected stdin/stdout. Runtime commands such as pause, resume, save state, reset, and input changes are sent to the Oasis MAME Lua plugin over stdin.

Runtime output is received as stdout lines. These lines are parsed into editor-facing updates such as:

- Lamps
- Reels
- Segments
- VFD brightness
- Dot matrix states

The relevant existing areas include:

- `MameEmulationService`
- `MameProcessRunner`
- `MameProcessStartInfoBuilder`
- `MameStdoutParser`
- `MameLampRuntimeAdapter`
- `MameReelRuntimeAdapter`
- `MameSegmentRuntimeAdapter`
- `MameVfdDotMatrixRuntimeAdapter`
- `MameInputCommandService`
- MAME Lua plugin assets under `Assets/MAME/plugins/oasis`

## New Native DLL-Core Backend

A native DLL backend will run emulator cores in-process.

The legacy reference class provided for analysis was `TechsClass.cpp`. It uses:

- `LoadLibrary`
- `GetProcAddress`
- Exported native functions
- Direct run-loop calls
- Direct polling for lamps, reels, display segments, meters, and other machine state
- Direct switch/input calls

That legacy class should be treated as an ABI reference only.

It should not be copied as-is.

The new implementation should use:

- C#
- `NativeLibrary.Load`
- `NativeLibrary.GetExport`
- Strongly typed delegates
- Safe disposal
- Explicit error reporting
- Backend-specific native library wrappers

## Architectural Decision

Introduce a backend-neutral layer above MAME and native DLL cores.

The editor should depend on backend-neutral concepts such as:

- Emulation backend lifecycle
- Runtime state updates
- Input changes
- Backend capabilities

The editor should not need to know whether runtime state came from:

- MAME stdout parsing
- Native DLL polling
- A future emulator backend

## Naming

Use:

- `IEmulationBackend`
- `EmulationBackendState`
- `EmulationLaunchRequest`
- `EmulationBackendCapabilities`

Avoid:

- `IEmulatorBackend`

The word "Emulation" matches the current Oasis terminology, including `IMameEmulationService` and `MameEmulationState`.

## Runtime Update Shape

Use incremental runtime events initially, not a full snapshot model.

Preferred initial event shape:

- Lamp changed
- Reel changed
- Segment changed
- VFD brightness changed
- Dot matrix changed
- Meter changed, if needed later

This maps naturally to the current MAME stdout parser and runtime adapter design.

A `MachineRuntimeSnapshot` or snapshot builder can be added later if a backend requires batched state publication.

## Proposed Layering

```text
Editor UI / Play View
    ↓
IEmulationBackend
    ↓
+-------------------------+
| MameEmulationBackend    |
| System6NativeBackend    |
| EpochNativeBackend      |
+-------------------------+
    ↓
Runtime update events
    ↓
Shared runtime adapters / runtime state store
    ↓
Panel and Face rendering
MAME Backend Strategy

The MAME backend should wrap the existing MAME implementation.

It should not rewrite:

MAME launch process
MAME Lua protocol
stdout parser logic
existing runtime adapters

The MAME backend should be a thin adapter over current services.

Native Backend Strategy

The native backend should have its own implementation.

It should not use MAME process services.

It should own:

Native DLL loading
Export binding
ROM loading
Run loop
State polling
Input mapping
Shutdown
Non Goals

Do not do these in the initial refactor:

Do not replace the MAME implementation.
Do not remove existing MAME classes.
Do not build the System6 native backend in Task 01.
Do not implement Epoch until System6 is understood.
Do not attempt save-state parity across backends initially.
Do not introduce a large dependency injection framework.
Do not move large amounts of UI code unless needed.
Initial Work Sequence
Add backend-neutral abstractions.
Wrap existing MAME implementation behind the new abstraction.
Add native DLL ABI layer.
Implement System6 native backend.
Integrate backend selection into Play View/editor commands.
Add Epoch once the System6 path is validated.
