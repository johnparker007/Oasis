# Copy / Paste / Duplicate Support Plan (Plan Only)

This document defines a planning-only approach for adding copy/paste/duplicate behavior in the Panel2D editor.

## Goals

- Let users duplicate selected objects in-place or with a visible offset.
- Let users copy/cut/paste selected objects across documents of compatible type.
- Preserve stable object identity guarantees by generating new IDs for pasted/duplicated elements.
- Keep behavior fully undoable/redoable via document command history.

## Non-Goals

- No implementation in this task.
- No cross-application clipboard format standardization in this task.
- No cabinet/machine editor clipboard behavior in this task.

## Selection Scope Rules

Planned selection constraints:
- Copy/cut/duplicate commands operate on the active document selection only.
- If no eligible objects are selected, command is a no-op.
- Non-persisted/instructional visuals are never included.

## ID Generation Requirements

ID safety is the core requirement for this feature.

For every duplicated/pasted element:
- Generate a new stable `ObjectId` (never reuse source IDs).
- Preserve user-facing properties where allowed (name/kind/size/position), then apply conflict handling.
- Preserve source-to-clone mapping in-memory during one operation for relative relationships/order.

Name handling policy (planned):
- Keep original names when unique.
- Apply deterministic suffixing when conflicts occur (e.g., `"Name (2)"`, `"Name (3)"`).
- Name conflict normalization must be deterministic for testability.

## Clipboard Payload Design (Planned)

Two payload tiers:

1. Internal editor payload (preferred when source and target are OasisEditor instances)
   - Includes schema version + element DTO list + optional metadata.
2. Optional text/JSON fallback payload
   - Enables basic persistence between app sessions.

Validation expectations:
- Reject unsupported schema versions with explicit errors.
- Ignore malformed payloads safely with user-visible feedback (output log).
- Never mutate document state when payload validation fails.

## Undo/Redo Requirements

Copy/paste/duplicate/cut must be represented as mutation commands with proper no-op guards.

Planned command semantics:

- `DuplicateSelectionMutationCommand`
  - Executes: clone selected elements with new IDs and offset.
  - Undo: remove inserted clones.
- `PasteElementsMutationCommand`
  - Executes: materialize validated clipboard elements with new IDs.
  - Undo: remove pasted elements.
- `CutSelectionMutationCommand`
  - Executes: copy payload then remove selected elements.
  - Undo: restore removed elements (with original IDs for undo fidelity).

History rules:
- No-op commands must not enter history.
- Undo/redo stays scoped to the owning document only.
- Executing a new mutation clears redo only in that document.

## Positioning Rules (Planned)

- Duplicate: apply consistent default offset (for discoverability).
- Paste: if pasting repeatedly, cascade offset each time.
- Optional future: paste at cursor position when initiated from canvas context.

## UI Command Entry Points (Planned)

- Main menu (`Edit`): Copy, Cut, Paste, Duplicate.
- Keyboard shortcuts: `Ctrl+C`, `Ctrl+X`, `Ctrl+V`, `Ctrl+D`.
- Canvas context menu: Copy/Cut/Paste/Duplicate for current selection.
- Hierarchy context menu: Copy/Cut/Paste/Duplicate for selected items.

Enable/disable policy:
- Copy/Cut/Duplicate enabled only with eligible selection.
- Paste enabled only when clipboard payload validates for current document type.

## Model / Storage Interactions

- Clipboard operations mutate the live `Panel2DDocumentModel` first.
- Save/load remains unchanged except persisted results of those mutations.
- Added elements from paste/duplicate must participate in existing dirty-state logic.

## Testing Plan (for implementation task)

Unit tests:
- ID regeneration always produces unique IDs.
- Name conflict resolution is deterministic.
- No-op commands do not enter undo history.

Command/history tests:
- Duplicate undo/redo restores exact pre/post state.
- Paste undo/redo removes/restores inserted elements correctly.
- Cut undo/redo restores removed elements and selection behavior.

Clipboard validation tests:
- Unsupported schema payload is rejected safely.
- Malformed payload is rejected without document mutation.

Smoke tests:
- Copy/paste single and multi-selection.
- Duplicate with expected offset and naming behavior.
- Cross-tab safety: operations apply to active document only.

## Suggested Implementation Sequence

1. Define clipboard payload contracts + validators.
2. Implement ID/name conflict helpers.
3. Add duplicate/paste/cut mutation commands with no-op guards.
4. Wire menu, shortcuts, and context menu command bindings.
5. Add tests and verify smoke flows.

