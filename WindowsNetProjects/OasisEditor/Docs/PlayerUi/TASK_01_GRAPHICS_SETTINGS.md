# Task 01: Oasis Player Graphics Settings

## Scope

Implement the first production runtime UI vertical slice in:

```text
UnityProjects/OasisPlayer
```

Read first:

1. `WindowsNetProjects/OasisEditor/Docs/PlayerUi/PLAYER_UI_CONTEXT.md`
2. this file

The task establishes the long-term Oasis Player UI Toolkit foundation and implements a functional Graphics Settings menu.

## Current Context

The Oasis Player currently has no end-user UI.

Runtime Face rendering already supports:

- runtime-owned Face materials
- ambient, main, and additional URP lighting
- point lights
- lamp-state and lamp lookup textures
- exposure-based lit-lamp rendering
- single-sided Face rendering
- working `FrontSide`
- working `FaceRotation`
- working `FaceFlipHorizontal`

The exposure-based lamp appearance is now substantially better than the previous washed-out implementation, but the Player needs global user-adjustable graphics settings for final lamp brightness and presentation.

These values are Player preferences. They must not be stored in individual machine assets or runtime machine exports.

## Goal

Create a UI Toolkit Graphics Settings menu that:

- can be opened at runtime
- exposes the initial Face lamp and bloom preferences
- previews changes immediately
- persists applied changes globally
- restores previous values when cancelled
- restores defined application defaults
- updates runtime rendering through centralised services rather than direct UI-to-material access

This task should leave a reusable foundation for later Audio, Controls, Accessibility, VR, and Developer settings pages.

## UI Technology

Use Unity UI Toolkit:

- UXML for screen structure
- USS for styling
- C# controller or presenter classes for behaviour

Do not implement the production menu with IMGUI or a new uGUI prefab hierarchy.

Do not build the entire screen procedurally in C# unless a specific element genuinely requires it.

## Initial Settings

Expose the following global Player graphics preferences.

### Face Lamps

- Lamp Exposure, in photographic stops
- Lamp Emission Strength, only if the current Face shader still has a distinct useful emission control

### Rendering

- Bloom Enabled
- Bloom Intensity

Before adding controls, inspect the current Face shader, runtime material setup, URP assets, renderer features, volumes, and post-processing configuration. Use the actual property names and runtime architecture where appropriate rather than creating disconnected duplicate settings.

If lamp emission was removed by the exposure-based shader replacement and no longer exists as a meaningful independent control, do not recreate obsolete rendering code merely to satisfy the draft setting list. Document the finding and expose only settings that map to a coherent current runtime feature.

## Ownership and Data Model

Create typed global Player settings rather than storing unrelated primitive values throughout the UI.

A reasonable shape may include:

```text
PlayerSettings
    GraphicsSettings
        LampExposureStops
        LampEmissionStrength
        BloomEnabled
        BloomIntensity
```

Names may be adjusted to match existing project conventions.

Define defaults centrally. Do not duplicate default values in UXML, controllers, persistence code, and rendering code.

The settings model should be easy to extend without redesigning the UI architecture.

## Services

Introduce clear responsibilities equivalent to:

```text
PlayerSettingsStore
    loads and saves persistent settings

PlayerSettingsService
    owns active settings and change notification

GraphicsSettingsApplier
    applies active graphics settings to runtime rendering

GraphicsSettingsController
    binds UI controls to an editable settings transaction
```

Exact class names may follow project conventions, but preserve the separation of concerns.

UI controllers must not:

- search the scene for every Face renderer
- edit Face materials directly
- edit URP volume components directly
- know where settings files are stored
- serialise settings themselves

A central runtime applier should update current and future runtime-owned Face materials consistently. Newly created Face materials must receive the current settings without requiring the settings menu to be reopened.

## Persistence

Persist settings globally outside machine packages.

Use an appropriate Unity user data location and a versionable serialisation format. Prefer a small explicit settings file over scattering values across unrelated `PlayerPrefs` keys, unless the project already has a deliberate settings persistence convention.

Requirements:

- missing settings file uses defaults
- invalid or partially corrupt settings fail safely
- unknown future fields do not prevent loading known settings where practical
- values are validated and clamped to sensible supported ranges
- persistence failures are reported clearly without making the Player unusable

Do not modify machine manifests or runtime export contracts.

## Transaction Behaviour

When the Graphics Settings menu opens:

1. Capture a baseline copy of the currently active graphics settings.
2. Populate controls from an editable copy.

While controls are edited:

- preview the editable values immediately through the settings service/applier
- do not persist every slider movement

Apply:

- makes the editable values active
- saves them
- establishes them as the new baseline

Cancel:

- restores the captured baseline values
- applies the restored values immediately
- does not save the abandoned edits

Restore Defaults:

- loads the central default graphics values into the editable copy
- updates the controls
- previews the defaults immediately
- does not persist until Apply is selected

Ensure closing the menu through Escape or another close action has deterministic Apply/Cancel semantics. Prefer Cancel semantics unless the UI explicitly states otherwise.

## Opening and Closing

Provide a minimal reliable way to open and close the Graphics Settings menu at runtime.

For the first vertical slice, Escape may open the menu and close it using Cancel semantics. Account for Escape also being used as a back action once nested settings navigation exists.

Do not implement the complete Main Menu or Machine Browser in this task.

When the settings menu is open:

- UI input should be available
- gameplay or cabinet interaction input should not accidentally activate behind the menu
- cursor visibility and lock state should be handled coherently for desktop use

Restore the previous interaction state when the menu closes.

## Control Behaviour

Use controls suitable for both pointer and non-pointer interaction.

For numeric settings, provide:

- a slider or equivalent adjustment control
- a readable current numeric value
- sensible min/max limits
- keyboard adjustment

Suggested initial ranges are starting points only and should be reconciled with the current shader and URP configuration:

```text
Lamp Exposure: 0 to 6 stops
Lamp Emission Strength: 0 to a conservative HDR-safe maximum
Bloom Intensity: range appropriate to the active URP version
```

Do not clamp HDR Face output to `0..1` merely because the UI has a bounded slider.

## Bloom Integration

Inspect how bloom is currently configured in Oasis Player before implementing the controls.

Use the existing URP volume/post-processing architecture if present. Do not create per-machine bloom configuration.

`Bloom Enabled` and `Bloom Intensity` are global user preferences.

The implementation should not assume that Bloom itself creates lamp brightness. Lamp exposure remains responsible for the primary bright artwork appearance; Bloom is a presentation effect over HDR highlights.

If no suitable runtime volume exists, create the smallest coherent central runtime setup and document any Unity Inspector or scene wiring that cannot be safely authored in code.

Avoid broad hand-edits to Unity scene YAML. If a serialized scene or asset reference must be assigned manually, implement the code and provide exact Inspector instructions instead of manufacturing fragile YAML references.

## Styling

Create a minimal reusable Oasis Player theme foundation.

Priorities:

- clear hierarchy
- readable labels and values
- consistent spacing
- usable focus states
- scalable layout
- sensible behaviour across common aspect ratios

Do not spend significant effort on final visual branding or elaborate animation in this task.

Avoid styling each control with isolated inline values when shared USS classes are appropriate.

## Tests

Add focused automated tests where the project test setup supports them.

At minimum, cover non-visual logic such as:

- default settings creation
- valid settings round-trip persistence
- missing-file fallback
- invalid-value clamping
- corrupt-file fallback
- Apply transaction behaviour
- Cancel restoring the baseline
- Restore Defaults without premature persistence
- settings change notification

Where practical, test the graphics applier independently from UI Toolkit views by injecting or abstracting the runtime targets it controls.

Do not rely only on manual UI testing for settings and persistence logic.

## Manual Verification

Verify in a running Oasis Player build or Play Mode:

1. Open Graphics Settings.
2. Change Lamp Exposure and confirm loaded Face lamps update immediately.
3. Confirm newly loaded or created Face materials use the current value.
4. Change Bloom Enabled and Bloom Intensity and confirm the runtime post-processing response.
5. Select Cancel and confirm all previewed values return to the baseline.
6. Reopen the menu and confirm controls show the restored values.
7. Change values, select Apply, restart the Player, and confirm persistence.
8. Select Restore Defaults, confirm live preview, then Cancel and confirm the prior applied values return.
9. Select Restore Defaults, Apply, restart, and confirm defaults persisted.
10. Verify mouse and keyboard navigation.
11. Verify controller or arcade-input navigation where the existing input system makes this available.
12. Verify the UI behaves sensibly at multiple resolutions and aspect ratios.
13. Verify opening the menu does not trigger cabinet interaction behind it.

## Preserve Existing Rendering Behaviour

Do not alter or regress:

- `FrontSide`
- Face rotation mapping
- horizontal flip
- Face UV transforms
- lamp-state texture sampling
- lamp lookup alignment
- cabinet geometry
- cabinet transforms
- runtime machine export semantics
- the exposure-based lamp algorithm except where required to make its existing parameter globally configurable
- environmental Face lighting
- single-sided culling

Do not reintroduce the previous washed-out lamp implementation or retain obsolete compatibility paths.

## Non-Goals

Do not implement:

- the full main menu
- machine browsing
- audio settings
- control rebinding
- accessibility settings
- VR UI
- world-space UI
- developer console
- per-machine graphics overrides
- cloud settings synchronisation
- elaborate final artwork or transitions

## Deliverable

Implement the focused vertical slice and report:

- files added and changed
- final UI and settings architecture
- persistence location and format
- settings defaults and supported ranges
- how current and newly created runtime materials receive settings
- how Bloom is controlled
- tests added and run
- manual verification performed
- any exact Unity Editor wiring steps still required
- any deferred limitations or follow-up tasks

## Implementation Note: Lamp Emission Strength

The current `Oasis/Face` shader exposes lamp appearance through `_OasisLampExposureStops` and does not retain a distinct lamp emission-strength property after the exposure-lighting cleanup. The first Graphics Settings screen should therefore omit a separate Lamp Emission Strength control until/unless a future shader design adds a meaningful independent emission parameter.
