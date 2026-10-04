# Cabinet semantic geometry — Stage 3 Player physics

## Runtime flow

The authored GLB remains the single source of Cabinet geometry and semantic identity:

```text
OasisCollider_* / OasisTrigger_* node or mesh name in the authored GLB
    -> Machine build validates the semantic mesh and copies the GLB byte-for-byte
    -> glTFast instantiates the Cabinet scene
    -> CabinetSemanticGeometrySetup classifies each instantiated mesh instance
    -> the instance receives a MeshCollider over its existing Unity Mesh
```

No collider or trigger list is serialized into `cabinet.runtime.json` or
`machine.runtime.json`. Their schema versions remain Cabinet 5 and Machine 6.

## glTFast identity and hierarchy

With glTFast 6.14.1, `InstantiateMainSceneAsync` creates a Unity transform hierarchy
from the glTF scene. Authored node names become GameObject names, the imported glTF
mesh name is retained on the `Mesh` assigned to `MeshFilter.sharedMesh`, and node
TRS becomes the corresponding local Unity transform. Repeated node references to a
glTF mesh produce separate GameObject/renderer instances that can share the imported
Unity Mesh. Consequently renderer visibility must be changed on each instance; the
shared Mesh asset must not be modified.

Player classification first examines `GameObject.name` (the node identity), then
falls back to `MeshFilter.sharedMesh.name` (the mesh identity) only when the node is
not semantic. This reproduces the Editor's node-name-over-mesh-name rule. Only the
ordinal prefixes `OasisFace_`, `OasisCollider_`, and `OasisTrigger_` have meaning.

## Collider policy

`OasisCollider_*` receives a static, non-trigger `MeshCollider` with `convex = false`,
preserving the authored concave topology. `OasisTrigger_*` receives a static trigger
`MeshCollider` with `convex = true`: Unity 6 rejects triggers on concave MeshColliders.
For triggers, setup assigns the shared mesh, enables convex cooking, and only then sets
`isTrigger = true`, avoiding even a temporary invalid concave-trigger configuration.
No Rigidbody is added to either kind.

Convex cooking replaces concave detail with a convex hull, can reject degenerate or
overly complex geometry, and Unity limits the cooked convex MeshCollider to 255
triangles. Trigger meshes must therefore be authored as simple shapes suitable for
Unity convex cooking.
If Unity rejects convex or trigger configuration, Player reports the semantic object
and directs the author to simplify its geometry; it does not fall back to different
physics or gameplay semantics.

The setup disables any Renderer on a physics-semantic instance, but retains its
MeshFilter, Mesh, name, hierarchy, and transform. It does not inspect materials or UVs,
so a POSITION/NORMAL-only mesh remains valid. `OasisFace_*` and ordinary visual meshes
are left untouched. The setup runs once immediately after successful glTFast scene
instantiation and logs discovered collider and trigger counts.
