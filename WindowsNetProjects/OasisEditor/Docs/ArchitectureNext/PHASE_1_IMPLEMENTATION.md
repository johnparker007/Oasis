# Architecture Next Phase 1 implementation

## Current authored schemas

- Project schema 9 is workspace-only: name, timestamps, `Assets` and `Generated` layout. There is no separate project-root `Machines` path and no platform, ROM, input, Face-mount, or reel-mapping state.
- Machine schema 1 is stored at `Assets/Machines/<Name>/asset.machine`. It owns a stable GUID, display name, Cabinet asset reference, Cabinet-target-to-Face assignments, temporary logical-reel-to-Cabinet-specification assignments, a discriminated Emulation runtime (`platform` plus only that platform's settings), and inputs.
- Cabinet schema 7 owns its model, target configuration, reflection receiver configuration, preview settings, and temporary physical reel specifications/default only. Installed Face and logical reel assignments are not part of Cabinet.
- Cabinet reflection sources serialize `sourceSurfaceTargetId`. A Machine build resolves that target through its surface assignments.
- Machine runtime schema 5 includes Machine identity, Cabinet and Face composition, emulation runtime configuration, and inputs. Oasis Player validates and retains the runtime definition; Player-side Fabric execution is intentionally deferred.

## Active Machine and composition editing

The active Machine is the actual open `DocumentTabViewModel`; there is no second mutable Machine clone. Runtime settings, MFME inputs, Cabinet selection, Face assignments, and reel assignments all execute document commands against that same model and use its dirty/save/undo lifecycle. A sole Machine is opened automatically. Zero or multiple Machines leave the context empty until a Machine document is explicitly selected.

The Machine editor discovers Cabinet and Face package assets, detects the selected Cabinet's valid `OasisFace_*` targets, and exposes Face and temporary Cabinet reel-specification dropdowns including `(None)`. The package directory supplies the asset/tab title while `Machine.DisplayName` remains independently authored.

Composition selectors bind by stable project-relative asset path/specification ID rather than transient choice-object identity. The Assets browser publishes one catalog-change notification after disk refresh; every open Machine rebuilds and deduplicates its choices without changing authored assignments, dirty state, or undo history. Missing selected references remain visible as missing choices for diagnostics.

Face and reel assignment commands synchronize their existing editor rows in place, including during undo/redo; they do not rebuild the active `ItemsControl` during a ComboBox selection transaction. Cabinet changes rebuild only Cabinet-dependent rows, while catalog refreshes preserve row instances whenever target/reel structure is unchanged. Machine choice ComboBoxes use an explicit `DisplayName` data template for both dropdown and collapsed presentation.

Saving updates the existing tab's path/title/clean metadata in place. The Machine model, composition rows, command owner, runtime state, selected/active identity, Cabinet context, and event subscriptions remain the same objects; Cabinet, Face, reel, runtime, input, and display-name state are not reconstructed. Face save-time package/export transformations are applied explicitly to the existing Face model after its file is written.

Standalone Cabinet tabs render no mounted Faces without context. When an active Machine references that Cabinet, the Cabinet viewer receives that Machine tab explicitly and previews only its `SurfaceAssignments`; it never scans Machines or persists composition on Cabinet.

Runtime schema 5 stores the complete selected platform settings in `platformSettingsJson`, a concrete JSON string Unity `JsonUtility` can reliably deserialize and validate. Oasis Player deliberately retains but does not execute those settings in Phase 1.

## Phase-1 reel bridge

The authored Face keeps logical `Reel:n` references and placement. Machine maps each logical reel to an embedded specification ID on its selected Cabinet. Machine build resolves this chain and continues to flatten physical width/radius into Face runtime manifests. Cabinet reel specifications are temporary and are scheduled for replacement by first-class Reel assets in Phase 3.

## Manual verification checklist

1. Create a project and confirm `Assets/Machines/<ProjectName>/asset.machine` exists.
2. Open that manifest and confirm a structured Machine editor appears.
3. Set a Cabinet asset reference on Machine.
4. Assign Faces to the Cabinet's `OasisFace_*` target IDs on Machine.
5. Configure logical reel assignments against the selected Cabinet's temporary specifications.
6. Select the platform and configure ROM/runtime settings; confirm the Machine manifest changes.
7. Import MFME into a Panel2D and confirm imported input definitions are written to the active Machine.
8. Open Face and Cabinet independently; confirm neither manifest contains Machine composition assignments.
9. Save, close, and reopen the Project and Machine.
10. Select the saved Machine and use Build/Preview in Oasis Player.
11. Verify Cabinet, assigned Faces, resolved reels, and target-bound reflections render.
12. Create a second Machine with a different platform/ROM configuration, explicitly select each, and confirm settings do not leak.
13. Add an invalid unassigned Face and confirm it does not affect the selected Machine build.
