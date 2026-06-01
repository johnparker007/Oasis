# MAME Debugger MVP Task List

This is the primary implementation checklist for Codex.

## Phase 1 - Protocol foundation

### WPF

- Create `Features/MameDebugger` folder structure.
- Create protocol request/response models.
- Create debugger stdout parser.
- Create debugger response router.
- Create debugger service abstraction.
- Add debugger-specific output log category.

### Lua

- Add `debug` command handler.
- Add JSON message parsing.
- Add protocol response helpers.
- Add protocol event helpers.

### Commands

Implement:

```text
ping
status
cpus
```

### Acceptance

- WPF can send request.
- Lua can respond.
- Responses correlate by request id.

---

## Phase 2 - Execution control

### Launch

- Add debugger-enabled launch mode.
- Verify MAME starts with debugger support enabled.

### Commands

Implement:

```text
run
break
step
```

### State monitoring

- Detect running/stopped transitions.
- Emit debugger events.

### Acceptance

- Break pauses execution.
- Step advances execution.
- Run resumes execution.
- UI updates correctly.

---

## Phase 3 - Breakpoints and watchpoints

### Commands

Implement:

```text
bp.set
bp.list
bp.enable
bp.disable
bp.clear

wp.set
wp.list
wp.enable
wp.disable
wp.clear
```

### Acceptance

- Breakpoints persist in UI while session runs.
- User can add/remove breakpoints.
- Breakpoint hit refreshes debugger state.

---

## Phase 4 - Registers and memory

### Commands

Implement:

```text
regs.get
regs.set
mem.read
mem.write
```

### Acceptance

- Register panel refreshes on stop.
- Memory panel refreshes on stop.
- Memory reads can target arbitrary addresses.

---

## Phase 5 - Disassembly

### Commands

Implement:

```text
disasm
```

### Acceptance

- Disassembly centers around current PC.
- Current PC line is highlighted.
- Breakpoint gutter can toggle breakpoints.

---

## Phase 6 - MVP completion

### Integration

- Dock windows open correctly.
- Panels refresh on stop.
- Protocol diagnostics visible in Output panel.

### Manual test scenarios

#### Scenario A

```text
Start emulation
Break
View registers
View disassembly
Resume
```

#### Scenario B

```text
Set breakpoint
Run
Hit breakpoint
Verify PC and disassembly location
```

#### Scenario C

```text
Open memory window
Inspect RAM
Modify value
Verify write succeeds
```

### Exit criteria

All acceptance criteria in README.md are satisfied.
