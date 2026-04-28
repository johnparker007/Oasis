# TASKS.md

## Current Focus — Unity-Style Inspector Editing for Panel2D Elements

PRIMARY IMPLEMENTATION REFERENCE:
- Docs/InspectorEditingPlan.md

Codex MUST read and follow that document before implementing any Inspector work.

Goal: replace the current read-only/summary-style Inspector with a Unity-style property editor for the selected Panel2D element.

---

## Execution Rules (READ FIRST)

- Do NOT attempt to build or run tests in Codex.
- Do NOT create Build/Test attempt logs.
- Implement code only.
- After each task, state what John must test locally.
- Follow Docs/InspectorEditingPlan.md for all architectural decisions.

---

## Phase U — Foundation (DO FIRST)

### U1 — Element Update Infrastructure
- [x] Add `PanelElementModelUpdate` (or equivalent updater structure)
- [x] Ensure all editable properties can be represented
- [x] Do NOT break existing cloning logic

### U2 — Generic Update Command
- [x] Add `CreateUpdateElementCommand(...)`
- [x] Must:
  - [x] target documentId
  - [x] target objectId
  - [x] store previous + new snapshot
  - [x] skip no-op updates
  - [x] mark document dirty only when needed

### U3 — Validation Layer
- [x] Add numeric validation helpers
- [x] Width/Height > 0 enforced
- [x] Invalid edits must not execute commands

### U4 — Tests (logic only)
- [x] Update command tests
- [x] Undo/redo tests
- [x] No-op prevention tests

---

## Phase V — Inspector Property System

### V1 — Property Row ViewModels
- [x] Add base property row VM
- [x] Add:
  - [x] string
  - [x] double
  - [x] int
  - [x] bool
  - [x] read-only/info

### V2 — Binding Strategy
- [x] Property rows must:
  - [x] read from selected element
  - [x] issue commands on change
  - [x] NOT mutate model directly

### V3 — Commit Behavior
- [x] Text/numeric commit on Enter or focus loss
- [x] Bool commit immediately
- [x] Avoid flooding undo stack

---

## Phase W — Inspector UI

### W1 — Replace Summary UI
- [x] Replace current InspectorView layout
- [x] Add grouped layout:
  - [x] Transform
  - [x] Common
  - [x] Type-specific
  - [x] Metadata

### W2 — Common Fields
- [x] Name
- [x] ObjectId (read-only)
- [x] Kind (read-only)
- [x] X/Y
- [x] Width/Height
- [x] Locked
- [x] Visible

### W3 — Canvas Integration
- [x] X/Y changes move element
- [x] Width/Height changes resize element
- [x] Name updates hierarchy

---

## Phase X — Element-Specific Fields

- [x] Lamp fields
- [x] Reel fields
- [x] SevenSegment fields
- [x] Alpha fields
- [x] Image/Background asset fields

- [x] Hide irrelevant fields

---

## Phase Y — Integration & Stability

- [x] Selection change refreshes Inspector
- [x] Undo/redo refreshes Inspector
- [x] Deleting selection clears Inspector
- [x] Save/load preserves edits

---

## Local Testing (John)

After each phase:

- Build solution
- Run tests
- Open project
- Edit element properties
- Verify:
  - movement
  - resizing
  - undo/redo
  - save/load persistence

---

## DO NOT DO

- No direct mutation of PanelElementModel
- No WPF logic in model/domain
- No large refactors outside this scope
- No new frameworks

---

## Backlog

(unchanged, handled later)
