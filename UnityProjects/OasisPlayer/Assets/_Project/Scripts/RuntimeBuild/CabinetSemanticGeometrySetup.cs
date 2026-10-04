using System;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public enum CabinetSemanticGeometryKind
    {
        Visual,
        FaceTarget,
        Collider,
        Trigger
    }

    /// <summary>The Player copy of the small, authored Cabinet GLB naming contract.</summary>
    public static class CabinetSemanticGeometry
    {
        public const string FaceTargetPrefix = "OasisFace_";
        public const string ColliderPrefix = "OasisCollider_";
        public const string TriggerPrefix = "OasisTrigger_";

        public static CabinetSemanticGeometryKind Classify(string nodeName, string meshName)
        {
            var nodeKind = ClassifyName(nodeName);
            return nodeKind != CabinetSemanticGeometryKind.Visual ? nodeKind : ClassifyName(meshName);
        }

        public static CabinetSemanticGeometryKind ClassifyName(string name)
        {
            if (name != null && name.StartsWith(FaceTargetPrefix, StringComparison.Ordinal)) return CabinetSemanticGeometryKind.FaceTarget;
            if (name != null && name.StartsWith(ColliderPrefix, StringComparison.Ordinal)) return CabinetSemanticGeometryKind.Collider;
            if (name != null && name.StartsWith(TriggerPrefix, StringComparison.Ordinal)) return CabinetSemanticGeometryKind.Trigger;
            return CabinetSemanticGeometryKind.Visual;
        }
    }

    public readonly struct CabinetSemanticGeometrySetupResult
    {
        public CabinetSemanticGeometrySetupResult(int colliderCount, int triggerCount)
        {
            ColliderCount = colliderCount;
            TriggerCount = triggerCount;
        }

        public int ColliderCount { get; }
        public int TriggerCount { get; }
    }

    /// <summary>Adds physics components to semantic mesh instances without changing their hierarchy or mesh assets.</summary>
    public static class CabinetSemanticGeometrySetup
    {
        public static CabinetSemanticGeometrySetupResult Setup(GameObject cabinetRoot)
        {
            if (cabinetRoot == null) throw new ArgumentNullException(nameof(cabinetRoot));

            var colliderCount = 0;
            var triggerCount = 0;
            foreach (var transform in cabinetRoot.GetComponentsInChildren<Transform>(true))
            {
                var meshFilter = transform.GetComponent<MeshFilter>();
                var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
                var kind = CabinetSemanticGeometry.Classify(transform.name, mesh != null ? mesh.name : null);
                if (kind != CabinetSemanticGeometryKind.Collider && kind != CabinetSemanticGeometryKind.Trigger) continue;

                var semanticName = CabinetSemanticGeometry.ClassifyName(transform.name) == kind
                    ? transform.name
                    : mesh != null ? mesh.name : transform.name;
                if (mesh == null)
                {
                    throw new InvalidOperationException($"{semanticName} could not create a MeshCollider because its instantiated GameObject has no MeshFilter mesh.");
                }

                var meshCollider = transform.GetComponent<MeshCollider>();
                if (meshCollider == null) meshCollider = transform.gameObject.AddComponent<MeshCollider>();
                ConfigureMeshCollider(meshCollider, mesh, kind, semanticName);

                foreach (var renderer in transform.GetComponents<Renderer>()) renderer.enabled = false;

                if (kind == CabinetSemanticGeometryKind.Collider) colliderCount++;
                else triggerCount++;
            }

            Debug.Log($"Cabinet semantic geometry: Colliders: {colliderCount}, Triggers: {triggerCount}");
            return new CabinetSemanticGeometrySetupResult(colliderCount, triggerCount);
        }

        private static void ConfigureMeshCollider(MeshCollider meshCollider, Mesh mesh, CabinetSemanticGeometryKind kind, string semanticName)
        {
            try
            {
                meshCollider.sharedMesh = mesh;
                if (kind == CabinetSemanticGeometryKind.Trigger)
                {
                    // Unity 6 rejects triggers on concave MeshColliders. Convex must be set first.
                    meshCollider.convex = true;
                    if (!meshCollider.convex)
                    {
                        throw new InvalidOperationException("Unity did not accept the mesh as convex");
                    }

                    meshCollider.isTrigger = true;
                    if (!meshCollider.isTrigger)
                    {
                        throw new InvalidOperationException("Unity did not accept the MeshCollider as a trigger");
                    }
                }
                else
                {
                    // Solid Cabinet geometry is static and retains its accurate concave topology.
                    meshCollider.isTrigger = false;
                    meshCollider.convex = false;
                }
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"{semanticName} could not create a {(kind == CabinetSemanticGeometryKind.Trigger ? "convex trigger" : "solid")} MeshCollider. " +
                    $"Simplify the authored geometry if Unity cannot cook this mesh. {exception.Message}", exception);
            }
        }
    }
}
