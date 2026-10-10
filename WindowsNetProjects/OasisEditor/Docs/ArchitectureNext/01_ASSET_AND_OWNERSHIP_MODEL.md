# Asset and Ownership Model

> Delivered Object3D/A8 baseline and existing contracts below remain current. The proposed evolution is in [Prefab/OasisObject architecture](13_PREFABS_OASISOBJECTS_AND_COMPONENTS.md) and [delivery plan](14_PREFAB_AND_POOL_DELIVERY_PLAN.md); none of P1–P8 is implemented. Proposed variants/functions/spawning are later milestones, not current functionality.

## Goal

Define what is reusable, what belongs to Machine composition, and what remains Project/workspace metadata.

## Reusable physical assets

Current/proven reusable physical assets include:

- Cabinet;
- Reel.

The delivered general reusable asset is:

- Object3D.

Likely future typed devices should be introduced only when a real workflow requires them, for example Button, CoinMech, NoteAcceptor, Joystick, Screen or Speaker.

### Object3D versus typed Device

Use a typed Device asset where the object has a specific host/mount/runtime role that deserves its own model.

Use Object3D for reusable general 3D objects instantiated directly by Machine, such as pool balls, moles or pucks.

Do not replace proven typed concepts such as Reel with Object3D merely to make everything generic.

## Game-specific surface assets

Face assets are game-specific surface assemblies.

They may contain:

- artwork;
- source/provenance metadata;
- lamp/reel/display placement;
- controls;
- game-specific apertures;
- logical machine-object references.

Face must not point back at the consuming Machine or Cabinet.

## Composition assets

- Machine;
- Installation.

Machine is the standalone composition root. Installation is an optional higher-level composition root for linked Machines.

## Asset identity and references

Use explicit stable references following current Oasis `AssetReference` conventions.

Do not infer composition by scanning directories.

Examples:

```text
Machine -> Cabinet asset
Machine -> Face asset
Machine -> Reel asset
Machine -> Object3D asset
Installation -> Machine asset
```

A build is the transitive closure of explicit references.

## One-way dependencies

Preferred direction:

```text
Project
  -> Machine
       -> Cabinet
       -> Faces
            -> Panel2D provenance
       -> Reel assets
       -> Object3D assets
       -> RuntimeDefinition

Installation
  -> Machine instances
  -> shared assemblies
```

Avoid reverse consumption references such as:

```text
Face -> Machine
Object3D -> Machine
Cabinet -> current game
```

## Reuse test

When deciding whether data belongs on a reusable asset or Machine, ask:

> If a different game reused this same physical thing, should the fact normally remain unchanged?

Examples:

Cabinet:
- GLB;
- Face target geometry;
- fixed Collider/Trigger geometry;
- reflection receiver geometry.

Machine:
- selected Cabinet;
- selected Faces;
- Object3D instances and transforms;
- game-specific inputs;
- game-specific behaviour;
- runtime configuration.

Object3D:
- reusable model;
- intrinsic collider defaults;
- Rigidbody defaults;
- physical mass/gravity defaults.

Reel:
- physical diameter/width;
- mechanism/model properties.

## Asset granularity

A standalone asset should earn its existence through reuse, independent identity/editing, or runtime behaviour.

Do not split trivial internal geometry into assets merely for conceptual purity.

## No inheritance framework

Do not add generic asset inheritance/variants until repeated real use cases justify it.

Prefer Machine-owned composition and narrowly scoped overrides.
