# Skia Edit View Migration Plan

This document defines the next rendering workstream: migrating the editable Panel2D view away from WPF component rendering and onto the shared Skia runtime renderer.

The target architecture is:

```text
Edit View
    ├─ shared Skia machine renderer
    └─ WPF overlay/editor chrome
```

The Play View Skia renderer is now working and visually close enough to the previous WPF implementation.

The next step is to make the Edit View use the same renderer so live emulation performance improves significantly while preserving existing editing workflows.

## Core Goal

The machine visuals themselves should no longer be represented by large WPF visual/control trees.

Instead:

```text
Skia draws machine visuals.
WPF draws editor chrome.
```

Machine visuals include:

- lamps;
- text lamps;
- alpha displays;
- segment displays;
- reels;
- backgrounds;
- future runtime-driven visuals.

WPF overlay/editor chrome includes:

- selection outlines;
- resize handles;
- drag handles;
- multi-select rectangles;
- hover highlights;
- editor context menus;
- drag/drop interaction;
- keyboard shortcuts;
- snapping/grid overlays.

## Why This Workstream Exists

The current hybrid system still leaves significant runtime rendering overhead inside the Edit View because many visual components are still WPF controls.

Even with runtime-state optimizations, rapidly changing:

- flashing lamps;
- alpha displays;
- segment displays;
- reels;

can still create high CPU usage and visible slowdown.

The Play View Skia renderer demonstrated that rendering machine visuals through Skia is significantly more scalable.

The editor now needs to consume that same renderer.

## Existing Areas To Inspect

Codex should inspect:

```text
Play View Skia renderer integration
Panel2DRenderer
Viewport transform classes
Current Edit View XAML structure
Current WPF element rendering path
Current selection/overlay logic
Canvas pan/zoom behavior
Selection hit-testing
Drag/resize interaction
```

Also inspect any remaining WPF visual creation logic for:

```text
lamp visuals
text lamps
segment displays
reels
backgrounds
```

The goal is to identify what should move entirely into Skia rendering and what should remain WPF overlay/editor-only behavior.

## Final Edit View Structure

Target structure:

```text
Edit View
┌──────────────────────────────┐
│ WPF overlay/editor layer      │
│  - selection                  │
│  - handles                    │
│  - drag rectangles            │
│  - context menus              │
│  - editor hit zones           │
│                               │
│ ┌──────────────────────────┐ │
│ │ Shared Skia renderer      │ │
│ │  - lamps                  │ │
│ │  - text lamps             │ │
│ │  - alpha displays         │ │
│ │  - reels                  │ │
│ └──────────────────────────┘ │
└──────────────────────────────┘
```

## Shared Renderer Rule

The Edit View and Play View should use the same renderer implementation.

Do not fork renderer behavior unnecessarily.

Preferred structure:

```text
Panel2DRenderer
    -> shared rendering logic

Play View
    -> renderer host

Edit View
    -> renderer host
    -> WPF overlay layer
```

The Edit View should not have its own separate runtime renderer implementation.

## WPF Overlay Direction

The WPF overlay should become lightweight.

Do not create WPF visual trees for every machine component.

Instead:

- overlay elements exist primarily for editor interaction;
- overlay elements may be transparent/invisible hit targets;
- overlay elements should not redraw runtime visuals.

The overlay should:

- track element bounds;
- consume shared viewport transform;
- map document coordinates to overlay coordinates.

## Pan/Zoom Requirements

The shared viewport transform must drive both:

```text
Skia renderer
WPF overlay
```

Panning and zooming should remain visually locked together.

Current middle-mouse pan and wheel zoom behavior should continue working.

Do not create separate transform systems.

## Hit Testing Direction

Short-term acceptable approach:

```text
transparent WPF overlay hit zones
```

Long-term direction:

```text
document-space hit-testing
```

through the shared viewport transform.

Codex should avoid over-engineering the hit-test system during this migration.

Priority is:

```text
preserve editing workflow
while removing WPF runtime rendering overhead
```

## Selection And Handles

Selection visuals should remain WPF overlay/editor chrome.

Examples:

```text
selection rectangle
resize handles
multi-select drag box
hover outline
```

These should render independently from the Skia machine visuals.

Selection overlays should:

- remain crisp at different zoom levels;
- remain responsive during live emulation;
- not require runtime redraw of machine visuals.

## Runtime Behavior

Live emulation should now appear smoothly in BOTH:

```text
Play View
Edit View
```

without the previous WPF runtime rendering overhead.

Runtime state updates should:

```text
runtime state changed
    -> invalidate Skia render surface
```

and should not:

- recreate WPF controls;
- rebuild large visual trees;
- invalidate large layout passes.

## WPF Rendering Removal

Codex should gradually remove or disable WPF component rendering paths used by the Edit View.

Examples:

```text
Canvas child-per-lamp rendering
TextBlock-per-lamp rendering
WPF alpha display visuals
WPF segment display visuals
```

Do not break:

- serialization;
- inspector behavior;
- selection;
- editing interaction.

Only the visual rendering path should move away from WPF.

## Overlay Interaction Rules

Overlay/editor logic should continue supporting:

- select single;
- multi-select;
- drag move;
- resize;
- keyboard delete;
- clipboard operations;
- snapping/grid;
- context menus.

These should continue using document/edit model behavior.

Runtime state should remain separate.

## Shared Text Layout

The shared text layout system introduced for the Skia renderer should now become the canonical layout source.

The Edit View should not rely on WPF automatic text wrapping/layout for machine visuals.

Any remaining WPF text overlay/editor labels should use the shared viewport transform.

## Diagnostics

Add lightweight diagnostics:

- Edit View frame timing;
- redraw frequency;
- overlay update frequency;
- runtime redraw counts;
- remaining WPF runtime visual counts if useful.

Use Output log sparingly.

## Tests

Add tests for non-WPF logic.

Suggested tests:

- viewport transform consistency;
- overlay/document coordinate conversion;
- selection bounds conversion;
- hit-testing conversion math;
- renderer host invalidation behavior.

Do not add heavy pixel-perfect rendering tests.

## Recommended Codex Steps

### Step 1 - Edit View Rendering Inventory

Document:

- current WPF runtime-rendered visuals;
- current overlay/editor-only visuals;
- current pan/zoom wiring;
- current selection overlay behavior;
- current Play View renderer integration.

Identify which WPF visuals can now be removed or disabled.

Do not implement large changes before this inventory.

### Step 2 - Add Shared Skia Renderer Host To Edit View

Embed the shared Skia renderer into the Edit View.

Use:

- shared viewport transform;
- shared runtime state;
- shared renderer implementation.

Initially allow old WPF rendering to coexist temporarily for migration.

### Step 3 - Move Runtime Visuals To Skia

Switch machine visuals to Skia rendering:

- lamps;
- text lamps;
- alpha displays;
- segment displays;
- reels;
- backgrounds.

Ensure live emulation renders through Skia.

### Step 4 - Keep WPF Overlay Only

Preserve:

- selection;
- handles;
- drag rectangles;
- context menus;
- keyboard editing.

through WPF overlay/editor-only visuals.

Do not render runtime machine visuals through WPF anymore.

### Step 5 - Remove WPF Runtime Visual Trees

Remove/disable:

- runtime WPF component trees;
- per-component Canvas child rendering;
- runtime TextBlock trees;
- runtime alpha/segment WPF visuals.

Retain only editor overlay visuals.

### Step 6 - Verify Pan/Zoom And Selection

Ensure:

- Skia rendering and overlay remain aligned;
- middle-mouse pan still works;
- wheel zoom still works;
- selection boxes align correctly;
- resize handles align correctly.

### Step 7 - Runtime Performance Verification

Verify:

- flashing lamps remain smooth;
- alpha displays no longer visibly stall the editor;
- runtime rendering no longer heavily spikes CPU;
- Edit View remains responsive during emulation.

### Step 8 - Cleanup And Consolidation

Consolidate shared renderer infrastructure.

Remove dead WPF rendering code where safe.

Document remaining overlay/editor-only responsibilities.

## Manual Verification

After implementation, John should verify:

- Edit View visually matches Play View closely enough;
- live emulation runs smoothly in Edit View;
- flashing lamps no longer cause severe slowdown;
- alpha displays render smoothly;
- segment displays render smoothly;
- reels remain smooth;
- selection still works;
- multi-selection still works;
- drag/resize still works;
- pan/zoom remains aligned;
- context menus still work;
- inspector integration still works;
- no unrelated editor behavior regressed.

## Out Of Scope For This Workstream

Do not:

- remove WPF entirely from the editor;
- rewrite docking/layout systems;
- add GPU/HLSL renderer yet;
- redesign document serialization;
- redesign undo/redo.

The goal is:

```text
high-performance shared machine rendering
with lightweight WPF editor overlays
```
