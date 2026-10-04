# Machine Composition / Overview View

## Current role

Machine Overview is a derived, read-only/navigational graph built from authoritative Machine dependencies.

It is not another source of truth.

## Authoritative data

Graph nodes/edges come from Machine, Cabinet, Face, Reel, runtime and provenance references.

Do not persist graph layout or graph edges as independent composition state.

## Interaction

The Overview supports navigation/inspection rather than arbitrary rewiring.

Keep it useful for:

- understanding Machine composition;
- opening referenced assets;
- showing missing/broken dependencies;
- provenance versus composition distinction.

## Dynamic Object3D extension

When Object3D instances are introduced, the Overview may summarize those dependencies/instances where useful, but do not render every repeated object as a large graph node if that makes the graph unreadable.

Fine-grained placement belongs in the future assembled Machine 3D viewport described in `11_DYNAMIC_OBJECTS_BEHAVIOUR_AND_SCRIPTING.md`.

The graph remains dependency/navigation-oriented; the 3D viewport becomes spatial-composition-oriented.
