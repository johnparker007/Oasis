# Codex Kickoff Prompt — Segmented Displays Phase 1

Work in the connected repository:

```text
johnparker007/Oasis
```

Create a new branch and PR from the latest `main`.

## Objective

Implement the first segmented-display runtime phase:

```text
shared segmented-display foundation
+ complete conventional 7-segment export/render/update path
```

Do not implement 16-segment displays in this PR. Build the shared architecture so the follow-up task can add 16-segment topology without duplicating runtime systems.

## Planning documents

Read and follow:

```text
Docs/SegmentDisplays/SEGMENTED_DISPLAYS_CONTEXT.md
Docs/SegmentDisplays/TASK_01_SHARED_FOUNDATION_AND_7_SEGMENT.md
Docs/SegmentDisplays/TASK_02_16_SEGMENT_ALPHA.md
```

Task 02 is architectural context only. Implement Task 01.

## Key rendering decision

Use:

```text
canonical normalized vector segment geometry
→ Unity-generated digit meshes
→ one shared emissive shader/material
→ one MaterialPropertyBlock per digit
→ emulator segment masks
```

Do not use:

- one texture per segment;
- rasterized Editor display textures as the primary representation;
- font rendering;
- a material instance per digit;
- duplicated 7-segment and 16-segment renderer stacks.

## Required investigation first

Before implementation, trace and summarize:

- current Editor 7-segment drawing geometry;
- Face generation and persistence;
- Inspector fields;
- runtime manifest display entries;
- Machine runtime references;
- Player loading and placement;
- emulator/backend display events;
- existing segment bit ordering;
- digit-count source;
- tests and fixtures.

Preserve an established mapping if one exists. Otherwise introduce and centralize the canonical mapping described in the planning docs.

## Implementation requirements

1. Centralize renderer-independent normalized 7-segment geometry.
2. Make Editor preview use that geometry or a generated equivalent.
3. Export explicit current-format display definition data.
4. Update schema versions only where serialized shapes change.
5. Support only the latest schema and remove obsolete parsing code.
6. Generate one mesh per digit topology/options and cache it.
7. Store segment index in shader-readable vertex data.
8. Create one shared emissive segmented-display shader/material.
9. Use `MaterialPropertyBlock` for mask, colors, and brightness.
10. Mount displays through the existing Face-to-Cabinet placement path.
11. Route emulator updates by existing machine display references.
12. Update masks without rebuilding meshes or instantiating materials.
13. Add production-path Editor tests and Unity EditMode tests.
14. Add an end-to-end fixture and document manual verification.

## Scope constraints

Do not:

- add backwards-compatibility infrastructure;
- add fallback readers;
- infer semantic display data from rectangle size when explicit data should exist;
- redesign emulator backend abstractions;
- implement dot matrix;
- implement 16-segment geometry in this PR;
- introduce structured buffers or batching before profiling;
- add arbitrary custom segment polygon authoring.

## Testing and verification

Run all tests permitted by repository `AGENTS.md` guidance.

At minimum cover:

- Editor unit/integration tests;
- Unity EditMode tests where available;
- static checks;
- schema fixture updates;
- production machine build with source documents closed.

Manual verification must exercise every segment A–G, decimal point, multiple digits, ordering/reversal, distinct on/off colors, emissive bloom, repeated state changes, reload/cleanup, and stable mesh/material counts.

## PR description

Include:

- current architecture discovered;
- chosen canonical bit mapping;
- geometry-sharing approach;
- manifest/schema changes;
- shader/property-block design;
- emulator routing;
- tests run;
- manual verification still required;
- explicit note that Task 02 remains a follow-up.
