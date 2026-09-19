# Cabinets, Surfaces and Faces

## Cabinet role

Cabinet is a reusable physical asset.

A designer should be able to author JPM Vogue once and reuse it for many games.

Cabinet should eventually own only reusable facts, such as:

- source/model GLB;
- stable named surface targets;
- target geometry/orientation defaults;
- reflection receiver geometry/settings defaults;
- reusable material roles;
- cabinet-hosted mounts;
- device profile defaults/compatibility where appropriate.

Game-specific Face assignments do not belong on Cabinet in the final architecture.

## Current transitional state

The current Cabinet schema owns:

- FaceAssignments;
- ReelSpecifications;
- ReelAssignments.

This was an improvement over Face -> Cabinet references and remains a useful intermediate state.

However:

- FaceAssignments are specific to a particular game/Machine and should move to Machine.
- ReelAssignments are machine composition and should move/reform under Machine/device resolution.
- ReelSpecifications should eventually be replaced by reusable Reel assets.

Do not interpret the current Cabinet schema as final architecture.

## GLB import and reusable Cabinet authoring

Current repeated workflow—importing the same GLB into every project and reconfiguring orientation, reflection targets and reel sizes—is a sign that Cabinet should be reusable/library-authored.

Target workflow:

1. Author/import `JPM Vogue.cabinet` once.
2. Configure its stable target orientation, reflection geometry, material roles and cabinet-hosted mounts.
3. Save it to a reusable library.
4. New game selects `JPM Vogue` rather than rebuilding it.

## Face role

Current fruit-machine Face should be viewed conceptually as a **game-specific surface assembly**, not merely artwork.

It can legitimately contain:

- artwork;
- Panel2D provenance;
- lamps/windows;
- reel positions and apertures;
- displays;
- glass-mounted controls;
- logical machine-object mappings;
- other game-specific geometry aligned to that surface.

Face must not know which Cabinet/Machine consumes it.

## Surface generalization

Video arcade machines introduce surfaces such as:

- control panel;
- bezel;
- marquee;
- side art.

Do not immediately rename/rewrite Face into a universal Surface in early phases.

Instead:

- keep Face stable for fruit-machine authoring during foundational refactors;
- design Machine/Cabinet composition using terminology broad enough to allow Surface later;
- introduce generalized Surface only when implementing the first real video-machine workflow.

## Material roles and appearance overrides

Cabinet should expose reusable semantic material roles where useful:

- Body;
- SideTrim;
- TopTrim;
- CoinDoor.

Machine may apply game-specific appearance overrides:

- color;
- material;
- sparkle/finish;
- decals where appropriate.

Avoid full inheritance/variant infrastructure initially.
