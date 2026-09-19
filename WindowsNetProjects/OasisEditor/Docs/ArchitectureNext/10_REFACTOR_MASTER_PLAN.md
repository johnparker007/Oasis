# Architecture Next — Master Refactor Plan

## Purpose

This is the phased implementation plan for moving the current Oasis Editor architecture toward the target model in this directory.

Each phase should be a separate Codex task/PR unless a phase is explicitly split further after repository inspection.

Every task must read:

- `00_OVERVIEW.md`;
- the specialist docs relevant to that phase;
- this master plan.

Do not implement later phases speculatively.

## Phase 0 — Current-state architecture audit

### Goal

Produce a repository-grounded audit before architectural code changes.

### Inspect

- EditorProject/project storage;
- platform/ROM settings;
- `.machine` dormant/stub paths;
- Cabinet model/storage/editor;
- current Cabinet FaceAssignments/ReelSpecifications/ReelAssignments;
- Face model/provenance/logical references;
- Panel2D and MFME import;
- runtime build root/manifests;
- Oasis Player readers;
- reflection system;
- asset path/package conventions;
- current document/view opening patterns.

### Deliverable

Create `Docs/ArchitectureNext/CURRENT_STATE_AUDIT.md` with:

- current authoritative owner of each major datum;
- current build dependency flow;
- current schemas/versions;
- current UI ownership;
- obsolete/transitional structures;
- mismatches against target architecture;
- risks/order constraints for later phases.

No production architecture change in this phase.

## Phase 1 — Make Machine a real first-class asset/composition root

### Goal

Introduce a real serialized Machine asset and make one Machine represent one playable unit.

### Move toward Machine

- selected Cabinet reference;
- game-specific Face/Surface assignments;
- fruit-machine platform/runtime configuration currently at Project level;
- ROM configuration currently at Project level;
- other machine-specific build metadata required for current fruit-machine workflow.

### Transitional migration

Move current Cabinet FaceAssignments to Machine.

Current Cabinet ReelAssignments/ReelSpecifications may remain temporarily if Phase 1 does not yet introduce Reel assets, but Machine build ownership must be designed so they can move cleanly later.

### UX

For the common case:

- new project automatically creates/selects one Machine;
- existing workflows should not require extra ceremony;
- Machine document becomes the place to understand one game's composition.

### Build

Change standalone Preview/Build root from Cabinet to Machine.

No project-wide dependency scanning.

### Delete

Remove superseded project/cabinet ownership once the new Machine schema is authoritative. No compatibility reader.

## Phase 2 — Make Cabinet reusable and intrinsic

### Goal

Cabinet contains reusable physical facts only.

### Cabinet should own

- GLB/model;
- named Face/Surface targets;
- orientation/front-side defaults;
- reflection receiver geometry/defaults;
- material roles;
- cabinet-hosted mount definitions when implemented.

### Remove from Cabinet

Anything game-specific that Phase 1 moved to Machine.

### UX

Improve Cabinet authoring as reusable physical asset editing rather than current-game configuration.

Do not introduce the shared library yet unless needed for clean internal references.

## Phase 3 — Introduce Reel assets and remove embedded Cabinet reel specifications

### Goal

Prove reusable typed Device assets using the most mature current physical device: Reel.

### Add

A first-class Reel asset containing current physical reel data:

- diameter;
- width;
- future-ready model/reference fields only where immediately useful;
- other existing physical reel properties that truly belong to the reusable mechanism.

### Face

Face continues to own reel placement/window and logical Reel:N.

Replace any need for literal Cabinet physical dimensions with a profile/type requirement suitable for the selected Cabinet family.

### Cabinet/Machine

Cabinet may supply compatible/default profile -> Reel asset mappings.

Machine resolves final physical Reel assets and owns overrides.

### Delete

- CabinetReelSpecification;
- obsolete ReelSpecificationId-style concepts;
- transitional reel configuration that no longer has a clear owner.

No compatibility support.

## Phase 4 — Formalize hosts, mounts and typed-device composition

### Goal

Extract the small general model proven by Reels.

### Establish

- host owns placement;
- typed mounts;
- typed Device asset references;
- Machine final resolution.

### Convert only real existing systems

Do not implement every possible future Device.

Likely candidates only where current workflows require them.

### Avoid

- universal Device mega-schema;
- generic inheritance framework;
- speculative support for pool/whack-a-mole/etc.

## Phase 5 — Reusable Oasis asset library

### Goal

Allow one authored Cabinet/Reel asset to be reused across projects.

### Initial scope

At minimum:

- Cabinet library;
- Reel library.

### Workflow target

New JPM game:

1. create project/Machine;
2. import MFME;
3. choose existing JPM Vogue Cabinet;
4. generate/assign Faces;
5. automatically resolve normal Reel profiles;
6. preview.

No repeated GLB import and Cabinet reconfiguration.

### Keep simple

Prefer a local library/reference model over package-manager complexity.

## Phase 6 — Machine Composition / Overview view

### Goal

Build the derived zoomable graph after ownership is stable.

### Features

- Machine-rooted graph;
- Cabinet/Faces/Panel2D provenance/Devices/Runtime;
- thumbnails;
- pan/zoom/fit;
- diagnostics;
- double-click opens actual asset;
- no independent graph persistence;
- no arbitrary rewiring initially.

## Phase 7 — Runtime abstraction

### Goal

Make Machine runtime backend explicit rather than assuming emulation.

### First implementation

Represent current fruit-machine emulation as an Emulation runtime definition owned by Machine.

Establish the narrow contract needed to add later:

- Scripted;
- Physics;
- Hybrid.

Do not implement whole new game categories in this phase.

## Phase 8 — Installation assets

### Goal

Support optional linked multi-machine compositions.

### Add

Installation asset with:

- Machine instances;
- instance transforms/layout;
- link topology;
- instance-specific configuration;
- future shared-assembly references.

### Preserve simplicity

Standalone Machine remains independently authorable/buildable.

## Phase 9 — First video-machine vertical slice

### Goal

Exercise the architecture with one concrete video arcade workflow, such as a simple JAMMA-style cabinet.

Use this to decide whether/when current Face should generalize to Surface.

Likely needs:

- monitor/screen;
- control-panel surface;
- joystick/buttons;
- bezel/marquee;
- coin mechanism;
- video runtime backend.

Do not generalize from theory; use this vertical slice to prove/refine the model.

## Phase 10 — First non-emulated physical-game vertical slice

### Goal

Exercise RuntimeDefinition and physical composition without ROM emulation.

Choose one concrete manageable example:

- simple scripted whack-a-mole; or
- simple physics-first pool table/coin pusher prototype.

Avoid pinball.

Use this phase to validate the boundary between logical devices and runtime-owned simulation entities.

## Architecture checkpoints

Do not automatically proceed through all phases.

### Checkpoint A — after Phase 2

Manually create a fruit machine with Machine as root and Cabinet as reusable intrinsic structure.

Question:

> Can I understand everything required to run this fruit machine by opening its Machine document?

If important game configuration is still hidden at Project/Cabinet level, fix it before Phase 3.

### Checkpoint B — after Phase 5

From a fresh project, build a new JPM fruit machine using a previously authored Vogue Cabinet/Reel library.

Question:

> Has repeated Cabinet/reel setup actually disappeared from the normal workflow?

If not, improve the model/library UX before graph/installation work.

### Checkpoint C — after Phase 7

Verify existing fruit-machine emulation still works through RuntimeDefinition and that physical composition has no dependency on emulator-specific ownership.

### Checkpoint D — after Phase 8

Verify a standalone Machine workflow remains no more complicated than before.

## Per-phase implementation requirements

Every implementation phase must include:

- repository inspection before changes;
- schema/version changes documented;
- Editor and Player updated together when runtime shapes change;
- focused automated tests;
- deletion of superseded code;
- manual verification checklist;
- summary of architectural decisions that differed from these docs because of actual repository constraints.

## Explicit exclusions

Until their dedicated phases:

- no generalized online asset marketplace/package registry;
- no arbitrary graph editing;
- no universal asset inheritance system;
- no universal Device schema;
- no pinball architecture;
- no backwards compatibility.
