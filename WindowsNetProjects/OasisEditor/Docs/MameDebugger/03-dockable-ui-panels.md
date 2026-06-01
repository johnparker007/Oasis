# Dockable UI Panels

## Important

Do not modify AvalonDock.

Only add Oasis Editor views, viewmodels, and LayoutAnchorable entries.

## New tool windows

Add to EditorToolWindowId:

```text
DebuggerControl
DebuggerDisassembly
DebuggerRegisters
DebuggerMemory
DebuggerBreakpoints
DebuggerWatchpoints
DebuggerStack
DebuggerConsole
```

## Suggested layout

### Center

```text
Disassembly
```

### Right

```text
Registers
Breakpoints
Watchpoints
```

### Bottom

```text
Memory
Stack
Debugger Console
```

### Floating

```text
Debugger Control
```

## MVP panel requirements

### Debugger Control

- CPU selector
- Run
- Break
- Step
- Current PC

### Registers

- Register grid
- Refresh on stop

### Breakpoints

- Add
- Remove
- Enable
- Disable

### Memory

- Address
- Length
- Hex values

### Disassembly

- Address
- Bytes
- Instruction text
- Current PC highlight
- Breakpoint gutter
