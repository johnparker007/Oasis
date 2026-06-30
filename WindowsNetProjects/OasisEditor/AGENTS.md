# AGENTS.md

Guidance for Codex and other coding agents working in `WindowsNetProjects/OasisEditor`.

## Read Order

Start with only:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`

Do not scan every Markdown file in this directory by default.

Read additional planning/task documents only when directed by `00_CURRENT_PRIORITY.md` or when they are directly relevant to the current task.

## Project Overview

Oasis Editor is a WPF desktop application for creating and editing slot machine content.

The editor supports:

- Panel2D assets
- Face assets
- Cabinet3D assets
- Machine assembly
- Export to a Unity-based runtime

The editor is moving to a folder-as-asset storage model. Current implementation details and active workstreams are described in the current planning documents rather than this file.

## Environment Constraints

The Codex execution environment does **not** contain the required Windows/.NET/WPF toolchain.

Do not:

- attempt to build the solution
- attempt to run the application
- attempt to execute unit tests
- create `BuildAndTestAttempt`-style Markdown files

After completing a task, describe what should be tested locally.

## Context Rules

Prefer targeted source inspection over broad repository scans.

Avoid reading archived plans, generated documentation, build outputs, logs, generated assets, or unrelated Markdown unless the current task requires them.

## Architecture Principles

- Keep business logic UI-independent.
- Do not place business logic in WPF views or code-behind.
- Prefer small, focused, testable classes.
- Extend existing patterns where appropriate.
- Keep changes minimal and task-focused.
- Do not refactor unrelated systems.

## Project Model

Projects contain folders such as:

```text
Assets/
Generated/
Machines/

General rules:

User-authored content belongs under Assets/.
Disposable runtime/cache/export output belongs under Generated/.
The asset package is the user-facing asset.
The manifest describes the asset.
Prefer stable internal asset IDs where appropriate.

The current package layout and storage conventions are defined by the active planning documents.

Commands and Undo

Where the existing architecture supports undo/redo:

edits should be implemented as document-scoped commands
commands must target the correct document
no-op commands should not be recorded
dirty state should only reflect real changes
Inspector

The Inspector should behave similarly to the Unity Inspector.

Inspector edits should update the underlying model through the existing command architecture rather than mutating models directly.

Performance

Avoid broad rebuilds for small edits.

Prefer:

incremental notifications
incremental Inspector refresh
incremental hierarchy refresh
updating only affected visuals
Theme

Do not hard-code UI colours.

Use the application's semantic theme resources.

New UI should function correctly in System, Light, and Dark themes.

Testing

Add automated tests where practical.

After completing implementation, describe the local verification steps required.

Do not attempt to execute builds or tests within Codex.