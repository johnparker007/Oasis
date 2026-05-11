# MAME ROM Management Plan

This document defines the next discrete MAME workstream: project-level ROM management, validation, auto-download behavior, and runtime ROM provisioning.

## Goals

The editor should make ROM management mostly invisible to the user while still allowing explicit/manual control.

Desired behavior:

- the project stores a MAME ROM name;
- the editor knows whether the ROM exists locally;
- the editor can download the ROM automatically or manually;
- ROM downloads run in the background;
- the user receives passive status/progress feedback;
- ROM setup integrates cleanly with the existing MAME provisioning architecture.

The design should reuse architectural patterns already introduced for:

- MAME version discovery;
- background provisioning;
- setup state orchestration;
- validation;
- diagnostics/logging.

## Project Settings UI

Add a new section to Project Settings.

Suggested layout:

```text
MAME ROM
---------------------------------
ROM Name: [ __________________ ] [Download]

Status: Installed / Missing / Downloading / Failed

[x] Automatically download missing ROMs
```

## ROM Name Field

Add a string field:

```text
RomName
```

Suggested internal name:

```text
MameRomName
```

This value should persist as part of the project settings.

## Download Trigger Behavior

Do NOT attempt download on every keypress.

Download checks should occur when:

- the ROM field loses focus;
- the user presses Enter in the ROM field;
- the project loads;
- the user explicitly clicks Download;
- validation runs before emulation.

The ROM field should support editing without triggering repeated network requests.

## Auto-Download Preference

Add a project-level checkbox:

```text
Automatically download missing ROMs
```

Suggested internal name:

```text
AutomaticallyDownloadMissingRoms
```

Recommended default:

```text
true
```

This should be a project setting rather than a global editor preference because different projects may have different workflows.

## ROM Status Label

Add a visible ROM status field.

Suggested states:

- Unknown
- Checking
- Installed
- Missing
- Queued
- Downloading
- Extracting
- Failed
- Invalid
- Cancelled

The status should update asynchronously.

## ROM Storage Layout

ROMs should be editor-managed runtime assets.

Suggested location:

```text
%LOCALAPPDATA%\\OasisEditor\\MAME\\roms\\
```

Examples:

```text
%LOCALAPPDATA%\\OasisEditor\\MAME\\roms\\barcrest.zip
%LOCALAPPDATA%\\OasisEditor\\MAME\\roms\\mpu4.zip
```

Avoid storing ROMs inside:

- project folders;
- source-controlled folders;
- build output folders.

## MAME Runtime Integration

The selected MAME install should launch using the managed ROM folder.

Preferred direction:

```text
-rompath "%LOCALAPPDATA%\\OasisEditor\\MAME\\roms"
```

The editor should own the ROM runtime location.

## ROM Download Source

The legacy Unity editor already contains ROM download logic using an archive.org collection.

Codex should:

- locate and study the legacy implementation in:

```text
UnityProjects/LayoutEditor
```

- port the archive.org ROM URL logic;
- modernize the architecture;
- reuse the newer async/background provisioning patterns;
- reuse diagnostics/progress patterns from the MAME installer work.

Do not blindly port Unity coroutine/UI patterns.

## ROM Download Architecture

Preferred architecture:

```text
Project Settings UI
    ↓
Project Settings ViewModel
    ↓
MameRomProvisioningService
    ↓
HTTP / Filesystem
```

Suggested services:

- `MameRomProvisioningService`
- `MameRomValidationService`
- `MameRomDownloadService`
- `MameRomStateService`
- `MameRomDiagnosticsService`

## ROM Validation Rules

Validation should confirm:

- ROM name is non-empty;
- ROM archive exists locally;
- ROM archive is readable;
- ROM archive is non-empty;
- ROM file extension is valid;
- ROM download was not interrupted/corrupted.

Validation failures should:

- appear in Project Settings;
- appear in diagnostics/logs;
- never crash the editor.

## ROM Auto-Download Policy

If `AutomaticallyDownloadMissingRoms == true`:

- on project load, validate ROM existence;
- if missing, queue/download ROM automatically in background;
- if ROM name changes and editing completes, validate/download automatically;
- before emulation, validate ROM again.

If disabled:

- do not auto-download;
- still validate existence;
- still allow manual download button;
- still show missing/error status.

## Background Behavior

ROM downloads should be:

- asynchronous;
- cancellable where safe;
- non-modal;
- visible in status/progress UI;
- logged to diagnostics/output.

Do not block the editor while ROM downloads occur.

## Existing ROM Handling

If the ROM already exists:

- do not re-download;
- validate and mark Installed;
- optionally compare file size/hash later if corruption handling becomes necessary.

## Retry / Failure Behavior

If ROM download fails:

- preserve existing working ROM if present;
- show Failed state;
- expose Retry action;
- log diagnostics;
- do not crash startup/project loading.

## State Machine

Suggested ROM states:

- Unknown
- Checking
- Installed
- Missing
- Queued
- Downloading
- Downloaded
- Extracting
- Failed
- Invalid
- Cancelled

The UI should bind to explicit state rather than ad-hoc booleans.

## Testing

Yes, this should be tested.

Add tests around:

- ROM-name persistence;
- auto-download enabled behavior;
- auto-download disabled behavior;
- delayed trigger behavior after field edit completion;
- project-load validation;
- existing ROM detection;
- failed download handling;
- validation rules;
- state transitions;
- preserving working ROMs after failed download;
- diagnostics logging.

Tests should not perform real internet downloads.

Prefer:

- fake download services;
- fake filesystem abstractions;
- fake provisioning orchestrators;
- fake validation services.

## Recommended Codex Steps

### Step 1 - Add Project Settings Fields

Add:

- `MameRomName`
- `AutomaticallyDownloadMissingRoms`

Default auto-download to true.

Preserve backwards compatibility with older project files.

### Step 2 - Add Project Settings UI

Add:

- ROM name textbox;
- ROM status label;
- Download button;
- auto-download checkbox.

Persist values correctly.

### Step 3 - Add Validation Service

Add ROM validation abstraction/service.

Implement:

- ROM existence checks;
- ROM-path resolution;
- ROM-state updates.

### Step 4 - Port Legacy Download Logic

Study and port the archive.org ROM URL logic from the Unity project.

Modernize:

- async handling;
- diagnostics;
- cancellation;
- progress reporting;
- background orchestration.

### Step 5 - Add Auto-Download Triggers

Implement:

- project-load validation;
- focus-loss trigger;
- Enter-key trigger;
- manual Download button;
- pre-emulation validation.

Avoid per-keystroke download attempts.

### Step 6 - Add Tests

Add unit tests for:

- project settings migration;
- trigger behavior;
- validation behavior;
- provisioning orchestration;
- state transitions;
- failure handling.

### Step 7 - Integrate With MAME Runtime

Ensure launched MAME uses the managed ROM directory.

Preferred direction:

```text
-rompath "%LOCALAPPDATA%\\OasisEditor\\MAME\\roms"
```

## Manual Verification

After implementation, John should verify:

- ROM name persists correctly;
- status updates correctly;
- auto-download works on project load;
- auto-download works after field edit completion;
- downloads do not trigger per keystroke;
- manual Download button works;
- ROMs appear in managed LocalAppData folder;
- MAME can see downloaded ROMs;
- failed downloads do not crash the editor;
- previous working ROMs remain usable.
