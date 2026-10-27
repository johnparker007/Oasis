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

Determine the execution environment before running verification.

Restricted cloud or non-Windows agents without the Windows/.NET/WPF toolchain:

- Do not attempt WPF builds, launch the application, or run Windows-dependent tests.
- Describe the verification required in a supported local environment.

Local Windows agents with the required SDK, WindowsDesktop reference pack and runtime:

- May build OasisEditor and run existing managed tests using supported local tools.
- May launch and interact with the editor when desktop automation is available and the user permits desktop use.
- When the user is using the PC, perform background work only; do not activate windows, capture the desktop, or send mouse/keyboard input without agreement.
- Use disposable projects for UI verification; preserve user content and uncommitted work.
- Prefer existing tooling and cached dependencies. Obtain approval before software installation or substantial configuration changes.
- Capture actual UI evidence before claiming visual verification. Accessibility text alone does not verify layout, rendering or theme quality.
- Report commands, outcomes, warnings, failures, skipped tests and verification limitations.
- Do not change Unity packages or configuration merely to experiment with automation without approval.

Do not create `BuildAndTestAttempt`-style Markdown files.

All architectural, command/undo, theme, language compatibility and current-priority rules remain in force.

## Shared and Player Guidance

Read the repository-root `AGENTS.md` for shared product direction, PC-use constraints, verification standards, and standing GitHub permissions for agents. Unity language compatibility rules live in `UnityProjects/OasisPlayer/AGENTS.md` at the repository root; consult them when changing shared contracts or Player code.

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
```

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

Run builds and tests only in a supported environment as described under Environment Constraints.
