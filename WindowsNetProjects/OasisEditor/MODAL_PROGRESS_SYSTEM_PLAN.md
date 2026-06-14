# Modal Progress System Plan

This document defines a new Oasis Editor workstream: introduce a shared modal progress system for long-running editor operations.

## Problem

Several operations currently stall the UI or appear to freeze the editor because there is no consistent progress reporting UI.

Known stall points:

- importing an MFME extract;
- loading saved documents such as `.panel2d` or `.face`;
- generating a Face from a Panel2D region;
- regenerating a Face from its source Panel2D;
- opening/showing Play View for a Face, especially when artwork, mask and runtime caches are loaded.

The editor needs one global progress system rather than one-off progress bars scattered through each feature.

## Goal

Add a reusable modal progress UI and service layer that long-running operations can use consistently.

Preferred behavior:

```text
Long-running operation starts
    -> modal progress overlay/dialog appears
    -> operation reports status/progress
    -> UI remains responsive where possible
    -> operation completes/fails/cancels
    -> modal closes and result/errors are reported
```

## User Experience Requirements

The progress UI should be modal.

While active:

- the user should not be able to start conflicting editor actions;
- the current operation name should be visible;
- the current step/status message should be visible;
- determinate progress should be shown where possible;
- indeterminate looping progress should be shown where normalized progress is not available;
- errors should be reported cleanly through existing error/logging paths.

The modal should support both:

```text
Determinate progress
    0% -> 100%

Indeterminate progress
    looping animation / marquee
```

Determinate examples:

- processing N files;
- importing N components;
- generating N Face elements;
- extracting masks for N lamps.

Indeterminate examples:

- opening a large document when no meaningful step count is available yet;
- first-time image cache warmup;
- waiting for a renderer/resource load path where progress cannot be normalized.

## Architecture Direction

Introduce a central progress service rather than direct dialog creation inside feature code.

Suggested types:

```text
IProgressDialogService
IEditorProgressScope
EditorProgressRequest
EditorProgressState
EditorProgressReporter
```

Actual names may differ to match existing code style.

The service should allow code such as:

```text
await progressService.RunAsync(
    new EditorProgressRequest("Importing MFME Extract"),
    async progress =>
    {
        progress.ReportIndeterminate("Reading extract...");
        progress.Report(0.25, "Importing lamps...");
        progress.Report(0.60, "Importing reels...");
        progress.Report(1.0, "Done");
    });
```

or equivalent.

## Core Concepts

### Progress Request

Should include:

- title;
- initial message;
- determinate/indeterminate mode;
- optional cancellation support;
- optional minimum display duration to avoid flicker;
- optional delay before showing for very fast operations.

### Progress State

Should include:

- title;
- message;
- progress value if determinate;
- indeterminate flag;
- cancellation availability;
- error state if needed.

### Progress Reporter

Should support:

```text
Report(double progress, string message)
ReportIndeterminate(string message)
ReportMessage(string message)
```

and should marshal updates onto the UI thread if required.

### Cancellation

Initial implementation may be non-cancellable if that is lower risk.

If cancellation is added, it should use `CancellationToken` and only enable Cancel for operations that safely support it.

Do not pretend an operation is cancellable if it cannot safely stop.

## Modal UI

Implement as either:

- a modal dialog window; or
- a modal overlay in the main editor shell.

Prefer the approach that fits existing WPF shell patterns.

The UI should include:

- title text;
- current status message;
- progress bar;
- optional Cancel button;
- optional details/error text if useful.

For indeterminate mode, use WPF progress bar indeterminate animation.

## Threading Requirements

Long-running work should not run on the UI thread if it can be safely moved.

However, many existing operations may currently be UI-thread-bound. Do not attempt a giant async rewrite in the first pass.

Safe initial strategy:

1. Add modal progress UI/service.
2. Wrap existing operations so the user sees progress/status.
3. Move expensive non-UI work off the UI thread incrementally where safe.
4. Add finer-grained progress reporting over time.

Be careful with WPF-bound document/view-model mutations.

## Operations To Integrate First

Prioritize these operations:

### 1. MFME Import

Progress opportunities:

- reading extract;
- loading assets;
- importing background/artwork;
- importing lamps;
- importing reels;
- importing displays;
- importing inputs;
- finalizing document.

Use determinate progress if counts are available; otherwise use step-based approximate progress.

### 2. Document Load

Progress opportunities:

- reading file;
- parsing JSON;
- building model;
- loading assets/caches;
- opening tab/view.

Use indeterminate initially if precise progress is not practical.

### 3. Generate Face From Region

Progress opportunities:

- validating source region;
- generating artwork;
- generating mask layer;
- generating lamps;
- generating buttons;
- generating displays;
- generating reels;
- opening Face document.

Determinate or step-based approximate progress should be feasible.

### 4. Regenerate Face

Progress opportunities:

- locating source Panel2D;
- validating provenance;
- regenerating artwork;
- regenerating mask;
- regenerating runtime elements;
- correlating/preserving manual elements;
- updating document.

Determinate or step-based approximate progress should be feasible.

### 5. Show/Open Face Play View

Progress opportunities:

- loading artwork asset;
- loading mask asset;
- loading/caching reel assets;
- warming render caches;
- preparing Face renderer state.

Use indeterminate initially if exact progress is not available.

## Logging And Error Handling

The progress system should integrate with existing Output log/error paths.

When an operation fails:

- close or transition the modal appropriately;
- log a useful error;
- avoid swallowing exceptions silently;
- do not leave editor state half-mutated where avoidable.

For multi-step operations, progress messages should be useful enough that John's bug reports can include the stage where the operation stalled or failed.

## Tests

Add non-WPF tests where practical.

Suggested tests:

- progress request/state defaults;
- determinate progress clamping;
- indeterminate state reporting;
- progress runner closes scope on success;
- progress runner closes scope on failure;
- cancellation token is passed through where supported;
- operation wrappers report expected progress messages using fake progress service.

Avoid fragile visual UI automation tests.

## Recommended Codex Steps

### Step 1 - Inventory Current Long-Running Operations

Inspect current code paths for:

- MFME import;
- document loading/opening;
- Face generation;
- Face regeneration;
- Play View loading/render cache warmup.

Document where progress hooks should be added.

Deliverable:

```text
ProgressSystem.Inventory.md
```

### Step 2 - Add Progress Abstractions

Add progress request/state/reporter/service abstractions.

Keep them UI-independent where possible.

### Step 3 - Add Modal Progress UI

Add the WPF modal dialog/overlay and bind it to progress state.

### Step 4 - Wire Global Service

Register/wire the service through existing main window/view-model/service patterns.

Ensure nested/conflicting progress operations are handled predictably.

Suggested initial behavior:

- reject nested modal progress operations with a clear error; or
- queue/prevent them.

Do not allow multiple progress modals to stack accidentally.

### Step 5 - Integrate Face Generation/Regeneration First

These are recent, well-contained workflows and should be good first integrations.

Add step-based progress.

### Step 6 - Integrate MFME Import

Add progress reporting around import stages.

### Step 7 - Integrate Document Load

Add indeterminate progress initially.

Improve to determinate later if practical.

### Step 8 - Integrate Face Play View Preparation

Add progress around expensive first-load/cache paths where practical.

Do not over-engineer renderer warmup if the current path does not expose clean hooks yet.

## Out Of Scope For First Pass

Do not implement:

- background task queue system;
- multiple simultaneous progress operations;
- complex task manager window;
- detailed per-file log console inside progress dialog;
- cancellation for unsafe operations;
- full async rewrite of the editor;
- progress for every possible small action.

## Manual Verification

John should verify:

- Face generation shows modal progress;
- Face regeneration shows modal progress;
- MFME import shows modal progress;
- loading large `.panel2d` and `.face` documents shows modal progress or at least does not appear frozen;
- opening Face Play View shows progress if preparation is slow;
- progress modal closes on success;
- errors are logged and modal does not get stuck;
- existing editor behavior remains unchanged after operations complete.
