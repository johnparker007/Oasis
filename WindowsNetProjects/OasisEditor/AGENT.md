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
