using System;
using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public sealed class RuntimeMachine
    {
        private readonly Dictionary<string, RuntimeObjectInstance> _objects = new Dictionary<string, RuntimeObjectInstance>(StringComparer.Ordinal);
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
            foreach (var declaration in build.Machine.anchors) RegisterAnchor(new RuntimeAnchor(declaration.id, declaration.displayName, declaration.position.Value, declaration.rotationEulerDegrees.Value));
            foreach (var declaration in build.Machine.inputs ?? Array.Empty<MachineInputDefinition>()) RegisterInput(declaration);
        }

        public ResolvedRuntimeBuild Build { get; private set; }
        public GameObject Cabinet { get; private set; }
        public IReadOnlyList<RuntimeFace> Faces { get { return _faces; } }
        public IReadOnlyDictionary<string, RuntimeObjectInstance> Objects { get { return _objects; } }
        public IReadOnlyDictionary<string, RuntimeAnchor> Anchors { get { return _anchors; } }
        public IReadOnlyDictionary<string, RuntimeTrigger> Triggers { get { return _triggers; } }
        public IReadOnlyDictionary<string, RuntimeInput> Inputs { get { return _inputs; } }
        public RuntimeMachineEventDispatcher Events { get; private set; }
        public RuntimeMachineCommandService Commands { get; private set; }
        public RuntimeTimerService Timers { get; private set; }
        public bool IsActive { get; private set; } = true;
        public bool HasStarted { get; private set; }
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
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrWhiteSpace(instance.Id)) throw new ArgumentException("Runtime Object3D instance ID is required.", nameof(instance));
            if (_objects.ContainsKey(instance.Id)) throw new InvalidOperationException($"Runtime Object3D instance ID '{instance.Id}' is already registered.");
            _objects.Add(instance.Id, instance);
            var identity = instance.Root.AddComponent<RuntimeObjectIdentity>();
            identity.Initialize(this, instance.Id);
            _objectIdentities.Add(identity, instance.Id);
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
            _objectIdentities.Clear();
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
        {
            Id = id;
            DisplayName = displayName;
            DefinitionId = definitionId;
            Definition = definition;
            Root = root;
            Collider = collider;
            Rigidbody = rigidbody;
            AuthoredInitialTransform = authoredInitialTransform;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string DefinitionId { get; private set; }
        public ResolvedRuntimeObjectDefinition Definition { get; private set; }
        public GameObject Root { get; private set; }
        public Collider Collider { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        public MachineRuntimeObjectTransform AuthoredInitialTransform { get; private set; }

        internal void Destroy()
        {
            if (Root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
            Root = null;
        }
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
