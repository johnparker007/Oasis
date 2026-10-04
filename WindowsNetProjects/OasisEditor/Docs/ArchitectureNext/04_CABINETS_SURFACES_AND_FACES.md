# Cabinets, Surfaces and Faces

## Cabinet role

Cabinet is a reusable fixed physical asset.

It owns intrinsic reusable facts such as:

- source/model GLB;
- `OasisFace_*` surface targets;
- `OasisCollider_*` fixed collision geometry;
- `OasisTrigger_*` fixed trigger geometry;
- target orientation/front-side defaults;
- reflection receiver geometry/settings;
- future cabinet-hosted mounts where a concrete workflow requires them.

Cabinet does not own game-specific Face assignments.

## GLB semantic geometry

Current Cabinet semantic prefixes are:

```text
OasisFace_<name>
OasisCollider_<name>
OasisTrigger_<name>
```

Node semantic identity takes precedence over mesh semantic identity.

`COL_*` and `TRG_*` are not Oasis compatibility aliases.

Machine build validates/preserves the authored Cabinet GLB. Oasis Player turns Collider/Trigger semantic geometry into Unity MeshColliders.

The GLB remains authoritative for that fixed geometry; do not duplicate Collider/Trigger mesh declarations into runtime JSON.

## Face role

Face is a game-specific surface assembly.

It may own:

- artwork;
- Panel2D provenance;
- lamp/reel/display layout;
- game-specific apertures;
- glass-mounted controls;
- logical machine-object mappings.

Face must not know which Cabinet or Machine consumes it.

Machine owns the assignment from Cabinet surface target to Face asset.

## Surface generalization

Do not rename/rewrite Face into a universal Surface abstraction until a real video-machine workflow proves the requirements.

The first concrete video/JAMMA vertical slice should drive that decision.

## Dynamic objects are not Cabinet contents

Pool balls, moles and similar designer-controlled dynamic/general objects are not part of the Cabinet GLB merely because they appear physically near the Cabinet.

Use Object3D assets plus Machine-owned instances. See `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.
