# Panel2D Runtime State Performance Plan

This document defines the next discrete workstream: separating high-frequency MAME runtime state from the design-time Panel2D document/edit pipeline so lamp flashing can run efficiently in the existing editor view.

## Problem

Basic MAME emulation is now able to drive lamps on a Panel2D, but flashing lamps currently make the Oasis Editor process consume excessive CPU.

This strongly suggests that runtime lamp state changes are flowing through design-time systems such as:

- document JSON refresh;
- undo/redo;
- dirty-state updates;
- inspector/property change pipeline;
- canvas rebuild/remap;
- layout validation;
- visual recreation;
- excessive UI-thread dispatching.

High-frequency runtime changes must not use the same path as user edits.

## Core Architecture

Use this rule:

```text
Design-time document model != Runtime state model
```

Preferred architecture:

```text
Panel2D document model
    -> design/edit view and static visual structure

MAME stdout parser
    -> runtime state model
    -> batched UI-frame runtime visual update
    -> existing lamp visuals only
```

The same edit view can show flashing lamps efficiently if runtime changes bypass document/edit systems.

## Goal

First milestone:

```text
MAME lamp updates
    -> runtime lamp state dictionary
    -> coalesced frame update at 30/60 Hz
    -> existing lamp visuals update lightweight state only
```

No canvas rebuilds.
No document mutations.
No JSON updates.
No undo/redo entries.
No dirty flag changes.
No inspector refreshes.

## Future Direction

After this is working, the same runtime state architecture should support:

- reels;
- 7-segment displays;
- 14/16-segment displays;
- meters;
- alpha displays;
- other MAME runtime-driven components.

Future preview/runtime pane:

```text
One document model.
One runtime state model.
Multiple views can consume them.
```

The edit pane should remain capable of showing live emulation, but a later optimized Preview/Runtime pane can render without editing chrome, selection, handles, or inspector overhead.

## Existing Areas To Inspect

Codex should inspect current implementation before changing it.

Likely files/classes:

```text
WindowsNetProjects/OasisEditor/OasisEditor/PanelRuntimeState.cs
WindowsNetProjects/OasisEditor/OasisEditor/PanelLayoutMapper.cs
WindowsNetProjects/OasisEditor/OasisEditor/Panel2DDocumentModel.cs
WindowsNetProjects/OasisEditor/OasisEditor/Panel2DDocumentStorage.cs
WindowsNetProjects/OasisEditor/OasisEditor/DocumentModel.cs
WindowsNetProjects/OasisEditor/OasisEditor/MainWindowViewModel.cs
WindowsNetProjects/OasisEditor/OasisEditor/CanvasPanBehavior.cs
WindowsNetProjects/OasisEditor/OasisEditor/CanvasPanZoomBehavior.cs
WindowsNetProjects/OasisEditor/OasisEditor/Views/InspectorView.xaml
WindowsNetProjects/OasisEditor/OasisEditor/Views/OutputLogView.xaml
```

Also inspect current MAME/runtime files created during the previous emulation workstream:

```text
MameEmulationService
MameProcessRunner
MameStdoutParser
MameLampStateParser
MameLampRuntimeAdapter
```

Actual names may differ. Use current repo state.

## Critical Non-Goals

Do not implement a separate preview pane yet.

Do not add reels/segments/meters yet.

Do not redesign all Panel2D components.

Do not use the document model as the live runtime state store.

Do not create undo/redo history for MAME lamp flashes.

Do not mark the project dirty when MAME updates runtime state.

## Performance Strategy

### 1. Runtime State Store

Add or refactor a runtime-only state store.

Suggested shape:

```text
PanelRuntimeState
    Lamps: Dictionary<int/string, bool>
    DirtyRuntimeKeys: set/list
    SetLampState(id, bool)
    SnapshotDirtyChangesAndClear()
```

Requirements:

- accepts high-frequency updates from MAME parser;
- coalesces repeated updates;
- records only latest state per lamp;
- exposes dirty changes for UI application;
- does not touch document model;
- does not touch undo/redo;
- can be tested without WPF.

### 2. Batched UI Application

Do not dispatch every lamp update individually to the UI thread.

Instead:

```text
MAME parser thread/background task
    -> update runtime state
    -> schedule/coalesce UI apply

UI thread timer/render loop
    -> apply latest dirty runtime state at max 30/60 Hz
```

Implementation options:

- `DispatcherTimer` at 30/60 Hz;
- `CompositionTarget.Rendering` throttled/coalesced;
- explicit `Dispatcher.BeginInvoke` guard that only allows one pending apply.

Preferred first implementation:

- simple coalesced `DispatcherTimer` or one-pending-dispatch mechanism;
- easy to reason about and test non-WPF parts.

### 3. Existing Visual Instance Updates

Runtime changes should update already-created visual instances.

For solid color/text lamps:

- pre-create/cache on/off brushes;
- update `Fill`, `Foreground`, `Background`, or `Opacity` only;
- avoid layout-affecting changes.

For graphical lamps:

- pre-load/cache on/off images where possible;
- prefer opacity swap between existing visuals or cached `Image.Source` swap;
- avoid rebuilding image controls or reloading files per flash.

Do not modify:

- `Width`;
- `Height`;
- `Margin`;
- child collection order;
- templates;
- document JSON;
- layout mapping;
- element model properties.

### 4. Runtime Visual Adapter

Add a small adapter between runtime state and existing visuals.

Suggested names:

```text
PanelRuntimeVisualRegistry
PanelRuntimeVisualAdapter
LampRuntimeVisualAdapter
```

Responsibilities:

- map lamp ID to existing WPF visual/control;
- apply runtime on/off state to that visual;
- register/unregister visuals as the Panel2D layout changes;
- ignore missing lamp IDs gracefully;
- log missing mappings at throttled diagnostic level.

### 5. Runtime Mapping

Lamp output from MAME must map to specific Panel2D lamp elements.

Codex should inspect existing lamp model fields.

If a stable mapping field already exists, use it.

If mapping is ambiguous:

- add the smallest non-disruptive adapter or field needed;
- document the mapping rule;
- avoid broad schema redesign in this phase.

### 6. Layout/Edit Changes Still Rebuild Normally

Design-time changes may still use the current layout rebuild path.

Examples:

- adding/removing lamp elements;
- moving/resizing lamps;
- changing lamp asset/image;
- changing lamp ID/mapping;
- loading a project.

When layout rebuilds happen:

- rebuild visual registry;
- then reapply current runtime state snapshot to new visuals;
- do not lose live lamp state if emulation is running.

## Instrumentation

Before or during refactor, add lightweight diagnostics:

- number of runtime lamp messages received per second;
- number of UI runtime apply batches per second;
- number of changed lamps applied per batch;
- whether a full canvas rebuild was triggered during runtime updates;
- warnings if runtime updates touch document/edit pipeline.

Use the enhanced Output log sparingly to avoid making logging itself a performance problem.

Prefer throttled diagnostic messages or a debug-only counter.

## Tests

This workstream should have tests for non-WPF logic.

Add tests for:

- `PanelRuntimeState` coalesces repeated lamp updates;
- dirty set contains only changed lamp IDs;
- unchanged repeated state does not create unnecessary dirty work;
- dirty snapshot clears after apply;
- runtime update does not mark document dirty;
- runtime update does not create undo history;
- runtime adapter applies only visible/current lamp IDs;
- missing lamp IDs are ignored/logged safely;
- filter/throttle diagnostics do not spam logs;
- layout rebuild can reapply current runtime state snapshot.

If WPF visual tests are hard, isolate most behavior into testable adapters/fakes.

Use fake visual targets such as:

```text
IRuntimeLampVisual
    SetRuntimeLampState(bool isOn)
```

Then the WPF implementation can wrap real controls.

## Recommended Codex Steps

### Step 1 - Inventory Current Runtime Lamp Path

Document the current path from MAME stdout lamp message to Panel2D visual change.

Identify whether each runtime lamp update currently touches:

- document model;
- JSON serialization;
- undo/redo;
- dirty flag;
- inspector;
- canvas rebuild;
- layout mapper;
- UI dispatch per lamp.

Create or update a small inventory note if useful.

### Step 2 - Add Runtime State Store

Add/refactor `PanelRuntimeState` so lamp state is runtime-only and coalesced.

Add tests for coalescing/dirty snapshot behavior.

### Step 3 - Add Runtime Visual Target Interface

Add a small testable abstraction for runtime lamp visuals.

Suggested:

```text
IRuntimeLampVisual
{
    void SetRuntimeLampState(bool isOn);
}
```

Implement fake targets for tests.

### Step 4 - Add Visual Registry / Adapter

Register lamp visuals by lamp ID when Panel2D visuals are created.

Apply runtime state changes through the registry/adapter.

Do not rebuild visuals when runtime state changes.

### Step 5 - Add Batched UI Apply Loop

Coalesce runtime updates and apply them to WPF visuals at max 30/60 Hz.

Ensure high-frequency MAME messages do not create high-frequency UI dispatches.

### Step 6 - Ensure Edit Pipeline Isolation

Confirm runtime lamp updates do not:

- mark document dirty;
- add undo entries;
- change inspector values;
- rewrite layout JSON;
- trigger full canvas reloads.

Add tests or diagnostics where practical.

### Step 7 - Apply To Existing Lamp Types

Wire the optimized runtime path to current supported lamp visuals:

- graphical lamps;
- solid color/text lamps.

Use lightweight property updates only.

### Step 8 - Manual Performance Verification

John should manually verify:

- MAME lamp flashing no longer maxes out a CPU core;
- lamps visibly flash in the edit Panel2D pane;
- editor remains responsive while emulation runs;
- selection/inspector editing still works when emulation is stopped;
- project is not marked dirty by lamp flashing;
- undo/redo history is not polluted by lamp flashing;
- layout edits still rebuild/refresh correctly;
- stopping emulation leaves the panel in a sensible state.

## Future Work After This Plan

After runtime lamp performance is fixed, add additional runtime-driven components in separate workstreams:

- reel state rendering;
- 7-segment displays;
- 14/16-segment displays;
- meters;
- input forwarding;
- dedicated Preview/Runtime pane;
- runtime frame/performance diagnostics panel.

Do not start these until lamp runtime updates are efficient.
