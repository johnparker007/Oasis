# Skia Edit View Rebuild Inventory (Phase 1)

This inventory is for the `SKIA_EDIT_VIEW_REBUILD_FROM_GAME_VIEW_PLAN.md` Phase 1 cut-line task.

## 1) Current Game/Play View foundation (reuse as-is first)

### Primary files/classes
- `OasisEditor/Views/PlayView.xaml`
  - Hosts `SKElement` (`PlaySkiaSurface`) as primary render surface.
  - Keeps a legacy hidden WPF `Canvas` (`PlayCanvas`, collapsed + non-hit-testable) still wired for fallback input paths.
- `OasisEditor/Views/PlayView.xaml.cs`
  - Owns Skia render loop and input event wiring.
  - Uses `Panel2DRenderer` with element renderers (`Background`, `Lamp`, `Reel`, `SevenSegment`, `Alpha`).
  - Implements middle-button pan, mouse-wheel zoom, pointer-id hit resolution from document-space.
- `OasisEditor/PanelViewportTransform.cs`
  - Canonical transform math: `DocumentToScreen`, `ScreenToDocument`, `WithPannedBy`, `WithZoomAt`.
- `OasisEditor/Rendering/Panel2DRenderer.cs` and `OasisEditor/Rendering/*.cs`
  - Shared renderer pipeline used by Play View.

### Play View pan/zoom behavior to reuse
- Current Skia pan/zoom state in `PlayView.xaml.cs` (`_skiaZoom`, `_skiaPan`) is functionally correct.
- Zoom around cursor is implemented and should become the edit-view baseline behavior.
- Transform calculation logic should be normalized to `PanelViewportTransform` usage in new Edit View to avoid duplicate math branches.

### Play View runtime state integration
- Runtime drawing already consumes `DocumentTabViewModel.RuntimeState` through `Panel2DRenderer`.
- This is the preferred path for new Edit View live emulation rendering.

## 2) Current old Edit View responsibilities (reference only, not foundation)

### Primary files/classes
- `OasisEditor/Views/PanelCanvasView.xaml`
  - Existing Panel2D edit surface based on WPF `Canvas` with `CanvasPanBehavior` attached properties.
- `OasisEditor/CanvasPanBehavior.cs`
  - Orchestrates selection, tool placement, pan/zoom forwarding, viewport syncing to document properties, and visual-state fanout.
- `OasisEditor/PanelLayoutMapper.cs`
  - Rebuilds/syncs WPF child visuals from layout JSON, including runtime visual state patching.
- `OasisEditor/CanvasSelectionBehavior.cs`, `CanvasPanZoomBehavior.cs`, `PanelToolPlacementController.cs`
  - Selection and interaction glue for WPF visual tree interaction.

### Old responsibilities that must be rebuilt on new Skia Edit View
- Viewport state persistence/binding (`PanelZoom`, `PanelPanX`, `PanelPanY`).
- Selection semantics (single select, clear on empty click, hierarchy/inspector sync).
- Tool interaction flow (incremental reintroduction after baseline select works).
- Document mutation dispatch via document-scoped commands.

### Old/WPF-heavy code paths to retire after replacement stabilizes
- WPF component-per-element rendering via `PanelLayoutMapper.ApplyPersistedLayout` + `PanelElementFactory.CreateVisualFromElement`.
- Runtime visual patching to WPF visuals (`PanelLayoutMapper.ApplyVisualState` over `Canvas` child map).
- Reliance on `CanvasSelectionBehavior` visual hit-testing for component selection.
- Edit-view interaction assumptions tied to legacy WPF child hit zones.

## 3) Current selection + document mutation APIs (must remain)

### Selection state APIs
- `ActiveDocumentContextService`:
  - `SetPanelSelection(documentId, selection)`
  - `ActivePanelSelection`
- `MainWindowViewModel.UpdateDocumentPanelSelection(Guid, PanelSelectionInfo?)`
  - Synchronizes document-level selection + hierarchy sync.
- `DocumentTabViewModel.HierarchySelectedPanelSelection`
  - Existing selected element contract for panel document context.

### Panel/document model access APIs
- `DocumentTabViewModel.GetPanelElements()`
- `DocumentTabViewModel.TryGetPanelElement(...)`
- `DocumentTabViewModel.SetPanelElements(..., PanelChangeEvent?)`
- `DocumentTabViewModel.MarkDirty()`
- `DocumentTabViewModel.PanelChanged` event (inspector/hierarchy fanout)

### Mutation/undo APIs
- `CanvasMutationCommands` factory methods for add/delete/rename/duplicate/paste/reorder/lock/visible/update.
- `CanvasCommandDispatcher.ExecuteMutation(...)` routes through `MainWindowViewModel.ExecuteDocumentCanvasCommand(...)` when available.
- `CommandService` enforces document ownership (`IDocumentCommand.DocumentId`) and undo/redo history.

## 4) Rebuild vs retire cut line

## Rebuild (new Skia Edit View path)
1. Skia-based edit surface using Play View renderer path.
2. Shared viewport transform state and cursor-centric zoom behavior.
3. Document-space hit testing against `DocumentTabViewModel.GetPanelElements()`.
4. Selection overlay layer + selection sync into existing document/hierarchy/inspector contracts.
5. Command-based mutation path (existing command service/contracts).

## Retire (once new path reaches parity milestones)
1. WPF runtime component rendering for Panel2D edit path (`PanelLayoutMapper` + `PanelElementFactory` runtime visuals).
2. Old visual-tree hit-test dependency for element interaction.
3. Old edit-view pan/zoom interaction path based on WPF canvas child geometry.

## 5) Minimum viable rebuild feature order (from current plan)

1. New Skia Edit View shell renders selected Panel2D and supports global pan/zoom.
2. Document-space hit testing service (topmost bounds-based).
3. Single click selection + clear selection.
4. Selection overlay alignment with viewport transform.
5. Multi-select rectangle.
6. Move selected elements using document-space delta and commands.
7. Resize handles.
8. Context/clipboard/reorder command wiring.

## 6) Immediate implementation guidance after this inventory

- Implement a **new** `SkiaPanel2DEditView` (or equivalent) side-by-side with existing `PanelCanvasView`.
- Copy Play View Skia host + pan/zoom wiring as the baseline rather than reusing old Canvas interaction internals.
- Keep old edit view in place temporarily for reference only until milestone routing is switched.
- Start with no tool placement and no WPF child element visuals in the new view; add editing interaction incrementally.
