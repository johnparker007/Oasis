# Architecture Next Phase 6 implementation

## Result and ownership rule

Phase 6 adds a Machine-owned **Overview** tab containing a zoomable, read-only composition graph. The graph is rebuilt from the current `MachineDocument` and its explicit Cabinet, Face, Panel2D provenance, and Reel references. It is transient presentation state: no graph node, edge, coordinate, selection, grouping, cache, or link is serialized. Machine Details remains the only composition editor. No Phase 7 runtime abstraction is included.

## Repository and UI audit

The pre-Phase-6 Machine UI was one scrollable form in `DocumentEditorView.xaml`. `DocumentTabViewModel` owns the current in-memory `MachineDocument`; its document commands are the authoritative dirty/undo path. The post-Phase-5 navigation seam is `SetAssetDocumentOpener`/`OpenMachineAsset`, supplied by `MainWindowViewModel.OpenReferencedAssetDocument`. That callback enters `DocumentWorkspaceViewModel.OpenOrSelectDocument`, so canonical path de-duplication and existing-tab activation already work and did not need reimplementation.

Machine Cabinet and Reel references use `AssetReference` (`Project` or `Library`) and the containment-checking `AssetReferenceResolver`. Face assignments and Panel2D provenance remain project-relative strings. Machine choice refresh already fingerprints selected dependency manifests and Cabinet geometry, and the project/Library watcher funnels catalog changes through `RefreshMachineCompositionChoices`. The active Machine is tracked by `MainWindowViewModel`; Cabinet preview receives it as composition context.

Panel2D uses Skia and a dedicated viewport interaction/transform stack. Face uses the existing Face workspace, compositor, preview invalidations, and zoom/pan state. Cabinet uses Helix/WPF 3D plus `CabinetFacePreviewSourceResolver`; it is not a cheap off-screen thumbnail API. Reel editing is a compact form. No graph framework exists. Theme colours are semantic dynamic resources. Diagnostics elsewhere primarily use validation results and the Output pane. There was no general-purpose asset-thumbnail service suitable for a small graph card.

These facts led to a focused Canvas control rather than a new dependency or generic diagram framework, and to metadata cards rather than a second Face/Panel2D/Cabinet renderer.

## Overview / Details UX

Machine documents now default to the first tab, **Overview**. **Details** contains the unchanged Machine Composition form for display name, Cabinet, platform, Face targets, Reels, and inputs. Switching tabs retains the same `DocumentTabViewModel` and `MachineDocument`; it neither saves nor dirties the document.

Overview supplies Fit to Content and Actual Size controls plus an explicitly labelled current zoom value. The wheel zooms around the pointer from 25% to 300%. Left-drag on empty background and middle-drag pan. Initial display fits the deterministic graph with a margin. Clicking selects/highlights a node; double-clicking a resolvable asset opens it. Nodes are intentionally not draggable: automatic layout must be understandable without manual repair.

The workspace-level TabControl, TabItem, and diagnostics Expander use small shared Oasis control templates backed exclusively by semantic theme resources. This avoids the platform/Fluent fallback chrome that initially produced light selected tabs and a light diagnostics surface. Selected tabs use the Oasis selection brush and a three-pixel lower accent; tab content, toolbar, Details scroll surface, graph viewport, and expanded diagnostics body remain on workspace/panel brushes.

## Derived graph model

`MachineCompositionGraphBuilder` is UI-independent traversal and returns:

- `MachineCompositionGraph` — ordered nodes, edges, and diagnostics;
- `MachineCompositionNode` — stable derived identity, typed kind, title/metadata, optional scope/path, missing state, and calculated layout;
- `MachineCompositionEdge` — source, destination, label, and semantic kind;
- `MachineCompositionDiagnostic` — warning/error and related node.

The implemented node kinds are Machine, Runtime, Cabinet, Face, Panel2D, Reel, and a missing Reel-assignment placeholder. Target IDs and logical Reel roles are compact edge labels rather than noisy standalone nodes. Node identity uses the Machine ID or normalized authoritative reference/path, so one shared Panel2D or Reel produces one card.

The builder starts at the in-memory Machine and reads only explicitly referenced manifests. It never enumerates project or Library catalogs. When a referenced Face is open, its current in-memory model is used by exact manifest-path identity; open documents never create relationships.

## Edge semantics

Composition edges are solid: Machine -> Cabinet, Machine -> Runtime, Cabinet (or Machine when no Cabinet is authored) -> Face with the target ID, and Face -> Reel with `Reel:n`. Provenance is a separate dashed/lighter Panel2D -> Face edge labelled `source`. The graph does not add Panel2D to build traversal or runtime state.

## Deterministic semantic layout and routing

The initial per-kind column loop was insufficient: Cabinet and Runtime shared a column but each kind restarted at row zero, causing exact overlap, while Face-to-Reel lines crossed the intervening Machine/Cabinet region. The revised layout uses topology-specific lanes rather than enum columns: Machine occupies the root lane; Runtime and Cabinet have distinct rows in the next lane; Faces form a vertically ordered physical-composition lane; Reels are adjacent to and ordered by their first consuming Face; and Panel2D sources occupy a separate lower provenance lane. Missing nodes use the same lanes. Face order comes from the same ordered `GlbCabinetFaceTargetDetector` result used by Machine Details, not target ID, Face title, or asset-path alphabetization. Assignments whose targets are no longer detected follow valid targets in deterministic target-ID/path order. Reel first-consumer ordering then uses this corrected Face order. Coordinates are recalculated and never persisted.

Edges now have a derived routing projection containing orthogonal polyline points and an explicit label position. Composition routes leave the source's right port, turn in the reserved inter-lane gutter, and enter the destination's left port. Provenance routes use their own gutter from the lower source lane and retain dashed styling. Solid composition strokes use the stronger semantic secondary-text brush at slightly greater thickness; provenance uses the lower-contrast muted-text brush, preserving hierarchy in dark and light themes without a literal colour. Machine-to-Cabinet and Machine-to-Runtime labels are omitted because node types make those branches unambiguous; target, source, and Reel labels remain. Cabinet-to-Face labels use the detector's user-facing target display name, retain the target ID on the edge, and occupy the final per-Face horizontal segment immediately before the destination. Other labels remain in reserved source-side gutter space rather than at a geometric midpoint where another card could cover them.

Route endpoints are not fixed card midpoints. For every node, incoming left-side and outgoing right-side connections are independently sorted by edge semantics, related-node layout position, relationship ID/label, and stable node identity. `N` connections receive evenly distributed side ports at `(i + 1) / (N + 1)` of card height, keeping them away from rounded corners. Composition sorts before provenance, so two relationships entering the same Face remain visibly separate through the final segment. Source port, destination port, polyline, and label contract all live in the transient derived route model and are never serialized or recomputed by WPF.

Intermediate vertical routing lanes are allocated after side ports. Edges sharing the same pair of horizontal node lanes and base gutter are stably ordered by source/destination layout, edge kind, relationship identity, label, and node IDs, then offset around the base gutter at 16 logical-unit intervals. Distinct semantic edges therefore retain different vertical trunks instead of intentionally sharing a long coincident segment; an already-aggregated Reel relationship remains one edge and one lane. Labels are positioned from the final routed geometry rather than the un-offset base gutter.

Edge labels have a graph-defined bounded width of 104 logical units. The view wraps within that width for up to roughly two lines, trims overflow with an ellipsis, and exposes the complete relationship text as a tooltip. A shared Oasis badge style supplies `PanelBackgroundBrush`, `BorderStrongBrush`, a thin outline, rounded corners, and compact padding, making the line interruption intentional while remaining subordinate to node cards. Vertical placement reserves two-line height rather than assuming a single baseline.

Logical Reel edges remain derived from every exact `FaceReelMount -> MachineReelAssignment` role, then the graph presentation aggregates edges sharing Face, physical Reel asset, and composition semantics. One role renders as `Reel 3`; several roles render as `Reels 0, 1, 2`. The edge retains the complete ordered logical-role ID array for diagnostics/tests and this aggregation has no effect on runtime/build resolution.

## Thumbnails and performance

Cards show the information that can be obtained without new rendering infrastructure: type, display name, scope, input count, detected Cabinet face-target count, and Reel dimensions. Repository audit found no lightweight shared thumbnail service: Face preview is coupled to the full compositor/workspace, Panel2D to its Skia view, and Cabinet to an interactive Helix viewport. Phase 6 therefore deliberately uses clear Face/Panel2D type cards instead of inventing duplicate renderers or costly off-screen WPF/3D capture. This is the principal repository-driven reduction from the ideal thumbnail target.

Graph refresh performs dependency-rooted file reads only when the Machine model or existing catalog/watcher pipeline reports change; it does not render per frame or scan unrelated assets. No bitmap cache was necessary because no new bitmap renderer was introduced.

## Diagnostics and missing assets

A missing or invalid Cabinet, Face, Panel2D provenance, or Reel remains a warning card with authored relative path and scope. A Face logical Reel role without a Machine assignment remains connected to an `Unassigned Reel` card. The diagnostic expander reports the issue count and lists messages. A single corrupt dependency does not discard other graph content. Unrelated malformed assets are not visited.

Cabinet card target counts also come from that shared GLB discovery result. `SurfaceTargetSettings` is intentionally not counted because it is a sparse override collection, not the Cabinet's target catalog. If the Cabinet model cannot be discovered, the scope remains visible but no false zero count is displayed.

Cabinet target IDs remain visible on assignment edges. The graph does not parse GLB target geometry independently; the established Machine Details refresh is still the authoritative target discovery/invalid-target editor. An assignment retained by the Machine is still shown, making the authored target intent apparent even if the Cabinet is absent.

## Navigation

Double-click supports Cabinet, Face, Panel2D, and Reel nodes. Resolved manifest paths use the existing referenced-asset opener, which in turn uses workspace open/select/de-duplication. Missing nodes have no open action and references are never cleared. Machine and Runtime nodes intentionally do not navigate or create documents.

## Refresh and dirty state

Machine mutation/undo notifications rebuild Overview immediately from the same in-memory Machine. Project/Library catalog refresh also rebuilds it, covering save, external delete, and restore through the existing watchers. Referenced open Face models are used when safely available. Refresh, selection, pan, zoom, Fit, and navigation execute no document command and do not call `MarkDirty`.

## Schema and runtime impact

There are no changes to Machine (schema 3), Face, Panel2D, Cabinet, Reel, runtime package, build traversal, or Oasis Player. Graph state is not persisted. Runtime remains the current `MachineEmulationRuntime` summarized by platform and input count; no `RuntimeDefinition` or Phase 7 implementation was started.

## Automated coverage

`MachineCompositionGraphTests` covers the basic Machine/Runtime/Cabinet/two-Face/two-Reel graph, target labels, shared Panel2D deduplication and provenance-only edges, Project and Library scope, missing Cabinet/Panel2D/Reel assignment diagnostics, unrelated corrupt assets, deterministic node/route/port/lane layout, two- and three-input port separation, multiple-output port distribution, distinct Cabinet and provenance trunks, rejection of non-trivial coincident segments, composition-before-provenance ordering, bounded long-label metadata, non-overlapping node rectangles, routes avoiding unrelated node interiors, exact Reel-role preservation through the aggregated `Reels 0, 1, 2` / `Reel 3` presentation, and construction without Machine mutation. Focused XAML tests assert that the Machine tabs, diagnostics, and graph label badge use shared Oasis styles and semantic resources rather than literal colours. Existing workspace navigation tests cover canonical already-open tab activation; the graph delegates to that seam rather than duplicating it.

Per repository instructions, the Windows/.NET/WPF toolchain is unavailable in the container and tests were not executed here.

## Manual verification checklist

1. Open an existing valid fruit-machine Machine and confirm Overview is the default tab.
2. Verify Machine, Runtime, Cabinet, Top/Bottom Faces, and unique Reel asset cards are visible.
3. Verify a shared MFME/Panel2D source appears once and dashed `source` edges reach both Faces.
4. Verify Reel 0/1/2 edges reach Standard Reel and Reel 3 reaches Small Reel.
5. Confirm Project/Library labels and Reel dimensions are correct.
6. Confirm Face cards are legible (Phase 6 uses metadata cards; thumbnail rendering is deferred).
7. Pan with empty-background left drag and middle drag.
8. Zoom in/out around the cursor with the wheel.
9. Use Fit to Content and 100%.
10. Switch to Details and back without loss of edits or dirty-state changes.
11. Change Cabinet in Details and confirm Overview updates.
12. Change a Face assignment and confirm Overview/provenance/Reel topology updates.
13. Change a Reel assignment and confirm the physical card/edges update.
14. Change runtime platform and confirm Runtime/Machine summaries update.
15. Double-click Cabinet, Face, Reel, and Panel2D cards; verify each authoritative asset opens.
16. Repeat for an already-open asset and confirm its existing tab activates without duplication.
17. Remove a referenced Library Reel externally; confirm Missing and diagnostic, then restore and confirm recovery.
18. Remove a referenced Library Cabinet; confirm Missing and diagnostic, then restore and confirm recovery.
19. Temporarily remove or corrupt a project Face; confirm graph remains usable and diagnoses it.
20. Add an unrelated malformed project/Library asset and confirm the graph is unaffected.
21. Select nodes, pan, zoom, Fit, and navigate; confirm the Machine never becomes dirty.
22. Build/Preview the Machine after graph use and confirm existing Oasis Player behavior is unchanged.

## Deliberately deferred

Arbitrary graph rewiring, connector dragging, drop-to-assign, edge deletion, node dragging/persistence, Inspector property editing, a generic Device graph framework, Cabinet off-screen rendering, a new thumbnail pipeline, 3D Buttons, generalized Surface, RuntimeDefinition, Installation, video machines, Library mutation, and runtime/Player graph data are deferred. Editing remains in Machine Details or the opened authoritative asset.

## Architecture deviations caused by repository facts

There is no authored Cabinet target catalog independent of the Cabinet GLB detector, and there is no reusable lightweight Face/Panel2D/Cabinet thumbnail service. Phase 6 therefore keeps target IDs on edges and uses compact metadata cards. It does not duplicate GLB parsing or any existing renderer. These are presentation limitations only and do not weaken the derived-graph ownership rule.
