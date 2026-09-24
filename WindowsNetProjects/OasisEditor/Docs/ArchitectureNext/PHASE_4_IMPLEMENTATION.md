# Architecture Next Phase 4 implementation

## Result

Phase 4 formalizes the smallest host/mount/device model demonstrated by the existing Reel workflow:

> Face owns where the Reel is mounted. Reel owns the reusable physical implementation. Machine chooses the Reel asset for each logical `Reel:n`.

The authored Face type is now `FaceReelMount`, serialized as `kind: "reelMount"`. This is a typed model, not a generic mount framework. No Device base class, mount interface, generic resolver, profile registry, library, or new asset type was introduced.

## Physical-concept audit

| Current concept | Classification | Evidence and Phase 4 decision |
|---|---|---|
| `FaceReelMount` (formerly `FaceReelDisplayElement`) | **Host placement/mount** | Face-owned stable object ID, rectangle, visibility/lock state, Panel2D provenance, logical `Reel:n`, reel-band artwork, crop/presentation, stops, direction/offset presentation, opacity, transmission mask, and reel-lamp presentation. It does not contain physical diameter/width or a final Reel asset reference. |
| Reel package (`asset.reel`) | **Reusable physical Device** | Independently reusable identity, display name, diameter and width. It remains the only Device asset implemented in this phase. |
| `FaceButtonElement` | **Host-placement candidate plus authored runtime element** | It has a Face rectangle, stable object ID, Panel2D provenance, and a separate logical input reference, but the repository has no reusable physical Button implementation, Machine Button assignment, or physical Button data to resolve. Renaming it alone would create broad schema/UI/input churn without completing a typed Device composition path, so it is deliberately deferred as the next typed-mount candidate. No Button asset/profile/path was added. |
| `FaceLampWindowElement` | **Pure authored visual/runtime element** | A game-artwork window/mask linked to logical lamp state, not an independently reusable physical package. |
| `FaceLampEmitterElement` | **Pure authored visual/runtime element** | Derived lamp illumination placement/tray data used to render a Face. It is not a reusable mechanism. |
| `FaceSevenSegmentDisplayElement` and `FaceAlphaDisplayElement` | **Pure authored visual/runtime elements** | Face regions and presentation settings linked to runtime display state. There is no reusable physical display package in the current workflow. |
| Panel2D reel/button elements and MFME data | **Source/import representation** | Conversion produces Face mounts/elements and logical references. Import never selects or creates reusable Device assets. |
| `MachineObjectReference.Reel(n)`, input references, lamp IDs, display IDs | **Logical runtime identity** | These identify runtime semantics and remain distinct from stable Face element IDs and reusable asset identity. |
| Cabinet `OasisFace_*` targets and `SurfaceTargetSettings` | **Reusable host surface targets** | They locate/orient whole Faces on Cabinet geometry, not physical Device mounts. |
| Cabinet model/reflection receiver geometry | **Reusable host geometry/presentation** | The Cabinet is architecturally a host, but the current schema and GLB conventions contain no authored Cabinet-hosted Button, CoinMech, NoteAcceptor, or other Device placement. |

## Face Reel mount model and identity

`FaceReelMount` continues to derive from the existing `FaceElementModel`, which already supplies the genuinely shared placement primitives: `ObjectId`, name, X/Y/width/height, visibility, transform lock, logical machine reference, and Panel2D provenance. A second `PhysicalMountRect`, `IMount`, generic `DeviceMount<T>`, or host hierarchy would duplicate this proven base without a current consumer, so none was added.

`FaceReelMount.ObjectId` is persistent authored placement identity. `MachineObjectReference.Reel(3)` is logical runtime identity. Regeneration matches by Panel2D provenance and preserves the existing stable Face object ID and authored logical reference; neither identity substitutes for the other.

The mount's `AssetPath` remains the reel **band/artwork** input used by Face rendering/export. It is not an `asset.reel` reference. Physical `DiameterMm`/`WidthMm` remain solely on `ReelDocument`; final `ReelAssetPath` remains solely on `MachineReelAssignment`.

## Schema 24

Face schema advances directly from 23 to 24. The current serialized kind is `reelMount`; the writer emits only this kind. `reelDisplay` and `reel` are obsolete and are not compatibility aliases: the current reader rejects either kind, as well as unknown element kinds, before reporting the document as openable. A successful `TryRead` or `TryReadValidated` therefore guarantees that the returned DTO can be materialized with `ToModel`. Schema 23 is rejected by the latest-only reader.

Face runtime schema advances from 9 to 10 to remove the unused, misleading `cabinetReelTargetId` field from both Editor and Player DTOs. Runtime Reel identity is now exactly `objectId` for the authored Face mount identity and `machineReference` for logical `Reel:n`; no Cabinet Reel identity exists. The authored mount remains flattened by the Editor, so Player neither sees a mount type name nor receives an authored Reel asset path.

## Generation and regeneration

Panel2D/MFME reel conversion now explicitly creates a `FaceReelMount` with transformed Face bounds, logical `Reel:n`, band/presentation fields, visibility/locking, and source element provenance. It does not create or select a Reel asset.

Face regeneration continues to match a reel mount through its source Panel2D element ID. It preserves the stable Face object ID and an existing authored logical machine reference, while source-driven bounds, name, band artwork, stops, offsets, reversal, visibility, and locking follow regenerated Panel2D data. Manual non-generated elements remain untouched. No Machine Reel assignment is copied into Face.

## Editor behavior

Hierarchy entries and groups are labelled **Reel Mount** / **Reel Mounts**, and selection kind is `reelMount`. Existing transform, selection, delete, command history, dirty-state, save/reopen, preview renderer, lamps, and runtime-state behavior continue through the renamed typed model. The Inspector still exposes Face placement and presentation fields; it does not expose physical Reel diameter/width or Reel asset assignment.

`FaceButtonElement` and its input routing remain unchanged. Lamp windows, emitters, and segment/alpha displays remain Face/runtime elements; no Lamp or Display asset was created.

## Machine composition and runtime

Machine requirement discovery now scans assigned Face `FaceReelMount` instances explicitly. Face mounts determine which logical Reel roles physically exist, and Machine assignments select Devices only for those current roles. An explicit Face-composition change prunes `MachineReelAssignment` entries whose logical references are no longer required as part of the same undoable Machine-document mutation; still-required assignments, including selections whose Reel asset is missing, remain untouched. Undo and redo therefore restore or remove the Face assignment and its Reel mappings atomically.

Saved Face content and asset-catalog refresh remain non-authoring dependency updates. They immediately refresh the visible Reel rows from current saved Face requirements without dirtying or silently rewriting the Machine. A stale serialized assignment may consequently remain temporarily in the underlying model after an external Face edit, but it is neither displayed as an active requirement nor traversed by build; the next explicit Face-composition mutation normalizes it. Reel row topology is derived only from current assigned-Face requirements and deduplicates the same logical role when one Face is mounted on multiple Cabinet targets.

Build resolution consumes each typed mount's logical reference, resolves it through `MachineReelAssignment`, validates the selected `ReelDocument`, and gives Face export the resolved asset. Diagnostics now name the Machine, Face, reel mount, logical reference, and failing Reel assignment/path. Face export combines mount placement/presentation with resolved Reel width/radius into the existing flattened runtime entry.

A standalone Face export supplies no Machine composition context. It still exports Face-owned reel data and leaves physical width/radius unresolved. Oasis Player requires no code or schema change.

## Cabinet and profiles decisions

Cabinet is a host type architecturally, but no Cabinet-hosted typed Device mount is sufficiently implemented to warrant authored schema. GLB discovery currently provides Face surface targets and reflection receivers only. Consequently schema 9 gains no speculative mount arrays, Device references, or UI.

Direct `Reel:n -> Reel asset` Machine assignments passed Phase 3 verification and remain sufficient. Phase 4 found no concrete need for `small-reel`/`standard-reel` profile tables, Cabinet defaults, or profile resolvers, so profiles remain deferred.

## Focused automated coverage

Tests were updated to compile and assert against `FaceReelMount` throughout storage, generation/regeneration, hierarchy/Inspector interaction, rendering/runtime state, Machine discovery, and runtime export. Dedicated storage coverage asserts schema-24 `reelMount` round-trip, placement and logical identity persistence, absence of physical dimensions/final Reel asset path, rejection of schema 23, and rejection of obsolete reel element kinds. Existing tests continue to cover stable regeneration identity, generated-field refresh, command/undo behavior, assigned-Face discovery, resolved physical dimensions, standalone unresolved dimensions, and unchanged renderer behavior.

## Manual verification checklist

1. Open a Face generated from MFME.
2. Select a Reel placement and verify the hierarchy identifies it as a Reel Mount.
3. Verify position, size, logical Reel, band, stops, offsets, lamps, and visibility properties still work.
4. Move, resize, and hide the mount; exercise undo and redo.
5. Save and reopen the Face; verify mount identity, placement, logical reference, and presentation persist.
6. Regenerate the Face from Panel2D; verify stable mount ID/logical reference preservation and expected source-driven updates.
7. Inspect `asset.face`; verify `kind` is `reelMount` and it contains neither physical diameter/width nor final Reel asset selection.
8. Open Machine and verify required logical Reel rows still derive from only its assigned Faces.
9. Assign Standard and Small Reel assets and save/reopen Machine.
10. Build/Preview the Machine in Oasis Player.
11. Verify reel geometry, scale, lamps, direction, band offsets, and platform behavior are unchanged.
12. Export/build the Face standalone and verify no Machine or Cabinet is required and physical dimensions remain unresolved.
13. Verify Face button clicking/input routing and Player/runtime button behavior remain unchanged (Button stays `FaceButtonElement`).
14. Open Cabinet and verify no unused mount UI, arrays, Device references, or profile tables were introduced.

## Architecture deviations

There is one intentional conservative decision: `FaceButtonElement` was not renamed in this phase. Its placement suggests the eventual `FaceButtonMount`, but the current repository has no reusable Button physical implementation or composition resolution. Reel is therefore the only complete typed host/mount/device path. This follows the phase rule to convert proven systems rather than naming speculative architecture into existence.
