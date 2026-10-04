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

`OasisCollider_*` receives a non-trigger `MeshCollider`; `OasisTrigger_*` receives a
trigger `MeshCollider`. Both reference the already instantiated `sharedMesh` and keep
`convex = false`. Unity 6 supports non-convex MeshColliders as static geometry (a
GameObject without a Rigidbody), including use of `Collider.isTrigger`. This preserves
concave authored trigger volumes rather than replacing them with a convex hull, which
would close holes and is limited in complexity. No Rigidbody is added to the Cabinet.
A future dynamic body interacting with a trigger can own the Rigidbody required for
trigger event delivery.

The setup disables any Renderer on a physics-semantic instance, but retains its
MeshFilter, Mesh, name, hierarchy, and transform. It does not inspect materials or UVs,
so a POSITION/NORMAL-only mesh remains valid. `OasisFace_*` and ordinary visual meshes
are left untouched. The setup runs once immediately after successful glTFast scene
instantiation and logs discovered collider and trigger counts.
