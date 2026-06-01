# Oasis Editor MAME Debugger Plan

## Purpose

This folder captures the implementation plan for adding a GUI-based MAME debugger to the new WPF Oasis Editor under `WindowsNetProjects/OasisEditor`.

The intended workflow is for Codex or another implementation agent to work through the task files in order, producing an MVP debugger that is useful for debugging code running inside the emulated machine rather than debugging MAME itself.

Target CPUs include machines using Z80, 6809, 68000, and similar MAME-supported CPU cores.

## Product goal

Add a dockable debugger UI to Oasis Editor that uses the existing Oasis MAME child-process integration and Lua plugin bridge to control MAME's internal debugger.

The MVP should provide:

- CPU selection, initially defaulting to the main CPU.
- Run, break, and step controls.
- Execution breakpoints.
- Data watchpoints.
- Register view.
- PC-centered disassembly view using MAME's existing CPU disassemblers.
- Memory viewer/editor.
- Basic stack view.
- Debugger console passthrough.

## Existing Oasis context

The current WPF editor already has most of the non-debugger infrastructure needed:

- `MameProcessRunner` starts MAME as a child process and redirects stdin/stdout/stderr.
- The Oasis Lua plugin starts a stdin polling thread and dispatches plaintext commands through `command_processor.lua`.
- Current emulation controls already use the stdin command channel for commands such as pause, resume, state save/load, reset, throttle, and exit.
- `EditorShellView.xaml` already hosts the editor layout using AvalonDock.
- `EditorToolWindowId` and `EditorShellView.xaml.cs` already provide the pattern for show/hide/focus behavior for dockable tool windows.

Relevant files:

```text
WindowsNetProjects/OasisEditor/OasisEditor/MameProcessRunner.cs
WindowsNetProjects/OasisEditor/OasisEditor/MainWindowViewModel.cs
WindowsNetProjects/OasisEditor/OasisEditor/Views/EditorShellView.xaml
WindowsNetProjects/OasisEditor/OasisEditor/Views/EditorShellView.xaml.cs
WindowsNetProjects/OasisEditor/OasisEditor/Views/EditorToolWindowId.cs
WindowsNetProjects/OasisEditor/Assets/MAME/plugins/oasis/init.lua
WindowsNetProjects/OasisEditor/Assets/MAME/plugins/oasis/system/stdin_thread.lua
WindowsNetProjects/OasisEditor/Assets/MAME/plugins/oasis/system/command_processor.lua
```

## Important design decision: do not modify AvalonDock

When these docs refer to adding debugger windows to AvalonDock, that means adding more Oasis Editor `LayoutAnchorable` entries and Oasis-side views/viewmodels.

Do not edit the AvalonDock package or fork AvalonDock.

## Transport decision

Keep the initial debugger protocol on the existing stdin/stdout bridge.

MAME's `-output network` may be useful later for high-volume MAME output notifications, but it does not directly replace the Oasis Lua stdin command/control channel. The debugger should be designed so the transport can be swapped later, but the MVP should not block on a network migration.

Initial transport:

```text
Oasis Editor -> process stdin -> Oasis Lua plugin -> MAME Lua debugger API
MAME Lua plugin -> process stdout -> Oasis Editor parser/router
```

Recommended abstraction:

```csharp
public interface IMameProtocolTransport
{
    Task SendAsync(string line, CancellationToken cancellationToken);
    event EventHandler<string> LineReceived;
}
```

The first implementation can wrap the existing `IMameProcessRunner.WriteStandardInputAsync(...)` and stdout parser path.

## Mandatory MAME debugger launch prerequisite

All debugger functionality depends on MAME being launched with debugger support enabled.

Codex must inspect the current launch path before implementing debugger control features:

```text
BuildMameLaunchRequest()
MameProcessStartInfoBuilder
MameEmulationService
MainWindowViewModel MAME launch command methods
```

If Oasis does not currently launch MAME with `-debug`, add a debugger-enabled launch mode that appends it.

Debugger-mode launch arguments must include:

```text
-debug
-plugin oasis
-output console
```

`-debug` is not optional for this subproject. Without it, MAME debugger APIs, breakpoint/watchpoint control, stepping, register inspection, and disassembly may be unavailable or behave inconsistently.

The Oasis debugger UI must handle the case where the current MAME process was not launched in debugger mode by showing a disabled/debugger-unavailable state and logging a clear warning.

## High-level architecture

```text
OasisEditor WPF
  Features/MameDebugger
    ViewModels
    Views
    Protocol models
    Service/router/parser
        |
        | stdin/stdout protocol
        v
MAME oasis Lua plugin
  oasis/system/commands/debug.lua
  oasis/system/debugger/*.lua
        |
        | MAME Lua debugger/device APIs
        v
MAME debugger and emulated CPU devices
```

## Planned docs

Work through these files in order:

1. `01-protocol-and-lua-bridge.md`
2. `02-wpf-debugger-service.md`
3. `03-dockable-ui-panels.md`
4. `04-mvp-task-list.md`
5. `05-follow-up-polish-and-risks.md`

## MVP acceptance criteria

The MVP is complete when a developer can:

1. Start MAME from Oasis Editor in debugger mode with `-debug` confirmed in the launch arguments.
2. Open debugger dock windows.
3. Select or default to the main CPU.
4. Break execution.
5. Step execution.
6. Add/remove/enable/disable an execution breakpoint.
7. Hit a breakpoint and see the UI refresh.
8. View registers for the stopped CPU.
9. View disassembly around the current PC.
10. View a page of memory.
11. See enough diagnostic output to debug protocol failures.

## Non-goals for MVP

Do not attempt these in the first implementation:

- Source-level debugging.
- Perfect stack traces.
- Full trace logging.
- Network transport migration.
- Complex symbol management.
- UI-perfect Visual Studio parity.
- Deep CPU-specific step-over/step-out behavior.

The MVP should be a practical GUI front-end over MAME's existing low-level debugger capabilities.
