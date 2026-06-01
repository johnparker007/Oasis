# Follow-up Polish And Risks

## Post-MVP enhancements

### Step over

Implement CPU-aware step-over support.

### Step out

Implement CPU-aware step-out support.

### Symbols

Allow Oasis project labels to appear in disassembly.

### Breakpoint persistence

Persist breakpoints per ROM/project.

### Layout persistence

Persist debugger window arrangement.

### Call stack improvements

Investigate shadow call stack tracking.

### Trace logging

Optional instruction tracing.

## Risks

### Disassembly extraction

MAME debugger command output may require parsing.

### CPU differences

Register naming differs significantly between CPUs.

### Multi-CPU systems

Debugger state must always be CPU-qualified.

### MAME version changes

Lua debugger APIs can change between releases.

## Review checkpoints

After each phase:

1. Open a pull request.
2. Run manual debugger test scenarios.
3. Review architecture before moving to the next phase.

Avoid building all phases in a single large branch.
