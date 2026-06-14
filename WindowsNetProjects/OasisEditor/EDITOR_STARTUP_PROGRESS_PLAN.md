# Editor Startup Progress Plan

This document defines a focused follow-up to the modal progress workstream: hide the partially initialized Editor window while opening a project from the Launcher, and show shared modal progress instead.

## Problem

The in-editor modal progress system now covers several slow operations, but opening the Editor from the Launcher can still show a partially rendered Editor window with large white or blank regions while WPF/editor initialization and first-load work completes.

Observed flow today:

```text
Launcher opens project
    -> MainWindow is constructed
    -> Launcher hides
    -> MainWindow shows
    -> Editor visual tree / shell / caches initialize
    -> user sees blank white regions during startup
```

Desired flow:

```text
Launcher opens project
    -> Launcher shows modal progress: Starting Editor
    -> Editor initializes hidden/off-screen/not-yet-shown
    -> expensive startup preparation completes
    -> Editor is shown only when ready for first useful render
    -> progress modal closes
```

## Current Code Notes

Current `LauncherWindowViewModel.OpenEditor` validates the project path, constructs `MainWindow`, hides the launcher, attaches `Closed`, and calls `mainWindow.Show()`.

Current `MainWindow` constructor performs WPF initialization, creates `EditorShellView`, assigns it to `EditorShellHost`, registers shortcuts, constructs `MainWindowViewModel`, subscribes tool-window events, and assigns `DataContext`.

This means the Editor window can be shown before its first useful render is complete.

## Goal

Add startup progress for Launcher -> Editor transition using the same progress system/style as the in-editor modal progress dialogs.

The main user-visible goal is:

```text
Do not show a half-rendered Editor window.
```

The progress dialog should say something like:

```text
Starting Editor...
Opening project...
Preparing editor shell...
Loading startup documents...
Finalizing editor...
```

Use indeterminate progress initially unless normalized progress is easy and reliable.

## Important Design Constraints

### Reuse Shared Progress UI

Do not create a completely separate progress visual style for Launcher startup.

Acceptable approaches:

- make the existing progress dialog/service usable from the Launcher;
- add a shared app-level progress dialog service used by both Launcher and Editor;
- add a Launcher-hosted progress dialog that reuses the existing progress controls/view models/styles.

### Avoid Showing MainWindow Until Ready

MainWindow should not be shown until enough initialization is complete that it can render without large blank/white uninitialized regions.

Possible approaches:

- construct/preload MainWindow while hidden, then show after readiness;
- add `MainWindow.PrepareForFirstShowAsync(...)`;
- delay `Show()` until after `ContentRendered`/layout/render preparation is complete;
- use `Opacity=0` only if no better option exists, then reveal once ready.

Prefer not to flash a hidden/transparent window on the taskbar if avoidable.

### Be Careful With UI Thread Work

Much WPF visual construction must remain on the UI thread.

Do not attempt a giant async rewrite of the editor startup path.

First pass should prioritize:

- showing startup progress before slow work begins;
- hiding the incomplete Editor window;
- identifying which work can safely move off the UI thread later.

If a UI-thread-only section blocks the progress animation briefly, that is acceptable for MVP as long as the user sees a proper startup progress modal instead of a broken Editor window.

## Recommended Implementation Steps

### Step 1 - Inventory Startup Path

Inspect and document:

- `LauncherWindowViewModel.OpenEditor`;
- `MainWindow` construction;
- `MainWindowViewModel` construction;
- project/document loading performed during startup;
- shell/tool-window creation;
- first render / first selected document logic;
- any Face/Panel2D render cache warmup triggered automatically.

Deliverable:

```text
EditorStartupProgress.Inventory.md
```

Include:

- what currently runs before `MainWindow.Show()`;
- what currently runs after `MainWindow.Show()`;
- which work is UI-thread-only;
- which work could be async/background later;
- which event is a reliable readiness point.

### Step 2 - Expose Shared Startup Progress To Launcher

Make the progress service or progress dialog usable from `LauncherWindowViewModel`.

Do not duplicate progress UI code.

If the existing progress service is too tightly coupled to `MainWindowViewModel`, extract the reusable parts so Launcher and Editor can share them.

### Step 3 - Add Hidden Editor Preparation Flow

Replace direct open flow with something like:

```text
await progressService.RunAsync(
    new EditorProgressRequest("Starting Editor", "Opening project...", Indeterminate),
    async progress =>
    {
        validate project;
        progress.ReportIndeterminate("Preparing editor shell...");
        create MainWindow but do not show it yet;
        await mainWindow.PrepareForFirstShowAsync(progress, token);
        progress.ReportIndeterminate("Finalizing editor...");
    });

hide launcher;
show main window;
```

Actual code shape may differ to fit the app.

### Step 4 - Add MainWindow Readiness Hook

Add a minimal readiness/preparation API, for example:

```text
Task PrepareForFirstShowAsync(IEditorProgressReporter progress, CancellationToken token)
```

or equivalent.

This should avoid major behavior changes. It can start as a small method that yields through dispatcher/layout stages and warms obvious startup caches.

### Step 5 - Error Handling

If startup fails:

- close/dispose any hidden MainWindow if created;
- keep or restore Launcher visibility;
- show/log a useful error;
- close the progress modal;
- do not leave an invisible main window alive.

### Step 6 - Manual Testing

Manual test cases:

- open project from Launcher for first time;
- verify progress appears before Editor;
- verify no large white/blank Editor window is visible;
- verify Editor appears only when ready;
- close project and return/reopen if supported;
- verify repeated open is still correct;
- verify startup failure returns to Launcher cleanly;
- verify existing in-editor progress dialogs still work.

## Out Of Scope For First Pass

Do not implement:

- full startup task scheduler;
- splash screen replacement unless necessary;
- background loading of every document type;
- full async rewrite of `MainWindowViewModel` construction;
- progress for every startup sub-step if the exact counts are not available.

## Acceptance Criteria

- Launcher -> Editor transition uses shared progress styling/system.
- Editor window is not visible while partially initialized.
- Startup progress modal appears with a clear message such as `Starting Editor`.
- Editor appears after the initial layout/render preparation is complete.
- Failure returns to Launcher cleanly.
- Existing in-editor modal progress behavior remains unchanged.
