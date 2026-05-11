# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. MAME emulation runtime integration.
2. Emulation OS menu port.
3. Hidden MAME process lifecycle.
4. stdio/Lua communication.
5. Lamp-state runtime integration.
6. Runtime parser/process tests.

Codex should prioritize this work before continuing older canvas/performance/layout tasks from `TASKS.md` or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `MAME_EMULATION_RUNTIME_PLAN.md`
3. `MAME_ROM_MANAGEMENT_PLAN.md`
4. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
5. `MAME_VERSION_DISCOVERY_PLAN.md`
6. `MAME_ARCHITECTURE_REDESIGN.md`
7. `CODEX_NEXT_STEPS_MAME.md`
8. `TASKS_MAME_EMULATION_PORT.md`
9. `AGENT.md`
10. `TASKS.md`

## Immediate Task

Implement the initial MAME emulation runtime integration incrementally.

Current desired direction:

- port the Emulation OS menu from the legacy editor;
- launch MAME as hidden child process;
- redirect stdin/stdout/stderr;
- integrate Oasis Lua plugin communication;
- parse lamp state output;
- wire lamp state into editor visuals/runtime state;
- modernize runtime architecture;
- add parser/process/state tests.

## Current Architectural Goals

- Emulation runtime should remain asynchronous and non-blocking.
- MAME should run as a managed hidden child process.
- Runtime lifecycle should be state-driven.
- stdio parsing should be resilient to malformed/unexpected lines.
- Lamp-state readback is the first runtime milestone.
- UI controls should not directly own Process instances.
- Startup/shutdown cleanup should be reliable.
- Runtime failures should never crash the editor.

## Testing Direction

Codex should add or extend unit tests around:

- menu command enabled-state logic;
- process start-info generation;
- stdout parser behavior;
- lamp state parsing;
- process lifecycle transitions;
- stop/cancel behavior;
- malformed stdout handling;
- validation failure behavior.

The tests should not launch real MAME.

Use fake services and fake process/stdout dependencies where practical.

## Do Not Work On Yet

Do not continue unrelated canvas, panel layout, performance, copy/paste, ordering, locking, visibility, or general editor tasks unless explicitly instructed.

Do not implement full gameplay input forwarding yet.

Do not implement reels/meters/segment displays yet.

## Desired Output From Codex

Codex should produce small focused changes that:

- add the Emulation menu shell;
- add runtime service abstractions;
- modernize process/stdout handling;
- add lamp runtime integration;
- add tests;
- preserve current provisioning behavior;
- include diagnostics/logging;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- Emulation menu appears;
- Start launches MAME hidden/background;
- Stop terminates MAME cleanly;
- stdout/stderr are visible in diagnostics/output log;
- lamp states are parsed correctly;
- lamp visuals update in the editor;
- failures do not crash the editor;
- no unrelated editor behavior changed.
