# AGENT.md

## Project Overview
This is a WPF-based desktop editor for designing slot machine content.

The editor supports:
- 2D panel layout (.panel2d)
- 3D cabinet integration (.cabinet3d)
- machine assembly (.machine)
- export to a runtime format for a Unity-based arcade application

Current product direction:
- Focus first on the 2D design workflow for panels containing artwork, lamp backlighting, and masks.
- Keep cabinet/3D model work planned but deferred. Blender-authored 3D models will come later.
- The editor should feel familiar to users of the Unity Editor where that improves usability, especially around hierarchy, inspector, project/assets browsing, and context menus.

## Critical Environment Constraint (IMPORTANT)
- The Codex execution environment does NOT have the required Windows/.NET/WPF toolchain.
- Do NOT attempt to build, run, or execute tests locally in Codex after making changes.
- Do NOT create or update "BuildAndTestAttempt" style files.
- After completing a task, describe what should be tested, and John will run builds/tests locally.
- Focus on correctness of code changes, not local execution in Codex.

## Architecture
The solution is structured as:

- Editor.Core -> domain logic (NO WPF dependencies)
- Editor.Shell.Wpf -> UI shell only
- Feature modules:
  - PanelEditor
  - CabinetEditor
  - MachineEditor

## Key Principles
- All mutations must go through ICommand (undo/redo support)
- Do NOT put business logic in WPF views or code-behind
- Keep Core fully UI-agnostic
- Prefer small, testable classes
- Prefer simple implementations over over-engineering

## Document Context Rules
- Each open document owns its own command history
- Undo/redo must operate only on the active document
- Commands must be bound to the document they were created for
- Commands must never apply to whichever document happens to be active later
- Selection state is document-specific
- Inspector and hierarchy panels must reflect the active document only
- Opening and closing document tabs are not part of undo/redo unless explicitly requested later

## Inspector Design Direction
- The Inspector must behave like the Unity Inspector.
- Selecting a Panel2D element must populate editable fields in the Inspector.
- Editing a field must:
  - update the underlying PanelElementModel via a document-scoped command
  - update the canvas immediately
  - update only the affected Inspector row/state unless selection or element type changed
  - update the hierarchy immediately only when hierarchy-visible data changed, such as name, kind, parent/grouping, order, visibility/lock icon state if displayed there, add/delete, or reorder
  - mark the document dirty
  - support undo/redo
- The Inspector must NOT directly mutate model objects.
- All edits must go through commands using clone-and-replace semantics.
- The Inspector must support both shared properties (X, Y, Width, Height, Name, visibility, lock) and type-specific properties (lamp number, reel stops, etc.).
- Hide irrelevant properties instead of disabling large blocks of UI.

## Performance and Change Notification Rules
These rules are mandatory for current and future OasisEditor work.

- Do NOT use `PanelLayoutJson` as a broad "everything changed, rebuild all UI" signal.
- JSON is persistence/save/load data, not the live change bus for routine editor interactions.
- Small element edits must not synchronously serialize the full panel layout, rebuild the entire Hierarchy, rebuild all Inspector rows, or recreate document panes.
- High-frequency UI interactions such as color picker slider drag, mouse movement, resize handles, and future drag transforms must have a preview/update path that avoids flooding the undo stack and avoids full panel-level refreshes.
- Commit one undoable command at the end of a continuous gesture when practical, for example color picker drag completed, transform drag completed, or focus/Enter commit for text fields.
- Use narrowly scoped document notifications/events for Panel2D changes. At minimum, distinguish:
  - selection changed
  - element property changed
  - element name changed
  - hierarchy structure changed (add/delete/reorder/group/ungroup/parent changes)
  - element visual changed
  - document persistence/dirty state changed
- For element property changes, include enough metadata for subscribers to decide what to update, such as document ID, object ID, changed property names, and flags like `AffectsCanvas`, `AffectsHierarchy`, `AffectsInspectorRows`, and `AffectsPersistence`.
- Inspector refresh must be incremental. Do not call `RebuildPropertyRows()` for every property commit unless the selected element identity, element kind, row set, or selection context changed.
- Hierarchy refresh must be incremental or skipped for non-structural property edits. X/Y/Width/Height/color changes must not rebuild the Hierarchy.
- Canvas visual updates should update or invalidate only affected element visuals where possible. Pan/zoom staying fast is not proof that the mutation path is healthy.
- Dirty state and save content must still remain correct. Deferring JSON regeneration is acceptable if save uses the canonical live model or a dirty serialization cache is invalidated and rebuilt only when needed.
- Tests should lock in the expected notification scope so future changes cannot reintroduce full-refresh behavior for simple edits.

## Project Model
The editor is project-based:
- Users must open/create a project before editing
- Projects contain Assets/, Machines/, Generated/, etc.

## Startup Flow Rules
- The application must start in a Launcher window
- The editor shell must NOT open without an active project
- Project creation/opening UI belongs in the Launcher window only
- Closing a project should return the user to the Launcher window
- The editor shell must assume a valid loaded project at all times

## Theme Rules
- Do not hard-code UI colors in views or code-behind
- Use semantic theme resources for editor UI colors
- New UI must work in System, Light, and Dark modes
- Prefer app-defined semantic brushes over direct Fluent resource usage in feature views

## WPF Maintainability Rules
- Large XAML files should not be preserved solely to give Codex more context
- `MainWindow.xaml` should act primarily as the application shell
- Use UserControls for cohesive UI regions such as asset browser, inspector, output/log, document tabs, and canvas host views
- Use ResourceDictionaries for shared styles, brushes, templates, and repeated UI resources
- Do not create UserControls only to reduce line count; extract only when the section has a clear responsibility
- Preserve existing bindings, commands, DataContext assumptions, and visual appearance during view extraction
- Prefer one view extraction per task

## ViewModel Maintainability Rules
- Avoid allowing `MainWindowViewModel` to become the owner of every editor concern
- Move cohesive child concepts into separate ViewModels when they have distinct state, commands, or responsibilities
- Keep public behavior and binding-facing property names stable during refactors unless a task explicitly allows a rename
- Prefer composition over broad rewrites

## Canvas Behavior Rules
- `CanvasPanBehavior.cs` should remain coordination/glue only
- Tool placement, command dispatch, selection, pan/zoom, visual mapping, and persistence mapping should stay in separate focused classes
- Canvas visuals are projections of the Panel2D model

## Command and Mutation Rules
- All Panel2D edits must be implemented as document-scoped commands
- Commands must be undoable and redoable
- Commands must target a specific document ID
- Commands must not operate on whichever document happens to be active
- No-op commands must not be recorded
- Model updates should use clone-and-replace, not mutation
- Commands must expose or trigger precise change metadata whenever the UI needs to update after command execution.
- No command should force unrelated panes to fully rebuild as a side effect of a simple property change.

## Context Menu Rules
- Implement right-click menus as reusable WPF resources/styles
- Commands must live in ViewModels or command services, not code-behind
- Do not duplicate keyboard/menu/context-menu behavior
- Commands must expose correct CanExecute state

## Code Review Rules
- The live editor state must be a UI-agnostic model
- JSON is persistence only, not the canonical model
- Panel2D elements must have stable IDs
- WPF types must not leak into domain models
- Dirty state must only reflect real changes
- Invalid operations must fail safely and visibly

## Testing Rules
- Do not attempt to run builds/tests in Codex
- After implementing a task, specify what should be tested locally
- Add automated tests for model and command behavior where practical
- Add regression tests for UI update fanout where practical. Simple property edits must not trigger full Inspector row rebuilds or full Hierarchy rebuilds.

## Current Focus
Follow TASKS.md in order.
Current priority: fix OasisEditor Inspector/color-picker performance by replacing broad `PanelLayoutJson`-driven refreshes with scoped Panel2D change notifications and incremental pane updates.

## How to Work
- Keep changes minimal and focused
- Do not refactor unrelated systems
- Prefer extending existing patterns (commands, cloners, ViewModels)
- If a task is unclear, ask for clarification
