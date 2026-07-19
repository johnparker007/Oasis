# Oasis Player UI Context

## Purpose

This document defines the long-term runtime UI direction for:

```text
UnityProjects/OasisPlayer
```

The Oasis Player UI is the end-user application interface for launching, configuring, and interacting with machines. It is distinct from the Oasis Editor and must not become an authoring interface for machine assets.

## Primary UI Technology

Use Unity UI Toolkit as the default technology for conventional Player UI, including:

- boot and loading screens
- main menus
- machine browsing and selection
- settings pages
- pause and escape overlays
- dialogs and notifications
- diagnostics and developer panels

Use:

- UXML for view structure
- USS for styling
- C# controllers and services for behaviour

Do not use IMGUI as the foundation of production end-user UI. IMGUI remains acceptable for temporary diagnostics and developer-only tooling.

## Hybrid UI Strategy

UI Toolkit is the default, not an absolute requirement for every future interface.

Specialised features may use another Unity UI approach where UI Toolkit is not a good technical fit, particularly:

- VR interaction
- world-space interfaces
- physical cabinet touchscreens or displays
- shader-driven UI
- heavily animated interfaces
- interfaces that depend on GameObject-centric or 3D interaction behaviour

Keep these specialised systems isolated. Do not abandon the shared UI Toolkit architecture merely because one future feature requires a different implementation.

## Architectural Principles

Separate UI presentation, application state, persistence, and runtime rendering.

A typical flow should be:

```text
UXML view
    -> controller or presenter
    -> typed Player settings service
    -> settings change notification
    -> runtime subsystem applier
```

UI controllers must not directly search for or modify Face materials, post-processing components, scene objects, or other rendering resources.

Runtime systems should receive settings through typed services and apply them centrally.

Avoid placing application logic in UXML callbacks or constructing entire screens procedurally when UXML is suitable.

## Suggested Project Organisation

Follow the existing Oasis Player project conventions where they are already established. A suitable structure is:

```text
UnityProjects/OasisPlayer/Assets/_Project/UI/
    Uxml/
    Styles/
    Controllers/
    Controls/

UnityProjects/OasisPlayer/Assets/_Project/Scripts/Settings/
    Models/
    Services/
    Appliers/
```

Do not reorganise unrelated existing code merely to match this example.

## Player Settings Ownership

Player settings are global user preferences. They are not stored in individual machine assets or machine runtime exports.

Machine assets describe authored machine content and semantics. Player settings describe how that content is presented on the user's hardware and according to the user's preferences.

Examples of Player-owned settings include:

- lamp exposure
- lamp emission strength
- bloom preferences
- display and quality options
- audio levels
- controls
- accessibility options
- VR preferences

Settings must be persisted outside machine packages and loaded independently of the selected machine.

## Settings Behaviour

Settings screens should support a consistent transaction model:

- opening a page establishes a baseline snapshot
- editing previews changes immediately where practical
- Apply persists the current values and establishes a new baseline
- Cancel restores the baseline values and closes or leaves the page
- Restore Defaults loads defined application defaults into the editable settings and previews them

The persistence layer, not individual UI controls, owns serialisation and storage location decisions.

## Input and Accessibility

The Player UI must not assume mouse-only operation.

Design the shared architecture to support:

- mouse
- keyboard
- gamepad or arcade controls
- future accessibility features

Ensure focus order and navigation are deliberate. Avoid interactions that can only be performed through pointer dragging when a keyboard or controller-compatible alternative is reasonable.

VR-specific interaction is outside the initial task and may use a specialised UI path later.

## Initial Menu Direction

The eventual application structure is expected to include areas such as:

```text
Main Menu
    Continue
    Machine Browser
    Settings
    Exit

Settings
    Graphics
    Audio
    Controls
    Accessibility
    VR
    Developer
```

Only the Graphics Settings vertical slice is currently in scope. Do not create placeholder implementations for all future pages unless a minimal navigation shell is genuinely required by the first task.

## First Vertical Slice

The first implementation is the Graphics Settings menu described in:

```text
WindowsNetProjects/OasisEditor/Docs/PlayerUi/TASK_01_GRAPHICS_SETTINGS.md
```

Its purpose is to establish and validate:

- UI Toolkit runtime setup
- reusable view and controller conventions
- typed global settings
- persistence
- live preview
- Apply, Cancel, and Restore Defaults behaviour
- keyboard and controller-friendly focus/navigation
- centralised application of graphics settings

Visual polish is secondary to a clean and extensible foundation.