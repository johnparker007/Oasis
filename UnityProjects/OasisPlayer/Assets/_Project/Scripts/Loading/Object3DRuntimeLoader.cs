using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GLTFast;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Loading
{
    public interface IObject3DModelDefinition : IDisposable
    {
        Task<bool> InstantiateAsync(Transform parent);
    }

    public interface IObject3DModelLoader
    {
        Task<IObject3DModelDefinition> LoadAsync(string path);
    }

    public sealed class GltfFastObject3DModelLoader : IObject3DModelLoader
    {
        public async Task<IObject3DModelDefinition> LoadAsync(string path)
        {
            var import = new GltfImport();
            if (!await import.Load(path))
            {
                import.Dispose();
                throw new InvalidOperationException($"glTFast failed to load Object3D GLB: {path}");
            }
            return new GltfFastObject3DModelDefinition(import);
        }

        private sealed class GltfFastObject3DModelDefinition : IObject3DModelDefinition
        {
            private readonly GltfImport _import;
            public GltfFastObject3DModelDefinition(GltfImport import) { _import = import; }
            public Task<bool> InstantiateAsync(Transform parent) { return _import.InstantiateMainSceneAsync(parent); }
            public void Dispose() { _import.Dispose(); }
        }
    }

    /// <summary>Loads reusable Object3D definitions once and creates Machine-owned live instances.</summary>
    public sealed class Object3DRuntimeLoader : IDisposable
    {
        private readonly IObject3DModelLoader _modelLoader;
        private readonly List<IObject3DModelDefinition> _loadedDefinitions = new List<IObject3DModelDefinition>();

        public Object3DRuntimeLoader(IObject3DModelLoader modelLoader) { _modelLoader = modelLoader ?? throw new ArgumentNullException(nameof(modelLoader)); }

        public async Task LoadAsync(RuntimeMachine machine, Transform machineRoot)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            if (machineRoot == null) throw new ArgumentNullException(nameof(machineRoot));
            var objectsRoot = new GameObject("Objects");
            objectsRoot.transform.SetParent(machineRoot, false);
            var definitions = new Dictionary<string, IObject3DModelDefinition>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var instances = machine.Build.Machine.objectInstances ?? Array.Empty<MachineRuntimeObjectInstance>();
                foreach (var declaration in instances)
                {
                    if (!machine.Build.ObjectDefinitions.TryGetValue(declaration.definitionId, out var definition))
                        throw Failure(declaration, null, "resolved definition is missing");
                    if (!definitions.TryGetValue(declaration.definitionId, out var loaded))
                    {
                        try { loaded = await _modelLoader.LoadAsync(definition.ModelPath); }
                        catch (Exception ex) { throw Failure(declaration, definition, $"failed to load definition: {ex.Message}", ex); }
                        definitions.Add(declaration.definitionId, loaded);
                        _loadedDefinitions.Add(loaded);
                    }
                    await InstantiateAsync(machine, objectsRoot.transform, declaration, definition, loaded);
                }
            }
            catch
            {
                machine.UnloadAssets();
                Destroy(objectsRoot);
                Dispose();
                throw;
            }
        }

        private static async Task InstantiateAsync(RuntimeMachine machine, Transform objectsRoot, MachineRuntimeObjectInstance declaration,
            ResolvedRuntimeObjectDefinition definition, IObject3DModelDefinition loaded)
        {
            var root = new GameObject(declaration.id);
            root.transform.SetParent(objectsRoot, false);
            try
            {
                root.transform.localPosition = declaration.transform.position.Value;
                root.transform.localRotation = Quaternion.Euler(declaration.transform.rotationEulerDegrees.Value);
                root.transform.localScale = declaration.transform.scale.Value;

                var intrinsic = new GameObject("Definition");
                intrinsic.transform.SetParent(root.transform, false);
                intrinsic.transform.localScale = Vector3.one * definition.Manifest.modelScale;
                intrinsic.transform.localRotation = UpAxisRotation(definition.Manifest.upAxis, declaration, definition);
                var model = new GameObject("Model");
                model.transform.SetParent(intrinsic.transform, false);
                if (!await loaded.InstantiateAsync(model.transform)) throw Failure(declaration, definition, "glTFast failed to instantiate the model");

                var collider = CreateCollider(intrinsic, model, definition.Manifest.collider, definition.Manifest.rigidbody.enabled, declaration, definition);
                Rigidbody rigidbody = null;
                if (definition.Manifest.rigidbody.enabled)
                {
                    rigidbody = intrinsic.AddComponent<Rigidbody>();
                    rigidbody.mass = definition.Manifest.rigidbody.mass;
                    rigidbody.useGravity = definition.Manifest.rigidbody.useGravity;
                }
                machine.RegisterObject(new RuntimeObjectInstance(declaration.id, declaration.displayName, declaration.definitionId,
                    definition, root, collider, rigidbody, declaration.transform));
            }
            catch (Exception ex)
            {
                Destroy(root);
                if (ex is InvalidOperationException && ex.Message.StartsWith("Object3D instance", StringComparison.Ordinal)) throw;
                throw Failure(declaration, definition, ex.Message, ex);
            }
        }

        private static Quaternion UpAxisRotation(string axis, MachineRuntimeObjectInstance instance, ResolvedRuntimeObjectDefinition definition)
        {
            if (axis == "Y") return Quaternion.identity;
            if (axis == "Z") return Quaternion.Euler(-90f, 0f, 0f);
            if (axis == "X") return Quaternion.Euler(0f, 0f, 90f);
            throw Failure(instance, definition, $"has unsupported up-axis '{axis}'");
        }

        private static Collider CreateCollider(GameObject physicsRoot, GameObject modelRoot, Object3DRuntimeCollider authored, bool hasRigidbody,
            MachineRuntimeObjectInstance instance, ResolvedRuntimeObjectDefinition definition)
        {
            switch (authored.kind)
            {
                case "None": return null;
                case "Sphere": var sphere = physicsRoot.AddComponent<SphereCollider>(); sphere.center = Vector(authored.center, "center", instance, definition); sphere.radius = authored.radius; return sphere;
                case "Box": var box = physicsRoot.AddComponent<BoxCollider>(); box.center = Vector(authored.center, "center", instance, definition); box.size = Vector(authored.size, "size", instance, definition); return box;
                case "Capsule":
                    var capsule = physicsRoot.AddComponent<CapsuleCollider>(); capsule.center = Vector(authored.center, "center", instance, definition); capsule.radius = authored.radius; capsule.height = authored.height;
                    if (authored.axis == "X") capsule.direction = 0; else if (authored.axis == "Y") capsule.direction = 1; else if (authored.axis == "Z") capsule.direction = 2;
                    else throw Failure(instance, definition, $"has unsupported capsule axis '{authored.axis}'");
                    return capsule;
                case "Mesh":
                    var meshes = modelRoot.GetComponentsInChildren<MeshFilter>(true);
                    var usable = new List<MeshFilter>();
                    foreach (var candidate in meshes) if (candidate.sharedMesh != null) usable.Add(candidate);
                    if (usable.Count != 1) throw Failure(instance, definition, $"Mesh collider requires exactly one usable mesh; found {usable.Count}. Compound/multi-mesh collision is not supported");
                    var meshCollider = usable[0].gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = usable[0].sharedMesh;
                    // Unity does not support a concave MeshCollider on a dynamic Rigidbody.
                    meshCollider.convex = hasRigidbody;
                    return meshCollider;
                default: throw Failure(instance, definition, $"has unsupported collider kind '{authored.kind}'");
            }
        }

        private static Vector3 Vector(float[] values, string field, MachineRuntimeObjectInstance instance, ResolvedRuntimeObjectDefinition definition)
        {
            if (values == null || values.Length != 3) throw Failure(instance, definition, $"collider {field} must contain exactly three values");
            return new Vector3(values[0], values[1], values[2]);
        }

        private static InvalidOperationException Failure(MachineRuntimeObjectInstance instance, ResolvedRuntimeObjectDefinition definition, string reason, Exception inner = null)
        {
            var name = definition != null && definition.Manifest != null ? $" ('{definition.Manifest.displayName}')" : string.Empty;
            return new InvalidOperationException($"Object3D instance '{instance.id}' failed for definition '{instance.definitionId}'{name}: {reason}.", inner);
        }

        public void Dispose()
        {
            foreach (var definition in _loadedDefinitions) definition.Dispose();
            _loadedDefinitions.Clear();
        }

        private static void Destroy(GameObject gameObject)
        {
            if (gameObject == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(gameObject); else UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
