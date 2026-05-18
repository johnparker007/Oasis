# Skia Runtime Renderer Inventory (Step 1)

This inventory captures the current runtime rendering architecture before implementing major shared Skia renderer code.

## Sources inspected

- `OasisEditor/Views/PlayView.xaml`
- `OasisEditor/Views/PlayView.xaml.cs`
- `OasisEditor/PanelLayoutMapper.cs`
- `OasisEditor/PanelRuntimeState.cs`
- `OasisEditor/CanvasPanZoomBehavior.cs`
- `OasisEditor/CanvasPanBehavior.cs`
- `OasisEditor/PanelElementFactory.cs`
- `OasisEditor/SegmentDisplayVisualBase.cs`
- `OasisEditor/DocumentTabViewModel.cs`

## Current runtime rendering bottlenecks

### 1) Runtime visuals are still WPF control-tree based

Current rendering creates and updates WPF elements (`Border`, `TextBlock`, `Image`, `Canvas`, custom `FrameworkElement` segment visuals) per panel element. Runtime updates mutate these visual tree nodes directly, so high-frequency updates pay WPF layout/visual invalidation costs repeatedly.

### 2) Runtime updates dispatch across all windows/canvases

`CanvasPanBehavior.OnPanelVisualStateChanged` scans all app windows and all canvases, then applies updates to matching document canvases. This broad fanout can add overhead as document/view count grows.

### 3) Segment display redraw path invalidates custom visuals frequently

Segment updates set masks/brightness arrays and call `InvalidateVisual()` on `SegmentDisplayVisualBase`, which repaints geometry in WPF for each update.

### 4) Reel/lamp updates are point updates but still bound to WPF element mutation

`PanelLayoutMapper.ApplyVisualState` updates lamp brushes/opacity and reel image offsets inside existing WPF visuals. Even incremental updates remain in a per-element WPF pipeline rather than a frame renderer.

## Current Play View/runtime rendering path

1. `PlayView` hosts a `Canvas` (`PlayCanvas`) inside a border.
2. On selected `Panel2D` document change, `PlayView.RefreshCanvasFromSelection` calls `PanelLayoutMapper.ApplyPersistedLayout`.
3. `ApplyPersistedLayout` clears persisted children, deserializes panel JSON, and recreates element visuals using `PanelElementFactory.CreateVisualFromElement`.
4. `PlayView` subscribes to `DocumentTabViewModel.PanelVisualStateChanged` and applies incremental runtime changes via `PanelLayoutMapper.ApplyVisualState`.
5. `PlayView` handles pointer/key input and maps clicks by resolving visual element IDs from WPF elements (`Uid`) (`TryResolveVisualElementId`).

Implication: Play View runtime currently depends on WPF visual identity/hit routing rather than renderer-owned hit-testing.

## Current panel element visual types and runtime state usage

### Visual kinds observed

Current panel runtime visuals include:

- Lamp visuals (solid color and/or artwork-based lamp presentation).
- Reel visuals (canvas/image-based strip offset updates).
- Segment display visuals (7-segment and 14/16-segment style via `SegmentDisplayVisualBase`).
- Basic editor/shared visual shapes from panel element factory.

### Runtime state container

`PanelRuntimeState` stores runtime values by object ID:

- lamp intensity (`double`),
- reel position (`int`),
- segment masks (`int[]`),
- segment brightness per cell (`double[]`),
- lamp test object marker.

It exposes *IfChanged methods to avoid duplicate updates and provides defaults when values are missing.

### Runtime-to-visual projection path

Runtime adapters mutate `DocumentTabViewModel.RuntimeState`, then raise `PanelVisualStateChanged` payloads. `PanelLayoutMapper.ApplyVisualState` resolves model kind (lamp/reel/alpha/segment) and applies control-level visual changes for each object ID.

## Current pan/zoom behavior

Pan/zoom behavior is currently split between:

- `CanvasPanZoomBehavior`: low-level transform operations (middle-mouse pan, mouse-wheel zoom, transform group setup);
- `CanvasPanBehavior`: attached behavior that syncs zoom/pan dependency properties (`PanelZoom`, `PanelPanX`, `PanelPanY`) and applies updates to canvas transform;
- `PlayView`: directly forwards mouse pan/zoom events to `CanvasPanZoomBehavior`.

Key observations:

- Transform state lives in WPF `ScaleTransform` + `TranslateTransform` on each canvas.
- Zoom pivot math is implemented in view space with inverse world-point reconstruction.
- No explicit shared `PanelViewportTransform` model exists yet across runtime renderer + hit-testing + overlays.
- Play View uses the generic canvas pan/zoom utility, but hit-testing still relies on WPF visual source traversal, not inverse-transform document-space hit tests.

## Renderer inventory summary (pre-Skia implementation)

### Reusable pieces

- Runtime state model (`PanelRuntimeState`) and per-object runtime semantics.
- Document/runtime event flow (`PanelVisualStateChanged`) that can drive render invalidation.
- Existing element model/type discrimination in `DocumentTabViewModel` / mapper logic.
- Existing input routing services for key/pointer actions.

### Pieces to replace for shared Skia runtime path

- WPF element-per-component runtime rendering in Play View.
- WPF visual-tree based hit source lookup (`Uid` + visual ancestor walk).
- Split transform ownership tied to per-canvas WPF transforms only.

### Migration guardrails

- Keep runtime state contracts stable while introducing renderer abstraction.
- Introduce shared viewport transform model first (for both draw + hit-test math).
- Keep Play View input routing behavior intact while replacing visual rendering backend.

## Step 1 completion

Renderer inventory captured before major renderer implementation, per `SKIA_RUNTIME_RENDERER_PLAN.md` Step 1.
