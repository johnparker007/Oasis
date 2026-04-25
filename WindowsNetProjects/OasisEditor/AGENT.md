# AGENT.md

## Project Overview
This is a WPF-based desktop editor for designing slot machine content.

The editor supports:
- 2D panel layout (.panel2d)
- 3D cabinet integration (.cabinet3d)
- machine assembly (.machine)
- export to a runtime format for a Unity-based arcade application

## Architecture
The solution is structured as:

- Editor.Core → domain logic (NO WPF dependencies)
- Editor.Shell.Wpf → UI shell only
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

## Canvas Behavior Refactor Rules
- Treat `CanvasPanBehavior.cs` as interaction code, not a dumping ground for document, persistence, selection, and command logic
- Split canvas behavior gradually into focused components
- Keep pan/zoom, selection, element creation, layout mapping, and mutation command logic separate where practical
- Do not change canvas behavior while extracting unless a task explicitly requests a behavior change

## Refactor Workflow Rules
- For risky files, propose a short split plan before editing
- Complete one refactor task at a time
- Build after each refactor task
- Fix compile errors, binding errors, and missing resource errors before marking a task complete
- Keep refactor-only changes separate from feature changes where practical

## Current Code Review Priorities
The immediate work is stability and maintainability for the existing WPF Panel2D editor. Before adding major new features, address the review-fix track in `TASKS.md` in order.

Priority order:
1. Correctness fixes that protect user work: dirty state, no-op command history, close-tab command safety.
2. Small duplication cleanup: shared add-element command.
3. Canvas behaviour split while preserving UX.
4. Introduce a live, UI-agnostic Panel2D model separate from JSON storage.
5. Add schema validation, migration hooks, and storage/model tests.
6. Only then plan layer ordering, locking, visibility, duplicate/copy/paste, cabinet import, machine assembly, or Unity export.

## Code Review Rules
- The live editor state should not be raw JSON long term. JSON is a persistence format, not the canonical in-memory model.
- Panel2D document state should move toward a UI-agnostic model containing elements with stable IDs, display names, kinds, coordinates, and dimensions.
- WPF controls, dependency properties, brushes, and canvas visuals must not leak into domain/model classes.
- Canvas visuals should be a projection of the Panel2D model.
- Save/load should preserve the existing `.panel2d` format until a dedicated schema migration task changes it.
- Dirty state must change only for real document mutations and must clear after successful save.
- No-op commands must not be recorded in undo/redo history.
- Commands that cannot execute should fail safely and visibly during development rather than silently corrupting state.
- Keep object identity based on stable object IDs, not display names or canvas positions.

## Panel2D Storage and Migration Rules
- Treat `SchemaVersion = 1` as the current `.panel2d` format.
- Add explicit validation before expanding the format.
- Add migration hooks before introducing schema version 2 or later.
- Unsupported future schema versions should produce clear errors rather than being silently normalised.
- Malformed or partially invalid files should fail gracefully with useful output-log/error messages.

## Canvas and Interaction Rules
- `CanvasPanBehavior.cs` should remain coordination/glue only.
- Tool placement, command dispatch, selection, pan/zoom, visual mapping, and persistence mapping should stay in separate focused classes.
- Non-persisted instructional or placeholder visuals must not be treated as selectable/persisted editor objects.
- Hierarchy should list persisted/model-backed objects only.
- Keep click-to-place, pan, zoom, selection, and hierarchy selection behaviour unchanged during refactor-only tasks.

## Testing Rules
- Build after every task.
- Smoke test the relevant editor flow after every task.
- Add automated tests for model/storage/command behaviour where practical.
- If a new test project is needed, keep it under `Oasis/WindowsNetProjects/OasisEditor` and limit it to the editor project.
- Do not proceed to the next task while compile errors, binding errors, missing resources, or failing tests remain.


## Current Focus
Follow TASKS.md in order.
Always implement the next unchecked task unless instructed otherwise.

For larger refactors:
- complete one small startup-flow task at a time
- keep the app runnable after each task where practical
- do not combine Launcher refactor work with unrelated editor changes

## How to Work
- Keep changes minimal and focused
- Do not refactor unrelated systems
- If a task is unclear, ask for clarification
