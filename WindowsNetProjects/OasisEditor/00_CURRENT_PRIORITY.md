# Current Priority for Codex

This file exists to disambiguate the next task when Codex is prompted with a broad instruction such as:

```text
We are working in WindowsNetProjects/OasisEditor. Please read the .md files in that dir, and proceed.
```

## Current Focus

The current active workstream is:

1. MAME auto-update policy.
2. Auto-update preference UI and persistence.
3. Automatic latest-version selection behavior.
4. Auto-update orchestration tests.
5. Background MAME provisioning stability.

Codex should prioritize this work before continuing older canvas/performance/layout tasks from `TASKS.md` or unrelated editor workstreams.

## Start Here

Read these files first, in this order:

1. `00_CURRENT_PRIORITY.md`
2. `MAME_AUTO_UPDATE_POLICY_PLAN.md`
3. `MAME_VERSION_DISCOVERY_PLAN.md`
4. `MAME_ARCHITECTURE_REDESIGN.md`
5. `CODEX_NEXT_STEPS_MAME.md`
6. `TASKS_MAME_EMULATION_PORT.md`
7. `AGENT.md`
8. `TASKS.md`

## Immediate Task

Implement the MAME auto-update policy incrementally.

Current desired direction:

- add an auto-update preference;
- default it to enabled;
- automatically install/select latest MAME when enabled;
- keep previous installed versions available;
- add orchestrator/policy tests;
- preserve resilient startup behavior.

## Current Architectural Goals

- MAME should remain mostly invisible to the user.
- Latest MAME should normally become active automatically.
- Existing working versions should not be deleted immediately.
- Auto-update should not interrupt a running MAME instance.
- All setup/provisioning should remain async/background-safe.
- Startup should never crash due to failed discovery/update.

## Testing Direction

Codex should add or extend unit tests around:

- preference migration/default behavior;
- auto-update enabled behavior;
- auto-update disabled behavior;
- latest-version selection behavior;
- deferred updates while MAME is running;
- fallback behavior during failed discovery;
- preserving previous working installs.

The tests should not require live internet access or real downloads.

Use fake services and fake orchestrator dependencies where practical.

## Do Not Work On Yet

Do not continue unrelated canvas, panel layout, performance, copy/paste, ordering, locking, visibility, or general editor tasks unless explicitly instructed.

Do not continue deep MAME runtime/process integration until the provisioning/update foundation is more stable.

## Desired Output From Codex

Codex should produce small focused changes that:

- add the auto-update preference;
- improve provisioning/update orchestration;
- add tests;
- preserve current provisioning behavior;
- include diagnostics/logging;
- include manual verification steps for John.

## Manual Test Expectations

After Codex makes the change, John should verify:

- new installs default to auto-update enabled;
- the checkbox persists correctly;
- enabling auto-update installs/selects latest MAME automatically;
- disabling auto-update preserves selected older versions;
- previous working installs remain available after update;
- startup remains stable when offline or discovery fails;
- no unrelated editor behavior changed.
