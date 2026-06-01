# WPF Debugger Service

## Goal

Keep debugger implementation isolated from the existing emulation runtime.

## New feature area

```text
OasisEditor/Features/MameDebugger
```

## Suggested classes

```text
MameDebuggerService
MameDebuggerProtocol
MameDebuggerResponseRouter
MameDebuggerStdoutParser
MameDebuggerState
```

## Responsibilities

### MameDebuggerService

- Public debugger API.
- Sends protocol requests.
- Raises debugger events.

### MameDebuggerResponseRouter

- Correlates request ids.
- Completes pending requests.

### MameDebuggerStdoutParser

- Detects protocol lines.
- Ignores unrelated MAME output.

### MameDebuggerState

Tracks:

```text
Current CPU
Execution state
Current PC
Breakpoints
Watchpoints
Registers
```

## Transport abstraction

Use a transport interface so stdio can be replaced later.

Initial implementation should wrap the current MAME process runner.
