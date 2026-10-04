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
                // Cabinet geometry is static. Keeping both kinds non-convex preserves authored topology;
                // Unity supports non-convex MeshColliders when no Rigidbody is attached.
                meshCollider.convex = false;
                meshCollider.sharedMesh = mesh;
                meshCollider.isTrigger = kind == CabinetSemanticGeometryKind.Trigger;

                foreach (var renderer in transform.GetComponents<Renderer>()) renderer.enabled = false;

                if (kind == CabinetSemanticGeometryKind.Collider) colliderCount++;
                else triggerCount++;
            }

            Debug.Log($"Cabinet semantic geometry: Colliders: {colliderCount}, Triggers: {triggerCount}");
            return new CabinetSemanticGeometrySetupResult(colliderCount, triggerCount);
        }
    }
}
