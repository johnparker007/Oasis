using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    /// <summary>Machine-space spatial queries that keep Unity math outside the script host boundary.</summary>
    public sealed class RuntimeMachineQueryService
    {
        private readonly RuntimeMachine _machine;

        internal RuntimeMachineQueryService(RuntimeMachine machine) { _machine = machine ?? throw new ArgumentNullException(nameof(machine)); }

        public RuntimeVector3 FramePosition(string anchorId, RuntimeVector3 localPosition)
        {
            var anchor = _machine.GetAnchor(anchorId);
            return RuntimeVector3.FromUnity(anchor.Position.ToUnity() + Quaternion.Euler(anchor.RotationEulerDegrees.ToUnity()) * localPosition.ToUnity());
        }

        public RuntimeVector3 FrameRotation(string anchorId, RuntimeVector3 localRotationEulerDegrees)
        {
            var anchor = _machine.GetAnchor(anchorId);
            return RuntimeVector3.FromUnity((Quaternion.Euler(anchor.RotationEulerDegrees.ToUnity()) * Quaternion.Euler(localRotationEulerDegrees.ToUnity())).eulerAngles);
        }

        public float SphereRadius(string prefabAlias)
        {
            if (!_machine.Build.PrefabDependencies.TryGetValue(prefabAlias ?? string.Empty, out var definition))
                throw new KeyNotFoundException($"Prefab dependency '{prefabAlias}' is not declared for this Machine.");
            var root = definition.Manifest.root;
            var colliderComponent = root != null ? root.GetComponent("Collider") : null;
            if (colliderComponent == null || colliderComponent.collider == null || colliderComponent.collider.kind != "Sphere")
                throw new InvalidOperationException($"Prefab '{prefabAlias}' does not have a root Sphere collider.");
            var transform = root.Transform;
            if (transform == null || transform.scale == null) throw new InvalidOperationException($"Prefab '{prefabAlias}' has no usable root Transform.");
            var scale = transform.scale.Value;
            if (Mathf.Abs(scale.x - scale.y) > .0001f || Mathf.Abs(scale.x - scale.z) > .0001f)
                throw new InvalidOperationException($"Prefab '{prefabAlias}' root Sphere collider has non-uniform scale.");
            var children = root.children ?? Array.Empty<Object3DRuntimeNode>();
            for (var i = 0; i < children.Length; i++)
                if (HasActiveBody(children[i])) throw new InvalidOperationException($"Prefab '{prefabAlias}' has an unsupported non-root Rigidbody for its root Sphere collider.");
            var radius = colliderComponent.collider.radius * scale.x;
            if (colliderComponent.collider.sourceFrame == "ModelLocal")
            {
                var model = root.GetComponent("Model");
                if (model == null || model.model == null) throw new InvalidOperationException($"Prefab '{prefabAlias}' has a ModelLocal Sphere collider without a root model.");
                radius *= model.model.scale;
            }
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f) throw new InvalidOperationException($"Prefab '{prefabAlias}' has an invalid effective Sphere radius.");
            return radius;
        }

        public bool SurfaceContainsSphere(string surfaceId, RuntimeVector3 center, float radius)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius), "Sphere radius must be finite and non-negative.");
            var surfaces = _machine.Build.Machine.geometrySurfaces ?? Array.Empty<MachineRuntimeGeometrySurface>();
            MachineRuntimeGeometrySurface surface = null;
            for (var i = 0; i < surfaces.Length; i++)
                if (surfaces[i] != null && surfaces[i].id == surfaceId) { surface = surfaces[i]; break; }
            if (surface == null) throw new KeyNotFoundException($"Geometry surface '{surfaceId}' is not declared for this Machine.");
            if (surface.position == null || surface.rotationEulerDegrees == null || surface.width <= 0f || surface.length <= 0f)
                throw new InvalidOperationException($"Geometry surface '{surfaceId}' has invalid dimensions or pose.");
            var local = Quaternion.Inverse(Quaternion.Euler(surface.rotationEulerDegrees.Value)) * (center.ToUnity() - surface.position.Value);
            const float contactTolerance = .01f;
            return Mathf.Abs(local.x) <= surface.width * .5f + radius &&
                   Mathf.Abs(local.z) <= surface.length * .5f + radius &&
                   Mathf.Abs(local.y) <= radius + contactTolerance;
        }

        private static bool HasActiveBody(Object3DRuntimeNode node)
        {
            if (node == null) return false;
            var component = node.GetComponent("Rigidbody");
            if (component != null && component.rigidbody != null && component.rigidbody.mode != "Static") return true;
            var children = node.children ?? Array.Empty<Object3DRuntimeNode>();
            for (var i = 0; i < children.Length; i++) if (HasActiveBody(children[i])) return true;
            return false;
        }
    }
}
