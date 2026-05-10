# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is the MAME Preferences and architecture redesign.

Codex should prioritize this work before continuing older canvas/performance/layout tasks from `TASKS.md` or other phase documents.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `MAME_ARCHITECTURE_REDESIGN.md`
3. `CODEX_NEXT_STEPS_MAME.md`
4. `TASKS_MAME_EMULATION_PORT.md`
5. `AGENT.md`
6. `TASKS.md`

## Immediate Task

Implement only the Preferences-window redesign and MAME settings architecture cleanup.

Specifically:

- Refactor Preferences into a two-pane category layout.
- Add a left category list with:
  - Appearance
  - MAME
- Move Light/Dark/System theme selection into the Appearance category.
- Move MAME controls into the MAME category.
- Remove editable MAME Install Root UI.
- Remove editable Lua Plugin Directory UI.
- Replace those editable path concepts with editor-managed paths.
- Introduce or prepare for a LocalAppData-managed MAME runtime root:

```text
%LOCALAPPDATA%\\OasisEditor\\MAME\\
```

- Treat Lua plugin deployment as automatic editor-managed behavior, not a user-configured directory.

## Do Not Work On Yet

Do not continue unrelated canvas, panel layout, performance, copy/paste, ordering, locking, visibility, or general editor tasks unless explicitly instructed.

Do not continue MAME process-launch/runtime integration until the Preferences/settings architecture has been stabilized.

## Desired Output From Codex

Codex should produce a small focused change that:

- updates the Preferences UI structure;
- keeps existing appearance preference behavior working;
- preserves existing MAME settings as needed, but changes their presentation and ownership model;
- documents any unimplemented MAME actions as TODOs or disabled buttons where appropriate;
- includes manual test steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- Preferences opens correctly.
- Appearance category displays theme selection.
- Theme selection still persists and applies.
- MAME category displays MAME-specific settings/actions.
- MAME Install Root is no longer an editable end-user field.
- Lua Plugin Directory is no longer an editable end-user field.
- No unrelated canvas/layout behavior changed.
