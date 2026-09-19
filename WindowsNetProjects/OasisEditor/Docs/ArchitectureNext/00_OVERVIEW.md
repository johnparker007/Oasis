# Oasis Architecture Next — Overview

## Purpose

This document set defines the target architecture for the next major Oasis Editor refactor. It is intentionally broader than the current fruit-machine-only workflow because the Editor and Oasis Player are intended to support:

- emulated fruit machines;
- video arcade machines such as JAMMA cabinets;
- linked multi-cabinet installations;
- physical arcade games driven by scripted logic;
- physics-heavy arcade games such as coin pushers and pool tables;
- reusable physical assets such as cabinets, reels, buttons, coin mechanisms, displays and other devices.

Pinball is explicitly out of scope for this architecture pass because it is likely to use a specialized external/editor workflow.

These documents describe target ownership and composition rules. They do not preserve compatibility with old Oasis project or runtime formats.

## Repository policy

Oasis is an early personal project. There are no external users, no released runtime package format and no stable public schema.

When serialized formats change:

- update Editor writer and Player reader together;
- increment the current schema/version;
- support only the latest format;
- update tests/fixtures to the latest format;
- allow old projects/generated builds to stop loading;
- regenerate/rebuild old data using the current Editor;
- delete obsolete code rather than preserving it.

Do not add migration layers, legacy DTOs, fallback parsers, dual readers/writers, compatibility branches or optional fields whose only purpose is obsolete data support.

## Core architectural terms

### Project

A Project is an authoring workspace/container.

It owns workspace-level concerns such as project metadata, asset locations and generated output. It is not the machine itself.

A Project may contain one or more Machine assets and, later, one or more Installation assets.

### Machine

A Machine is one independently playable/runtime unit.

Examples:

- one fruit machine;
- one Pac-Man cabinet;
- one linked racing cabinet;
- one slave cabinet in a linked fruit-machine installation;
- one coin pusher;
- one whack-a-mole game;
- one pool table.

A Machine does not imply ROM emulation. Its runtime can be emulated, scripted, physics-driven or hybrid.

### Installation

An Installation is an optional composition above Machine.

It is used only when several independently running Machines and/or shared physical assemblies are intended to form one linked physical setup.

Examples:

- four linked fruit-machine slaves plus a shared topper;
- two to four linked driving cabinets;
- a multi-unit attraction with a shared jackpot display.

Standalone Machines do not require an Installation.

### Cabinet / physical host

A Cabinet is a reusable physical structure, not a particular game.

It owns facts that remain true when different games are installed in that cabinet family/model, such as:

- base 3D model;
- named surface/Face targets;
- default surface orientation;
- reflection receiver geometry;
- material roles;
- cabinet-hosted mounting points;
- default/compatible device profiles where appropriate.

### Face / Surface

A Face is the game-specific assembly associated with a cabinet surface. Current fruit-machine Faces contain artwork plus semantic/physical layout such as reel windows, lamp windows, displays and potentially glass-mounted controls.

The architecture should leave room to generalize this into a broader Surface concept for video-game bezels, marquees, control-panel artwork and similar surfaces.

### Device asset

A Device asset is a reusable typed physical implementation.

Examples:

- Reel;
- Button;
- CoinMech;
- NoteAcceptor;
- Joystick;
- Screen;
- Speaker;
- Dice;
- Wheel.

Do not introduce a giant universal device schema. Prefer strongly typed assets with only small shared infrastructure.

### Runtime definition

A RuntimeDefinition describes how the Machine behaves.

Examples:

- emulation backend;
- scripted high-level game logic;
- physics simulation;
- hybrid physics + scripted/emulated logic.

Emulation is one backend, not the architectural center of Oasis.

## Non-negotiable ownership invariants

1. Project is a workspace, not a machine.
2. Machine is one independently playable/runtime unit.
3. Installation composes Machine instances/shared assemblies; it is optional.
4. Face/Surface never knows which Machine or Cabinet instance consumes it.
5. Reusable Device assets never know which Machine consumes them.
6. Placement belongs to the physical host that contains the mount/aperture.
7. Physical implementation belongs to a typed reusable Device asset.
8. Machine owns final game-specific composition.
9. Installation owns relationships between Machine instances.
10. Build traversal follows explicit references from its root. Do not scan the project to infer ownership.
11. Do not duplicate authoritative relationships in both directions.
12. Provenance is not the same as composition.
13. A graph/overview view is derived from authoritative assets; it is not another source of truth.
14. Do not add backwards compatibility.

## Placement rule

The host that physically contains a device placement owns that placement.

Examples:

- reel windows/positions on a game-specific glass belong to Face;
- a button mounted through a game-specific glass belongs to Face;
- a cabinet-mounted button position belongs to Cabinet;
- a coin-mech mounting position belongs to Cabinet;
- a note-acceptor mounting position belongs to Cabinet.

The physical Reel/Button/CoinMech implementation is a separate reusable asset.

## Composition rule

Machine resolves hosts, surfaces, devices and runtime into one playable unit.

A future Machine might conceptually look like:

```text
Machine: Bonanza
  Runtime
    Platform / ROM configuration

  Cabinet
    JPM Vogue

  Surface assignments
    TopGlass    -> Bonanza Top.face
    BottomGlass -> Bonanza Bottom.face

  Device resolution
    Face Reel:0 profile standard-reel -> JPM Standard Reel
    Face Reel:3 profile small-reel    -> JPM Small Reel

  Appearance overrides
    SideTrim -> red sparkle
```

## Current-state warning

The current repository is transitional.

At the time this architecture was written:

- Cabinet owns explicit Face assignments and logical-reel -> Cabinet reel-spec assignments;
- project metadata still owns fruit-machine platform/ROM settings;
- a dormant `.machine` document type exists but is not yet the true composition root;
- Face retains Panel2D provenance and logical machine-object references;
- the runtime build is currently rooted in Cabinet.

Those current choices are stepping stones. Later phases intentionally move game-specific composition from Cabinet/Project into Machine and replace embedded reel specifications with reusable Reel assets.

## Implementation strategy

Do not implement this architecture in one giant PR.

The master plan defines phases. Each implementation task must:

- read this overview;
- read only the relevant specialist documents;
- inspect the current repository before changing code;
- implement only its assigned phase;
- avoid speculative implementation of later-phase concepts.

The target architecture is broad; each phase should remain narrow and testable.
