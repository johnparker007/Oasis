# Skia Edit View Rebuild From Game View Plan

This document supersedes the previous strategy of incrementally patching the old WPF-heavy Panel2D Edit View into a Skia-backed view.

The old Edit View architecture is proving too tangled with WPF component rendering, selection behavior, hit testing, and pan/zoom assumptions.

The new strategy is:

```text
Stop patching the old Edit View.
Use the working Game/Play View as the base for a new Skia Edit View.
Rebuild editor interaction as a clean overlay/interaction layer on top.
```

## Current Situation

The Game/Play View Skia renderer works well:

- it renders the layout correctly;
- pan/zoom behaves correctly;
- Skia runtime visuals are performant;
- it is much closer to the desired long-term render architecture.

The old Edit View is bugged and difficult to patch:

- pan/zoom only works in some regions;
- behavior appears tied to old WPF component hit targets or invisible rectangles;
- old WPF runtime component rendering is still influencing interaction behavior;
- Codex has struggled to migrate it incrementally.

Therefore, the next implementation path should be replacement/rebuild rather than mutation.

## Target Architecture

```text
New Skia Edit View
    ├─ Skia machine render layer
    │    - same renderer used by Game/Play View
    │    - same pan/zoom behavior as Game/Play View
    │    - same runtime state rendering as Game/Play View
    │
    └─ Editor interaction/overlay layer
         - selection outline
         - hover outline
         - drag selection rectangle
         - resize handles
         - context menus
         - future multi-select/edit tools
```

The key rule remains:

```text
Skia draws machine visuals.
WPF or lightweight overlay logic handles editor interaction chrome.
```

But now the new Edit View should be created from the working Game/Play View foundation, not from the old WPF Edit View foundation.

## Main Rule For Codex

Do not continue trying to fix the old Edit View internals.

Use the old Edit View only as a reference for:

- existing selection behavior;
- inspector synchronization;
- edit commands;
- context menus;
- drag/resize workflows;
- document mutation APIs.

Do not preserve old WPF component rendering paths unless temporarily needed for reference.

## Naming Guidance

Suggested names:

```text
SkiaPanel2DEditView
SkiaPanel2DEditViewModel
Panel2DEditInteractionController
Panel2DSelectionOverlay
Panel2DHitTestService
Panel2DSelectionService
```

Actual names may differ to match existing code style.

## Migration Strategy

Use a side-by-side replacement initially.

Recommended:

1. Keep the old Edit View in the codebase temporarily as legacy/reference.
2. Create a new Skia-based Edit View based on the working Game/Play View.
3. Make the main editor route Panel2D documents to the new Skia Edit View once the first milestone works.
4. Remove old WPF component-rendering edit path only after the new path is stable.

Avoid a giant deletion-only commit before the replacement exists.

## Phase 1 - Inventory And Cut Line

Codex should document:

- current Game/Play View files/classes;
- current Skia renderer host files/classes;
- current Game/Play pan/zoom implementation;
- current old Edit View files/classes;
- old Edit View responsibilities that must be rebuilt;
- old WPF component rendering code that should be retired;
- document mutation APIs used by old edit interactions.

Deliverable:

```text
SkiaEditViewRebuild.Inventory.md
```

This inventory should identify the minimum viable edit features to rebuild first.

## Phase 2 - New Skia Edit View Shell

Create a new Edit View shell based on the working Game/Play View.

Requirements:

- uses the shared Skia renderer;
- uses the same pan/zoom behavior as Game/Play View;
- renders current Panel2D document;
- renders runtime state live;
- does not create WPF child visuals per lamp/component;
- does not use old WPF component-rendering path;
- does not yet need full editing features.

First acceptance milestone:

```text
Opening a Panel2D document shows the Skia-rendered layout in the Edit View.
Pan/zoom works everywhere, not just over old component hit zones.
Live MAME runtime visuals still render.
```

## Phase 3 - Document-Space Hit Testing

Add clean document-space hit testing.

Use the same viewport transform as Game/Play View:

```text
screen point
    -> inverse viewport transform
    -> document point
    -> hit-test document element bounds
```

Initial hit testing can be simple:

- topmost element under point;
- rectangular bounds only;
- ignore pixel-perfect transparency for now.

Do not rely on WPF visual hit testing for machine components.

Add tests for:

- screen-to-document conversion;
- document-to-screen conversion;
- topmost element selection;
- empty-space click behavior.

## Phase 4 - Single Selection Overlay

Implement click-to-select.

Requirements:

- click component selects it;
- click empty space clears selection;
- selected component shows overlay outline;
- selection syncs to existing Inspector if practical;
- selection does not mutate runtime state;
- selection does not require WPF component visuals.

Overlay may be:

- WPF Canvas overlay above Skia surface; or
- lightweight Skia overlay pass, if easier initially.

Preferred long-term:

```text
WPF overlay/editor chrome above Skia renderer
```

But first milestone may draw selection in Skia if that is faster to stabilize. If Codex chooses this shortcut, it must document it and keep the design open for WPF handles later.

## Phase 5 - Pan/Zoom Alignment For Overlay

Ensure overlay follows the same viewport transform as the Skia render layer.

Requirements:

- selection outline aligns at all zoom levels;
- panning keeps overlay locked to component;
- zooming around mouse cursor behaves like Game/Play View;
- no separate old Edit View pan/zoom state remains active.

## Phase 6 - Multi-Select Rectangle

Implement drag-selection rectangle.

Requirements:

- mouse drag on empty space creates selection rectangle;
- elements whose bounds intersect or are contained are selected according to chosen policy;
- selected elements show overlays;
- shift/ctrl selection modifiers can be added incrementally;
- hidden/locked elements follow existing editor rules if present.

If existing multi-select behavior is not yet stable, implement the simplest useful version first and document TODOs.

## Phase 7 - Move Selected Elements

Implement drag-to-move selected elements.

Requirements:

- selected element(s) move in document space;
- movement uses the shared transform for mouse delta;
- document mutation uses existing edit/document APIs;
- undo/redo is preserved if current command system supports it;
- inspector updates after move;
- Skia render invalidates after document changes.

Do not reintroduce WPF component visuals to make movement work.

## Phase 8 - Resize Handles

Add resize handles once single/multi select and movement are stable.

Requirements:

- handles drawn above selected element;
- handles remain clickable at all zoom levels;
- resize mutates document model through existing APIs;
- undo/redo works if available;
- Skia render invalidates after resize.

## Phase 9 - Context Menus And Commands

Reattach editor commands:

- delete;
- copy;
- paste;
- duplicate;
- bring forward/back if existing;
- context menu actions.

Use old Edit View only as a reference for command routing and document mutation.

## Phase 10 - Retire Old WPF Runtime Edit Rendering

Once the new Skia Edit View supports the minimum editing workflow, retire old WPF runtime rendering paths.

Remove/disable:

- WPF component-per-lamp visual rendering;
- WPF TextBlock runtime lamp rendering;
- WPF segment/alpha runtime visuals;
- old Edit View pan/zoom behavior if replaced;
- invisible WPF hit rectangles that caused pan/zoom inconsistencies.

Keep only code that remains useful for:

- editor overlays;
- inspector;
- document mutation;
- commands;
- shared services.

## Important Scope Control

Do not try to rebuild every old edit feature in one pass.

Minimum viable sequence:

```text
1. Skia Edit View renders and pans/zooms correctly.
2. Click-to-select works.
3. Selection overlay works.
4. Drag-select multi-select works.
5. Drag-to-move works.
6. Resize handles return.
7. Context menu/clipboard/delete return.
```

Stop and test after each milestone.

## Runtime Rules

The new Edit View should continue to show live runtime state:

- lamps;
- text lamps;
- alpha displays;
- segment displays;
- reels.

Runtime updates should invalidate/redraw the Skia surface, not create WPF runtime visuals.

## Tests

Add/extend tests for non-WPF logic:

- viewport transform math;
- document-space hit testing;
- topmost selection order;
- selection service behavior;
- multi-select rectangle calculations;
- document-space drag delta calculation;
- overlay bounds calculation;
- command guard behavior.

Avoid heavy pixel-perfect tests.

## Diagnostics

Add lightweight diagnostics while migrating:

- which Edit View implementation is active;
- pan/zoom state;
- selected element id(s);
- hit-test result;
- old WPF component renderer disabled/enabled state;
- Skia invalidation/frame timing if useful.

Use Output log sparingly to avoid log spam.

## Manual Verification

After each milestone, John should verify:

### Shell

- Panel2D opens in new Skia Edit View;
- view visually matches Game/Play View;
- pan works everywhere;
- zoom works everywhere;
- live emulation still renders.

### Selection

- click component selects it;
- click empty space clears selection;
- selection outline aligns with component;
- inspector selection sync still works if implemented.

### Multi-Select

- drag rectangle selects multiple components;
- modifier keys work if implemented;
- selection overlays align after pan/zoom.

### Editing

- selected components can move;
- resize works when reintroduced;
- undo/redo still works where expected;
- context menu still works;
- delete/copy/paste return.

### Performance

- live lamps/alpha/reels in Edit View no longer cause severe WPF slowdown;
- CPU usage is closer to Game/Play View;
- no old WPF runtime visual tree remains active for machine visuals.

## Out Of Scope

Do not:

- rewrite inspector system;
- rewrite serialization;
- rewrite document model;
- introduce HLSL/GPU custom shaders;
- implement 3D buttons;
- implement edit-view Play Mode.

This workstream is about replacing the broken old Edit View foundation with the working Skia Game/Play View foundation, then rebuilding editor interaction on top.
