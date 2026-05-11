# MAME ROM Management Plan

This document defines the next discrete MAME workstream: project-level ROM management, global ROM download source preferences, validation, auto-download behavior, and runtime ROM provisioning.

## Goals

The editor should make ROM management mostly invisible to the user while still allowing explicit/manual control.

Desired behavior:

- the project stores a MAME ROM name;
- global MAME preferences store where ROMs are downloaded from;
- global MAME preferences store whether ROM downloads use `.zip` or `.7z`;
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

Add or maintain a MAME ROM section in Project Settings.

Suggested layout:

```text
MAME ROM
---------------------------------
ROM Name: [ __________________ ] [Download]

Status: Installed / Missing / Downloading / Failed

[x] Automatically download missing ROMs
```

Project Settings should contain project-specific ROM identity and behavior only.

Project Settings should NOT contain the archive.org base URL or archive file extension preference.

## MAME Preferences UI

Add a ROM download source section to:

```text
Preferences -> MAME
```

Suggested layout:

```text
ROM Downloads
---------------------------------
Download URL base: [ https://archive.org/download/mame-0.272-romset-complete-merged/arcade ]
Archive format:    [ .7z v ]
                 [ Reset to default ]
```

The URL field is the base path used to construct per-ROM download URLs.

The archive format dropdown should support only:

- `.7z`
- `.zip`

Default archive format:

```text
.7z
```

## Default ROM Download Source

Default full example URL:

```text
https://archive.org/download/mame-0.272-romset-complete-merged/arcade/j2kingcl.7z
```

From this, the default configurable pieces should be:

Default base URL:

```text
https://archive.org/download/mame-0.272-romset-complete-merged/arcade
```

Default extension:

```text
.7z
```

For ROM name:

```text
j2kingcl
```

Constructed URL should be:

```text
https://archive.org/download/mame-0.272-romset-complete-merged/arcade/j2kingcl.7z
```

## Reset to Default

Add a `Reset to default` button in Preferences -> MAME -> ROM Downloads.

It should restore:

```text
RomDownloadBaseUrl = https://archive.org/download/mame-0.272-romset-complete-merged/arcade
RomDownloadArchiveExtension = .7z
```

The reset should update the UI immediately and persist using the normal preferences save path.

## Preference Model Fields

Add editor/global preference fields:

```text
RomDownloadBaseUrl
RomDownloadArchiveExtension
```

Recommended enum for extension:

```text
MameRomArchiveExtension
    SevenZip
    Zip
```

or a validated string constrained to:

```text
.7z
.zip
```

Recommended defaults:

```text
RomDownloadBaseUrl = https://archive.org/download/mame-0.272-romset-complete-merged/arcade
RomDownloadArchiveExtension = .7z
```

Preserve backwards compatibility with older preference JSON.

If old preferences do not contain these fields, load defaults.

## ROM Name Field

Add or maintain a project-level string field:

```text
RomName
```

Suggested internal name:

```text
MameRomName
```

This value should persist as part of the project settings.

## Download URL Construction

Do not store a full per-ROM URL in each project.

Construct the download URL from:

- global `RomDownloadBaseUrl` preference;
- project `MameRomName`;
- global `RomDownloadArchiveExtension` preference.

Construction rules:

- trim whitespace from ROM name;
- reject empty ROM names;
- trim trailing `/` from base URL before combining;
- ensure extension begins with `.`;
- only allow `.zip` and `.7z`;
- do not double-append extension if ROM name already contains `.zip` or `.7z`; instead normalize ROM name to extensionless internally or reject with a clear validation message.

Preferred construction:

```text
{baseUrl.TrimEnd('/')}/{romNameWithoutExtension}{extension}
```

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

Add or maintain a project-level checkbox:

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

Add or maintain a visible ROM status field.

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
%LOCALAPPDATA%\\OasisEditor\\MAME\\roms\\j2kingcl.7z
%LOCALAPPDATA%\\OasisEditor\\MAME\\roms\\j2kingcl.zip
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

- port any still-useful archive.org ROM URL logic;
- update it to use the new global MAME Preferences base URL and archive extension;
- modernize the architecture;
- reuse the newer async/background provisioning patterns;
- reuse diagnostics/progress patterns from the MAME installer work.

Do not blindly port Unity coroutine/UI patterns.

## ROM Download Architecture

Preferred architecture:

```text
Preferences -> MAME ROM Download Source
    ↓
Editor Preferences Model
    ↓
Project Settings UI
    ↓
Project Settings ViewModel
    ↓
MameRomProvisioningService
    ↓
MameRomDownloadService
    ↓
HTTP / Filesystem
```

Suggested services:

- `MameRomProvisioningService`
- `MameRomValidationService`
- `MameRomDownloadService`
- `MameRomStateService`
- `MameRomDiagnosticsService`
- `MameRomUrlBuilder`

## ROM Validation Rules

Validation should confirm:

- ROM name is non-empty;
- ROM name is valid after trimming/removing extension;
- base URL is non-empty and absolute HTTP/HTTPS URL;
- archive extension is either `.zip` or `.7z`;
- ROM archive exists locally;
- ROM archive is readable;
- ROM archive is non-empty;
- ROM file extension is valid;
- ROM download was not interrupted/corrupted.

Validation failures should:

- appear in Project Settings or Preferences depending on scope;
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

If the global ROM download source settings are invalid:

- do not attempt download;
- show a clear Preferences/MAME status error;
- keep Project Settings ROM status in Missing/Invalid Source state;
- do not crash project load.

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

Validation should check both configured extension and, if useful, the alternative supported extension.

Example:

- configured extension is `.7z`;
- `j2kingcl.zip` exists locally;
- UI can report that an alternate format exists rather than blindly downloading again.

The initial implementation may keep this simple and only check the configured extension, but Codex should document the chosen behavior.

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
- InvalidSource
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
- URL construction;
- default ROM source preference values;
- reset-to-default behavior;
- `.zip` and `.7z` extension handling;
- invalid base URL behavior;
- invalid extension behavior;
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

### Step 1 - Add Global ROM Download Preferences

Add editor/global preference fields:

- `RomDownloadBaseUrl`
- `RomDownloadArchiveExtension`

Defaults:

- `https://archive.org/download/mame-0.272-romset-complete-merged/arcade`
- `.7z`

Preserve backwards compatibility with older preference files.

### Step 2 - Add MAME Preferences UI

In Preferences -> MAME, add:

- ROM download base URL textbox;
- archive format dropdown with `.7z` and `.zip` only;
- Reset to default button.

Persist values correctly.

### Step 3 - Add URL Builder

Add a pure/testable URL builder that combines:

- base URL;
- ROM name;
- extension.

Add tests for:

- normal `.7z` URL;
- normal `.zip` URL;
- trailing slash base URL;
- ROM name with whitespace;
- invalid empty ROM;
- invalid extension;
- ROM name accidentally containing extension.

### Step 4 - Wire Download Service To Preferences

Update ROM download service/provisioning to use the URL builder and global preferences.

Remove hardcoded ROM download source assumptions from project-level logic.

### Step 5 - Add Reset Tests / Preference Tests

Add tests for:

- default values;
- migration from old preferences;
- reset-to-default command;
- persistence of changed URL/extension.

### Step 6 - Continue Project ROM Flow

Continue/finish:

- project-load validation;
- focus-loss trigger;
- Enter-key trigger;
- manual Download button;
- pre-emulation validation.

Avoid per-keystroke download attempts.

### Step 7 - Integrate With MAME Runtime

Ensure launched MAME uses the managed ROM directory.

Preferred direction:

```text
-rompath "%LOCALAPPDATA%\\OasisEditor\\MAME\\roms"
```

## Manual Verification

After implementation, John should verify:

- ROM name persists correctly;
- global ROM download base URL persists correctly;
- archive extension dropdown persists correctly;
- Reset to default restores archive.org base URL and `.7z`;
- constructed URL for `j2kingcl` matches the provided archive.org example;
- status updates correctly;
- auto-download works on project load;
- auto-download works after edit completion;
- downloads do not trigger per keystroke;
- manual Download button works;
- both `.zip` and `.7z` settings are accepted;
- ROMs appear in managed LocalAppData folder;
- MAME can see downloaded ROMs;
- failed downloads do not crash the editor;
- previous working ROMs remain usable.
