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
- [ ] Add `PanelElementModelUpdate` (or equivalent updater structure)
- [ ] Ensure all editable properties can be represented
- [ ] Do NOT break existing cloning logic

### U2 — Generic Update Command
- [ ] Add `CreateUpdateElementCommand(...)`
- [ ] Must:
  - [ ] target documentId
  - [ ] target objectId
  - [ ] store previous + new snapshot
  - [ ] skip no-op updates
  - [ ] mark document dirty only when needed

### U3 — Validation Layer
- [ ] Add numeric validation helpers
- [ ] Width/Height > 0 enforced
- [ ] Invalid edits must not execute commands

### U4 — Tests (logic only)
- [ ] Update command tests
- [ ] Undo/redo tests
- [ ] No-op prevention tests

---

## Phase V — Inspector Property System

### V1 — Property Row ViewModels
- [ ] Add base property row VM
- [ ] Add:
  - [ ] string
  - [ ] double
  - [ ] int
  - [ ] bool
  - [ ] read-only/info

### V2 — Binding Strategy
- [ ] Property rows must:
  - [ ] read from selected element
  - [ ] issue commands on change
  - [ ] NOT mutate model directly

### V3 — Commit Behavior
- [ ] Text/numeric commit on Enter or focus loss
- [ ] Bool commit immediately
- [ ] Avoid flooding undo stack

---

## Phase W — Inspector UI

### W1 — Replace Summary UI
- [ ] Replace current InspectorView layout
- [ ] Add grouped layout:
  - [ ] Transform
  - [ ] Common
  - [ ] Type-specific
  - [ ] Metadata

### W2 — Common Fields
- [ ] Name
- [ ] ObjectId (read-only)
- [ ] Kind (read-only)
- [ ] X/Y
- [ ] Width/Height
- [ ] Locked
- [ ] Visible

### W3 — Canvas Integration
- [ ] X/Y changes move element
- [ ] Width/Height changes resize element
- [ ] Name updates hierarchy

---

## Phase X — Element-Specific Fields

- [ ] Lamp fields
- [ ] Reel fields
- [ ] SevenSegment fields
- [ ] Alpha fields
- [ ] Image/Background asset fields

- [ ] Hide irrelevant fields

---

## Phase Y — Integration & Stability

- [ ] Selection change refreshes Inspector
- [ ] Undo/redo refreshes Inspector
- [ ] Deleting selection clears Inspector
- [ ] Save/load preserves edits

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
