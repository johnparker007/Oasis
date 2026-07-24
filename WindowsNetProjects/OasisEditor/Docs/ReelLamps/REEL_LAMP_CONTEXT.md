# Reel Lamp Implementation Context

## Purpose

Implement MFME reel lamps end-to-end in Oasis Editor and Oasis Player.

A standard mechanical reel commonly has three independently controlled lamps fixed behind the visible reel window:

- top
- middle
- bottom

Each lamp is driven by a normal machine lamp number. The emulator updates the shared machine lamp state, and the reel rendering consumes those states.

## Source behaviour

In MFME FML, reel-lamp configuration is stored on the Reel component rather than as standalone Lamp components.

The importer must decode the three reel-lamp assignments and the MFME reel `Opaque` setting from the Reel data.

Exact source property names and sentinel values must be confirmed from the current FML decoder and representative fixtures before implementation. Do not guess undocumented field meanings.

## Rendering model

The reel assembly contains two conceptually separate layers:

1. A rotating reel band.
2. Three fixed light emitters behind the reel window.

The lamp positions do not rotate with the reel band. Only the reel-band sampling position changes as the reel spins.

Each light should produce a soft, approximately circular illumination with configurable falloff, centred on the top, middle, or bottom visible symbol position.

The final reel appearance must combine:

- the reel-band artwork and its current rotational offset
- scene lighting and ambient light
- the three ROM-controlled reel-lamp contributions
- an optional transmission mask derived from the reel artwork

## Opaque behaviour

MFME's `Opaque` flag controls how reel-lamp light is masked.

When `Opaque` is false:

- do not generate or apply a reel-lamp transmission mask
- the full circular lamp illumination and falloff may be visible across the reel band

When `Opaque` is true:

- derive a transmission mask from the blank background around the reel-band artwork
- the usually uniform light-grey or white background is treated as non-transmissive
- coloured/darker symbol artwork remains transmissive so the lamps shine primarily through symbols

The derived mask should be generated deterministically by the Editor during import or asset processing, stored as an authored/generated asset according to the repository's current asset-package conventions, and exported into the current runtime package.

Do not make the Unity Player independently re-derive the mask.

## Data ownership

Reel lamps are semantically part of a reel, not free-standing 2D lamp components.

The preferred current model is therefore reel-owned lamp configuration containing three slots. Each slot should at minimum identify:

- position: top, middle, or bottom
- optional machine lamp number
- local vertical centre
- radius or spread
- intensity multiplier

Use sensible defaults for geometry/rendering values so imported MFME layouts work without manual adjustment. Expose tuning in the Editor Inspector where practical.

Do not create hidden standalone Lamp components solely to reuse existing lamp rendering. Reuse the machine lamp-state plumbing, not the unrelated component representation.

## Shared machine-state integration

Reel lamps use the same lamp-number namespace and emulator-driven state as ordinary lamps.

The Editor preview and Player runtime should read reel-lamp brightness from the existing shared lamp-state service/model. Avoid a second reel-specific event pipeline unless the current architecture makes an adapter necessary.

Expected behaviour:

- missing lamp assignment: slot remains off
- lamp value zero: no reel-lamp contribution
- non-zero lamp value: brightness drives that slot's contribution
- intermediate brightness values should be preserved if the existing lamp model supports them

## Current-format policy

Oasis is an early personal project with no compatibility requirement.

If the serialized reel asset, machine build manifest, or runtime package shape changes:

- update the Editor writer and Player reader together
- increment the current schema version
- support only the latest version
- update fixtures and tests
- delete superseded format code

Do not add migrations, legacy DTOs, fallback readers, optional compatibility fields, or dual formats.

## Suggested target architecture

### Editor/domain

A reel owns three `ReelLamp` or equivalent slot records plus a mask mode/asset reference.

### Import

The MFME Reel importer maps source lamp numbers and `Opaque` into the current reel model, then invokes deterministic mask generation when required.

### Editor preview

The reel renderer composites fixed lamp fields in reel-local/window space with the rotating band sample. It obtains brightness from the existing machine lamp state.

### Runtime export

The machine build writes the three lamp assignments, rendering parameters, mask mode, and optional mask texture reference into the latest runtime format.

### Unity Player

A reel material/shader samples the rotating band and optional mask, evaluates three fixed lamp fields in window UV space, applies brightness supplied by the reel runtime component, and combines the result with ambient/scene lighting.

## Initial non-goals

- arbitrary numbers of reel lamps beyond the three MFME positions
- physically accurate volumetric lighting inside the reel cabinet
- shadows cast by internal reel-lamp geometry
- compatibility with obsolete generated runtime packages
- manual mask painting tools
- redesign of the general machine lamp-state system

Keep the implementation small and extensible without introducing speculative abstractions.