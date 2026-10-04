# Physical Hosts, Mounts, Devices and General Objects

## Core host rule

Placement for a mounted device belongs to the physical host. Physical implementation belongs to the reusable typed asset. Machine resolves the final composition.

Current important hosts include:

- Cabinet;
- Face.

## Typed mounted devices

Examples:

### Face-hosted reel

Face owns:

- reel placement/aperture;
- logical Reel reference;
- visual orientation.

Reel asset owns intrinsic reusable physical properties.

### Cabinet-hosted control/device

Cabinet owns the mount/opening/placement.

The typed reusable device owns its physical implementation.

Do not prematurely create one universal mount/device schema.

## Logical machine references

Logical references identify runtime semantics, not physical asset identity.

Current examples include:

- `lamp:17`;
- `reel:2`;
- `input:start`;
- display references.

The active Object3D architecture extends this with:

- `object:<id>`.

When Cabinet trigger behaviour is exposed to scripting, use a stable typed trigger identity rather than raw Unity object paths.

## General simulation objects are not typed devices

Physics-heavy or scripted games contain reusable objects such as:

- pool balls;
- pucks;
- moles;
- prize balls.

These should not be forced into Reel/Button/etc. device schemas.

Use reusable Object3D assets plus Machine-owned instances.

This is distinct from saying dynamic entities are anonymous runtime-only objects: authored gameplay objects need stable Machine identity so Editor composition, Player registries, triggers and scripting can refer to them consistently.

## Runtime-only ephemeral entities

A future runtime may also create truly ephemeral entities that are not authored Machine instances.

Do not require every transient simulation particle/object to become an authored Object3D instance or MachineObjectReference.

The distinction is:

- authored, designer-addressable object -> Object3D + Machine instance;
- ephemeral runtime-only entity -> runtime-owned state.
