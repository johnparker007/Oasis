# Phase T Smoke Test Attempt

Date: 2026-04-28

## Goal

Attempt the next unchecked task in `TASKS.md`:

- **Phase T — Smoke test importing into an empty project/document**

## Planned checklist

- Imported assets copied under `Assets/MfmeImport/...`
- Imported native elements present at expected positions/sizes
- Hierarchy groups include imported native elements
- Selection and inspector work for imported elements
- Save/reopen preserves native elements and asset paths
- Undo/redo import remains document-local

## Environment constraints

1. The container does not have the .NET SDK/CLI (`dotnet` unavailable).
2. OasisEditor is a WPF desktop app and cannot be launched in this headless Linux container.

Because of those constraints, the manual UI smoke flow for Phase T could not be executed here.

## Verification attempted

- `dotnet --info`

## Result

- `/bin/bash: line 1: dotnet: command not found`

## Next step on a Windows dev machine

Run the full Phase T smoke checklist from `TASKS.md` using the existing MFME smoke fixture.
