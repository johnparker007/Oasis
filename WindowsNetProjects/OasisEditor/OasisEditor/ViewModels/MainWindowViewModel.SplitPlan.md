# MainWindowViewModel Split Plan (Plan Only)

## Goals
- Keep current bindings and command names stable during refactor.
- Reduce `MainWindowViewModel` responsibilities using composition.
- Preserve document-scoped command behavior and undo/redo routing.

## Proposed Split Order
1. **DocumentWorkspaceViewModel**
   - Own document tab collection/selection and open-close-replace flows.
   - Own document creation/open/save helpers and document mutation command wrappers.
   - Surface existing command-facing properties consumed by the shell.

2. **AssetBrowserViewModel**
   - Own asset discovery/refresh and `AssetBrowserItems` state.
   - Own selected-asset state and refresh command can-execute updates.

3. **InspectorViewModel**
   - Own inspector title/type/path/summary projection for current selection.
   - Own inspector summary edit/apply command state and validation.

4. **OutputLogViewModel**
   - Own output entry collection, append helpers, and clear command behavior.

## Composition Strategy
- Keep `MainWindowViewModel` as orchestration/root shell VM.
- Inject child VMs via constructor (or create internally first, then migrate to injection later).
- Forward existing shell bindings through pass-through properties to avoid XAML churn initially.
- Migrate one child VM at a time and build after each extraction.

## Risk Controls
- Do not rename binding-facing members during initial split.
- Keep mutation command classes in place until document extraction is stable.
- Add focused regression checks after each extraction:
  - tab open/close/select
  - active-document undo/redo labels and enabled state
  - asset selection + inspector sync
  - output log append/clear behavior
