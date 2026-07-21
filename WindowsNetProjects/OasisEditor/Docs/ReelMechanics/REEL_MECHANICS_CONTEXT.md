# Cabinet Reel Mechanics Context

## Purpose

Define the current authoring and runtime ownership model for conventional mechanical reel dimensions.

The Player already generates cylindrical reel meshes and places them against resolved cabinet Face surfaces. The remaining problem is that the Editor currently derives physical reel dimensions from the Face element rectangle:

```text
physicalWidth  = element.Width / 1000
physicalRadius = element.Height / 2000
```

That approximation treats the visible Face window as the physical reel mechanism. It is not reliable because a window commonly exposes only part of the reel circumference and may be sized for artwork, masking, or perspective.

## Current architecture

Recent merged work established:

- generated Face semantic reel elements;
- Face runtime reel manifest entries;
- Player-generated cylindrical reel geometry;
- Player placement using the resolved cabinet Face target;
- placement depth based on `physicalRadius` plus surface clearance.

The Player should continue receiving concrete physical dimensions in the runtime package. It should not need to understand Editor cabinet presets.

## Ownership decision

### Cabinet asset

The Cabinet owns a collection of named reel specifications representing the physical mechanisms supported by that cabinet.

Each specification has at least:

```text
id
name
diameterMm
widthMm
```

Example:

```text
jpm-small     JPM Small Reel       80 mm   42 mm
jpm-standard  JPM Standard Reel   210 mm   50 mm
```

The Cabinet also owns a default reel specification ID used when creating or generating Face reel components.

Use stable IDs for references. Display names may be edited without breaking selections.

### Face reel component

`FaceReelDisplayElement` is the authoritative place to select a cabinet reel specification.

The Face reel already represents the physical presentation of a semantic reel on a particular cabinet Face. It should store:

```text
reelSpecificationId
```

The Panel2D reel remains responsible for source layout and machine semantics such as machine binding, band asset, stops, reversal, and band offset. Do not couple reusable Panel2D assets to cabinet-specific physical reel dimensions.

### Runtime package

During runtime export, resolve the Face reel's selected specification from the Cabinet and write concrete values to the current Face runtime reel entry:

```text
physicalWidth  = widthMm / 1000
physicalRadius = diameterMm / 2000
```

The Unity Player continues consuming `physicalWidth` and `physicalRadius` exactly as it does now.

Do not export the Cabinet preset collection or preset ID to the Player unless a later runtime feature requires it.

## Inspector behavior

### Cabinet Inspector

Provide editing for:

- reel specification name;
- diameter in millimetres;
- width in millimetres;
- default reel specification.

Support adding, renaming, editing, and deleting specifications. Keep IDs stable when names change.

Deletion must not silently retarget Face reels. Existing references become unresolved and should produce clear validation.

### Face reel Inspector

When a Face reel is selected and its cabinet context can be resolved, show a `Reel Size` dropdown populated from that Cabinet's reel specifications.

Also show read-only resolved dimensions for clarity:

```text
Diameter
Width
```

When no Cabinet context is available, retain the stored ID and show an unresolved/ unavailable state rather than copying physical values into the Face.

## Generation and regeneration

When generating a Face reel from Panel2D:

1. assign the Cabinet default reel specification when cabinet context is available;
2. otherwise leave the reference unresolved and report that configuration is required;
3. preserve an existing reel specification selection during regeneration;
4. never overwrite a user's explicit Face reel selection with a newly inferred value.

Do not automatically infer reel diameter from the Face rectangle once cabinet specifications exist.

## Validation

Runtime export must fail clearly, or omit the invalid reel with an explicit diagnostic consistent with existing export policy, when:

- the Cabinet has no reel specifications;
- the Face reel has no selected specification;
- the selected ID does not exist;
- diameter or width is zero, negative, non-finite, or otherwise invalid.

Do not silently fall back to the old Face rectangle calculation.

## Schema policy

This project has no backwards-compatibility requirement.

When serialized authored models change:

- update the current Cabinet and Face schemas directly;
- increment current schema versions as appropriate;
- update readers and writers together;
- update fixtures and tests to the latest shape;
- delete superseded estimation code;
- do not add migrations, legacy DTOs, compatibility readers, or fallback parsing.

The Face runtime schema only needs a version bump if its serialized runtime shape changes. Replacing the source of existing `physicalWidth` and `physicalRadius` values does not by itself require a runtime schema change.

## Scope boundary

This work is about conventional generated mechanical reels only.

Do not redesign:

- reel band position semantics;
- Player reel mesh topology;
- Face surface placement math;
- emulator reel updates;
- segment or alpha display placement;
- imported authored 3D reel models.
