# Output Log Enhancement Plan

This document defines the next discrete editor workstream: improving the Output pane logging system so it can support MAME/Lua/stdout debugging and provide Unity-style persistent editor logs.

## Goals

The Output pane should become useful for debugging runtime systems such as MAME process launch, Lua plugin communication, stdout/stderr protocol parsing, ROM provisioning, and editor actions.

Required outcomes:

- filter visible log rows by severity;
- support standard Windows-style multi-selection;
- copy selected visible rows to the clipboard;
- add context menu actions;
- write all log entries to disk;
- preserve only current and previous editor-run log files;
- add Open Log and Show In Explorer actions.

## Existing Areas To Inspect

Codex should inspect the current implementation before changing it.

Likely files:

```text
WindowsNetProjects/OasisEditor/OasisEditor/OutputLogEntry.cs
WindowsNetProjects/OasisEditor/OasisEditor/Views/OutputLogView.xaml
WindowsNetProjects/OasisEditor/OasisEditor/Views/OutputLogView.xaml.cs
WindowsNetProjects/OasisEditor/OasisEditor/MainWindow.xaml
WindowsNetProjects/OasisEditor/OasisEditor/MainWindowViewModel.cs
```

Also inspect any existing shared context menu systems used by:

```text
Assets pane
Hierarchy pane
```

Reuse shared menu command patterns where practical.

## Log Severity Filtering

Add three filter checkboxes above the Output pane row list:

```text
[x] Info   [x] Warning   [x] Error
```

Behavior:

- all three filters should default to checked;
- unchecking a severity hides rows of that severity from the Output pane;
- re-checking a severity shows those rows again;
- filtering affects only the visible pane, not the underlying log collection;
- filtering must not delete entries;
- filtering must not affect the disk log file.

Suggested internal properties:

```text
ShowInfoLogs
ShowWarningLogs
ShowErrorLogs
```

Filtering should be implemented with a view/filter layer, not by removing entries from the source collection.

## Multi-Selection Requirements

The Output pane currently supports hover and single-row selection.

Add standard Windows-style multi-selection:

- click selects a single row;
- Ctrl+click toggles a row in/out of the selection;
- Shift+click selects the visible range between the anchor row and clicked row;
- keyboard Ctrl+A may select all visible rows if practical;
- selected rows should remain visually selected;
- hidden filtered rows must not participate in range selection or copy output.

Important:

- selection operates on visible rows after filtering;
- if filters change, hidden rows should be excluded from the active visible selection/copy behavior;
- implementation may clear or normalize selection after filter changes if that is simpler and documented.

## Clipboard Copy Requirements

Ctrl+C should copy selected visible rows to the Windows clipboard.

Behavior:

- if one visible row is selected, copy that row;
- if multiple visible rows are selected, copy all selected visible rows;
- preserve visible row order;
- exclude rows hidden by filters;
- use a text format suitable for pasting into AI chats, emails, or text documents.

Suggested row text format:

```text
[HH:mm:ss.fff] [Info] Message text
[HH:mm:ss.fff] [Warning] Message text
[HH:mm:ss.fff] [Error] Message text
```

If the current row model already contains date/time, use it. If it does not, add a timestamp to log entries.

## Context Menu Requirements

Add a right-click context menu to the Output pane row list.

If one row is selected:

```text
Clear Log
Copy Row
```

If multiple rows are selected:

```text
Clear Log
Copy Rows
```

Behavior:

- right-clicking a selected row should preserve current selection;
- right-clicking an unselected visible row may select that row and clear previous selection, matching common Windows behavior;
- Copy Row/Rows should copy only selected visible rows;
- Clear Log clears the in-memory/current pane log only;
- Clear Log must not clear or truncate the disk log file.

Reuse existing app context menu command patterns where practical.

## Disk Log Files

Add persistent text-file logging similar to Unity Editor logs.

Log directory should live under the editor-managed AppData system/runtime area.

Preferred location:

```text
%LOCALAPPDATA%\\OasisEditor\\System\\Logs\\
```

Alternative acceptable location if current runtime path conventions differ:

```text
%LOCALAPPDATA%\\OasisEditor\\Logs\\
```

Use existing `MameRuntimePaths` or AppData path helpers if present, but do not put logs inside a specific MAME version directory.

## Disk Log Filenames

Use exactly:

```text
Editor.log
Editor-prev.log
```

## Startup Log Rotation

On editor launch:

1. Ensure log directory exists.
2. If `Editor-prev.log` exists, delete it.
3. If `Editor.log` exists, rename/move it to `Editor-prev.log`.
4. Create a new empty `Editor.log`.
5. Append all future log entries for this run to `Editor.log`.

There should be at most:

```text
Editor.log
Editor-prev.log
```

in normal operation.

## Disk Log Behavior

Every log entry added to the Output pane should also be appended to `Editor.log`.

Disk log behavior:

- append entries asynchronously or cheaply enough to avoid UI stalls;
- preserve entry order;
- include timestamp/severity/message;
- include exception/details text if available;
- tolerate file write errors without crashing the editor;
- if disk logging fails, show an in-memory warning if safe, but avoid infinite logging recursion.

Clear Log behavior:

- clears visible/in-memory Output pane entries;
- does not delete or truncate `Editor.log`;
- future entries continue appending to `Editor.log`.

## Open Log / Show In Explorer

Add buttons above or near the Output pane row list:

```text
Open Log
Show In Explorer
```

### Open Log

Opens current log file:

```text
Editor.log
```

using the OS default text editor.

Implementation guidance:

- use `ProcessStartInfo` with `UseShellExecute = true`;
- ensure the file exists before opening;
- log a warning if open fails.

### Show In Explorer

Opens the log directory in Windows Explorer.

Implementation guidance:

- use `explorer.exe` or `ProcessStartInfo` with `UseShellExecute = true` on the directory;
- ensure directory exists before opening;
- log a warning if open fails.

## Logging Service Architecture

Avoid placing file I/O logic directly inside WPF controls.

Suggested architecture:

```text
OutputLogService
    ├── in-memory ObservableCollection / collection view
    ├── severity filtering state
    ├── clipboard formatting helper
    ├── disk log sink
    └── log file location provider
```

Suggested classes/interfaces:

```text
IOutputLogService
OutputLogService
OutputLogEntry
OutputLogSeverity
OutputLogFileSink
OutputLogPaths
OutputLogSelectionViewModel
OutputLogClipboardFormatter
```

If existing classes already exist, extend them instead of replacing everything.

## Severity Model

If not already present, define severity values:

```text
Info
Warning
Error
```

Map existing log calls onto these severities.

Future-compatible optional severities such as Debug/Trace can be deferred.

## Tests

This workstream should have tests for non-WPF logic.

Add or extend tests for:

- severity filtering;
- visible-row selection model;
- Shift range selection over visible rows;
- Ctrl toggle selection;
- copy formatting single row;
- copy formatting multiple rows;
- hidden filtered rows excluded from copy;
- context menu command text/availability if implemented in a view-model;
- log path generation;
- startup rotation behavior;
- append behavior;
- Clear Log not clearing disk log;
- Open Log / Show In Explorer command guards where practical.

Do not require tests to open Notepad/Explorer.

Use fake filesystem/process abstractions where practical.

## Recommended Codex Steps

### Step 1 - Inventory Current Output Log

Document current Output pane implementation:

- current row model;
- severity support;
- current selection behavior;
- current clear/copy behavior;
- current logging call sites;
- any existing service or direct view-model implementation.

### Step 2 - Add Disk Log Service

Implement log path helper, startup rotation, and append behavior.

Add tests for:

- rotation;
- append;
- Clear Log not truncating disk file.

### Step 3 - Add Severity Filters

Add Info/Warning/Error checkboxes and view filtering.

Add tests for filtering logic.

### Step 4 - Add Multi-Selection And Copy

Implement visible-row multi-selection and Ctrl+C copy.

Add tests for:

- single selection;
- Ctrl multi-select;
- Shift range select;
- filtered hidden rows excluded;
- copy text formatting.

### Step 5 - Add Context Menu

Add right-click menu:

- Clear Log;
- Copy Row or Copy Rows depending on selected count.

Reuse shared context menu command patterns where practical.

### Step 6 - Add Open Log / Show In Explorer Buttons

Add buttons to Output pane toolbar.

Use the disk log service paths.

Handle failures gracefully.

### Step 7 - Wire MAME Runtime Logs Through This System

Ensure MAME process launch, stdout/stderr, Lua command, parser, ROM, and provisioning logs use the enhanced Output log service.

## Manual Verification

After implementation, John should verify:

- Info/Warning/Error filters show/hide rows correctly;
- filtering does not delete rows;
- single row selection still works;
- Ctrl+click multi-select works;
- Shift+click range select works after scrolling;
- Ctrl+C copies selected visible rows;
- hidden filtered rows are not copied;
- context menu shows Copy Row for one row;
- context menu shows Copy Rows for multiple rows;
- Clear Log clears pane only;
- `Editor.log` exists in AppData system/log folder;
- previous run rotates to `Editor-prev.log`;
- Clear Log does not clear `Editor.log`;
- Open Log opens the current log file;
- Show In Explorer opens the log folder;
- MAME debug logs are easier to copy into AI/chat tools.
