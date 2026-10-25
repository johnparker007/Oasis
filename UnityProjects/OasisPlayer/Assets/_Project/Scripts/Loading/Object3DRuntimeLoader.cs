using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>Loads reusable Prefab definitions and creates Machine-owned live entity trees.</summary>
    public sealed class Object3DRuntimeLoader : IDisposable, IRuntimePrefabFactory
    {
        private readonly IObject3DModelLoader _modelLoader;
        private readonly Dictionary<string, IObject3DModelDefinition> _loadedModels = new Dictionary<string, IObject3DModelDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Mesh> _generatedMeshes = new List<Mesh>();
        private readonly Dictionary<string, RuntimePrefabTemplate> _templates = new Dictionary<string, RuntimePrefabTemplate>(StringComparer.Ordinal);
        private GameObject _templatesRoot;
        private Transform _objectsRoot;
        private RuntimeMachine _machine;

        public Object3DRuntimeLoader(IObject3DModelLoader modelLoader) { _modelLoader = modelLoader ?? throw new ArgumentNullException(nameof(modelLoader)); }

        public async Task LoadAsync(RuntimeMachine machine, Transform machineRoot)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            if (machineRoot == null) throw new ArgumentNullException(nameof(machineRoot));
            var objectsRoot = new GameObject("Objects");
            objectsRoot.transform.SetParent(machineRoot, false);
            _objectsRoot = objectsRoot.transform;
            _templatesRoot = new GameObject("PrefabTemplates");
            _templatesRoot.transform.SetParent(machineRoot, false);
            _templatesRoot.SetActive(false);
            machine.SetMachineSpace(machineRoot);
            _machine = machine;
            try
            {
                await LoadTemplatesAsync(machine);
                machine.AttachPrefabFactory(this);
                var orderedInstances = OrderInstances(machine);
                for (var i = 0; i < orderedInstances.Count; i++)
                {
                    var declaration = orderedInstances[i];
                    var definition = machine.Build.ObjectDefinitions[declaration.definitionId];
                    Transform parent;
                    if (string.IsNullOrEmpty(declaration.parentId)) parent = objectsRoot.transform;
                    else
                    {
                        var parentInstance = machine.GetObject(declaration.parentId);
                        parent = parentInstance.Root.transform;
                        if (HasActiveBody(definition.Manifest.root) && HasActiveBodyInAncestors(machine, declaration.parentId))
                            throw Failure(declaration, definition, $"has an active Rigidbody below parent instance '{declaration.parentId}', which already owns an active Rigidbody");
                    }
                    await InstantiateAsync(machine, parent, declaration, definition);
                }
            }
            catch
            {
                machine.UnloadAssets();
                Destroy(objectsRoot);
                Destroy(_templatesRoot);
                _templatesRoot = null;
                Dispose();
                throw;
            }
        }

        private async Task LoadTemplatesAsync(RuntimeMachine machine)
        {
            var definitions = new Dictionary<string, ResolvedRuntimeObjectDefinition>(StringComparer.Ordinal);
            foreach (var item in machine.Build.ObjectDefinitions) definitions[item.Key] = item.Value;
            foreach (var item in machine.Build.PrefabDependencies) definitions[item.Value.Manifest.definitionId] = item.Value;
            foreach (var item in definitions)
            {
                var declaration = TemplateDeclaration(machine, item.Key, item.Value);
                var root = new GameObject("Template_" + item.Key);
                root.SetActive(false);
                root.transform.SetParent(_templatesRoot.transform, false);
                try
                {
                    var nodes = new List<RuntimeObjectNode>();
                    await BuildNodeAsync(declaration, item.Value, item.Value.Manifest.root, root, true, false, nodes, null);
                    _templates.Add(item.Key, new RuntimePrefabTemplate(item.Key, item.Value, root, nodes));
                }
                catch
                {
                    Destroy(root);
                    throw;
                }
            }
        }

        private static MachineRuntimeObjectInstance TemplateDeclaration(RuntimeMachine machine, string definitionId,
            ResolvedRuntimeObjectDefinition definition)
        {
            var instances = machine.Build.Machine.objectInstances ?? Array.Empty<MachineRuntimeObjectInstance>();
            for (var i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance != null && instance.definitionId == definitionId) return instance;
            }
            foreach (var item in machine.Build.PrefabDependencies)
            {
                if (item.Value.Manifest.definitionId != definitionId) continue;
                return new MachineRuntimeObjectInstance
                {
                    id = "prefab:" + item.Key, displayName = item.Key, definitionId = definitionId,
                    transform = IdentityTransform(), active = false
                };
            }
            return new MachineRuntimeObjectInstance
            {
                id = "definition:" + definitionId, displayName = definition.Manifest.displayName, definitionId = definitionId,
                transform = IdentityTransform(), active = false
            };
        }

        private static List<MachineRuntimeObjectInstance> OrderInstances(RuntimeMachine machine)
        {
            var declarations = machine.Build.Machine.objectInstances ?? Array.Empty<MachineRuntimeObjectInstance>();
            var byId = new Dictionary<string, MachineRuntimeObjectInstance>(StringComparer.Ordinal);
            for (var i = 0; i < declarations.Length; i++)
            {
                var declaration = declarations[i];
                if (declaration == null || string.IsNullOrWhiteSpace(declaration.id) || string.IsNullOrWhiteSpace(declaration.definitionId) || declaration.transform == null)
                    throw new InvalidOperationException($"Machine Object3D instance at index {i} is invalid.");
                if (byId.ContainsKey(declaration.id)) throw new InvalidOperationException($"Duplicate Machine Object3D instance ID '{declaration.id}'.");
                if (!machine.Build.ObjectDefinitions.ContainsKey(declaration.definitionId)) throw Failure(declaration, null, "resolved definition is missing");
                byId.Add(declaration.id, declaration);
            }

            for (var i = 0; i < declarations.Length; i++)
            {
                var declaration = declarations[i];
                if (!string.IsNullOrEmpty(declaration.parentId) && !byId.ContainsKey(declaration.parentId))
                    throw Failure(declaration, machine.Build.ObjectDefinitions[declaration.definitionId], $"references missing parent instance '{declaration.parentId}'");
                if (declaration.parentId == declaration.id) throw Failure(declaration, machine.Build.ObjectDefinitions[declaration.definitionId], "cannot parent itself");
            }

            var ordered = new List<MachineRuntimeObjectInstance>(declarations.Length);
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            while (ordered.Count < declarations.Length)
            {
                var madeProgress = false;
                for (var i = 0; i < declarations.Length; i++)
                {
                    var declaration = declarations[i];
                    if (emitted.Contains(declaration.id)) continue;
                    if (!string.IsNullOrEmpty(declaration.parentId) && !emitted.Contains(declaration.parentId)) continue;
                    ordered.Add(declaration);
                    emitted.Add(declaration.id);
                    madeProgress = true;
                }
                if (!madeProgress)
                {
                    MachineRuntimeObjectInstance cycle = null;
                    for (var i = 0; i < declarations.Length; i++) if (!emitted.Contains(declarations[i].id)) { cycle = declarations[i]; break; }
                    throw Failure(cycle, machine.Build.ObjectDefinitions[cycle.definitionId], "authored parent hierarchy contains a cycle");
                }
            }
            return ordered;
        }

        private Task InstantiateAsync(RuntimeMachine machine, Transform parent, MachineRuntimeObjectInstance declaration,
            ResolvedRuntimeObjectDefinition definition)
        {
            var rootNode = definition.Manifest.root;
            if (rootNode == null || rootNode.Transform == null) throw Failure(declaration, definition, "Prefab root or root Transform is missing");
            if (!_templates.TryGetValue(definition.Manifest.definitionId, out var template)) throw Failure(declaration, definition, "Prefab template is missing");
            var root = UnityEngine.Object.Instantiate(template.Root, parent, false);
            root.name = declaration.id;
            root.SetActive(false);
            try
            {
                var initial = CombineRootTransform(declaration.transform, rootNode.Transform, declaration, definition);
                ApplyTransform(root.transform, initial);
                if (HasShear(root.transform.localToWorldMatrix)) throw Failure(declaration, definition, "authored parent and placement transforms compose to a shear that the runtime physics hierarchy cannot represent");

                var authoredInitial = ToMachineTransform(initial);
                var nodes = CloneRuntimeNodes(template, root.transform, declaration.id, authoredInitial);
                var rootNodeRuntime = nodes[0];
                var instance = new RuntimeObjectInstance(declaration.id, declaration.displayName, declaration.definitionId,
                    definition, root, rootNodeRuntime.Collider, rootNodeRuntime.Rigidbody, authoredInitial,
                    nodes, declaration.parentId, declaration.active);
                machine.RegisterObject(instance);
                root.SetActive(declaration.active);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Destroy(root);
                if (ex is InvalidOperationException && ex.Message.StartsWith("Object3D instance", StringComparison.Ordinal)) throw;
                throw Failure(declaration, definition, ex.Message, ex);
            }
        }

        RuntimeObjectInstance IRuntimePrefabFactory.Instantiate(RuntimeMachine machine, string runtimeId, string prefabAlias, RuntimePose machinePose)
        {
            if (machine == null) throw new ArgumentNullException(machine == null ? nameof(machine) : nameof(machinePose));
            if (!machine.Build.PrefabDependencies.TryGetValue(prefabAlias, out var definition)) throw new KeyNotFoundException($"Prefab dependency '{prefabAlias}' is not declared for this Machine.");
            var declaration = new MachineRuntimeObjectInstance
            {
                id = runtimeId,
                displayName = prefabAlias,
                definitionId = definition.Manifest.definitionId,
                transform = new MachineRuntimeObjectTransform
                {
                    position = new RuntimeVector3Definition { x = machinePose.Position.X, y = machinePose.Position.Y, z = machinePose.Position.Z },
                    rotationEulerDegrees = new RuntimeVector3Definition { x = machinePose.RotationEulerDegrees.X, y = machinePose.RotationEulerDegrees.Y, z = machinePose.RotationEulerDegrees.Z },
                    scale = new RuntimeVector3Definition { x = 1f, y = 1f, z = 1f }
                },
                active = true
            };
            var parent = _objectsRoot;
            if (parent == null) throw new InvalidOperationException("Runtime Objects root is unavailable.");
            if (HasActiveBody(definition.Manifest.root) && HasActiveBodyInAncestors(machine, declaration.parentId))
                throw Failure(declaration, definition, "cannot spawn an active Rigidbody beneath another active Rigidbody");
            var rootNode = definition.Manifest.root;
            if (!_templates.TryGetValue(definition.Manifest.definitionId, out var template)) throw Failure(declaration, definition, "Prefab template is missing");
            var root = UnityEngine.Object.Instantiate(template.Root, parent, false);
            root.name = runtimeId;
            root.SetActive(false);
            try
            {
                var initial = CombineRootTransform(declaration.transform, rootNode.Transform, declaration, definition);
                ApplyTransform(root.transform, initial);
                if (HasShear(root.transform.localToWorldMatrix)) throw Failure(declaration, definition, "spawn pose composes to an unsupported shear");
                var authoredInitial = ToMachineTransform(initial);
                var nodes = CloneRuntimeNodes(template, root.transform, runtimeId, authoredInitial);
                return new RuntimeObjectInstance(runtimeId, declaration.displayName, declaration.definitionId, definition,
                    root, nodes[0].Collider, nodes[0].Rigidbody, authoredInitial, nodes, string.Empty, true);
            }
            catch
            {
                Destroy(root);
                throw;
            }
        }

        private static List<RuntimeObjectNode> CloneRuntimeNodes(RuntimePrefabTemplate template, Transform root, string rootId,
            MachineRuntimeObjectTransform initial)
        {
            var result = new List<RuntimeObjectNode>(template.Nodes.Count);
            for (var i = 0; i < template.Nodes.Count; i++)
            {
                var source = template.Nodes[i];
                var nodeObject = ResolvePath(root, source.NodePath).gameObject;
                var collider = source.ColliderPath != null ? ResolvePath(root, source.ColliderPath).GetComponent<Collider>() : null;
                var body = source.BodyPath != null ? ResolvePath(root, source.BodyPath).GetComponent<Rigidbody>() : null;
                result.Add(new RuntimeObjectNode(source.IsRoot ? rootId : CreateChildRuntimeId(rootId, source.PrefabNodeId),
                    rootId, source.PrefabNodeId, source.Name, nodeObject, collider, body, source.AuthoredTransform,
                    source.IsRoot ? initial : null));
            }
            return result;
        }

        private static MachineRuntimeObjectTransform IdentityTransform()
        {
            return new MachineRuntimeObjectTransform
            {
                position = new RuntimeVector3Definition(),
                rotationEulerDegrees = new RuntimeVector3Definition(),
                scale = new RuntimeVector3Definition { x = 1f, y = 1f, z = 1f }
            };
        }

        private async Task<RuntimeObjectNode> BuildNodeAsync(MachineRuntimeObjectInstance declaration, ResolvedRuntimeObjectDefinition definition,
            Object3DRuntimeNode authoredNode, GameObject nodeObject, bool isRoot, bool hasBodyAncestor,
            List<RuntimeObjectNode> nodes, MachineRuntimeObjectTransform rootInitialTransform)
        {
            if (authoredNode == null) throw Failure(declaration, definition, "Prefab hierarchy contains a null node");
            var authoredTransform = authoredNode.Transform;
            if (authoredTransform == null) throw Failure(declaration, definition, $"node '{authoredNode.name}' has no Transform component");
            if (!isRoot) ApplyTransform(nodeObject.transform, authoredTransform);

            var colliderComponent = authoredNode.GetComponent("Collider");
            var bodyComponent = authoredNode.GetComponent("Rigidbody");
            if ((colliderComponent != null || bodyComponent != null) && HasShear(nodeObject.transform.localToWorldMatrix))
                throw Failure(declaration, definition, $"node '{authoredNode.name}' physical Transform hierarchy composes to a shear that the runtime physics hierarchy cannot represent");

            var body = CreateRigidbody(nodeObject, bodyComponent != null ? bodyComponent.rigidbody : null, declaration, definition, authoredNode);
            var ownsActiveBody = body != null;
            var hasBody = hasBodyAncestor || ownsActiveBody;

            GameObject modelCorrection = null;
            GameObject modelObject = null;
            var modelComponent = authoredNode.GetComponent("Model");
            Object3DRuntimeModel modelDefinition = modelComponent != null ? modelComponent.model : null;
            if (modelDefinition != null)
            {
                if (string.IsNullOrWhiteSpace(modelDefinition.path) || !definition.ModelPaths.TryGetValue(modelDefinition.path, out var modelPath))
                    throw Failure(declaration, definition, $"node '{authoredNode.name}' references unresolved model '{modelDefinition.path}'");
                var imported = await LoadModelAsync(modelPath, declaration, definition);
                modelCorrection = new GameObject("ModelCorrection");
                modelCorrection.transform.SetParent(nodeObject.transform, false);
                ApplyIntrinsicCorrection(modelCorrection.transform, modelDefinition.scale, UpAxisRotation(modelDefinition.upAxis, declaration, definition));
                modelObject = new GameObject("Model");
                modelObject.transform.SetParent(modelCorrection.transform, false);
                if (!await imported.InstantiateAsync(modelObject.transform)) throw Failure(declaration, definition, $"node '{authoredNode.name}' GLB failed to instantiate");
            }

            var collider = colliderComponent != null
                ? CreateCollider(nodeObject, modelCorrection, modelObject, colliderComponent.collider, modelDefinition, hasBody, declaration, definition, authoredNode)
                : null;
            var runtimeNode = new RuntimeObjectNode(isRoot ? declaration.id : CreateChildRuntimeId(declaration.id, authoredNode.id),
                declaration.id, authoredNode.id, authoredNode.name, nodeObject, collider, body, authoredTransform,
                isRoot ? rootInitialTransform : null);
            nodes.Add(runtimeNode);

            var children = authoredNode.children ?? Array.Empty<Object3DRuntimeNode>();
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                var childObject = new GameObject(string.IsNullOrWhiteSpace(child != null ? child.name : null) ? "PrefabNode" : child.name);
                childObject.transform.SetParent(nodeObject.transform, false);
                await BuildNodeAsync(declaration, definition, child, childObject, false, hasBody, nodes, null);
            }
            return runtimeNode;
        }

        private async Task<IObject3DModelDefinition> LoadModelAsync(string modelPath, MachineRuntimeObjectInstance declaration, ResolvedRuntimeObjectDefinition definition)
        {
            var absolutePath = Path.GetFullPath(modelPath);
            if (_loadedModels.TryGetValue(absolutePath, out var loaded)) return loaded;
            try { loaded = await _modelLoader.LoadAsync(absolutePath); }
            catch (Exception ex) { throw Failure(declaration, definition, $"failed to load GLB '{absolutePath}': {ex.Message}", ex); }
            if (loaded == null) throw Failure(declaration, definition, $"model loader returned no GLB import for '{absolutePath}'");
            _loadedModels.Add(absolutePath, loaded);
            return loaded;
        }

        private static RuntimeObjectNodeTransform CombineRootTransform(MachineRuntimeObjectTransform placement,
            Object3DRuntimeTransform source, MachineRuntimeObjectInstance declaration, ResolvedRuntimeObjectDefinition definition)
        {
            if (placement == null || placement.position == null || placement.rotationEulerDegrees == null || placement.scale == null ||
                source.position == null || source.rotationEulerDegrees == null || source.scale == null)
                throw Failure(declaration, definition, "placement or Prefab root Transform is incomplete");
            var placementMatrix = Matrix4x4.TRS(placement.position.Value, Quaternion.Euler(placement.rotationEulerDegrees.Value), placement.scale.Value);
            var sourceMatrix = Matrix4x4.TRS(source.position.Value, Quaternion.Euler(source.rotationEulerDegrees.Value), source.scale.Value);
            var combined = placementMatrix * sourceMatrix;
            var x = new Vector3(combined.m00, combined.m10, combined.m20);
            var y = new Vector3(combined.m01, combined.m11, combined.m21);
            var z = new Vector3(combined.m02, combined.m12, combined.m22);
            var scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f) throw Failure(declaration, definition, "combined root Transform scale must be positive");
            var nx = x / scale.x; var ny = y / scale.y; var nz = z / scale.z;
            if (Mathf.Abs(Vector3.Dot(nx, ny)) > 0.0001f || Mathf.Abs(Vector3.Dot(nx, nz)) > 0.0001f || Mathf.Abs(Vector3.Dot(ny, nz)) > 0.0001f)
                throw Failure(declaration, definition, "Machine placement and Prefab root Transform compose to a shear that cannot be represented by one root entity");
            if (Vector3.Dot(Vector3.Cross(nx, ny), nz) <= 0f) throw Failure(declaration, definition, "combined root Transform has an unsupported mirrored orientation");
            var rotation = Quaternion.LookRotation(nz, ny);
            return new RuntimeObjectNodeTransform(new Vector3(combined.m03, combined.m13, combined.m23), rotation, scale);
        }

        private static MachineRuntimeObjectTransform ToMachineTransform(RuntimeObjectNodeTransform transform)
        {
            return new MachineRuntimeObjectTransform
            {
                position = new RuntimeVector3Definition { x = transform.Position.x, y = transform.Position.y, z = transform.Position.z },
                rotationEulerDegrees = new RuntimeVector3Definition { x = transform.Rotation.eulerAngles.x, y = transform.Rotation.eulerAngles.y, z = transform.Rotation.eulerAngles.z },
                scale = new RuntimeVector3Definition { x = transform.Scale.x, y = transform.Scale.y, z = transform.Scale.z }
            };
        }

        private static void ApplyTransform(Transform target, RuntimeObjectNodeTransform transform)
        {
            target.localPosition = transform.Position;
            target.localRotation = transform.Rotation;
            target.localScale = transform.Scale;
        }
        private static void ApplyTransform(Transform target, Object3DRuntimeTransform transform)
        {
            if (transform == null || transform.position == null || transform.rotationEulerDegrees == null || transform.scale == null)
                throw new InvalidOperationException("Prefab node Transform payload is incomplete.");
            target.localPosition = transform.position.Value;
            target.localRotation = Quaternion.Euler(transform.rotationEulerDegrees.Value);
            target.localScale = transform.scale.Value;
        }

        private static Rigidbody CreateRigidbody(GameObject owner, Object3DRuntimeRigidbody authored,
            MachineRuntimeObjectInstance declaration, ResolvedRuntimeObjectDefinition definition, Object3DRuntimeNode node)
        {
            if (authored == null) return null;
            if (authored.mode == "Static") return null;
            if (authored.mode != "Dynamic" && authored.mode != "Kinematic") throw Failure(declaration, definition, $"node '{node.name}' has unsupported Rigidbody mode '{authored.mode}'");
            var body = owner.AddComponent<Rigidbody>();
            body.isKinematic = authored.mode == "Kinematic";
            body.useGravity = authored.useGravity;
            if (authored.mode == "Dynamic") body.mass = authored.mass;
            return body;
        }

        private Collider CreateCollider(GameObject nodeObject, GameObject modelCorrection, GameObject modelObject,
            Object3DRuntimeCollider authored, Object3DRuntimeModel model, bool hasBodyAncestor,
            MachineRuntimeObjectInstance declaration, ResolvedRuntimeObjectDefinition definition, Object3DRuntimeNode node)
        {
            if (authored == null || authored.kind == "None") return null;
            var sourceFrameModel = authored.sourceFrame == "ModelLocal";
            if (authored.sourceFrame != "ModelLocal" && authored.sourceFrame != "NodeLocal")
                throw Failure(declaration, definition, $"node '{node.name}' has unsupported collider source frame '{authored.sourceFrame}'");
            if (sourceFrameModel && model == null) throw Failure(declaration, definition, $"node '{node.name}' Collider uses ModelLocal without a Model component");

            if (authored.kind == "Mesh")
            {
                if (modelObject == null) throw Failure(declaration, definition, $"node '{node.name}' Mesh collider requires a Model component");
                var filters = modelObject.GetComponentsInChildren<MeshFilter>(true);
                var usable = new List<MeshFilter>();
                for (var i = 0; i < filters.Length; i++) if (filters[i] != null && filters[i].sharedMesh != null) usable.Add(filters[i]);
                if (usable.Count != 1) throw Failure(declaration, definition, $"node '{node.name}' Mesh collider requires exactly one usable mesh; found {usable.Count}; compound/multi-mesh collision is not supported");
                if (HasShear(usable[0].transform.localToWorldMatrix)) throw Failure(declaration, definition, $"node '{node.name}' GLB mesh transform composes to a shear that its collider cannot represent");
                var target = sourceFrameModel ? usable[0].gameObject : nodeObject;
                var meshCollider = target.AddComponent<MeshCollider>();
                meshCollider.isTrigger = authored.isTrigger;
                meshCollider.sharedMesh = sourceFrameModel ? usable[0].sharedMesh : BakeMeshToNodeLocal(usable[0], nodeObject.transform, declaration, definition, node);
                meshCollider.convex = hasBodyAncestor;
                return meshCollider;
            }

            GameObject correction = nodeObject;
            if (sourceFrameModel)
            {
                correction = new GameObject("PhysicsCorrection");
                correction.transform.SetParent(nodeObject.transform, false);
                var correctionRotation = UpAxisRotation(model.upAxis, declaration, definition);
                ApplyIntrinsicCorrection(correction.transform, model.scale, correctionRotation);
            }
            Collider collider;
            switch (authored.kind)
            {
                case "Sphere":
                    var sphere = correction.AddComponent<SphereCollider>();
                    sphere.center = Vector(authored.center, "center", declaration, definition, node);
                    sphere.radius = authored.radius;
                    collider = sphere;
                    break;
                case "Box":
                    var box = correction.AddComponent<BoxCollider>();
                    box.center = Vector(authored.center, "center", declaration, definition, node);
                    box.size = Vector(authored.size, "size", declaration, definition, node);
                    collider = box;
                    break;
                case "Capsule":
                    var capsule = correction.AddComponent<CapsuleCollider>();
                    capsule.center = Vector(authored.center, "center", declaration, definition, node);
                    capsule.radius = authored.radius;
                    capsule.height = authored.height;
                    if (authored.axis == "X") capsule.direction = 0;
                    else if (authored.axis == "Y") capsule.direction = 1;
                    else if (authored.axis == "Z") capsule.direction = 2;
                    else throw Failure(declaration, definition, $"node '{node.name}' has unsupported capsule axis '{authored.axis}'");
                    collider = capsule;
                    break;
                default: throw Failure(declaration, definition, $"node '{node.name}' has unsupported collider kind '{authored.kind}'");
            }
            collider.isTrigger = authored.isTrigger;
            return collider;
        }

        private Mesh BakeMeshToNodeLocal(MeshFilter source, Transform nodeRoot, MachineRuntimeObjectInstance declaration,
            ResolvedRuntimeObjectDefinition definition, Object3DRuntimeNode node)
        {
            var mesh = new Mesh { name = $"{declaration.id}_{node.id}_Collider" };
            var sourceMesh = source.sharedMesh;
            var relative = nodeRoot.worldToLocalMatrix * source.transform.localToWorldMatrix;
            var vertices = sourceMesh.vertices;
            for (var i = 0; i < vertices.Length; i++) vertices[i] = relative.MultiplyPoint3x4(vertices[i]);
            mesh.vertices = vertices;
            mesh.triangles = sourceMesh.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _generatedMeshes.Add(mesh);
            return mesh;
        }

        private static Vector3 Vector(float[] values, string field, MachineRuntimeObjectInstance instance,
            ResolvedRuntimeObjectDefinition definition, Object3DRuntimeNode node)
        {
            if (values == null || values.Length != 3) throw Failure(instance, definition, $"node '{node.name}' Collider {field} must contain exactly three values");
            return new Vector3(values[0], values[1], values[2]);
        }

        private static bool HasActiveBody(Object3DRuntimeNode node)
        {
            if (node == null) return false;
            var body = node.GetComponent("Rigidbody");
            if (body != null && body.rigidbody != null && body.rigidbody.mode != "Static") return true;
            var children = node.children ?? Array.Empty<Object3DRuntimeNode>();
            for (var i = 0; i < children.Length; i++) if (HasActiveBody(children[i])) return true;
            return false;
        }

        private static bool HasActiveBodyInAncestors(RuntimeMachine machine, string parentId)
        {
            var currentId = parentId;
            while (!string.IsNullOrEmpty(currentId))
            {
                var parent = machine.GetObject(currentId);
                for (var i = 0; i < parent.Nodes.Count; i++) if (parent.Nodes[i].Rigidbody != null) return true;
                currentId = parent.ParentId;
            }
            return false;
        }

        private static string CreateChildRuntimeId(string rootId, string nodeId)
        {
            if (string.IsNullOrEmpty(rootId) || string.IsNullOrEmpty(nodeId)) throw new InvalidOperationException("Prefab node runtime identity requires root and node IDs.");
            return $"@node:{rootId.Length}:{rootId}:{nodeId}";
        }

        private static Quaternion UpAxisRotation(string axis, MachineRuntimeObjectInstance instance, ResolvedRuntimeObjectDefinition definition)
        {
            if (axis == "Y") return Quaternion.identity;
            if (axis == "Z") return Quaternion.Euler(-90f, 0f, 0f);
            if (axis == "X") return Quaternion.Euler(0f, 0f, 90f);
            throw Failure(instance, definition, $"has unsupported up-axis '{axis}'");
        }

        private static void ApplyIntrinsicCorrection(Transform target, float modelScale, Quaternion rotation)
        {
            target.localScale = Vector3.one * modelScale;
            target.localRotation = rotation;
        }

        private static bool HasShear(Matrix4x4 matrix)
        {
            var x = new Vector3(matrix.m00, matrix.m10, matrix.m20);
            var y = new Vector3(matrix.m01, matrix.m11, matrix.m21);
            var z = new Vector3(matrix.m02, matrix.m12, matrix.m22);
            if (x.sqrMagnitude <= Mathf.Epsilon || y.sqrMagnitude <= Mathf.Epsilon || z.sqrMagnitude <= Mathf.Epsilon) return true;
            x.Normalize(); y.Normalize(); z.Normalize();
            return Mathf.Abs(Vector3.Dot(x, y)) > 0.0001f || Mathf.Abs(Vector3.Dot(x, z)) > 0.0001f || Mathf.Abs(Vector3.Dot(y, z)) > 0.0001f;
        }

        private static InvalidOperationException Failure(MachineRuntimeObjectInstance instance, ResolvedRuntimeObjectDefinition definition, string reason, Exception inner = null)
        {
            var name = definition != null && definition.Manifest != null ? $" ('{definition.Manifest.displayName}')" : string.Empty;
            return new InvalidOperationException($"Object3D instance '{instance.id}' failed for definition '{instance.definitionId}'{name}: {reason}.", inner);
        }

        public void Dispose()
        {
            if (_machine != null) _machine.DetachPrefabFactory(this);
            _machine = null;
            Destroy(_templatesRoot);
            _templatesRoot = null;
            _objectsRoot = null;
            _templates.Clear();
            foreach (var definition in _loadedModels.Values) if (definition != null) definition.Dispose();
            _loadedModels.Clear();
            for (var i = 0; i < _generatedMeshes.Count; i++) if (_generatedMeshes[i] != null) Destroy(_generatedMeshes[i]);
            _generatedMeshes.Clear();
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
        }

        private static void Destroy(GameObject gameObject)
        {
            if (gameObject == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(gameObject); else UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private readonly struct RuntimeObjectNodeTransform
        {
            public RuntimeObjectNodeTransform(Vector3 position, Quaternion rotation, Vector3 scale) { Position = position; Rotation = rotation; Scale = scale; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
        }

        private sealed class RuntimePrefabTemplate
        {
            public RuntimePrefabTemplate(string definitionId, ResolvedRuntimeObjectDefinition definition, GameObject root,
                IReadOnlyList<RuntimeObjectNode> sourceNodes)
            {
                DefinitionId = definitionId;
                Definition = definition;
                Root = root;
                Nodes = new List<TemplateNode>(sourceNodes.Count);
                for (var i = 0; i < sourceNodes.Count; i++)
                {
                    var node = sourceNodes[i];
                    Nodes.Add(new TemplateNode(node.PrefabNodeId, node.Name, node.AuthoredTransform,
                        TransformPath(root.transform, node.Transform),
                        node.Collider != null ? TransformPath(root.transform, node.Collider.transform) : null,
                        node.Rigidbody != null ? TransformPath(root.transform, node.Rigidbody.transform) : null,
                        i == 0));
                }
            }

            public string DefinitionId { get; }
            public ResolvedRuntimeObjectDefinition Definition { get; }
            public GameObject Root { get; }
            public List<TemplateNode> Nodes { get; }
        }

        private sealed class TemplateNode
        {
            public TemplateNode(string prefabNodeId, string name, Object3DRuntimeTransform authoredTransform,
                int[] nodePath, int[] colliderPath, int[] bodyPath, bool isRoot)
            {
                PrefabNodeId = prefabNodeId;
                Name = name;
                AuthoredTransform = authoredTransform;
                NodePath = nodePath;
                ColliderPath = colliderPath;
                BodyPath = bodyPath;
                IsRoot = isRoot;
            }
            public string PrefabNodeId { get; }
            public string Name { get; }
            public Object3DRuntimeTransform AuthoredTransform { get; }
            public int[] NodePath { get; }
            public int[] ColliderPath { get; }
            public int[] BodyPath { get; }
            public bool IsRoot { get; }
        }

        private static int[] TransformPath(Transform root, Transform item)
        {
            var result = new List<int>();
            var current = item;
            while (current != null && current != root)
            {
                result.Add(current.GetSiblingIndex());
                current = current.parent;
            }
            if (current != root) throw new InvalidOperationException("Prefab template node is outside its template root.");
            result.Reverse();
            return result.ToArray();
        }

        private static Transform ResolvePath(Transform root, int[] path)
        {
            var current = root;
            for (var i = 0; i < path.Length; i++) current = current.GetChild(path[i]);
            return current;
        }
    }
}
