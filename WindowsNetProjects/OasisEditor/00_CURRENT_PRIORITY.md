# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. Output log enhancements.
2. Persistent disk logging.
3. Severity filtering.
4. Multi-selection and clipboard copy.
5. Context menu integration.
6. Runtime logging integration.

Codex should prioritize this work before continuing deeper MAME runtime/debugging tasks or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `OUTPUT_LOG_ENHANCEMENT_PLAN.md`
3. `MAME_EMULATION_RUNTIME_PLAN.md`
4. `MAME_ROM_MANAGEMENT_PLAN.md`
5. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
6. `MAME_VERSION_DISCOVERY_PLAN.md`
7. `MAME_ARCHITECTURE_REDESIGN.md`
8. `CODEX_NEXT_STEPS_MAME.md`
9. `TASKS_MAME_EMULATION_PORT.md`
10. `AGENT.md`
11. `TASKS.md`

## Immediate Task

Implement the enhanced Output pane logging system incrementally.

Current desired direction:

- add Info/Warning/Error filters;
- add persistent disk log files;
- add Windows-style multi-selection;
- add Ctrl+C clipboard copy;
- add right-click context menu actions;
- add Open Log and Show In Explorer actions;
- improve logging architecture for MAME/Lua/stdout debugging;
- add tests.

## Current Architectural Goals

- Logging should remain useful for runtime debugging.
- Filtering should not mutate/remove source log entries.
- Multi-selection should behave like standard Windows controls.
- Copy should only include visible selected rows.
- Disk logging should survive app restarts.
- Clear Log should only clear the visible/in-memory pane.
- File I/O should not live directly inside WPF controls.
- Logging failures should never crash the editor.

## Testing Direction

Codex should add or extend unit tests around:

- severity filtering;
- selection behavior;
- Shift range selection;
- Ctrl toggle selection;
- clipboard formatting;
- filtered hidden-row exclusion;
- log file rotation;
- append behavior;
- Clear Log behavior;
- log path generation.

The tests should not open real Notepad/Explorer windows.

Use fake filesystem/process abstractions where practical.

## Do Not Work On Yet

Do not continue unrelated canvas, panel layout, performance, copy/paste, ordering, locking, visibility, or general editor tasks unless explicitly instructed.

Do not continue deeper gameplay-input runtime work until logging/debugging support is stronger.

## Desired Output From Codex

Codex should produce small focused changes that:

- add Output pane filtering;
- add Output pane selection/copy improvements;
- add persistent log-file support;
- modernize logging architecture;
- add tests;
- integrate runtime diagnostics cleanly;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- Info/Warning/Error filters work correctly;
- Ctrl+click and Shift+click selection work correctly;
- Ctrl+C copies selected visible rows correctly;
- hidden filtered rows are excluded from copied text;
- context menu options change correctly between single/multi selection;
- `Editor.log` and `Editor-prev.log` rotate correctly;
- Clear Log only clears the pane;
- Open Log opens the current log file;
- Show In Explorer opens the log directory;
- MAME runtime/stdout diagnostics are easier to inspect and copy;
- no unrelated editor behavior changed.
