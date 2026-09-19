# Machine / Project Composition View

## Purpose

Provide a zoomable navigational visualization showing how authoritative assets and provenance fit together.

This is not a second source of truth and not initially a graph editor.

## Derived view

The graph is built in memory by traversing explicit references.

Do not persist graph edges in a separate "pipeline" asset.

The authoritative data remains in Machine, Cabinet, Face/Surface, Device, Runtime and provenance assets.

## Terminology

Prefer names such as:

- Machine Overview;
- Composition View;
- Asset Graph.

Avoid "Pipeline" as the primary term because many edges represent composition/reference rather than transformation.

## Initial root

Once Machine is first-class, Machine should be the primary graph root.

Example:

```text
Machine: Bonanza
  -> Cabinet: JPM Vogue
  -> Face: Top Glass
       -> source Panel2D
  -> Face: Bottom Glass
       -> source Panel2D
  -> Reel assets
  -> Runtime / ROMs
```

Installation view can show Machine instances and allow drill-down into each Machine graph.

## Provenance versus composition

Use visually/semantically distinct edge types.

Examples:

- Machine **uses** Cabinet;
- Machine **mounts** Face on target;
- Face **derived/authored from** Panel2D;
- Panel2D **imported from** MFME source;
- Machine **resolves device as** Reel asset.

MFME/import source should be styled as external provenance, not as an equal project asset unless it later becomes a first-class source asset.

## Node content

Far zoom:
- compact type + name.

Medium zoom:
- thumbnail/preview;
- key health/status summary.

Closer zoom:
- source/reference details;
- counts/diagnostics;
- hosted device summaries where useful.

Do not render every lamp/button/reel as a full graph node by default. That becomes unreadable quickly.

Fine-grained mappings can appear inside a selected node/details panel.

## Interaction — first version

Initial view should be primarily read-only/navigation:

- pan/zoom;
- fit graph;
- frame selection;
- select node;
- double-click/open asset;
- show in Assets;
- upstream/downstream highlighting;
- broken reference diagnostics.

Do not initially support arbitrary drag/drop rewiring.

Graph editing has heterogeneous semantics and should be added only after navigation proves useful.

## Broken references

The graph should make dependency failures visible:

- missing Face;
- missing Cabinet;
- missing Device;
- broken provenance;
- unresolved profile;
- invalid runtime dependency.

This view should help explain build failures, not just decorate the project.
