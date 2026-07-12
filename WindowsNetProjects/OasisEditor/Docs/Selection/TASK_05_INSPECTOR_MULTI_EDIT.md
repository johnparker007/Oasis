# Task 05 - Inspector Multi-Edit

## Goal

Implement Unity-style Inspector aggregation and atomic multi-object editing for selected Panel2D and Face components.

## Property State Model

Represent each property as one of:

- Common value
- Mixed value
- Unavailable

Do not use `-` as the internal value. The UI may display a dash for a mixed numeric or text field.

Use a three-state checkbox for mixed boolean values.

## Inspector Titles

Use useful aggregate titles, for example:

- `3 Lamps`
- `4 Reel Displays`
- `5 Components`

The exact text may follow existing naming conventions.

## Same-Type Selection

When all selected components have the same concrete type:

- Show common/base fields.
- Show meaningful type-specific fields.
- Show a common value when all selected values match.
- Show mixed state when values differ.
- Committing a value applies it to every eligible selected component.

Retain explicit property descriptors/builders rather than introducing broad reflection-based editing.

## Mixed-Type Selection

Initially show only these safe common fields:

- X
- Y
- Width
- Height
- Visible
- Lock Transform

Do not show bulk Name editing for mixed types.

## Transform Lock Rules

- `Lock Transform` remains editable across selections.
- Mixed lock values use an indeterminate checkbox.
- Transform fields should not mutate transform-locked items.
- Clearly define and test whether a transform edit applies only to unlocked selected items or is rejected when any selected item is locked.

Preferred initial behaviour: apply transform edits to unlocked eligible items and leave locked items unchanged, matching viewport group movement. The Inspector should communicate this consistently and avoid silent ambiguity where practical.

## Editing Semantics

- X, Y, Width, and Height edits are absolute assignments.
- Entering a value into a mixed field sets that value on all eligible selected items.
- Width and Height retain positive-value validation.
- One committed field edit creates one undo entry.
- One undo restores every affected object.
- Do not add relative syntax such as `+=10`.

## Refresh Behaviour

The Inspector must refresh when:

- Selection membership changes
- Primary selection changes where title/type display depends on it
- Any selected object's relevant property changes
- Undo/redo changes selected objects
- A selected object is deleted

Avoid rebuilding every row unnecessarily during high-frequency move previews. Preserve or improve the current suppression/throttling behaviour.

## Special Selection Domains

Preserve existing single-selection Inspector support for:

- Panel Face Source Shapes
- Face mask layers
- Document-level Face properties
- Asset Browser inspection

Do not force unsupported special-domain combinations into multi-edit. Show a clear read-only summary when the selection combination is unsupported.

## Out of Scope

- Relative expressions
- Alignment/distribution
- Group resize
- Context-menu redesign
- Bulk rename
- Cross-domain Face Source Shape/component multi-edit

## Tests

Add tests for:

- Common numeric/text values
- Mixed numeric/text values
- Mixed boolean values
- Same-type type-specific rows
- Mixed-type base rows only
- Absolute assignment to multiple objects
- Locked-item transform exclusion
- Atomic undo/redo
- Validation failures without partial mutation
- Refresh after selected-object changes
- Existing single-selection special-domain Inspector behaviour

## Manual Test Checklist

- Select two lamps with equal X and confirm the common value appears.
- Give them different Y values and confirm mixed state appears.
- Enter a Y value and confirm both update.
- Undo once and confirm both return.
- Test mixed Visible and Lock Transform checkboxes.
- Select mixed component types and confirm only common base fields appear.
- Include a transform-locked component and confirm transform edits follow the documented rule.
- Confirm single-selection Face Source Shape, mask-layer, document, and asset inspection still work.

## Completion Report

Report property aggregation design, files changed, supported same-type fields, mixed-type behaviour, lock handling, tests run, and manual tests requested.

Stop after Task 05 for user testing and review.
