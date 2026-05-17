# MAME Input Map and Play View Plan

This document defines the next discrete MAME workstream: centralizing imported/input definitions into an Input Map, importing MFME lamp/button input metadata, and routing mouse/keyboard input from a non-editing Play View to MAME through the Oasis Lua/stdin command path.

## Summary

The editor should separate input identity from visual representation.

Core rule:

```text
InputDefinition != Lamp visual != Button visual
```

An input definition describes what should be sent to MAME.

Visuals such as 2D lamps, imported MFME buttons, and future 3D physical buttons can link to the same input definition.

## Desired First Milestone

After this workstream, the editor should be able to:

```text
Import MFME layout
    -> import lamps
    -> import MFME button components as Oasis lamps
    -> create Input Map entries from MFME button/coin input metadata
    -> show Input Map table/window
    -> open Play View
    -> click linked 2D lamp/button visuals
    -> send MAME set_input_value tag/mask/state commands
    -> press keyboard shortcuts while Play View is focused
    -> send the same MAME input commands
```

This workstream should focus on 2D input. 3D physical buttons will come later.

## Legacy Source Areas To Inspect

Codex must inspect the legacy Unity implementation before writing this feature.

Primary files:

```text
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MFME/ExtractImporter.cs
UnityProjects/LayoutEditor/Assets/_Project/Scripts/Oasis/MAME/MameController.cs
```

Search additionally for:

```text
ShortcutKeyHelper
MameInputPortHelper
GetMamePortTag
GetMAMEPortInputMaskName
SetButtonState
SetCoinState
ButtonNumber
Shortcut1
Shortcut2
HasButtonInput
HasCoinInput
```

The legacy importer already contains important behavior:

- MFME layout platform is mapped from `GamFile["System"]` into a MAME platform enum.
- MFME ROM name is imported into project MAME settings.
- MFME lamps with `HasButtonInput` enable input metadata, copy button number, inversion, and shortcut data.
- MFME lamps with coin input enable coin metadata.
- MFME button components are converted into lamp components and then imported as lamps.
- Legacy MAME input sending uses `set_input_value <tag> <mask> <0|1>`.
- Legacy button input resolves tag from button number plus fruit machine platform.
- Legacy coin input currently sends hardcoded tag/mask: `COINS` / `1`.

## Design Direction

Add a project-level Input Map.

Suggested project model:

```text
Project
  InputDefinitions[]
    - Id
    - Name
    - Kind
    - ButtonNumber
    - CoinInput
    - Inverted
    - KeyboardShortcut
    - LinkedVisualElementId
    - MamePortTag
    - MameMask
    - Notes
```

Prefer storing source/import fields plus resolved fields where useful.

Suggested `Kind` values:

```text
Button
Coin
Switch
Unknown
```

Do not make lamp visuals own the full input behavior long-term.

Lamps should only have an optional link to an input definition:

```text
LampElement.LinkedInputId
```

or an equivalent adapter/mapping if schema changes should be minimized.

## Input Map Window / Tool Pane

Add a UI window/pane/tool view for the Input Map.

Suggested title:

```text
Input Map
```

Suggested columns:

```text
Name | Kind | Button No | Coin | Key | Linked Visual | Tag | Mask | Inverted | Notes
```

Initial functionality:

- display imported input definitions;
- allow basic editing of keyboard shortcut, name, notes if safe;
- allow selecting linked visual if a good existing selector is available;
- show unresolved/missing tag/mask warnings;
- show duplicate button/key warnings.

If full editing is too much for the first pass, create read-only table plus diagnostics first.

## MFME Import Requirements

### Lamps With Inputs

When importing an MFME lamp with button input:

```text
MFME Lamp + HasButtonInput
    -> Oasis Lamp visual
    -> InputDefinition Kind=Button
    -> ButtonNumber from MFME ButtonNumberAsString
    -> Inverted from MFME Inverted
    -> KeyboardShortcut from MFME Shortcut1 when available
    -> LinkedVisualElementId set to imported lamp id
```

When importing an MFME lamp with coin input:

```text
MFME Lamp + HasCoinInput
    -> Oasis Lamp visual
    -> InputDefinition Kind=Coin
    -> Inverted from MFME Inverted
    -> KeyboardShortcut from MFME Shortcut1 when available
    -> LinkedVisualElementId set to imported lamp id
```

### MFME Buttons

MFME Button components should no longer be skipped.

They should be converted into Oasis lamps, matching the legacy Unity approach:

```text
MFME Button
    -> create equivalent MFME/Lamp import path
    -> Oasis Lamp visual
    -> InputDefinition
    -> linked visual id
```

This keeps 2D button visuals lamp-like for now, while input behavior is centralized.

## Keyboard Shortcut Import

Legacy code references `ShortcutKeyHelper.GetKeyCode(extractComponentLamp.Shortcut1)`.

Codex should locate and port the shortcut conversion table/helper if it exists.

If the helper is incomplete or not easily found:

- implement a small documented MFME shortcut-to-WPF-key mapper;
- cover common keys first;
- log unsupported shortcut names during import;
- preserve raw MFME shortcut string on the input definition for future conversion improvements.

Suggested fields:

```text
RawMfmeShortcut
KeyboardShortcut
```

Tests should cover the mapped keys and unsupported values.

## Platform-Specific MAME Tag/Mask Resolution

Legacy `MameController.SetButtonState` does:

```text
tag = MameInputPortHelper.GetMamePortTag(buttonNumber, platform)
mask = MameInputPortHelper.GetMAMEPortInputMaskName(buttonNumber)
set_input_value tag mask state
```

Codex should locate and port the relevant MameInputPortHelper behavior.

If helper source is not found quickly, create a placeholder resolver abstraction with a minimal working platform mapping based on current supported layouts, and document missing platforms.

Suggested services:

```text
IMameInputPortResolver
MameInputPortResolver
MfmeShortcutKeyMapper
MameInputCommandService
```

Resolver inputs:

```text
Project platform type
InputDefinition
ButtonNumber
CoinInput
```

Resolver output:

```text
MameInputCommandTarget
    - Tag
    - Mask
```

For coin input, preserve legacy initial behavior if still appropriate:

```text
tag = COINS
mask = 1
```

## Play View

Create a non-editing Play View window/pane.

Purpose:

- render the Panel2D document using existing visuals/runtime state;
- no selection/edit handles/inspector behavior;
- capture mouse and keyboard for gameplay input;
- show hand cursor over clickable linked visuals;
- route input down/up events to MAME.

Suggested menu command:

```text
Emulation -> Open Play View
```

or:

```text
View -> Play View
```

Preferred first implementation:

- separate window or dockable pane is acceptable;
- reuse existing Panel2D rendering code where possible;
- disable editing chrome and hit-test only input-linked visuals.

Do not implement edit-view Play Mode yet. That can come later using the same input-routing system.

## Input Runtime Flow

Mouse flow:

```text
Play View pointer down on linked visual
    -> resolve LinkedInputId
    -> resolve input tag/mask for current platform
    -> send set_input_value tag mask 1

Pointer up / lost capture / mouse leave safety
    -> send set_input_value tag mask 0
```

Keyboard flow:

```text
Play View focused
    -> KeyDown matches InputDefinition.KeyboardShortcut
    -> send input down once, ignoring key repeat

KeyUp
    -> send input up
```

Do not send key input when:

- Play View is not focused;
- MAME is not running;
- input cannot resolve tag/mask;
- ROM/project is not loaded.

## MAME Command Integration

Reuse existing MAME stdin/Lua communication path.

Command format from legacy:

```text
set_input_value <tag> <mask> <0|1>
```

Add safe guards:

- do not write if process is not running;
- do not write if stdin cannot be written;
- log command failures through Output Log;
- throttle duplicate warnings.

## State / Safety Requirements

Input down/up state should be tracked so that stuck inputs are avoided.

On any of these events, release all active inputs:

- Play View loses focus;
- Play View closes;
- Emulation stops;
- MAME process exits;
- project closes;
- exception while sending input;
- mouse capture lost.

## Tests

Add tests for non-WPF logic.

Suggested tests:

- MFME lamp with button input creates InputDefinition;
- MFME lamp with coin input creates InputDefinition;
- MFME button imports as Oasis lamp plus InputDefinition;
- imported input links to visual element id;
- shortcut mapping converts known MFME shortcut strings;
- unsupported shortcut is preserved/logged;
- platform resolver returns expected tag/mask for supported platform/button examples;
- coin resolver returns expected coin tag/mask;
- command service formats `set_input_value` correctly;
- keyboard key repeat does not spam input down;
- key up sends input release;
- focus loss releases active inputs;
- mouse down/up sends correct down/up commands;
- unresolved input does not send commands and logs warning;
- Play View disabled/not focused does not send keyboard input.

Do not require tests to launch real MAME.

Use fake MAME command writer, fake input definitions, and fake visual hit-test targets.

## Recommended Codex Steps

### Step 1 - Legacy Input Inventory

Create or update an inventory note summarizing:

- MFME input fields on lamps/buttons;
- MFME button import behavior;
- shortcut helper behavior;
- platform tag/mask helper behavior;
- legacy MAME command format;
- missing/open questions.

Do not implement major code until this inventory is captured.

### Step 2 - Add Project InputDefinition Model

Add project-level input definitions and serialization.

Add default/migration behavior for projects without Input Map data.

### Step 3 - Add Input Map UI

Add an Input Map window/tool pane/table.

Start read-only if necessary, then add safe edits.

### Step 4 - Update MFME Import

Import:

- MFME lamp inputs;
- MFME coin inputs;
- MFME button components as Oasis lamps;
- shortcut fields;
- linked visual element ids.

### Step 5 - Port Shortcut/Platform Helpers

Port or recreate:

- MFME shortcut-to-WPF key mapping;
- platform/button-to-MAME tag/mask mapping;
- coin input mapping.

### Step 6 - Add MAME Input Command Service

Implement input down/up command formatting and guarded send through existing stdin service.

### Step 7 - Add Play View Shell

Add non-editing Play View window/pane.

Render current Panel2D without selection/edit chrome.

### Step 8 - Add Mouse Input Routing

Click/hold linked 2D visual elements to send input down/up.

Use hand cursor over clickable visuals.

### Step 9 - Add Keyboard Input Routing

When Play View is focused, map KeyDown/KeyUp to InputDefinitions.

Handle key repeat and focus loss safely.

### Step 10 - Manual Verification

John should verify:

- Input Map table appears;
- MFME lamps with inputs create InputDefinitions;
- MFME buttons import as Oasis lamps;
- imported button visuals are clickable in Play View;
- keyboard shortcuts work while Play View is focused;
- inputs release correctly;
- MAME receives `set_input_value` commands;
- basic gameplay input works without breaking edit mode.

## Out Of Scope For This Workstream

Do not implement yet:

- 3D physical buttons;
- editable input capture UI beyond simple fields;
- complex multi-key shortcut combinations unless already easy from legacy code;
- input recording/macros;
- global keyboard hooks;
- edit-view Play Mode.

These should come later after the Input Map and Play View are stable.
