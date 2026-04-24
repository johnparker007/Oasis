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

## Docking Rules
- WPF DockPanel is only a layout container and must not be used as the editor docking system
- Use a dedicated docking framework (AvalonDock) for dockable tabs and tool windows
- Docking framework usage must be isolated to Editor.Shell.Wpf
- Feature modules must not depend directly on AvalonDock types
- Distinguish between:
  - Documents (Panel2D, Cabinet3D, Machine)
  - Tool windows (Hierarchy, Inspector, Assets, Output)
- The editor must remain functional even if the docking framework is replaced later

## Theme Rules
- Do not hard-code UI colors in views or code-behind
- Use semantic theme resources for editor UI colors
- New UI must work in System, Light, and Dark modes
- Prefer app-defined semantic brushes over direct Fluent resource usage in feature views

## WPF Maintainability Rules
- Large XAML files should not be preserved solely to give Codex more context
- MainWindow.xaml should act primarily as the application shell
- Use UserControls for cohesive UI regions
- Use ResourceDictionaries for shared styles and brushes
- Do not extract views purely to reduce line count

## ViewModel Maintainability Rules
- Avoid overloading MainWindowViewModel
- Extract cohesive logic into dedicated ViewModels
- Prefer composition over large rewrites

## Canvas Behavior Refactor Rules
- Keep canvas behavior modular (pan/zoom, selection, etc.)
- Do not mix document logic into canvas interaction code

## Refactor Workflow Rules
- Complete one refactor at a time
- Build after each change
- Fix errors before continuing
- Keep refactor-only changes separate from feature work

## Current Focus
Follow TASKS.md in order.
Always implement the next unchecked task unless instructed otherwise.

For large changes:
- complete 2–3 tasks at a time
- keep the app runnable
- avoid mixing unrelated changes

## How to Work
- Keep changes minimal and focused
- Do not refactor unrelated systems
- Ask for clarification if needed