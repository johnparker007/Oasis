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
