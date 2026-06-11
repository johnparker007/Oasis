# Face Generation Settings Plan

## Purpose

Face generation now creates mask data, auto-authored trays, emitters, overlays, runtime textures, and a texture-driven CPU preview. The next problem is authoring control: generated masks and trays can be close but not always the right size.

The editor needs tunable generation settings so a user can iterate:

```text
Generate Face
  -> inspect mask/tray overlays
  -> adjust generation settings
  -> regenerate Face
  -> inspect again
```

This is especially important for MFME-derived art, where mask extraction and tray derivation are heuristic.

## Core Decision

Use two related but separate settings groups:

1. Mask extraction settings
2. Tray derivation settings

Do not use one threshold for both jobs.

Mask extraction controls which pixels become part of the visible lamp mask.

Tray derivation controls how large the inferred physical lamp tray becomes.

These are different concepts:

```text
Mask threshold
  -> visible light through artwork/mask

Tray expansion
  -> inferred physical tray/compartment size
```

## Current Behaviour

`FaceMaskLayerExtractionService` currently uses a brightness delta threshold. The default threshold is hard-coded as `DefaultExtractionThreshold = 24`.

Pixels are included only when:

```text
Brightness(lampOnPixel) - Brightness(backgroundPixel) >= threshold
```

The contribution bounds used for rough tray authoring are then derived from the surviving pixels. This means dim falloff pixels are discarded, so contribution bounds can be too small for physical trays.

This behaviour is useful for generating clean visible masks, but it is not enough for physical tray inference.

## Per-Face Settings

Each Face document should persist the settings last used to generate/regenerate that Face.

Reason: one project may contain multiple Face documents extracted from different regions of the same Panel2D/MFME export, and each Face may need different tuning.

Suggested model shape:

```csharp
public sealed class FaceGenerationSettingsModel
{
    public byte MaskExtractionThreshold { get; init; } = 24;
    public double TrayBoundsInflationPercent { get; init; } = 15d;
    public double TrayBoundsPaddingPixels { get; init; } = 4d;
    public bool ClampTrayBoundsToLampWindow { get; init; } = false;
}
```

The exact names can follow project conventions.

The settings should be stored in the `.face` document, not only in global app settings. On save/close/reload, the Face document should reopen with the settings last used for that Face.

## App Default Settings

The app should also have default settings used when creating a new Face extraction.

Reason: users may work with a consistent style of source art and should not have to repeatedly tune the same defaults.

Suggested app-level defaults:

```text
Default mask extraction threshold
Default tray bounds inflation percent
Default tray bounds padding pixels
Default clamp tray bounds to lamp window
Show generation settings before regenerate
Show generation settings before generate
```

If project/app settings infrastructure is not currently mature enough, implement the least invasive persistence mechanism that fits current conventions, but keep the separation clear:

```text
app defaults -> copied into new Face generation settings
Face settings -> stored per Face and used for regeneration
```

## Generation/Regeneration UI

A simple dialog or panel should allow users to view and edit Face generation settings.

For the first implementation, keep it minimal:

```text
Face Generation Settings

Mask extraction threshold: [24]
Tray inflation percent:   [15]
Tray padding pixels:      [4]
Clamp tray to lamp rect:  [ ]

[Generate Face] or [Regenerate Face]
[Cancel]
```

The UI should reflect the selected Face document's settings when a Face is selected.

When generating a new Face from a Panel2D selection, the UI should start from app defaults.

When regenerating an existing Face, the UI should start from that Face document's persisted settings.

## Dialog Behaviour

The general app settings should decide whether the settings dialog appears automatically.

Suggested behaviour:

```text
Generate Face:
  if ShowGenerationSettingsBeforeGenerate is true
    show settings UI using app defaults
  else
    generate using app defaults

Regenerate Face:
  if ShowGenerationSettingsBeforeRegenerate is true
    show settings UI using selected Face settings
  else
    regenerate using selected Face settings
```

The user should also be able to open the settings UI manually from the top menu or another existing command location.

Manual access is important when the automatic popup is disabled.

## Menu/Command Direction

Add a menu/command entry equivalent to:

```text
Face -> Generation Settings...
```

or fit it into the existing app menu structure.

When a Face document is selected:

- show/edit settings for that Face
- include a Regenerate Face action where practical

When a Panel2D region is selected and Face generation is possible:

- show/edit default settings for the next Face generation
- include a Generate Face action where practical

Do not over-build this in the first implementation. A basic dialog invoked from the existing Generate/Regenerate commands is enough if menu integration would require broad UI changes.

## Regeneration Flow

Regeneration should use the selected Face document's persisted generation settings.

When the user changes settings in the dialog and confirms regeneration:

1. update the Face document's generation settings
2. regenerate the mask layer using the chosen mask extraction threshold
3. auto-author rough trays using the chosen tray derivation settings
4. update overlays/runtime-related data as existing flows require
5. mark the document dirty when settings or generated data changed

## Tray Derivation Settings

The first implementation should not lower the mask threshold just to make trays bigger.

Instead, derive trays from mask contribution bounds plus expansion:

```text
rough tray bounds = contribution bounds
rough tray bounds = inflate by TrayBoundsPaddingPixels
rough tray bounds = inflate by TrayBoundsInflationPercent
```

If no contribution bounds exist, fall back to lamp window bounds.

`ClampTrayBoundsToLampWindow` is optional, but useful to avoid excessive expansion. If implemented, it should clamp the expanded tray to the original lamp window bounds.

## Suggested First Implementation Scope

Do this first:

- add per-Face generation settings model
- persist it in the latest Face schema
- add app-level default generation settings if there is a clear settings location
- make Face generation copy app defaults into the new Face
- make Face regeneration use the Face's persisted settings
- pass `MaskExtractionThreshold` into mask extraction
- pass tray expansion settings into auto-authoring
- add a minimal settings UI for generate/regenerate flows
- add optional automatic popup behaviour if it fits current command structure

Do not do this yet:

- full manual tray editing
- full app settings redesign
- complex live preview inside the settings dialog
- connected-component tray tracing
- Unity integration

## Testing Guidance

Codex cannot run the WPF/.NET build in its environment. John will run builds/tests locally.

Suggested tests:

- new Face stores generation settings copied from defaults
- saved/reloaded Face preserves generation settings
- regeneration uses Face-specific mask threshold
- regeneration updates Face-specific settings when user confirms new values
- lower mask threshold increases or preserves mask contribution pixel count in controlled test data
- tray padding/inflation increases generated tray bounds while mask extraction remains unchanged
- settings dialog/view model initializes from selected Face settings for regeneration
- settings dialog/view model initializes from app defaults for new Face generation
- automatic dialog flag can be respected without breaking direct commands

Avoid tests requiring visible WPF windows where practical.

## Non-Goals

- Do not migrate older `.face` files unless explicitly requested.
- Do not replace the tray overlay/editor roadmap.
- Do not switch runtime export to authored trays as part of this task unless already trivial and explicitly requested.
- Do not implement Unity shader/runtime work.
