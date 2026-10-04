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

        public static string ResolveSemanticName(string nodeName, string meshName)
        {
            return ClassifyName(nodeName) != CabinetSemanticGeometryKind.Visual ? nodeName : meshName;
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
                    : mesh != null ? CabinetSemanticGeometry.ResolveSemanticName(transform.name, mesh.name) : transform.name;
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

        /// <summary>Registers trigger identities from the same winning semantic names used during classification.</summary>
        public static int RegisterTriggers(GameObject cabinetRoot, RuntimeMachine machine)
        {
            if (cabinetRoot == null) throw new ArgumentNullException(nameof(cabinetRoot));
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            var count = 0;
            foreach (var transform in cabinetRoot.GetComponentsInChildren<Transform>(true))
            {
                var filter = transform.GetComponent<MeshFilter>();
                var meshName = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : null;
                if (CabinetSemanticGeometry.Classify(transform.name, meshName) != CabinetSemanticGeometryKind.Trigger) continue;
                var semanticName = CabinetSemanticGeometry.ResolveSemanticName(transform.name, meshName);
                var id = semanticName != null && semanticName.StartsWith(CabinetSemanticGeometry.TriggerPrefix, StringComparison.Ordinal)
                    ? semanticName.Substring(CabinetSemanticGeometry.TriggerPrefix.Length) : string.Empty;
                if (!RuntimeIdentity.IsValid(id)) throw new InvalidOperationException($"{semanticName ?? CabinetSemanticGeometry.TriggerPrefix} has an empty or invalid logical trigger ID.");
                var collider = transform.GetComponent<MeshCollider>();
                if (collider == null || !collider.isTrigger) throw new InvalidOperationException($"Cabinet semantic trigger '{semanticName}' has not been configured as a trigger MeshCollider.");
                machine.RegisterTrigger(new RuntimeTrigger(id, collider));
                var relay = transform.GetComponent<RuntimeTriggerRelay>();
                if (relay == null) relay = transform.gameObject.AddComponent<RuntimeTriggerRelay>();
                relay.Initialize(machine, id);
                count++;
            }
            Debug.Log($"Cabinet semantic triggers registered: {count}");
            return count;
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
