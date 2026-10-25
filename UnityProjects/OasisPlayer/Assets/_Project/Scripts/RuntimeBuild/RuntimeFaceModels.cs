using System;
using System.Collections.Generic;
using Oasis.Scripting;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public interface IRuntimePrefabFactory
    {
        RuntimeObjectInstance Instantiate(RuntimeMachine machine, string runtimeId, string prefabAlias, RuntimePose machinePose);
    }

    public sealed class RuntimeMachine
    {
        public const int MaxLiveObjects = 256;
        public const int MaxLiveNodes = 2048;
        public const int MaxLiveBodies = 256;
        public const int MaxSpawnsPerScriptEvent = 128;
        public const int MaxSpawnAllocations = 100000;
        private readonly Dictionary<string, RuntimeObjectInstance> _objects = new Dictionary<string, RuntimeObjectInstance>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeObjectNode> _objectNodes = new Dictionary<string, RuntimeObjectNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeAnchor> _anchors = new Dictionary<string, RuntimeAnchor>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeTrigger> _triggers = new Dictionary<string, RuntimeTrigger>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeInput> _inputs = new Dictionary<string, RuntimeInput>(StringComparer.Ordinal);
        private readonly Dictionary<RuntimeObjectIdentity, string> _objectIdentities = new Dictionary<RuntimeObjectIdentity, string>();
        private readonly List<RuntimeFace> _faces = new List<RuntimeFace>();
        private readonly List<string> _warnings = new List<string>();
        private RuntimeLampStateTexture _lampStateTexture;
        private RuntimeSegmentDisplayRenderer _segmentDisplayRenderer;
        private IDisposable _behaviorRuntime;
        private readonly List<RuntimeCabinetReflectionBinding> _cabinetReflectionBindings = new List<RuntimeCabinetReflectionBinding>();
        private readonly List<RuntimeTextureAsset> _cabinetReflectionTextures = new List<RuntimeTextureAsset>();
        private readonly HashSet<string> _dynamicObjectIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly string _sessionId = Guid.NewGuid().ToString("N");
        private long _generation = 1;
        private long _nextSpawnId;
        private int _spawnAllocations;
        private int _scriptDispatchDepth;
        private int _spawnsThisScriptEvent;
        private IRuntimePrefabFactory _prefabFactory;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private RuntimeReelLampDiagnosticMode _reelLampDiagnosticMode = RuntimeReelLampDiagnosticMode.FollowLampState;
        private float _reelLampDiagnosticMultiplier = 1f;
#endif

        public RuntimeMachine(ResolvedRuntimeBuild build, GameObject cabinet)
        {
            Build = build;
            Cabinet = cabinet;
            LampState = new RuntimeLampState();
            ReelState = new RuntimeReelState();
            SegmentDisplayState = new RuntimeSegmentDisplayState();
            Events = new RuntimeMachineEventDispatcher();
            Timers = new RuntimeTimerService(Events);
            Commands = new RuntimeMachineCommandService(this);
            Queries = new RuntimeMachineQueryService(this);
            foreach (var declaration in build.Machine.anchors) RegisterAnchor(new RuntimeAnchor(declaration.id, declaration.displayName, declaration.position.Value, declaration.rotationEulerDegrees.Value));
            foreach (var declaration in build.Machine.inputs ?? Array.Empty<MachineInputDefinition>()) RegisterInput(declaration);
        }

        public ResolvedRuntimeBuild Build { get; private set; }
        public GameObject Cabinet { get; private set; }
        public IReadOnlyList<RuntimeFace> Faces { get { return _faces; } }
        public IReadOnlyDictionary<string, RuntimeObjectInstance> Objects { get { return _objects; } }
        public IReadOnlyDictionary<string, RuntimeObjectNode> ObjectNodes { get { return _objectNodes; } }
        public IReadOnlyDictionary<string, RuntimeAnchor> Anchors { get { return _anchors; } }
        public IReadOnlyDictionary<string, RuntimeTrigger> Triggers { get { return _triggers; } }
        public IReadOnlyDictionary<string, RuntimeInput> Inputs { get { return _inputs; } }
        public RuntimeMachineEventDispatcher Events { get; private set; }
        public RuntimeMachineCommandService Commands { get; private set; }
        public RuntimeMachineQueryService Queries { get; private set; }
        public Transform MachineSpace { get; private set; }
        public RuntimeTimerService Timers { get; private set; }
        public bool IsActive { get; private set; } = true;
        public string SessionId { get { return _sessionId; } }
        public long Generation { get { return _generation; } }
        public bool HasStarted { get; private set; }
        public RuntimeFabricBehavior FabricBehavior { get; internal set; }
        public string EmulationFault { get; internal set; }
        public IReadOnlyList<string> Warnings { get { return _warnings; } }
        public IReadOnlyList<RuntimeCabinetReflectionBinding> CabinetReflectionBindings { get { return _cabinetReflectionBindings; } }
        public RuntimeLampState LampState { get; private set; }
        public RuntimeReelState ReelState { get; private set; }
        public RuntimeSegmentDisplayState SegmentDisplayState { get; private set; }
        public RuntimeLampStateTexture LampStateTexture
        {
            get
            {
                if (_lampStateTexture == null) _lampStateTexture = new RuntimeLampStateTexture(LampState);
                return _lampStateTexture;
            }
        }

        public bool ApplyDynamicState()
        {
            var changed = false;
            if (_lampStateTexture != null) changed |= _lampStateTexture.Upload(LampState);
            if (_segmentDisplayRenderer != null) changed |= _segmentDisplayRenderer.ApplyDynamicState(this);
            foreach (var face in _faces)
            {
                for (var i = 0; i < face.ReelRenderBindings.Count; i++)
                {
                    changed |= face.ReelRenderBindings[i].ApplyReelState(ReelState);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    changed |= face.ReelRenderBindings[i].ApplyLampState(LampState, _reelLampDiagnosticMode, _reelLampDiagnosticMultiplier);
#else
                    changed |= face.ReelRenderBindings[i].ApplyLampState(LampState);
#endif
                }
            }
            return changed;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SetReelLampDiagnostics(RuntimeReelLampDiagnosticMode mode, float multiplier)
        {
            _reelLampDiagnosticMode = mode;
            _reelLampDiagnosticMultiplier = Mathf.Clamp(multiplier, 1f, 20f);
        }
#endif

        public void SetSegmentDisplayRenderer(RuntimeSegmentDisplayRenderer renderer)
        {
            _segmentDisplayRenderer = renderer;
        }

        public void RegisterFace(RuntimeFace face)
        {
            if (face != null) _faces.Add(face);
        }

        public void RegisterObject(RuntimeObjectInstance instance)
        {
            RegisterObjectCore(instance, false);
        }

        private void RegisterObjectCore(RuntimeObjectInstance instance, bool dynamic)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrWhiteSpace(instance.Id)) throw new ArgumentException("Runtime Object3D instance ID is required.", nameof(instance));
            if (_objects.ContainsKey(instance.Id)) throw new InvalidOperationException($"Runtime Object3D instance ID '{instance.Id}' is already registered.");
            var nodes = instance.Nodes;
            if (_objects.Count + 1 > MaxLiveObjects) throw new InvalidOperationException($"Machine live Object3D limit ({MaxLiveObjects}) was exceeded.");
            var nodeCount = nodes.Count;
            var bodyCount = instance.Rigidbody != null ? 1 : 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null) throw new ArgumentException("Runtime Object3D node registry contains a null node.", nameof(instance));
                if (_objectNodes.ContainsKey(nodes[i].RuntimeId)) throw new InvalidOperationException($"Runtime Object3D node ID '{nodes[i].RuntimeId}' is already registered.");
                for (var j = 0; j < i; j++) if (nodes[j].RuntimeId == nodes[i].RuntimeId) throw new InvalidOperationException($"Runtime Object3D node ID '{nodes[i].RuntimeId}' is duplicated.");
                if (nodes[i].Rigidbody != null && (i != 0 || nodes[i].Rigidbody != instance.Rigidbody)) bodyCount++;
            }
            if (_objectNodes.Count + nodeCount > MaxLiveNodes) throw new InvalidOperationException($"Machine live Object3D node limit ({MaxLiveNodes}) was exceeded.");
            var registeredBodies = 0;
            foreach (var obj in _objects.Values)
            {
                if (obj.Nodes.Count == 0) { if (obj.Rigidbody != null) registeredBodies++; }
                else for (var i = 0; i < obj.Nodes.Count; i++) if (obj.Nodes[i].Rigidbody != null) registeredBodies++;
            }
            if (registeredBodies + bodyCount > MaxLiveBodies) throw new InvalidOperationException($"Machine live Rigidbody limit ({MaxLiveBodies}) was exceeded.");
            _objects.Add(instance.Id, instance);
            RuntimeObjectIdentity identity = null;
            try
            {
                for (var i = 0; i < nodes.Count; i++) _objectNodes.Add(nodes[i].RuntimeId, nodes[i]);
                identity = instance.Root.AddComponent<RuntimeObjectIdentity>();
                identity.Initialize(this, instance.Id);
                _objectIdentities.Add(identity, instance.Id);
                if (dynamic && !_dynamicObjectIds.Add(instance.Id)) throw new InvalidOperationException($"Runtime Object3D instance ID '{instance.Id}' is already registered as a dynamic object.");
            }
            catch
            {
                _objects.Remove(instance.Id);
                for (var i = 0; i < nodes.Count; i++) _objectNodes.Remove(nodes[i].RuntimeId);
                if (identity != null) _objectIdentities.Remove(identity);
                _dynamicObjectIds.Remove(instance.Id);
                throw;
            }
        }

        public void AttachPrefabFactory(IRuntimePrefabFactory factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (_prefabFactory != null) throw new InvalidOperationException("Runtime Prefab factory is already attached.");
            _prefabFactory = factory;
        }

        public void DetachPrefabFactory(IRuntimePrefabFactory factory)
        {
            if (ReferenceEquals(_prefabFactory, factory)) _prefabFactory = null;
        }

        public OasisScriptReferenceValue GetObjectReference(string objectId)
        {
            if (!IsActive) throw new InvalidOperationException("Machine session has been unloaded.");
            if (!TryGetObject(objectId, out _)) throw new KeyNotFoundException($"Runtime Object3D instance '{objectId}' is not registered.");
            return OasisScriptReferenceValue.OasisObject(objectId, _sessionId, _generation);
        }

        public RuntimeObjectInstance InstantiatePrefab(string alias, RuntimePose pose)
        {
            if (!IsActive) throw new InvalidOperationException("Machine session has been unloaded.");
            if (_prefabFactory == null) throw new InvalidOperationException("Runtime Prefab factory is unavailable.");
            if (Build.PrefabDependencies == null || !Build.PrefabDependencies.ContainsKey(alias ?? string.Empty))
                throw new KeyNotFoundException($"Prefab dependency '{alias}' is not declared for this Machine.");
            if (_objects.Count >= MaxLiveObjects) throw new InvalidOperationException($"Machine live Object3D limit ({MaxLiveObjects}) was reached.");
            if (_spawnAllocations >= MaxSpawnAllocations) throw new InvalidOperationException($"Machine spawn allocation limit ({MaxSpawnAllocations}) was reached.");
            if (_scriptDispatchDepth > 0 && _spawnsThisScriptEvent >= MaxSpawnsPerScriptEvent)
                throw new InvalidOperationException($"Script event spawn limit ({MaxSpawnsPerScriptEvent}) was reached.");
            var definition = Build.PrefabDependencies[alias];
            var nodeCost = CountPrefabNodes(definition.Manifest.root);
            if (_objectNodes.Count + nodeCost > MaxLiveNodes) throw new InvalidOperationException($"Machine live Object3D node limit ({MaxLiveNodes}) would be exceeded by Prefab '{alias}'.");
            var bodyCost = CountActiveBodies(definition.Manifest.root);
            if (bodyCost > 1) throw new InvalidOperationException($"Prefab '{alias}' has nested active Rigidbodies, which are unsupported.");
            if (CountLiveBodies() + bodyCost > MaxLiveBodies) throw new InvalidOperationException($"Machine live Rigidbody limit ({MaxLiveBodies}) would be exceeded by Prefab '{alias}'.");
            var id = "@spawn:" + (++_nextSpawnId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _spawnAllocations++;
            if (_scriptDispatchDepth > 0) _spawnsThisScriptEvent++;
            RuntimeObjectInstance instance = null;
            var registered = false;
            try
            {
                instance = _prefabFactory.Instantiate(this, id, alias, pose);
                RegisterObjectCore(instance, true);
                registered = true;
                instance.Root.SetActive(true);
                return instance;
            }
            catch
            {
                if (registered) DestroyDynamicObject(id);
                else if (instance != null) instance.Destroy();
                throw;
            }
        }

        public bool IsDynamicObject(string objectId) { return _dynamicObjectIds.Contains(objectId ?? string.Empty); }

        private int CountLiveBodies()
        {
            var count = 0;
            foreach (var instance in _objects.Values)
            {
                if (instance.Nodes.Count == 0) { if (instance.Rigidbody != null) count++; }
                else for (var i = 0; i < instance.Nodes.Count; i++) if (instance.Nodes[i].Rigidbody != null) count++;
            }
            return count;
        }

        private static int CountPrefabNodes(Object3DRuntimeNode node)
        {
            if (node == null) return 0;
            var count = 1;
            var children = node.children ?? Array.Empty<Object3DRuntimeNode>();
            for (var i = 0; i < children.Length; i++) count += CountPrefabNodes(children[i]);
            return count;
        }

        private static int CountActiveBodies(Object3DRuntimeNode node)
        {
            if (node == null) return 0;
            var component = node.GetComponent("Rigidbody");
            var count = component != null && component.rigidbody != null && component.rigidbody.mode != "Static" ? 1 : 0;
            var children = node.children ?? Array.Empty<Object3DRuntimeNode>();
            for (var i = 0; i < children.Length; i++) count += CountActiveBodies(children[i]);
            return count;
        }

        public bool DestroyDynamicObject(string objectId)
        {
            if (!_dynamicObjectIds.Remove(objectId ?? string.Empty)) return false;
            if (!_objects.TryGetValue(objectId, out var instance)) return false;
            _objects.Remove(objectId);
            for (var i = 0; i < instance.Nodes.Count; i++) _objectNodes.Remove(instance.Nodes[i].RuntimeId);
            var identities = new List<RuntimeObjectIdentity>();
            foreach (var item in _objectIdentities) if (item.Value == objectId) identities.Add(item.Key);
            for (var i = 0; i < identities.Count; i++) _objectIdentities.Remove(identities[i]);
            instance.Destroy();
            return true;
        }

        public bool IsLiveObjectReference(OasisScriptReferenceValue reference)
        {
            return IsActive && reference != null && reference.IsLiveObject &&
                reference.Type.Equals(OasisScriptType.OasisObjectRef) && reference.SessionId == _sessionId &&
                reference.Generation == _generation && _objects.ContainsKey(reference.Id);
        }

        public void BeginScriptEvent()
        {
            if (_scriptDispatchDepth++ == 0) _spawnsThisScriptEvent = 0;
        }

        public void EndScriptEvent()
        {
            if (_scriptDispatchDepth > 0) _scriptDispatchDepth--;
            if (_scriptDispatchDepth == 0) _spawnsThisScriptEvent = 0;
        }

        public void SetMachineSpace(Transform machineSpace)
        {
            if (machineSpace == null) throw new ArgumentNullException(nameof(machineSpace));
            MachineSpace = machineSpace;
        }

        public bool TryGetObject(string id, out RuntimeObjectInstance instance)
        {
            return _objects.TryGetValue(id ?? string.Empty, out instance);
        }

        public RuntimeObjectInstance GetObject(string id)
        {
            if (!TryGetObject(id, out var instance)) throw new KeyNotFoundException($"Runtime Object3D instance '{id}' is not registered.");
            return instance;
        }

        public bool TryGetObjectNode(string runtimeNodeId, out RuntimeObjectNode node) { return _objectNodes.TryGetValue(runtimeNodeId ?? string.Empty, out node); }
        public RuntimeObjectNode GetObjectNode(string runtimeNodeId)
        {
            if (!TryGetObjectNode(runtimeNodeId, out var node)) throw new KeyNotFoundException($"Runtime Object3D node '{runtimeNodeId}' is not registered.");
            return node;
        }

        public void RegisterAnchor(RuntimeAnchor anchor)
        {
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            if (string.IsNullOrWhiteSpace(anchor.Id)) throw new ArgumentException("Runtime anchor ID is required.", nameof(anchor));
            if (_anchors.ContainsKey(anchor.Id)) throw new InvalidOperationException($"Runtime anchor ID '{anchor.Id}' is already registered.");
            _anchors.Add(anchor.Id, anchor);
        }

        public bool TryGetAnchor(string id, out RuntimeAnchor anchor) { return _anchors.TryGetValue(id ?? string.Empty, out anchor); }

        public RuntimeAnchor GetAnchor(string id)
        {
            if (!TryGetAnchor(id, out var anchor)) throw new KeyNotFoundException($"Runtime anchor '{id}' is not registered.");
            return anchor;
        }

        public void RegisterTrigger(RuntimeTrigger trigger)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            if (!RuntimeIdentity.IsValid(trigger.Id)) throw new ArgumentException($"Cabinet trigger ID '{trigger.Id}' is empty or invalid.", nameof(trigger));
            if (_triggers.ContainsKey(trigger.Id)) throw new InvalidOperationException($"Duplicate Cabinet trigger ID '{trigger.Id}'.");
            _triggers.Add(trigger.Id, trigger);
        }

        public bool TryGetTrigger(string id, out RuntimeTrigger trigger) { return _triggers.TryGetValue(id ?? string.Empty, out trigger); }
        public RuntimeTrigger GetTrigger(string id)
        {
            if (!TryGetTrigger(id, out var trigger)) throw new KeyNotFoundException($"Runtime trigger '{id}' is not registered.");
            return trigger;
        }

        public bool SetInputState(string inputId, bool pressed)
        {
            if (!_inputs.TryGetValue(inputId ?? string.Empty, out var input)) throw new KeyNotFoundException($"Runtime input '{inputId}' is not registered.");
            if (input.IsPressed == pressed) return false;
            input.IsPressed = pressed;
            Events.Publish(pressed ? (RuntimeMachineEvent)new RuntimeInputPressedEvent(input.Id) : new RuntimeInputReleasedEvent(input.Id));
            return true;
        }

        public void CompleteStartup()
        {
            if (HasStarted) return;
            HasStarted = true;
            Events.Publish(new RuntimeMachineStartedEvent());
        }

        internal void AttachBehaviorRuntime(IDisposable behaviorRuntime)
        {
            if (behaviorRuntime == null) throw new ArgumentNullException(nameof(behaviorRuntime));
            if (!IsActive || HasStarted || _behaviorRuntime != null)
                throw new InvalidOperationException("Machine behaviour must attach once, before startup and unload.");
            _behaviorRuntime = behaviorRuntime;
        }

        internal bool TryGetObjectIdentity(RuntimeObjectIdentity identity, out string objectId)
        {
            objectId = null;
            return identity != null && _objectIdentities.TryGetValue(identity, out objectId);
        }

        private void RegisterInput(MachineInputDefinition declaration)
        {
            if (declaration == null || !RuntimeIdentity.IsValid(declaration.id))
                throw new InvalidOperationException($"Machine input '{(declaration != null ? declaration.name : string.Empty)}' has invalid logical ID '{(declaration != null ? declaration.id : string.Empty)}'.");
            var id = declaration.id;
            if (_inputs.ContainsKey(id)) throw new InvalidOperationException($"Runtime input ID '{id}' is already registered.");
            _inputs.Add(id, new RuntimeInput(id, declaration.name));
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning)) _warnings.Add(warning);
        }

        public void AddCabinetReflectionBinding(RuntimeCabinetReflectionBinding binding) { if (binding != null) _cabinetReflectionBindings.Add(binding); }
        public void AddCabinetReflectionTexture(RuntimeTextureAsset texture) { if (texture != null) _cabinetReflectionTextures.Add(texture); }

        public void UnloadAssets()
        {
            IsActive = false;
            _generation++;
            if (_behaviorRuntime != null)
            {
                _behaviorRuntime.Dispose();
                _behaviorRuntime = null;
            }
            Timers.Clear();
            Events.Clear();
            for (var i = 0; i < _cabinetReflectionBindings.Count; i++) _cabinetReflectionBindings[i].Dispose();
            _cabinetReflectionBindings.Clear();
            for (var i = 0; i < _cabinetReflectionTextures.Count; i++) _cabinetReflectionTextures[i].Unload();
            _cabinetReflectionTextures.Clear();
            foreach (var face in _faces)
            {
                face.UnloadAssets();
            }

            if (_lampStateTexture != null)
            {
                _lampStateTexture.Dispose();
                _lampStateTexture = null;
            }

            _faces.Clear();
            foreach (var instance in _objects.Values) instance.Destroy();
            _objects.Clear();
            _objectNodes.Clear();
            _objectIdentities.Clear();
            _dynamicObjectIds.Clear();
            _prefabFactory = null;
            MachineSpace = null;
            _anchors.Clear();
            _triggers.Clear();
            _inputs.Clear();
            _warnings.Clear();
        }
    }

    /// <summary>Lightweight Machine-space anchor data. It deliberately has no Unity GameObject or Transform.</summary>
    public sealed class RuntimeAnchor
    {
        public RuntimeAnchor(string id, string displayName, Vector3 position, Vector3 rotationEulerDegrees) : this(id, displayName, RuntimeVector3.FromUnity(position), RuntimeVector3.FromUnity(rotationEulerDegrees)) { }
        public RuntimeAnchor(string id, string displayName, RuntimeVector3 position, RuntimeVector3 rotationEulerDegrees) { Id = id; DisplayName = displayName; Position = position; RotationEulerDegrees = rotationEulerDegrees; }
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public RuntimeVector3 Position { get; private set; }
        public RuntimeVector3 RotationEulerDegrees { get; private set; }
    }

    public sealed class RuntimeObjectInstance
    {
        public RuntimeObjectInstance(string id, string displayName, string definitionId, ResolvedRuntimeObjectDefinition definition,
            GameObject root, Collider collider, Rigidbody rigidbody, MachineRuntimeObjectTransform authoredInitialTransform)
            : this(id, displayName, definitionId, definition, root, collider, rigidbody, authoredInitialTransform, null, string.Empty, true)
        {
        }

        public RuntimeObjectInstance(string id, string displayName, string definitionId, ResolvedRuntimeObjectDefinition definition,
            GameObject root, Collider collider, Rigidbody rigidbody, MachineRuntimeObjectTransform authoredInitialTransform,
            IReadOnlyList<RuntimeObjectNode> nodes, string parentId, bool initiallyActive)
        {
            Id = id;
            DisplayName = displayName;
            DefinitionId = definitionId;
            Definition = definition;
            Root = root;
            Collider = collider;
            Rigidbody = rigidbody;
            AuthoredInitialTransform = authoredInitialTransform;
            Nodes = nodes ?? Array.Empty<RuntimeObjectNode>();
            ParentId = parentId ?? string.Empty;
            InitiallyActive = initiallyActive;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string DefinitionId { get; private set; }
        public ResolvedRuntimeObjectDefinition Definition { get; private set; }
        public GameObject Root { get; private set; }
        public Collider Collider { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        public MachineRuntimeObjectTransform AuthoredInitialTransform { get; private set; }
        public IReadOnlyList<RuntimeObjectNode> Nodes { get; private set; }
        public string ParentId { get; private set; }
        public bool InitiallyActive { get; private set; }

        internal void Destroy()
        {
            if (Root == null) return;
            // Unity destruction is deferred in Play Mode. Remove the physical object
            // immediately so a new game cannot collide with the previous generation.
            Root.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
            Root = null;
        }
    }

    /// <summary>A stable Prefab node projection. Child runtime IDs derive only from root and asset node IDs.</summary>
    public sealed class RuntimeObjectNode
    {
        public RuntimeObjectNode(string runtimeId, string rootObjectId, string prefabNodeId, string name,
            GameObject gameObject, Collider collider, Rigidbody rigidbody, Object3DRuntimeTransform authoredTransform,
            MachineRuntimeObjectTransform initialTransform = null, bool initiallyActive = true)
        {
            RuntimeId = runtimeId;
            RootObjectId = rootObjectId;
            PrefabNodeId = prefabNodeId;
            Name = name;
            GameObject = gameObject;
            Collider = collider;
            Rigidbody = rigidbody;
            AuthoredTransform = authoredTransform;
            InitialTransform = initialTransform ?? new MachineRuntimeObjectTransform
            {
                position = authoredTransform != null ? authoredTransform.position : new RuntimeVector3Definition(),
                rotationEulerDegrees = authoredTransform != null ? authoredTransform.rotationEulerDegrees : new RuntimeVector3Definition(),
                scale = authoredTransform != null ? authoredTransform.scale : new RuntimeVector3Definition { x = 1f, y = 1f, z = 1f }
            };
            InitiallyActive = initiallyActive;
        }

        public string RuntimeId { get; private set; }
        public string RootObjectId { get; private set; }
        public string PrefabNodeId { get; private set; }
        public string Name { get; private set; }
        public GameObject GameObject { get; private set; }
        public Transform Transform { get { return GameObject != null ? GameObject.transform : null; } }
        public Collider Collider { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        public Object3DRuntimeTransform AuthoredTransform { get; private set; }
        public MachineRuntimeObjectTransform InitialTransform { get; private set; }
        public bool InitiallyActive { get; private set; }
    }

    public sealed class RuntimeFace
    {
        public RuntimeFace(MachineRuntimeFaceReference reference, FaceRuntimeManifest manifest, Transform cabinetTarget, RuntimeTextureAsset artwork, RuntimeTextureAsset mask)
            : this(reference, manifest, cabinetTarget, artwork, mask, null, null, null)
        {
        }

        public RuntimeFace(MachineRuntimeFaceReference reference, FaceRuntimeManifest manifest, Transform cabinetTarget, RuntimeTextureAsset artwork, RuntimeTextureAsset mask, RuntimeTextureAsset trayId, RuntimeTextureAsset lampIds0, RuntimeTextureAsset lampWeights0)
        {
            Reference = reference;
            Manifest = manifest;
            CabinetTarget = cabinetTarget;
            Artwork = artwork;
            Mask = mask;
            TrayId = trayId;
            LampIds0 = lampIds0;
            LampWeights0 = lampWeights0;
        }

        public MachineRuntimeFaceReference Reference { get; private set; }
        public FaceRuntimeManifest Manifest { get; private set; }
        public Transform CabinetTarget { get; private set; }
        public RuntimeTextureAsset Artwork { get; private set; }
        public RuntimeTextureAsset Mask { get; private set; }
        public RuntimeTextureAsset TrayId { get; private set; }
        public RuntimeTextureAsset LampIds0 { get; private set; }
        public RuntimeTextureAsset LampWeights0 { get; private set; }
        public RuntimeFaceRenderBinding RenderBinding { get; private set; }
        private readonly List<RuntimeReelRenderBinding> _reelRenderBindings = new List<RuntimeReelRenderBinding>();
        public IReadOnlyList<RuntimeReelRenderBinding> ReelRenderBindings { get { return _reelRenderBindings; } }

        public void SetRenderBinding(RuntimeFaceRenderBinding renderBinding)
        {
            RenderBinding = renderBinding;
        }

        public void AddReelRenderBinding(RuntimeReelRenderBinding binding)
        {
            if (binding != null) _reelRenderBindings.Add(binding);
        }

        public void UnloadAssets()
        {
            for (var i = 0; i < _reelRenderBindings.Count; i++)
            {
                _reelRenderBindings[i].Dispose();
            }
            _reelRenderBindings.Clear();

            if (RenderBinding != null)
            {
                RenderBinding.Dispose();
                RenderBinding = null;
            }

            if (Artwork != null) Artwork.Unload();
            if (Mask != null) Mask.Unload();
            if (TrayId != null) TrayId.Unload();
            if (LampIds0 != null) LampIds0.Unload();
            if (LampWeights0 != null) LampWeights0.Unload();
            var reels = Manifest != null && Manifest.reels != null ? Manifest.reels : new FaceRuntimeReelManifestEntry[0];
            foreach (var reel in reels)
            {
                if (reel != null && reel.BandTexture != null) reel.BandTexture.Unload();
                if (reel != null && reel.TransmissionMaskTexture != null) reel.TransmissionMaskTexture.Unload();
            }
        }
    }

    public sealed class RuntimeTextureAsset
    {
        public RuntimeTextureAsset(string path, Texture2D texture)
        {
            Path = path;
            Texture = texture;
        }

        public string Path { get; private set; }
        public Texture2D Texture { get; private set; }

        public void Unload()
        {
            if (Texture != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Texture);
                else UnityEngine.Object.DestroyImmediate(Texture);
                Texture = null;
            }
        }
    }
}
