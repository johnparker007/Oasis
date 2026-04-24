# CanvasPanBehavior Split Plan (Plan Only)

This document captures a behavior-preserving extraction plan for `OasisEditor/CanvasPanBehavior.cs`.

## Current responsibilities observed

`CanvasPanBehavior` is currently handling multiple concerns:

1. **Pan/zoom interaction**
   - Mouse middle-button panning state
   - Mouse wheel zoom with pivot preservation
   - Render transform creation/management

2. **Selection interaction**
   - Hit-testing/selectable element traversal
   - Current selected element tracking
   - Attached selection flags (`IsSelectable`, `IsSelected`)

3. **Element creation interaction**
   - Rectangle/image tool checks
   - Click-to-create element placement math in canvas space
   - Default dimensions for new elements

4. **Layout mapping and persistence**
   - JSON-to-visual materialization
   - Visual-to-JSON extraction
   - Persisted element tagging and hydration guards

5. **Mutation command implementation**
   - Add rectangle/image commands (execute/undo)
   - Element identity comparison logic
   - Command dispatch glue through document/shell command routing

## Proposed extraction sequence

Order aligns with `TASKS.md` so each step stays small and reviewable.

### 1) Extract selection behavior
- Create `CanvasSelectionBehavior.cs`.
- Move:
  - Selection attached properties (`IsSelectable`, `IsSelected`)
  - Selected element state management
  - Selectable hit-test traversal
  - Selection-specific handling from left-click path
- Keep public attached-property names unchanged.

### 2) Extract pan/zoom behavior
- Create `CanvasPanZoomBehavior.cs`.
- Move:
  - Pan state attached properties (`StartPoint`, `Origin`, `IsPanning`)
  - Pan handlers (`OnMouseDown`, `OnMouseMove`, `OnMouseUp`, `OnLostMouseCapture`, `EndPan`)
  - Zoom handler (`OnMouseWheel`)
  - Transform helper (`EnsureTransformGroup`) and zoom constants
- Keep input gestures and zoom limits unchanged.

### 3) Extract element factory
- Create `PanelElementFactory.cs`.
- Move:
  - `CreateVisualFromElement`
  - `CreatePlaceholderImageSource`
  - Default rectangle/image dimensions
- Provide two-direction helpers if useful:
  - `CreateVisual(PanelElementFile)`
  - `TryCreateElement(FrameworkElement visual, out PanelElementFile element)`

### 4) Extract layout mapper
- Create `PanelLayoutMapper.cs`.
- Move:
  - `ApplyPersistedLayout`
  - `SyncPanelLayout`
  - `CreateElementFromVisual`
  - Layout re-entry guard usage pattern
- Mapper should depend on `Panel2DDocumentStorage` + `PanelElementFactory`, not on mouse behavior.

### 5) Extract mutation commands
- Create `CanvasMutationCommands.cs`.
- Move:
  - `AddRectangleMutationCommand`
  - `AddImageMutationCommand`
  - `IsSameElement`
- Keep command descriptions and undo/redo semantics unchanged.

### 6) Reduce CanvasPanBehavior to composition/glue
- Leave `CanvasPanBehavior` as attachment/orchestration layer:
  - Wire events to specialized behavior modules
  - Coordinate layout-json dependency property callbacks
  - Route canvas mutation command execution through existing document-aware path
- No behavior change; only delegation.

## Guardrails for implementation tasks

- Preserve existing attached property names used by XAML/bindings.
- Preserve command descriptions (`"Add rectangle"`, `"Add image"`).
- Keep persisted/non-persisted element filtering behavior identical.
- Keep document-aware command routing exactly as-is.
- Run `dotnet build` after each extraction step and fix issues immediately.
