# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. MAME live version discovery.
2. Version-catalog fallback architecture.
3. Version discovery tests.
4. Background MAME provisioning stability.

Codex should prioritize this work before continuing older canvas/performance/layout tasks from `TASKS.md` or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `MAME_VERSION_DISCOVERY_PLAN.md`
3. `MAME_ARCHITECTURE_REDESIGN.md`
4. `CODEX_NEXT_STEPS_MAME.md`
5. `TASKS_MAME_EMULATION_PORT.md`
6. `AGENT.md`
7. `TASKS.md`

## Immediate Task

Implement the MAME version discovery redesign incrementally.

Current desired direction:

- replace hardcoded latest-version discovery;
- use live upstream release discovery;
- implement layered fallback behavior;
- add parser/service tests;
- keep startup resilient when offline or when upstream pages change.

## Current Architectural Goals

- Prefer mamedev.org release discovery.
- Use GitHub releases as fallback.
- Use LocalAppData cache as tertiary fallback.
- Keep compiled seed versions as final fallback.
- Keep all discovery async and background-safe.
- Never crash startup due to discovery failure.
- Keep MAME provisioning automatic.

## Testing Direction

Codex should begin introducing proper unit tests around:

- release-page parsing;
- version normalization;
- version ordering;
- fallback chains;
- cache compatibility;
- malformed/unexpected HTML.

The tests should not require live internet access.

Use fake HTML content and fake service implementations where practical.

## Do Not Work On Yet

Do not continue unrelated canvas, panel layout, performance, copy/paste, ordering, locking, visibility, or general editor tasks unless explicitly instructed.

Do not continue deep MAME runtime/process integration until the version discovery/provisioning foundation is more stable.

## Desired Output From Codex

Codex should produce small focused changes that:

- improve live version discovery;
- improve resilience/fallback handling;
- add tests;
- preserve current provisioning behavior;
- include diagnostics/logging;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- online startup discovers latest MAME;
- offline startup still succeeds via cache or seed fallback;
- malformed cache or failed refresh does not crash startup;
- auto-provisioning still installs valid MAME versions;
- Preferences displays latest-version state clearly;
- no unrelated editor behavior changed.
