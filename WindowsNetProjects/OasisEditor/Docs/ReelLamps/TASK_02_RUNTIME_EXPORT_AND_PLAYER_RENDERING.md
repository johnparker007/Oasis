# Task 02 - Reel Lamp Runtime Export and Player Rendering

## Status

Blocked until the importer/data-model work described in `REEL_LAMP_CONTEXT.md` is complete and merged.

This task covers the next end-to-end slice:

- Editor preview rendering
- runtime package export
- Unity Oasis Player loading
- ROM-controlled reel-lamp rendering

Do not reopen or redesign the MFME source decoding unless the completed importer work exposes a concrete defect.

## Read first

- `Docs/ReelLamps/REEL_LAMP_CONTEXT.md`
- the completed reel-lamp importer/data-model implementation
- the current reel rendering implementation in Oasis Editor
- the current reel runtime export format and writer
- the current Unity Oasis Player reel loader and renderer
- the existing standard lamp state/event plumbing in both Editor and Player
- the existing glass-art shader/material path for scene-lighting integration patterns

## Goal

A machine imported from MFME and exported by Oasis Editor must display three independently ROM-controlled lamps behind each supported reel in both:

1. the Oasis Editor preview
2. the Unity Oasis Player

The visual result must preserve the key physical behaviour:

- top, middle, and bottom lamp positions are fixed within the reel assembly
- the reel band rotates independently beneath/through those fixed light fields
- each lamp is driven by its assigned machine lamp number
- `Opaque` reels use the Editor-generated transmission mask
- non-opaque reels show the unmasked circular light and falloff
- scene lights and ambient lighting continue to affect the reel surface

## Architectural constraints

### Reel ownership

Reel lamps remain owned by the Reel model. Do not convert them into hidden or generated standalone Lamp components.

Reuse the existing shared machine lamp-state pipeline. Do not introduce a duplicate reel-specific ROM state model.

### Current schema only

There are no compatibility requirements.

If the runtime package shape changes:

- change the current format directly
- increment the current schema version
- update the Editor writer and Player reader together
- update fixtures and tests
- delete superseded fields/readers
- support only the latest format

Do not add migration code, legacy readers, fallback parsing, dual formats, or compatibility-only optional fields.

### Mask ownership

The Editor-generated reel transmission mask is authoritative.

The Unity Player must load and use the exported mask. It must not independently derive a mask from the reel-band texture.

## Work package A - Confirm the completed source model

Before making rendering changes, inspect the merged Phase 1 implementation and document the actual current fields/types used for:

- the three reel-lamp slots
- lamp-number assignment
- slot position
- radius/spread
- intensity
- `Opaque` or mask mode
- generated mask asset reference

Use those current types directly where practical. Make only small cleanup changes if required for rendering/export.

Do not add speculative abstraction for arbitrary internal lights or future reel types.

## Work package B - Editor reel preview

Extend the existing Editor reel rendering path so reel lamps are visible during preview and emulation.

### State input

For each of the three reel-lamp slots:

- no lamp number means off
- lamp brightness zero means off
- non-zero lamp brightness contributes illumination
- preserve intermediate brightness values where supported by the existing lamp state model

Use the same machine lamp-state source used by ordinary Lamp components.

### Coordinate behaviour

Implement two distinct coordinate spaces:

- **band UV space**, which changes with reel rotation
- **window/reel-local UV space**, which remains fixed for lamp positions

The lamp centres must not rotate or scroll with the band.

A practical rendering sequence is:

1. calculate the rotating band sample coordinate
2. sample the reel band
3. evaluate three radial lamp fields using fixed window UV coordinates
4. optionally multiply each lamp field by the transmission mask sampled using the rotating band coordinate, so the mask follows the printed symbols on the moving band
5. combine lamp contribution with the normally lit reel surface

The exact implementation may differ according to the current renderer, but these coordinate semantics must be preserved.

### Mask behaviour

For an opaque reel:

- sample the generated transmission mask in the same rotating coordinate space as the reel-band artwork
- restrict internal lamp contribution using that mask
- do not incorrectly mask the normal unlit reel artwork or scene lighting

For a non-opaque reel:

- do not bind or sample a mask unnecessarily
- allow the complete soft circular field and falloff to remain visible

### Visual defaults

Use the imported/model defaults for lamp centres, spread, and intensity.

The three default centres should correspond to the centres of the top, middle, and bottom visible symbol positions.

Avoid hard-coding assumptions in multiple renderers. Keep default geometry in shared model/import logic or one clearly defined rendering-default location.

### Editor diagnostics

Where consistent with existing Inspector patterns, expose read-only or editable reel-lamp information:

- top/middle/bottom lamp number
- mask mode or opaque state
- mask asset presence
- optional spread/intensity controls if those properties are already part of the current model

Do not let Inspector work expand into a general reel redesign.

## Work package C - Runtime package export

Extend the current machine/runtime build export so each reel includes all information required by the Player.

At minimum export, using the final current model names:

- three reel-lamp slot records or an equivalent fixed three-slot structure
- lamp number for each slot
- fixed local centre/position for each slot
- spread/radius
- intensity multiplier
- mask mode
- optional mask texture asset reference

### Asset packaging

When a reel uses an opaque transmission mask:

- include the generated mask texture in the runtime package
- use the repository's current asset/package path conventions
- ensure references are deterministic and relative/current-format appropriate
- deduplicate only through existing asset packaging infrastructure; do not create a separate mask package system

When a reel is non-opaque:

- omit the mask reference
- do not export a redundant generated mask

### Validation

The exporter should fail clearly for invalid current-format data that cannot render correctly, such as:

- opaque/masked mode with a missing required mask asset
- mask reference that cannot be resolved or packaged
- invalid lamp rendering values if the model enforces ranges

Do not silently reinterpret broken current-format assets as non-opaque for compatibility.

## Work package D - Unity Oasis Player loading

Update the Player runtime model and loader together with the Editor writer.

On machine load:

- resolve each reel's three lamp assignments
- load its optional transmission mask
- configure material/shader properties for fixed lamp geometry
- subscribe/bind to the normal shared lamp-state mechanism

The reel runtime component should update lamp brightness efficiently as emulator lamp state changes.

Prefer a material property block or the project's equivalent per-instance property mechanism so reels do not require unique cloned materials solely for brightness updates.

Do not create Unity `Light` objects for each reel lamp. These are local transmitted/emissive effects inside the reel shader, not scene lights.

## Work package E - Unity reel shader/material

Implement or extend a dedicated reel shader that supports:

- normal scene and ambient lighting, consistent with the project's current rendering pipeline
- rotating reel-band sampling
- three fixed soft radial lamp fields
- independent brightness for each field
- optional transmission mask
- additive/emissive internal illumination without destroying the underlying reel artwork

### Required coordinate semantics

- band texture and transmission mask rotate/scroll with reel position
- lamp centres stay fixed in visible-window space
- lamp falloff stays fixed while symbols move through it

### Suggested field evaluation

For each lamp slot, derive a smooth radial field from the fixed window UV coordinate:

```text
field = smooth radial falloff(distance(windowUv, lampCentre), radius)
contribution = field * lampBrightness * lampIntensity
```

For opaque reels:

```text
contribution *= transmissionMask(rotatingBandUv)
```

Combine the sum of the three contributions with the scene-lit reel sample.

The exact curve is implementation-dependent, but avoid a hard-edged circle. The result should resemble illumination from a lamp mounted behind the reel.

### Brightness and colour

Initially use the existing standard warm/neutral lamp colour convention if the project has one, otherwise use a single configurable default reel-lamp colour.

Do not add per-reel-lamp colour serialization unless the completed source model already contains it or a real current requirement is found.

Ensure brightness zero has no emissive contribution and brightness transitions do not require material recreation.

### Built-in Render Pipeline

Implement this for the Unity rendering pipeline currently used by Oasis Player. Do not introduce URP/HDRP variants or Shader Graph solely for this task unless the Player has already migrated.

## Work package F - Behaviour during reel movement

Verify all of the following states:

- stopped reel, all lamps off
- stopped reel, each lamp individually on
- stopped reel, multiple lamps on
- spinning reel, lamps off
- spinning reel, one or more lamps on
- lamp state changing while reel spins
- reel stopping while lamps remain on

The light fields must stay fixed while the band and mask move through them.

Avoid special-case logic that disables lamps while spinning unless existing machine behaviour explicitly requires it.

## Work package G - Tests

Add tests at the most appropriate existing layers.

### Editor/domain/export tests

Cover:

- reel-lamp data is included in current runtime serialization
- all three lamp numbers round-trip through the current writer/reader pair where such tests exist
- intermediate rendering parameters are preserved
- opaque reel exports the transmission mask reference and packages the file
- non-opaque reel omits the mask reference/file
- invalid opaque reel with missing mask fails clearly
- schema version is incremented when the serialized shape changes
- obsolete runtime shape is removed rather than retained

### Player loader tests

Cover:

- three lamp slots load correctly
- missing assignment remains off
- optional mask loading behaviour
- current schema only
- bad mask references produce a useful error

### Rendering tests

Use the project's existing rendering-test approach if available.

At minimum add deterministic tests or a small test scene/fixture demonstrating:

- lamp fields do not move with reel rotation
- mask does move with reel rotation
- opaque mode restricts lamp contribution
- non-opaque mode leaves radial falloff visible
- lamp state values update the material properties

Avoid brittle screenshot tests unless the repository already uses them successfully.

## Manual acceptance checklist

Use a representative imported MFME machine with reel-lamp assignments.

### Editor

- Start emulation and confirm normal artwork lamps still function.
- Confirm each reel's top, middle, and bottom lamp responds to its ROM lamp number.
- Confirm lamp positions remain fixed while reels spin.
- Confirm opaque reel symbols transmit light while the blank reel background substantially blocks it.
- Confirm non-opaque reels show the unmasked lamp glow/falloff.
- Confirm stopping position does not reset or move lamp centres.

### Export and Player

- Build the machine using the current Editor.
- Load it in the current Unity Player.
- Confirm the same lamp assignments and visual behaviour as the Editor.
- Confirm mask assets resolve from the runtime package.
- Confirm no compatibility/fallback path is needed for older builds; regenerate the test build using the current Editor.

### Performance

- Confirm lamp brightness updates do not allocate per frame under normal operation.
- Confirm materials are not cloned every lamp update.
- Confirm the shader uses one reel draw path rather than extra scene lights or overlay GameObjects per lamp.

## Completion criteria

This task is complete when:

- Editor preview displays all three ROM-controlled reel lamps
- runtime export contains the latest reel-lamp and mask data
- Unity Player loads and renders that data
- fixed-light/moving-band coordinate behaviour is correct
- opaque and non-opaque modes match the intended MFME behaviour
- Editor and Player use the same shared lamp-state semantics
- schema, fixtures, and tests represent only the latest supported format
- obsolete format code introduced or superseded by this change has been removed

## Out of scope

- more than three internal reel lamps
- physical Unity scene lights inside the cabinet
- volumetric light or internal shadows
- bloom/post-processing redesign
- hand-painted mask editor
- support for obsolete machine builds
- general reel asset or Inspector redesign
- colour data not present in the current source/model

## Suggested Codex kickoff prompt

Read:

- `Docs/ReelLamps/REEL_LAMP_CONTEXT.md`
- `Docs/ReelLamps/TASK_02_RUNTIME_EXPORT_AND_PLAYER_RENDERING.md`

First inspect the merged Phase 1 reel-lamp importer/data-model implementation and the current Editor and Unity reel rendering/export paths.

Then implement this task end-to-end in small coherent commits or clearly separated change groups:

1. Editor preview rendering
2. current runtime export/schema update
3. Unity Player loader/state binding
4. Unity reel shader/material support
5. automated tests and representative fixture updates

Preserve the key coordinate rule: the band and transmission mask rotate, while the top/middle/bottom lamp fields remain fixed in reel-window space.

Use the existing shared machine lamp-state pipeline. Do not generate standalone Lamp components or Unity scene lights. Support only the latest schema and remove superseded format code.