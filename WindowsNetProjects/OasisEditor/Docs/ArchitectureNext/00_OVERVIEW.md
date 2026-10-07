# Oasis Architecture Next — Overview

## Purpose

This document set defines the durable target architecture for Oasis Editor and Oasis Player. It intentionally extends beyond fruit-machine emulation to support video arcade machines, linked installations, scripted physical games, and physics-heavy machines such as pool tables and coin pushers.

These documents describe current ownership rules and active target architecture. Historical implementation-phase notes are intentionally removed once merged so future work is not guided by obsolete transitional state.

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

A Machine is one independently playable/runtime unit and the primary standalone composition/build root.

Examples:

- one fruit machine;
- one Pac-Man cabinet;
- one linked racing cabinet;
- one slave cabinet in a linked installation;
- one coin pusher;
- one whack-a-mole game;
- one pool table.

A Machine does not imply ROM emulation.

### Installation

An Installation is an optional composition above Machine, used only when multiple independently running Machines and/or shared assemblies form one linked physical setup.

Standalone Machines do not require an Installation.

### Cabinet

A Cabinet is a reusable fixed physical structure, not a particular game.

It owns reusable facts such as:

- base 3D model;
- `OasisFace_*` surface targets;
- `OasisCollider_*` fixed collision geometry;
- `OasisTrigger_*` fixed trigger geometry;
- target orientation defaults;
- reflection receiver geometry/settings;
- future cabinet-hosted mounts where justified.

### Face / Surface

A Face is the current game-specific surface assembly. It can contain artwork, lamp/reel/display layout, controls and provenance. It does not know which Machine or Cabinet consumes it.

Generalizing Face into a broader Surface concept remains deferred until a real video-machine workflow requires it.

### Device asset

A Device asset is a reusable typed physical implementation such as Reel, Button, CoinMech or Display hardware.

Do not introduce a universal Device mega-schema.

### Object3D asset

Object3D is the reusable physical asset for general 3D objects that are instantiated directly by Machine rather than mounted through a typed host/device relationship.

Examples:

- pool ball;
- mole;
- puck;
- prize ball;
- movable prop.

Object3D owns intrinsic model/physics defaults. Machine owns each instance's identity and placement.

### Runtime definition

A RuntimeDefinition describes how the Machine behaves. Emulation is one runtime implementation rather than the architectural center of Oasis.

Native Oasis behaviour uses `OasisRuntimeDefinition` plus one required Oasis Script
source. Emulation forbids authored behaviour; hybrid support remains deferred.

## Ownership invariants

1. Project is a workspace, not a machine.
2. Machine is one independently playable/runtime unit.
3. Installation composes Machine instances/shared assemblies and is optional.
4. Face never knows which Machine or Cabinet consumes it.
5. Reusable Cabinet/Reel/Object3D assets never know which Machine consumes them.
6. Placement belongs to the owning host or Machine composition.
7. Intrinsic physical implementation belongs to the reusable asset.
8. Machine owns final game-specific composition.
9. Installation owns relationships between Machine instances.
10. Build traversal follows explicit references from its root; do not scan a project to infer ownership.
11. Do not duplicate authoritative relationships in both directions.
12. Provenance is not composition.
13. Derived overview/viewport data is not another source of truth.
14. Do not add backwards compatibility.

## Composition rule

Machine resolves reusable assets and game-specific content into one playable unit.

Current examples include:

- Cabinet selection;
- Face assignments;
- Reel asset resolution;
- Machine inputs;
- Machine-owned runtime configuration.

The active Object3D track extends this with Machine-owned dynamic/general 3D object instances and later behaviour/scripting.

## Implementation strategy

Do not implement future architecture in one giant PR.

For active sequencing, read:

- `10_ACTIVE_ROADMAP.md`;
- the specialist document relevant to the task.

For the current dynamic-object/scripting track, read:

- `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.

Inspect the current repository before each phase and implement only that phase.
