# Physical Hosts, Mounts and Devices

## Core rule

**Placement belongs to the physical host. Physical implementation belongs to a typed reusable Device asset. Machine resolves the final composition.**

This rule exists because real arcade hardware does not place every device at Cabinet level.

## Hosts

A host is an asset/assembly whose geometry physically contains or positions another object.

Current important hosts:

- Cabinet;
- Face/Surface.

Future hosts/assemblies may be added when required.

## Examples

### Face-hosted reel

Different games using the same cabinet can place reels in different locations and combinations.

Therefore Face owns:

- reel position;
- reel aperture/window;
- logical machine reference such as Reel:0;
- orientation/visual placement;
- requested physical profile/type where required.

Reusable Reel asset owns:

- physical diameter;
- width;
- 3D mechanism;
- pivot/axis;
- reusable lighting/mechanical properties.

### Cabinet-hosted button

Cabinet owns:

- button mount transform;
- opening/mount geometry;
- compatible/default button profile.

Button asset owns the reusable button model/physical behavior.

Machine maps game input semantics to the mount/device as needed.

### Face-hosted button

If the button passes through a game-specific glass, Face owns the mount/aperture.

The reusable Button asset remains separate.

### Coin mechanism / note acceptor

Typically Cabinet owns the mount/aperture.

CoinMech or NoteAcceptor asset owns reusable physical implementation.

## Typed mounts

Do not prematurely create a giant generic mount schema.

Prefer clear typed concepts where requirements differ:

- FaceReelMount;
- FaceButtonMount;
- CabinetButtonMount;
- CabinetCoinMechMount;
- CabinetNoteAcceptorMount.

Small shared primitives for transforms/profile IDs may be extracted when duplication becomes real.

## Device profiles

A host may need to express a compatibility/requested profile without embedding full physical dimensions.

Example:

```text
Face reel mount
  logicalReference = Reel:3
  requestedProfile = small-reel
```

Cabinet family knowledge may map:

```text
small-reel -> JPM Small Reel asset
```

This allows a Face to encode that its artwork/aperture expects a small reel without coupling it to literal dimensions or a particular Machine instance.

Exact profile schema should be proven with Reel assets before generalization.

## Logical machine objects

Logical references identify runtime semantics, not physical implementation.

Examples:

- Reel:0;
- Lamp:27;
- input/button ID;
- display ID.

A Face may retain logical references because they describe what the visual/physical element represents.

Do not use logical references as substitutes for physical asset identity.

## Simulation entities are not necessarily devices

Physics-heavy games may contain entities such as:

- coins;
- pool balls;
- pucks;
- pusher-bed contents.

Do not force these into MachineObjectReference/device infrastructure.

The runtime may own dynamic simulation entities directly.
