# Asset and Ownership Model

## Goal

Define what is a reusable asset, what belongs to Machine composition, and what remains project/workspace metadata.

## Main asset categories

### Reusable physical assets

These describe reusable real-world objects or structures.

Initial/likely types:

- Cabinet;
- Reel;
- Button;
- CoinMech;
- NoteAcceptor;
- Joystick;
- Screen;
- Speaker;
- Dice;
- Wheel.

Additional types should be introduced only when a real workflow requires them.

### Game-specific surface assets

Current Face assets are game-specific surface assemblies.

They may contain:

- artwork;
- source/provenance metadata;
- lamp windows;
- reel windows and reel positions;
- displays;
- game-specific apertures;
- glass-mounted buttons/devices;
- logical machine-object references.

A future generalized Surface abstraction may cover:

- fruit-machine glass Faces;
- arcade control panels;
- bezels;
- marquees;
- side-art surfaces.

Do not force that generalization until real video-game workflows require it.

### Composition assets

- Machine;
- Installation.

Composition assets reference reusable/game-specific assets. Referenced assets must not point back at the composition that consumes them.

## Asset identity and references

Use explicit stable asset references/paths following existing Oasis project asset conventions.

Do not infer ownership by scanning directories.

Examples:

```text
Machine -> Cabinet asset
Machine -> Face/Surface asset
Machine -> Device asset
Installation -> Machine asset
```

A build should be the transitive closure of those explicit references.

## One-way dependencies

Preferred direction:

```text
Project
  -> Machine
       -> Cabinet
       -> Faces/Surfaces
            -> Panel2D provenance
       -> Devices
       -> RuntimeDefinition

Installation
  -> Machine instances
  -> shared assemblies
```

Avoid:

```text
Face -> Cabinet
Device -> Machine
Cabinet -> current game
Machine A -> Machine B
```

For linked machines, Installation owns links between instances.

## Reuse test

When deciding whether state belongs on a reusable asset or Machine, ask:

> If a different game is installed in this same physical object, should this fact normally remain unchanged?

If yes, it probably belongs on the reusable asset.

Examples:

Cabinet:
- GLB;
- face-target geometry;
- reflection receiver geometry;
- cabinet-hosted button mounts.

Machine:
- selected Cabinet;
- selected Faces;
- selected ROM set;
- game-specific cabinet appearance overrides.

Face:
- reel placement on this game's glass;
- reel window/aperture;
- artwork;
- logical Reel:N link.

Reel:
- physical diameter;
- width;
- 3D mechanism;
- pivot/axis;
- reusable lighting/mechanical properties.

## Asset granularity

Prefer one asset per reusable physical type when the item has independent identity/reuse.

Example:

```text
Assets/Reels/JPM Standard Reel/asset.reel
Assets/Reels/JPM Small Reel/asset.reel
```

rather than one monolithic asset containing every JPM reel definition.

Do not split trivial internal geometry into assets merely for purity. Standalone assets should earn their existence through reuse, identity, independent editing or runtime behavior.

## No inheritance framework yet

Do not introduce a generic asset inheritance/variant hierarchy in the first refactor.

For game-specific variation, prefer Machine-owned overrides over reusable base assets.

Example:

```text
Machine CabinetAppearanceOverrides
  SideTrim -> red sparkle
```

Only introduce reusable Cabinet variants later if repeated real data shows that full physical variants are needed.
