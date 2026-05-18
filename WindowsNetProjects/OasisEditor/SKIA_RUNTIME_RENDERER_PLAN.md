# Skia Runtime Renderer Plan

This document defines the next major rendering workstream: introducing a shared SkiaSharp-based runtime renderer for Panel2D machine visuals.

Initial focus:

```text
Play View -> Skia renderer
```

Long-term direction:

```text
Edit View
    -> shared Skia machine renderer
    -> WPF editing overlay/chrome above renderer
```

## Why This Workstream Exists

The current WPF element-per-component rendering approach performs adequately for editing static layouts, but runtime rendering performance degrades significantly when many components update rapidly.

Examples:

- large numbers of flashing lamps;
- text-based lamps;
- 7-segment displays;
- 14/16-segment alpha displays;
- reels updating rapidly.

The runtime state architecture improvements helped significantly, but WPF control-tree rendering is still becoming the bottleneck during high-frequency emulation.

The project now needs a dedicated runtime renderer.

## Core Rendering Direction

The new architecture should be:

```text
Panel2D document model
+ runtime state
+ viewport transform
    -> shared Skia renderer
```

Skia becomes the canonical machine renderer.

WPF remains responsible for:

- docking/windows;
- menus/toolbars;
- inspectors;
- editor overlays;
- selection chrome;
- resize handles;
- drag/drop interaction;
- context menus.

## Rendering Goals

The renderer should:

- handle hundreds of rapidly updating runtime elements smoothly;
- support runtime redraw at 30/60 FPS;
- redraw entire frames cheaply;
- avoid large WPF control trees;
- support shared runtime rendering between Play View and Edit View;
- support future runtime-driven components.

## Initial Scope

### Phase 1

Add:

```text
Skia Play View
```

The Play View should:

- render the Panel2D using SkiaSharp;
- consume current runtime state;
- support live emulation;
- support runtime input hit-testing;
- support pan/zoom;
- support clickable inputs.

The existing WPF edit view remains unchanged initially.

### Phase 2

Refactor Edit View to use:

```text
Skia machine renderer
+ WPF overlay layer
```

The edit overlay handles:

- selection;
- resize handles;
- multi-select rectangles;
- snapping/grid;
- drag/drop;
- editor interaction.

## Existing Areas To Inspect

Codex should inspect:

```text
Panel2D document/view model classes
PanelRuntimeState
Play View implementation
Runtime lamp adapters
Current WPF rendering path
Canvas pan/zoom behavior
```

Also inspect:

```text
lamp visuals
text lamp visuals
7-seg visuals
alpha display visuals
reel visuals
```

Identify:

- which rendering behavior should move into Skia;
- which behavior should remain editor-only overlay logic.

## Dependencies

Add SkiaSharp dependencies appropriate for WPF.

Likely:

```text
SkiaSharp
SkiaSharp.Views.WPF
```

Do not tightly couple renderer logic directly to WPF controls.

## Renderer Architecture

Preferred architecture:

```text
Panel2DRenderer
    -> render context
    -> draw background
    -> draw lamps
    -> draw text lamps
    -> draw segment displays
    -> draw reels
```

Renderer input:

```text
Panel2D document model
PanelRuntimeState
Viewport transform
Render settings
```

Renderer output:

```text
SkCanvas drawing commands
```

Do not put runtime rendering logic directly inside the Play View control.

## Renderer Components

Suggested renderer structure:

```text
IPanel2DRenderer
Panel2DRenderer

LampRenderer
TextLampRenderer
SevenSegmentRenderer
AlphaDisplayRenderer
ReelRenderer
BackgroundRenderer
```

If some runtime component types are not yet stable, stub them incrementally.

## Shared Text Layout

Do not rely on WPF automatic text wrapping/layout for runtime rendering.

Add a shared text layout layer.

Preferred direction:

```text
Shared text layout engine
    -> line wrapping
    -> alignment
    -> clipping
    -> line positions

WPF edit view can consume layout results later.
Skia renderer consumes layout results immediately.
```

The goal is to make runtime text rendering deterministic and reusable.

Skia text layout should become canonical.

## Viewport Transform

Add/shared viewport transform state:

```text
PanelViewportTransform
    - pan
    - zoom
    - document-to-screen transform
    - screen-to-document transform
```

Both:

```text
Skia renderer
WPF overlay/edit layer
```

must consume the same transform state.

Do not allow separate pan/zoom systems.

## Play View Requirements

The Play View should:

- use the Skia renderer;
- render runtime state live;
- support pan/zoom;
- support runtime input hit-testing;
- support hand cursor over clickable elements;
- support keyboard focus/input routing;
- avoid editing chrome.

The Play View should not create WPF child elements per lamp.

## Hit Testing

Initial Play View hit testing can use:

```text
mouse screen point
    -> inverse viewport transform
    -> document-space point
    -> hit-test panel element bounds
```

Do not require WPF visual hit testing for runtime elements.

## Edit View Long-Term Direction

Long-term target:

```text
Edit View
    ├─ Skia machine render layer
    └─ WPF overlay layer
```

The WPF overlay remains responsible for:

- selection rectangles;
- resize handles;
- drag/drop interaction;
- multi-select box;
- hover outlines;
- context menus.

The Skia layer handles:

- machine visuals;
- lamps;
- alpha displays;
- reels;
- segment displays;
- runtime animation/state.

## Performance Rules

The renderer should:

- reuse cached images/paints/fonts where possible;
- avoid per-frame allocations where practical;
- redraw full frame efficiently;
- consume runtime state snapshots;
- avoid creating/discarding many objects every frame.

Do not prematurely optimize into GPU shaders/HLSL yet.

First validate that:

```text
shared Skia runtime renderer
```

solves the current bottleneck.

## Runtime Frame Timing

Preferred initial behavior:

```text
runtime updates
    -> invalidate renderer
    -> redraw at capped rate
```

Suggested cap:

```text
60 FPS max
```

Do not attempt uncapped rendering initially.

## Tests

Add tests for non-WPF logic.

Suggested tests:

- viewport transform math;
- screen/document coordinate conversion;
- text layout wrapping;
- alignment calculations;
- runtime state consumption;
- renderer component selection;
- hit-testing math.

Do not attempt pixel-perfect image snapshot tests initially.

## Diagnostics

Add lightweight diagnostics:

- renderer frame time;
- redraw count;
- runtime invalidation frequency;
- frame skips/throttling;
- hit-test diagnostics if useful.

Use Output log sparingly.

## Recommended Codex Steps

### Step 1 - Renderer Inventory

Inspect and document:

- current runtime rendering bottlenecks;
- current visual types;
- reusable runtime state paths;
- current pan/zoom behavior;
- existing Play View rendering behavior.

Do not implement major renderer code before this inventory.

### Step 2 - Add Skia Dependencies And Render Surface

Add SkiaSharp dependencies.

Create a simple Skia render surface in the Play View.

Render:

- clear background;
- viewport transforms;
- simple test geometry.

### Step 3 - Add Shared Viewport Transform

Create shared pan/zoom transform model.

Use it in:

- Play View renderer;
- Play View hit testing.

Add tests for transform math.

### Step 4 - Add Core Panel Renderer

Add:

```text
Panel2DRenderer
```

with:

- background rendering;
- basic element traversal;
- renderer dispatch by element type.

### Step 5 - Add Lamp Renderers

Implement:

- graphical lamps;
- solid color lamps;
- text lamps.

Use runtime state.

### Step 6 - Add Shared Text Layout Layer

Add deterministic wrapping/alignment layout service.

Use it in Skia text rendering.

### Step 7 - Add Segment Display Renderers

Implement:

- 7-segment displays;
- 14/16-segment alpha displays.

Prioritize correctness over heavy optimization initially.

### Step 8 - Add Reel Rendering

Port/runtime-render reels.

Use cached assets where practical.

### Step 9 - Add Runtime Hit Testing/Input

Play View:

- hand cursor over clickable inputs;
- click/hold input routing;
- keyboard routing.

Use shared viewport transform.

### Step 10 - Manual Runtime Verification

John should verify:

- Play View renders live machine visuals smoothly;
- lamp flashing no longer causes severe CPU spikes;
- alpha displays render smoothly;
- pan/zoom behaves correctly;
- clickable inputs still work;
- runtime visuals visually match existing edit view closely enough;
- text layout remains acceptable.

### Step 11 - Plan Edit View Migration

Only after Play View is stable:

- introduce shared Skia render layer into Edit View;
- retain WPF overlay/editor chrome;
- preserve selection/multi-select/edit workflows.

Do not rewrite the edit interaction layer immediately.

## Out Of Scope For This Workstream

Do not:

- rewrite the entire editor interaction model;
- remove WPF completely;
- add GPU/HLSL renderer yet;
- optimize for 1000+ FPS;
- rewrite docking/inspector systems.

The goal is:

```text
high-performance runtime rendering
while preserving WPF editor productivity
```
