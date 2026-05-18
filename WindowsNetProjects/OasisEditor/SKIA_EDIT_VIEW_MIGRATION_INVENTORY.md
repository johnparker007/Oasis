# Skia Edit View Migration Inventory

This inventory captures Step 1 of `SKIA_EDIT_VIEW_MIGRATION_PLAN.md` before major Edit View rendering changes. The goal is to separate machine visuals that should move to the shared Skia renderer from WPF chrome that should remain editor-only.

## Current Edit View Rendering Structure

The editable Panel2D surface is hosted by `PanelCanvasView`.

Current structure:

```text
PanelCanvasView
  TabControl
    DocumentTabViewModel
      Border: PanelCanvasContainer
        Grid (clip bounds)
          Border
            Canvas (1400 x 900)
              WPF runtime/component visuals
          Floating instruction overlay
```

The edit canvas is currently the same WPF `Canvas` that handles:

- visual element hosting;
- panning and zooming;
- selection hit-testing;
- placement tools;
- mutation dispatch;
- runtime visual state updates.

The canvas binds these attached properties from `CanvasPanBehavior`:

- `PanelLayoutJson`;
- `SelectedPanelSelection`;
- `PanelZoom`;
- `PanelPanX`;
- `PanelPanY`.

## Current WPF Runtime-Rendered Visuals

`PanelLayoutMapper.ApplyPersistedLayout` deserializes the Panel2D layout and creates one WPF visual per visible element through `PanelElementFactory.CreateVisualFromElement`.

Runtime/component visuals currently created as WPF controls include:

| Panel element kind | WPF visual path | Migration decision |
| --- | --- | --- |
| Background | `Border` plus optional `Image` child | Move runtime drawing to Skia; keep only editor hit/selection overlay if needed. |
| Lamp / text lamp | `Border`/`Image`/`TextBlock` placeholder stack | Move runtime drawing to Skia; keep transparent/edit overlay hit zone. |
| Reel | `Border` + clipped `Grid` + nested `Canvas` with scrolling `Image` children | Move runtime drawing and reel offset to Skia. |
| Seven segment | `Border` + `SevenSegmentDisplayVisual` | Move runtime drawing to Skia. |
| Alpha / 16 segment | `Border` + `AlphaSixteenSegmentDisplayVisual` | Move runtime drawing to Skia. |
| Label | `Border` + `TextBlock` placeholder stack | Treat as machine visual where it is part of the panel; render through Skia unless later classified as editor-only annotation. |
| Rectangle / Image | `Rectangle` or `Image` | Move panel machine/background drawing to Skia; retain WPF overlay only for edit handles/hit zones. |

Runtime state currently mutates WPF visuals through `PanelLayoutMapper.ApplyVisualState`:

- lamps update WPF brush/opacity/image source;
- reels update child image offsets with `Canvas.SetTop`;
- alpha/seven segment visuals update custom WPF segment visual state and invalidate those visuals.

These are the primary WPF runtime paths to remove or disable after the Edit View Skia layer is in place.

## Current Overlay / Editor-Only Visuals

Editor-only behavior currently lives on the same WPF elements as the runtime visuals. There is no dedicated overlay layer yet.

Current editor chrome/interaction responsibilities:

- selection state attached by `CanvasSelectionBehavior.IsSelectable` and `CanvasSelectionBehavior.IsSelected`;
- selected styling through XAML styles for `Rectangle`, `Border`, and `Image`;
- active selection propagated through `DocumentTabViewModel.HierarchySelectedPanelSelection`;
- click-to-select from `CanvasPanBehavior.OnMouseLeftButtonDown` using `CanvasSelectionBehavior.FindSelectableElement`;
- placement tools via `PanelToolPlacementController.TryHandlePlacement`;
- command execution via `CanvasCommandDispatcher.ExecuteMutation`;
- hierarchy/inspector sync through `CanvasCommandDispatcher.NotifyDocumentSelection`.

Inventory conclusion: selection and editor interactions should remain WPF, but the WPF visual carrying those interactions should become a lightweight transparent overlay/hit zone rather than the runtime-rendered machine visual.

## Current Pan / Zoom Wiring

Edit View pan and zoom are driven by `CanvasPanBehavior`, which delegates transform math to `CanvasPanZoomBehavior`.

Current behavior:

- middle mouse begins pan and captures the canvas;
- mouse move updates the canvas `TranslateTransform`;
- mouse wheel updates the canvas `ScaleTransform` and pan offsets around the mouse pivot;
- `CanvasPanBehavior.UpdateViewportStateFromCanvas` writes the transform back to `DocumentTabViewModel.PanelZoom`, `PanelPanX`, and `PanelPanY`;
- changes to those properties call `ApplyViewportStateToCanvas`, keeping the WPF canvas transform synchronized with document viewport state.

The shared `PanelViewportTransform` already represents the canonical coordinate conversion and zoom limits used by Skia renderer code.

Migration implication: the Edit View Skia surface should consume the same `PanelZoom`, `PanelPanX`, and `PanelPanY` values as the WPF overlay so Skia machine visuals and WPF editor chrome remain locked together.

## Current Selection Overlay Behavior

Selection is not a separate overlay today. It is styled directly on the runtime visual element:

- `CanvasSelectionBehavior.SelectElement` marks an element as selected;
- XAML style triggers alter `Stroke`, `BorderBrush`, `BorderThickness`, or `Opacity`;
- `CanvasPanBehavior.OnSelectedPanelSelectionChanged` locates a matching WPF child in the canvas and selects it;
- `NotifyActiveDocumentSelection` converts the selected WPF visual back to `PanelSelectionInfo`.

Migration implication: selection outlines and future resize handles should move to a WPF overlay layer that maps model bounds through `PanelViewportTransform`. During the incremental phase, the existing child visuals can temporarily remain as overlay/hit-test carriers while Skia begins drawing the machine visuals underneath.

## Current Play View Skia Renderer Integration

Play View already uses the shared renderer path:

```text
PlayView.xaml
  SKElement PlaySkiaSurface
    PaintSurface -> Panel2DRenderer.Render(...)
```

Important integration details:

- `PlayView` owns an `IPanel2DRenderer` made from the shared element renderers: background, lamp, reel, seven segment, and alpha;
- `OnPlaySkiaSurfacePaintSurface` clears the canvas, creates a `PanelViewportTransform`, applies pan/zoom to the Skia canvas, and calls `Render` with `selected.GetPanelElements()` and `selected.RuntimeState`;
- mouse wheel and middle mouse pan maintain `_skiaZoom` and `_skiaPan` locally;
- pointer hit-testing converts screen points to document coordinates through `PanelViewportTransform.ScreenToDocument`;
- `PanelVisualStateChanged` invalidates the Skia surface so runtime updates redraw without WPF runtime visual mutation.

Migration implication: Edit View should reuse this renderer and invalidation pattern, but unlike Play View it should bind to `DocumentTabViewModel.PanelZoom/Pan` so the WPF overlay remains aligned.

## Renderer Coverage Observations

The shared Skia renderer currently dispatches by `PanelElementKind` and skips invisible elements.

Renderer coverage available now:

- background;
- lamp/text lamp;
- reel;
- seven segment;
- alpha / 16 segment.

Obvious gaps or follow-up checks:

- rectangle/image/label rendering may still need explicit Skia coverage or classification;
- WPF edit canvas still hosts all visible elements, so initial Skia embedding will duplicate visuals until runtime WPF visuals are hidden or replaced with transparent overlay hit zones;
- Play View and Edit View pan/zoom math should be consolidated further around `PanelViewportTransform.WithZoomAt` to avoid divergent implementations.

## Initial Removal / Disable Candidates

After a shared Edit View Skia host is embedded and visually verified, the following WPF runtime paths can be removed or disabled incrementally:

1. `PanelLayoutMapper.ApplyVisualState` updates for lamps/reels/segments in the Edit View.
2. WPF `Image`/`TextBlock`/custom segment visual creation for machine visuals in `PanelElementFactory` when used by the Edit View.
3. Per-runtime-element WPF opacity/brush/segment invalidation in the edit canvas.
4. Nested reel `Canvas` image scrolling in WPF.
5. Runtime visual registry dependency for Edit View machine redraws.

Keep or replace with lightweight WPF overlay responsibilities:

- selection hit zones;
- selection outlines;
- placement tool input;
- context menus;
- drag/resize handles;
- hierarchy/inspector selection synchronization;
- keyboard editing and command dispatch.

## Recommended Next Increment

Proceed with Step 2 by embedding an Edit View `SKElement` behind the existing WPF canvas:

- render the selected Panel2D document with `Panel2DRenderer`;
- consume `DocumentTabViewModel.PanelZoom`, `PanelPanX`, and `PanelPanY`;
- invalidate on `PanelLayoutJson`, viewport property changes, and `PanelVisualStateChanged`;
- keep existing WPF canvas above it temporarily for selection/placement workflows.

This creates a safe coexistence point before disabling WPF runtime visuals.
