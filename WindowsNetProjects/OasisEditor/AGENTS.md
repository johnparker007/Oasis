# AGENTS.md

Guidance for Codex and other coding agents working in `WindowsNetProjects/OasisEditor`.

## Read Order

Start with only:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`

Do not scan every Markdown file in this directory by default.

Read additional planning/task documents only when directed by `00_CURRENT_PRIORITY.md` or when they are directly relevant to the current task.

## Project Overview

Oasis Editor is the portable Avalonia desktop application for authoring arcade machines and assets. The canonical application, MCP host and test projects are `OasisEditor`, `OasisEditor.Mcp` and `OasisEditor.Tests`. The former WPF Editor is retired; historical native-integration notes are kept under `Docs/NativeIntegration/`.

The Editor supports Panel2D, Face, Reel, Cabinet3D, Prefab and Machine authoring, native emulation preview, and Player build/export. Shared services are organized into `Oasis.Authoring.Core`, `Oasis.Emulation`, `Oasis.Rendering2D`, `Oasis.RuntimeBuild` and `Oasis.Rendering3D`.

## Environment Constraints

The canonical Editor is portable and targets .NET 9. Check project target frameworks and the installed SDK before building. Do not attempt builds or tests unsupported by the current environment.

- Desktop interaction and native emulation checks are separate from source/build validation. Report the exact checks performed and limitations.
- Do not activate windows, capture the desktop, or send mouse/keyboard input while the user is working without explicit agreement.
- Use disposable projects for UI verification; preserve user content and uncommitted work.
- Prefer existing tooling and cached dependencies. Obtain approval before software installation or substantial configuration changes.
- Capture actual UI evidence before claiming visual verification. Accessibility text alone does not verify layout, rendering or theme quality.
- Report commands, outcomes, warnings, failures, skipped tests and verification limitations.
- Do not change Unity packages or configuration merely to experiment with automation without approval.

Do not create `BuildAndTestAttempt`-style Markdown files.

Do not describe the retired WPF application as an available authoring surface. Preserve its implementation notes only under `Docs/NativeIntegration/` when relevant.

All architectural, command/undo, theme, language compatibility and current-priority rules remain in force.

## Shared and Player Guidance

Read the repository-root `AGENTS.md` for shared product direction, PC-use constraints, verification standards, and standing GitHub permissions for agents. Unity language compatibility rules live in `UnityProjects/OasisPlayer/AGENTS.md` at the repository root; consult them when changing shared contracts or Player code.

## Context Rules

Prefer targeted source inspection over broad repository scans.

Avoid reading archived plans, generated documentation, build outputs, logs, generated assets, or unrelated Markdown unless the current task requires them.

## Architecture Principles

- Keep business logic UI-independent.
- Do not place business logic in desktop views or code-behind.
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
