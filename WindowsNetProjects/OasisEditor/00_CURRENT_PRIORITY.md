# Current Priority for Codex

## Read First

Read only:

1. `AGENTS.md`
2. `00_CURRENT_PRIORITY.md`

Do not scan all Markdown files in this directory.
Open additional plans or task documents only when they are directly relevant to the requested work.

## Current Focus

Priority workstream:

- CLI/headless automation pipeline architecture
- Extraction of reusable project/import/save services
- Automation command runner abstractions
- UI-independent MFME import workflow
- Future CLI conversion workflow support
- Preparing for future OasisEditor.Core / OasisEditor.Cli separation

## Immediate Direction

Do not build an in-app terminal.

Instead:

- extract reusable project/import/save services
- add automation command abstractions
- make MFME import callable without WPF dialogs/views
- build a reusable automation pipeline
- prepare for future CLI/headless workflows

## Architectural Goals

- Core project/import/export logic should become UI-independent
- GUI and CLI workflows should reuse the same services
- Automation should not require visible WPF views
- Existing editor workflows should continue working
- Future OasisEditor.Core / OasisEditor.Cli separation should become easier

## Testing Direction

Prefer tests around:

- automation command runner behavior
- command sequencing and failure handling
- cancellation handling
- create project and panel services
- MFME import automation invocation
- save service invocation

Avoid tests requiring visible WPF windows.
