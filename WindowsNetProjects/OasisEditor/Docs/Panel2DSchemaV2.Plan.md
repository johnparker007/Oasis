# Panel2D Schema V2 Plan (Native Oasis Components for MFME Import)

## Scope

This document completes the **Phase N schema design task** before storage code edits.

Goal: add native Oasis component kinds and properties needed for MFME import output while keeping MFME-specific concepts out of the core Panel2D schema.

## Decision: Introduce Schema Version 2

Schema version **2** is required for this feature track because version 1 currently supports only:

- `rectangle`
- `image`
- `anchor`
- `zone`

Native imported components need explicit, first-class kinds and native properties that version 1 cannot represent without overloading unrelated fields.

## Backward Compatibility Rules

- Schema version 1 files must continue to load.
- Schema version 1 files should be normalized into the in-memory model with sensible defaults for any newly introduced v2-only properties.
- Save behavior for an unchanged v1 document can remain as-is until a dedicated migration policy is implemented in code.
  - If the document contains only v1-compatible element kinds, saving as v1 is acceptable.
  - If the document contains v2-native kinds/properties, saving must write schema version 2.

## Future Schema Behavior

- Any schema version greater than the editor-supported version must be rejected with a clear error.
- Any unsupported lower/unknown version must be rejected with a clear error.
- Errors should remain explicit and avoid silent coercion.

## Native Kinds to Add in V2

Use Oasis-native naming and semantics:

- `background`
- `lamp`
- `reel`
- `sevenSegment`
- `alpha`

No MFME-specific kind names or source tags should be required by the core model.

## Proposed V2 Native Property Shape

Add a generic optional object on each element:

- `native` (optional, object)

`native` contains Oasis-native fields only (all optional unless required by a kind):

- `assetPath` (string, project-relative)
- `number` (int; lamp/reel/segment display number where applicable)
- `text` (string)
- `textColor` (RGBA object)
- `onColor` (RGBA object)
- `offColor` (RGBA object)
- `displayColor` (RGBA object; seven-segment)
- `reversed` (bool; alpha/reel)
- `stops` (int; reel)
- `visibleScale` (float; reel)
- `outline` (bool; lamp visual placeholder behavior)

Optional import provenance, if retained, should be isolated and generic:

- `importSource` (optional object)
  - `format` (string, e.g. `"MFME"`)
  - `layoutName` (string, optional)

This provenance object must not be required by command behavior, selection identity, undo/redo, or serialization validity.

## Validation and Normalization (Design Targets)

- Keep existing object-ID and name normalization rules.
- Reject invalid element dimensions consistently across old and new kinds.
- Reject invalid kind/native property combinations with clear messages.
- Allow missing optional native fields so placeholder visuals can still render.
- Keep identity object-ID-based.

## Migration Outline

1. Parse `schemaVersion`.
2. If `1`, load using existing rules and map into model with default `native = null`.
3. If `2`, parse known v2 kinds + native property object.
4. Reject any unknown schema version with explicit error text.

## Non-Goals in This Step

- No storage code change yet.
- No mapper implementation yet.
- No WPF visual projection changes yet.
- No MFME field names introduced into core Panel2D schema.
