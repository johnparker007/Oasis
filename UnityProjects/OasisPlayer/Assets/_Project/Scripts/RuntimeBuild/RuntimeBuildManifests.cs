using System;
using Oasis.Scripting;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public static class FaceRuntimeContract
    {
        public const int SchemaVersion = 10;

        public static bool IsSupportedSchemaVersion(int schemaVersion)
        {
            return schemaVersion == SchemaVersion;
        }
    }

    [Serializable]
    public sealed class MachineRuntimeManifest
    {
        public string schema = string.Empty;
        public int schemaVersion;
        public string machineId = string.Empty;
        public string displayName = string.Empty;
        public string cabinetManifest = string.Empty;
        public MachineRuntimeFaceReference[] faces = Array.Empty<MachineRuntimeFaceReference>();
        public MachineRuntimeObjectInstance[] objectInstances = Array.Empty<MachineRuntimeObjectInstance>();
        public MachineRuntimePrefabDependency[] prefabDependencies = Array.Empty<MachineRuntimePrefabDependency>();
        public MachineRuntimeGeometrySurface[] geometrySurfaces = Array.Empty<MachineRuntimeGeometrySurface>();
        public MachineRuntimeAnchor[] anchors;
        public MachineRuntimeManifestDefinition runtime;
        public MachineInteractionDefinition interaction = new MachineInteractionDefinition();
        public MachineInputDefinition[] inputs = Array.Empty<MachineInputDefinition>();
    }

    [Serializable] public sealed class MachineInteractionDefinition
    {
        public string kind = "Buttons";
        public string cuePrefabAlias;
        public string playingSurfaceId;
        public void Validate(MachineRuntimeManifest machine)
        {
            if (kind == "Buttons")
            {
                if (!string.IsNullOrEmpty(cuePrefabAlias) || !string.IsNullOrEmpty(playingSurfaceId)) throw new InvalidDataException("Buttons interaction cannot contain Pool parameters.");
                return;
            }
            if (kind != "Pool") throw new InvalidDataException("Unknown interaction profile: " + kind);
            if (machine.prefabDependencies == null || !Array.Exists(machine.prefabDependencies, p => p != null && p.alias == cuePrefabAlias) ||
                machine.geometrySurfaces == null || !Array.Exists(machine.geometrySurfaces, p => p != null && p.id == playingSurfaceId))
                throw new InvalidDataException("Pool interaction requires declared cue Prefab and playing surface references.");
        }
    }

    [Serializable] public sealed class RuntimeVector3Definition { public float x; public float y; public float z; public Vector3 Value { get { return new Vector3(x, y, z); } } }
    [Serializable] public sealed class MachineRuntimeObjectTransform { public RuntimeVector3Definition position = new RuntimeVector3Definition(); public RuntimeVector3Definition rotationEulerDegrees = new RuntimeVector3Definition(); public RuntimeVector3Definition scale = new RuntimeVector3Definition(); }
    [Serializable] public sealed class MachineRuntimeObjectInstance { public string id = string.Empty; public string displayName = string.Empty; public string definitionId = string.Empty; public string definitionManifest = string.Empty; public MachineRuntimeObjectTransform transform = new MachineRuntimeObjectTransform(); public string parentId = string.Empty; public bool active = true; }
    [Serializable] public sealed class MachineRuntimePrefabDependency { public string alias = string.Empty; public string definitionId = string.Empty; public string definitionManifest = string.Empty; }
    [Serializable] public sealed class MachineRuntimeGeometrySurface { public string id = string.Empty; public RuntimeVector3Definition position; public RuntimeVector3Definition rotationEulerDegrees; public float width; public float length; }
    [Serializable] public sealed class MachineRuntimeAnchor { public string id = string.Empty; public string displayName = string.Empty; public RuntimeVector3Definition position = new RuntimeVector3Definition(); public RuntimeVector3Definition rotationEulerDegrees = new RuntimeVector3Definition(); }
    [Serializable] public sealed class Object3DRuntimeTransform { public RuntimeVector3Definition position = new RuntimeVector3Definition(); public RuntimeVector3Definition rotationEulerDegrees = new RuntimeVector3Definition(); public RuntimeVector3Definition scale = new RuntimeVector3Definition(); }
    [Serializable] public sealed class Object3DRuntimeModel { public string path = string.Empty; public float scale; public string upAxis = string.Empty; }
    [Serializable] public sealed class Object3DRuntimeCollider { public string kind = string.Empty; public float[] center = Array.Empty<float>(); public float radius; public float[] size = Array.Empty<float>(); public float height; public string axis = string.Empty; public string sourceFrame = string.Empty; public bool isTrigger; }
    [Serializable] public sealed class Object3DRuntimeRigidbody { public string mode = string.Empty; public float mass; public bool useGravity; }
    [Serializable] public sealed class Object3DRuntimeComponent { public string componentId = string.Empty; public string kind = string.Empty; public int contractVersion; public Object3DRuntimeTransform transform; public Object3DRuntimeModel model; public Object3DRuntimeCollider collider; public Object3DRuntimeRigidbody rigidbody; }
    [Serializable] public sealed class Object3DRuntimeNode
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public Object3DRuntimeComponent[] components = Array.Empty<Object3DRuntimeComponent>();
        public Object3DRuntimeNode[] children = Array.Empty<Object3DRuntimeNode>();
        public Object3DRuntimeComponent GetComponent(string kind)
        {
            if (components == null) return null;
            for (var i = 0; i < components.Length; i++) if (components[i] != null && components[i].kind == kind) return components[i];
            return null;
        }
        public Object3DRuntimeTransform Transform
        {
            get { var component = GetComponent("Transform"); return component != null ? component.transform : null; }
        }
    }
    [Serializable] public sealed class Object3DRuntimeManifest { public string schema = string.Empty; public int schemaVersion; public string definitionId = string.Empty; public string displayName = string.Empty; public Object3DRuntimeNode root; }

    public sealed class ResolvedRuntimeObjectDefinition
    {
        public ResolvedRuntimeObjectDefinition(string manifestPath, IReadOnlyDictionary<string, string> modelPaths, Object3DRuntimeManifest manifest) { ManifestPath = manifestPath; ModelPaths = modelPaths ?? throw new ArgumentNullException(nameof(modelPaths)); Manifest = manifest; }
        public string ManifestPath { get; private set; }
        public IReadOnlyDictionary<string, string> ModelPaths { get; private set; }
        public Object3DRuntimeManifest Manifest { get; private set; }
    }

    [Serializable]
    public sealed class MachineRuntimeManifestDefinition
    {
        public string kind = string.Empty;
        public string platform = string.Empty;
        // Complete selected-platform settings encoded as JSON for reliable retention without Fabric hosting.
        public string platformSettingsJson = string.Empty;
        public MachineRuntimeBehavior behavior;
    }
    [Serializable] public sealed class MachineRuntimeBehavior
    {
        public string kind = string.Empty;
        public string source = string.Empty;
    }

    [Serializable]
    public sealed class MachineInputDefinition
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string keyboardShortcut = string.Empty;
        public int kind;
        public string buttonNumber = string.Empty;
        public bool coinInput;
        public int? coinChannel;
        public int? coinValue;
        public bool inverted;
        public string rawMfmeShortcut = string.Empty;
        public string linkedVisualElementId;
        public string notes = string.Empty;
    }

    [Serializable]
    public sealed class MachineRuntimeFaceReference
    {
        public string faceId = string.Empty;
        public string assetName = string.Empty;
        public string cabinetFaceTargetId = string.Empty;
        public string frontSide = RuntimeFaceFrontSideExtensions.NormalValue;
        public int faceRotation;
        public bool faceFlipHorizontal;
        public string manifest = string.Empty;

        public string ResolvedManifestPath
        {
            get { return string.IsNullOrWhiteSpace(manifest) ? string.Empty : manifest.Trim(); }
        }
    }

    public enum RuntimeFaceFrontSide
    {
        Normal,
        Inverted
    }

    public static class RuntimeFaceFrontSideExtensions
    {
        public const string NormalValue = "normal";
        public const string InvertedValue = "inverted";

        public static RuntimeFaceFrontSide Parse(string value)
        {
            return string.Equals(value != null ? value.Trim() : string.Empty, InvertedValue, StringComparison.OrdinalIgnoreCase)
                ? RuntimeFaceFrontSide.Inverted
                : RuntimeFaceFrontSide.Normal;
        }

        public static bool IsInverted(this MachineRuntimeFaceReference reference)
        {
            return reference != null && Parse(reference.frontSide) == RuntimeFaceFrontSide.Inverted;
        }
    }

    [Serializable]
    public sealed class CabinetRuntimeManifest
    {
        public string schema = string.Empty;
        public int schemaVersion;
        public string cabinetId = string.Empty;
        public string glb = string.Empty;
        public float scale = 1f;
        public string upAxis = "Y";
        public RuntimeCabinetReflectionDefinition[] reflections = Array.Empty<RuntimeCabinetReflectionDefinition>();
    }

    [Serializable] public sealed class RuntimeCabinetReflectionVector { public float x; public float y; public float z; public Vector3 Value { get { return new Vector3(x, y, z); } } }
    [Serializable] public sealed class RuntimeCabinetReflectionPlaneDefinition { public RuntimeCabinetReflectionVector origin = new RuntimeCabinetReflectionVector(); public RuntimeCabinetReflectionVector right = new RuntimeCabinetReflectionVector(); public RuntimeCabinetReflectionVector up = new RuntimeCabinetReflectionVector(); public RuntimeCabinetReflectionVector normal; public float width; public float height; }
    [Serializable] public sealed class RuntimeCabinetReflectionSourceDefinition { public string sourceSurfaceTargetId = string.Empty; public RuntimeCabinetReflectionPlaneDefinition plane = new RuntimeCabinetReflectionPlaneDefinition(); }
    [Serializable] public sealed class RuntimeCabinetReflectionSettings { public bool enabled = true; public float strength; public float unlitArtworkStrength; public float litLampStrength; public float fresnelPower; public float fresnelStrength; public float roughness; public float distortion; public float edgeFade; }
    [Serializable] public sealed class RuntimeCabinetReflectionDefinition { public string id = string.Empty; public string targetId = string.Empty; public int materialSlot; public RuntimeCabinetReflectionSourceDefinition[] sources = Array.Empty<RuntimeCabinetReflectionSourceDefinition>(); public RuntimeCabinetReflectionSettings settings = new RuntimeCabinetReflectionSettings(); public string visibilityMask = string.Empty; }

    [Serializable]
    public sealed class FaceRuntimeManifest
    {
        public int schemaVersion;
        public string faceId = string.Empty;
        public int width;
        public int height;
        public int textureWidth;
        public int textureHeight;
        public string artwork = string.Empty;
        public string mask = string.Empty;
        public string trayId = string.Empty;
        public string lampIds0 = string.Empty;
        public string lampWeights0 = string.Empty;
        public string lampIds1 = string.Empty;
        public string lampWeights1 = string.Empty;
        public string trayIdDebug = string.Empty;
        public string lampWeightsDebug = string.Empty;
        public FaceRuntimeLampManifestEntry[] lamps = Array.Empty<FaceRuntimeLampManifestEntry>();
        public FaceRuntimeElementManifestEntry[] trays = Array.Empty<FaceRuntimeElementManifestEntry>();
        public FaceRuntimeReelManifestEntry[] reels = Array.Empty<FaceRuntimeReelManifestEntry>();
        public FaceRuntimeSevenSegmentDisplayManifestEntry[] sevenSegmentDisplays = Array.Empty<FaceRuntimeSevenSegmentDisplayManifestEntry>();
        public FaceRuntimeAlphaSegmentDisplayManifestEntry[] alphaSegmentDisplays = Array.Empty<FaceRuntimeAlphaSegmentDisplayManifestEntry>();
        public FaceRuntimeButtonManifestEntry[] buttons = Array.Empty<FaceRuntimeButtonManifestEntry>();
    }

    [Serializable]
    public class FaceRuntimeElementManifestEntry
    {
        public string objectId = string.Empty;
        public string machineReference = string.Empty;
        public string name = string.Empty;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class FaceRuntimeLampManifestEntry : FaceRuntimeElementManifestEntry
    {
        public string sourceLampWindowObjectId = string.Empty;
        public int lampId;
        public int trayId;
    }

    [Serializable]
    public sealed class FaceRuntimeReelManifestEntry : FaceRuntimeElementManifestEntry
    {
        public string reelBand = string.Empty;
        public int stops;
        public bool isReversed;
        public float bandOffset;
        public float physicalWidth;
        public float physicalRadius;
        public string transmissionMask = string.Empty;
        public bool reelLampsEnabled = true;
        public FaceRuntimeReelLampManifestEntry[] reelLamps = Array.Empty<FaceRuntimeReelLampManifestEntry>();

        [NonSerialized]
        public RuntimeTextureAsset BandTexture;
        [NonSerialized]
        public RuntimeTextureAsset TransmissionMaskTexture;
    }

    [Serializable]
    public sealed class FaceRuntimeReelLampManifestEntry
    {
        public string position = string.Empty;
        public int lampId = -1;
        public float localVerticalCenter;
        // Zero/non-positive selects the stop-derived aperture radius; positive is an authored override.
        public float radius = 0f;
        public float intensity;
    }

    [Serializable]
    public class FaceRuntimeSegmentDisplayManifestEntry : FaceRuntimeElementManifestEntry
    {
        public string topology = "sevenSegment";
        public int digitCount = 1;
        public string onColorHex = string.Empty;
        public string offColorHex = string.Empty;
        public bool showDecimalPoint;
        public bool showCommaTail;
        public bool isReversed;
    }

    [Serializable]
    public sealed class FaceRuntimeSevenSegmentDisplayManifestEntry : FaceRuntimeSegmentDisplayManifestEntry
    {
    }

    [Serializable]
    public sealed class FaceRuntimeAlphaSegmentDisplayManifestEntry : FaceRuntimeSegmentDisplayManifestEntry
    {
    }

    [Serializable]
    public sealed class FaceRuntimeButtonManifestEntry : FaceRuntimeElementManifestEntry
    {
        public string inputReference = string.Empty;
    }

    public sealed class ResolvedRuntimeBuild
    {
        public ResolvedRuntimeBuild(string buildRoot, MachineRuntimeManifest machine, string cabinetManifestPath, CabinetRuntimeManifest cabinet, string glbPath, MachineRuntimeFaceReference[] faces, IReadOnlyDictionary<string, ResolvedRuntimeObjectDefinition> objectDefinitions, OasisScriptProgram scriptProgram = null,
            IReadOnlyDictionary<string, ResolvedRuntimeObjectDefinition> prefabDependencies = null)
        {
            BuildRoot = buildRoot;
            Machine = machine;
            CabinetManifestPath = cabinetManifestPath;
            Cabinet = cabinet;
            GlbPath = glbPath;
            Faces = faces ?? Array.Empty<MachineRuntimeFaceReference>();
            ObjectDefinitions = objectDefinitions ?? new Dictionary<string, ResolvedRuntimeObjectDefinition>();
            ScriptProgram = scriptProgram;
            PrefabDependencies = prefabDependencies ?? new Dictionary<string, ResolvedRuntimeObjectDefinition>();
        }

        // Session-ready program. A8.4 supplies the real host and event adapter.
        public OasisScriptProgram ScriptProgram { get; }
        public string BuildRoot { get; }
        public MachineRuntimeManifest Machine { get; }
        public string CabinetManifestPath { get; }
        public CabinetRuntimeManifest Cabinet { get; }
        public string GlbPath { get; }
        public IReadOnlyList<MachineRuntimeFaceReference> Faces { get; }
        public IReadOnlyDictionary<string, ResolvedRuntimeObjectDefinition> ObjectDefinitions { get; }
        public IReadOnlyDictionary<string, ResolvedRuntimeObjectDefinition> PrefabDependencies { get; }
    }

    public static class RuntimeBuildLoader
    {
        public const string MachineSchema = "oasis.machine.runtime";
        public const string CabinetSchema = "oasis.cabinet.runtime";
        public const string ObjectSchema = "oasis.object3d.runtime";
        public const int MachineSchemaVersion = 12;
        public const int ObjectSchemaVersion = 2;

        public static bool TryLoad(string buildDirectory, out ResolvedRuntimeBuild build, out string error)
        {
            build = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(buildDirectory))
            {
                error = "Build directory is required.";
                return false;
            }

            var root = Path.GetFullPath(buildDirectory);
            if (!Directory.Exists(root))
            {
                error = $"Build directory does not exist: {root}";
                return false;
            }

            var machinePath = Path.Combine(root, "machine.runtime.json");
            if (!File.Exists(machinePath))
            {
                error = $"Machine runtime manifest is missing: {machinePath}";
                return false;
            }

            MachineRuntimeManifest machine;
            try
            {
                machine = RuntimeManifestJsonReader.ReadMachine(File.ReadAllText(machinePath));
            }
            catch (Exception ex)
            {
                error = $"Machine manifest is invalid JSON: {machinePath}. {ex.Message}";
                return false;
            }

            if (machine == null || machine.schema != MachineSchema || machine.schemaVersion != MachineSchemaVersion)
            {
                error = $"Unsupported machine manifest schema/version in {machinePath}.";
                return false;
            }

            OasisScriptProgram scriptProgram = null;
            if (machine.runtime == null) { error = $"Machine runtime definition is missing in {machinePath}."; return false; }
            if (machine.runtime.kind == "Emulation")
            {
                if (machine.runtime.behavior != null || !IsSupportedEmulationPlatform(machine.runtime.platform)
                    || !LooksLikeJsonObject(machine.runtime.platformSettingsJson))
                { error = $"Machine Emulation runtime definition is invalid in {machinePath}."; return false; }
            }
            else if (machine.runtime.kind == "Oasis")
            {
                var behavior = machine.runtime.behavior;
                if (behavior == null || behavior.kind != "OasisScript")
                { error = $"Machine '{machine.displayName}' Oasis runtime requires OasisScript behavior."; return false; }
                if (!TryResolveContained(root, root, behavior.source, out var sourcePath, out error))
                { error = $"Machine '{machine.displayName}' behavior source: {error}"; return false; }
                if (behavior.source != "behavior/behavior.oasis")
                { error = "Oasis behavior source must be behavior/behavior.oasis."; return false; }
                if (!string.IsNullOrEmpty(machine.runtime.platform) || !string.IsNullOrEmpty(machine.runtime.platformSettingsJson))
                { error = "Oasis runtime must not contain Emulation settings."; return false; }
                if (!File.Exists(sourcePath)) { error = $"Machine '{machine.displayName}' behavior/behavior.oasis is missing."; return false; }
                try
                {
                    var compilation = OasisScriptCompiler.Compile(File.ReadAllText(sourcePath), behavior.source);
                    if (!compilation.Success)
                    { error = $"Machine '{machine.displayName}': {string.Join(Environment.NewLine, compilation.Diagnostics)}"; return false; }
                    scriptProgram = compilation.Program;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                { error = $"Machine '{machine.displayName}' behavior/behavior.oasis: {exception.Message}"; return false; }
            }
            else { error = $"Unknown Machine runtime kind '{machine.runtime.kind}'."; return false; }

            if (machine.anchors == null)
            {
                error = $"Machine anchors collection is missing or null in {machinePath}.";
                return false;
            }
            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            var anchors = machine.anchors;
            for (var i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || !IsCompositionId(anchor.id) || string.IsNullOrWhiteSpace(anchor.displayName)
                    || anchor.position == null || anchor.rotationEulerDegrees == null
                    || !IsFinite(anchor.position) || !IsFinite(anchor.rotationEulerDegrees) || !anchorIds.Add(anchor.id))
                { error = $"Machine anchor at index {i} is invalid or duplicated in {machinePath}."; return false; }
            }

            var objectInstances = machine.objectInstances ?? Array.Empty<MachineRuntimeObjectInstance>();
            if (!TryValidateObjectInstances(objectInstances, machinePath, out error)) return false;

            if (!TryResolveContained(root, root, machine.cabinetManifest, out var cabinetPath, out error))
            {
                error = $"Invalid cabinet manifest path in {machinePath}: {error}";
                return false;
            }

            if (!File.Exists(cabinetPath))
            {
                error = $"Cabinet runtime manifest is missing: {cabinetPath}";
                return false;
            }

            CabinetRuntimeManifest cabinet;
            try
            {
                cabinet = JsonUtility.FromJson<CabinetRuntimeManifest>(File.ReadAllText(cabinetPath));
            }
            catch (Exception ex)
            {
                error = $"Cabinet manifest is invalid JSON: {cabinetPath}. {ex.Message}";
                return false;
            }

            if (cabinet == null || cabinet.schema != CabinetSchema || cabinet.schemaVersion != 5)
            {
                error = $"Unsupported cabinet manifest schema/version in {cabinetPath}.";
                return false;
            }

            if (!TryResolveContained(root, Path.GetDirectoryName(cabinetPath), cabinet.glb, out var glbPath, out error))
            {
                error = $"Invalid cabinet GLB path in {cabinetPath}: {error}";
                return false;
            }

            if (!File.Exists(glbPath))
            {
                error = $"Cabinet GLB is missing: {glbPath}";
                return false;
            }

            if (Debug.isDebugBuild)
            {
                var modifiedUtc = File.GetLastWriteTimeUtc(machinePath).ToString("O");
                for (var i = 0; i < (machine.faces != null ? machine.faces.Length : 0); i++)
                {
                    var reference = machine.faces[i];
                    if (reference == null) continue;
                    Debug.Log($"Oasis runtime manifest loaded: path='{machinePath}', modifiedUtc='{modifiedUtc}', machineId='{machine.machineId}', schemaVersion={machine.schemaVersion}, faceId='{reference.faceId}', cabinetFaceTargetId='{reference.cabinetFaceTargetId}', frontSide='{reference.frontSide}', isInverted={reference.IsInverted()}.");
                }
            }

            var objectDefinitions = new Dictionary<string, ResolvedRuntimeObjectDefinition>(StringComparer.OrdinalIgnoreCase);
            var definitionManifestPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var requests = new List<MachineRuntimeObjectInstance>(objectInstances);
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dependency in machine.prefabDependencies ?? Array.Empty<MachineRuntimePrefabDependency>())
            {
                if (dependency == null || !IsCompositionId(dependency.alias) || !aliases.Add(dependency.alias)
                    || !Guid.TryParse(dependency.definitionId, out _))
                { error = "Prefab dependency alias/definition ID is invalid or duplicated."; return false; }
                requests.Add(new MachineRuntimeObjectInstance { id = "prefab:" + dependency.alias, definitionId = dependency.definitionId, definitionManifest = dependency.definitionManifest });
            }
            var surfaceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var surface in machine.geometrySurfaces ?? Array.Empty<MachineRuntimeGeometrySurface>())
            {
                if (surface == null || !IsCompositionId(surface.id) || !surfaceIds.Add(surface.id)
                    || surface.position == null || surface.rotationEulerDegrees == null || !IsFinite(surface.position) || !IsFinite(surface.rotationEulerDegrees)
                    || !IsFinitePositive(surface.width) || !IsFinitePositive(surface.length))
                { error = "Geometry surface ID, finite pose and positive dimensions are required; IDs must be unique."; return false; }
            }
            try
            {
                if (machine.interaction == null) throw new InvalidDataException("Machine interaction profile is required.");
                machine.interaction.Validate(machine);
            }
            catch (InvalidDataException exception) { error = exception.Message; return false; }
            for (var i = 0; i < requests.Count; i++)
            {
                var instance = requests[i];
                if (objectDefinitions.ContainsKey(instance.definitionId))
                {
                    var priorManifest = definitionManifestPaths[instance.definitionId];
                    string repeatedManifestPath;
                    if (!TryResolveContained(root, root, instance.definitionManifest, out repeatedManifestPath, out error)
                        || !string.Equals(priorManifest, repeatedManifestPath, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    { error = $"Object3D definition ID '{instance.definitionId}' is referenced through conflicting manifests in {machinePath}."; return false; }
                    continue;
                }
                if (!TryResolveContained(root, root, instance.definitionManifest, out var objectManifestPath, out error))
                { error = $"Invalid Object3D manifest path for instance '{instance.id}' in {machinePath}: {error}"; return false; }
                if (!File.Exists(objectManifestPath)) { error = $"Object3D runtime manifest is missing: {objectManifestPath}"; return false; }
                Object3DRuntimeManifest objectManifest;
                try { objectManifest = RuntimeManifestJsonReader.ReadObject(File.ReadAllText(objectManifestPath)); }
                catch (Exception ex) { error = $"Object3D manifest is invalid JSON: {objectManifestPath}. {ex.Message}"; return false; }
                if (!TryValidateObjectManifest(objectManifest, instance.definitionId, objectManifestPath, root, out var modelPaths, out error)) return false;
                objectDefinitions.Add(instance.definitionId, new ResolvedRuntimeObjectDefinition(objectManifestPath, modelPaths, objectManifest));
                definitionManifestPaths.Add(instance.definitionId, objectManifestPath);
            }
            if (!ValidateObjectBodyParenting(objectInstances, objectDefinitions, out error)) return false;
            var declaredPrefabs = new Dictionary<string, ResolvedRuntimeObjectDefinition>(StringComparer.Ordinal);
            foreach (var dependency in machine.prefabDependencies ?? Array.Empty<MachineRuntimePrefabDependency>()) declaredPrefabs.Add(dependency.alias, objectDefinitions[dependency.definitionId]);
            build = new ResolvedRuntimeBuild(root, machine, cabinetPath, cabinet, glbPath, machine.faces, objectDefinitions, scriptProgram, declaredPrefabs);
            return true;
        }

        private static bool ValidateObjectBodyParenting(MachineRuntimeObjectInstance[] instances,
            Dictionary<string, ResolvedRuntimeObjectDefinition> definitions, out string error)
        {
            error = string.Empty;
            var byId = new Dictionary<string, MachineRuntimeObjectInstance>(StringComparer.Ordinal);
            for (var i = 0; i < instances.Length; i++) byId[instances[i].id] = instances[i];
            foreach (var instance in instances)
            {
                var definition = definitions[instance.definitionId];
                if (!HasActiveBody(definition.Manifest.root)) continue;
                var parentId = instance.parentId;
                while (!string.IsNullOrEmpty(parentId))
                {
                    var parent = byId[parentId];
                    var parentDefinition = definitions[parent.definitionId];
                    if (HasActiveBody(parentDefinition.Manifest.root))
                    {
                        error = $"Machine Object3D instance '{instance.id}' has an active Rigidbody beneath active Rigidbody instance '{parent.id}'; nested body ownership is unsupported.";
                        return false;
                    }
                    if (!IsUnitScale(parent.transform.scale) || !IsUnitScale(parentDefinition.Manifest.root.Transform.scale))
                    {
                        error = $"Machine Object3D instance '{instance.id}' has an active Rigidbody beneath scaled parent instance '{parent.id}'; that hierarchy is unsupported.";
                        return false;
                    }
                    parentId = parent.parentId;
                }
            }
            return true;
        }

        private static bool HasActiveBody(Object3DRuntimeNode node)
        {
            if (node == null) return false;
            for (var i = 0; i < node.components.Length; i++)
            {
                var component = node.components[i];
                if (component.kind == "Rigidbody" && component.rigidbody.mode != "Static") return true;
            }
            for (var i = 0; i < node.children.Length; i++) if (HasActiveBody(node.children[i])) return true;
            return false;
        }

        private static bool TryValidateObjectInstances(MachineRuntimeObjectInstance[] instances, string machinePath, out string error)
        {
            error = string.Empty;
            var byId = new Dictionary<string, MachineRuntimeObjectInstance>(StringComparer.Ordinal);
            for (var i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance == null || !IsCompositionId(instance.id) || string.IsNullOrWhiteSpace(instance.displayName)
                    || !Guid.TryParse(instance.definitionId, out _) || string.IsNullOrWhiteSpace(instance.definitionManifest)
                    || instance.transform == null || instance.transform.position == null || instance.transform.rotationEulerDegrees == null || instance.transform.scale == null
                    || !IsFinite(instance.transform.position) || !IsFinite(instance.transform.rotationEulerDegrees) || !IsPositiveScale(instance.transform.scale)
                    || !byId.TryAdd(instance.id, instance))
                {
                    error = $"Machine Object3D instance at index {i} is invalid or duplicated in {machinePath}.";
                    return false;
                }
            }

            foreach (var instance in instances)
            {
                if (string.IsNullOrEmpty(instance.parentId)) continue;
                if (!byId.ContainsKey(instance.parentId))
                {
                    error = $"Machine Object3D instance '{instance.id}' references missing parent '{instance.parentId}' in {machinePath}.";
                    return false;
                }
                if (string.Equals(instance.id, instance.parentId, StringComparison.Ordinal))
                {
                    error = $"Machine Object3D instance '{instance.id}' cannot parent itself in {machinePath}.";
                    return false;
                }
            }

            var states = new Dictionary<string, byte>(StringComparer.Ordinal);
            foreach (var instance in instances)
                if (!VisitParent(instance, byId, states, machinePath, out error)) return false;
            return true;
        }

        private static bool VisitParent(MachineRuntimeObjectInstance instance, Dictionary<string, MachineRuntimeObjectInstance> byId,
            Dictionary<string, byte> states, string machinePath, out string error)
        {
            error = string.Empty;
            if (states.TryGetValue(instance.id, out var state))
            {
                if (state == 1) { error = $"Machine Object3D parent hierarchy contains a cycle at '{instance.id}' in {machinePath}."; return false; }
                return true;
            }
            states[instance.id] = 1;
            if (!string.IsNullOrEmpty(instance.parentId) && !VisitParent(byId[instance.parentId], byId, states, machinePath, out error)) return false;
            states[instance.id] = 2;
            return true;
        }

        private static bool TryValidateObjectManifest(Object3DRuntimeManifest manifest, string expectedDefinitionId, string manifestPath,
            string buildRoot, out IReadOnlyDictionary<string, string> modelPaths, out string error)
        {
            modelPaths = null;
            error = string.Empty;
            if (manifest == null || manifest.schema != ObjectSchema || manifest.schemaVersion != ObjectSchemaVersion
                || !Guid.TryParse(manifest.definitionId, out _) || !string.Equals(manifest.definitionId, expectedDefinitionId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(manifest.displayName) || manifest.root == null)
            {
                error = $"Unsupported or invalid Object3D manifest schema/definition in {manifestPath}.";
                return false;
            }

            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var componentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var models = new Dictionary<string, string>(StringComparer.Ordinal);
            var activeNodes = new HashSet<Object3DRuntimeNode>();
            var visitedNodes = new HashSet<Object3DRuntimeNode>();
            var packageDirectory = Path.GetDirectoryName(manifestPath);
            if (!ValidateObjectNode(manifest.root, "root", packageDirectory, nodeIds, componentIds, models, activeNodes, visitedNodes, false, 0, out _, out error)) return false;
            modelPaths = models;
            return true;
        }

        private static bool ValidateObjectNode(Object3DRuntimeNode node, string context, string packageDirectory,
            HashSet<string> nodeIds, HashSet<string> componentIds, Dictionary<string, string> modelPaths,
            HashSet<Object3DRuntimeNode> activeNodes, HashSet<Object3DRuntimeNode> visitedNodes, bool hasScaledAncestor, int depth, out int activeBodyCount, out string error)
        {
            activeBodyCount = 0;
            error = string.Empty;
            if (node == null) { error = $"Object3D {context}: node is null."; return false; }
            context = $"Object3D node '{node.name}' ({context})";
            if (depth > 256) { error = $"{context}: hierarchy exceeds the supported depth of 256."; return false; }
            if (!activeNodes.Add(node)) { error = $"{context}: hierarchy contains a cycle."; return false; }
            if (!visitedNodes.Add(node)) { error = $"{context}: node occurs more than once; hierarchy must be a tree."; return false; }
            if (!Guid.TryParse(node.id, out _) || !nodeIds.Add(node.id)) { error = $"{context}: node ID must be a unique stable GUID."; return false; }
            if (string.IsNullOrWhiteSpace(node.name) || node.components == null || node.children == null)
            { error = $"{context}: name, components, and children are required."; return false; }

            var kinds = new HashSet<string>(StringComparer.Ordinal);
            var hasTransform = false;
            var hasModel = false;
            Object3DRuntimeCollider collider = null;
            Object3DRuntimeRigidbody rigidbody = null;
            for (var i = 0; i < node.components.Length; i++)
            {
                var component = node.components[i];
                if (component == null || !Guid.TryParse(component.componentId, out _) || !componentIds.Add(component.componentId) || string.IsNullOrWhiteSpace(component.kind))
                { error = $"{context}: component at index {i} has an invalid or duplicated ID."; return false; }
                if (!kinds.Add(component.kind) || component.contractVersion != 1)
                { error = $"{context} component '{component.kind}' ({component.componentId}): duplicate or unsupported contract version."; return false; }
                var payloadCount = (component.transform == null ? 0 : 1) + (component.model == null ? 0 : 1)
                    + (component.collider == null ? 0 : 1) + (component.rigidbody == null ? 0 : 1);
                var hasExpectedPayload = false;
                switch (component.kind)
                {
                    case "Transform": hasExpectedPayload = component.transform != null; hasTransform = true; if (component.transform != null && !ValidateTransform(component.transform, context, out error)) return false; break;
                    case "Model": hasExpectedPayload = component.model != null; hasModel = true; if (component.model != null && !ValidateModel(component.model, context, packageDirectory, modelPaths, out error)) return false; break;
                    case "Collider": hasExpectedPayload = component.collider != null; collider = component.collider; if (component.collider != null && !ValidateCollider(component.collider, context, out error)) return false; break;
                    case "Rigidbody": hasExpectedPayload = component.rigidbody != null; rigidbody = component.rigidbody; if (component.rigidbody != null && !ValidateRigidbody(component.rigidbody, context, out error)) return false; break;
                    default: error = $"{context} component '{component.kind}' ({component.componentId}): unknown component kind."; return false;
                }
                if (!hasExpectedPayload || payloadCount != 1)
                { error = $"{context} component '{component.kind}' ({component.componentId}): payload must match its kind exactly."; return false; }
            }
            if (!hasTransform) { error = $"{context}: exactly one Transform component is required."; return false; }
            if (!hasModel && collider != null && collider.kind != "None" && collider.sourceFrame != "NodeLocal")
            { error = $"{context} Collider: sourceFrame must be NodeLocal when the node has no Model."; return false; }
            if (!hasModel && collider != null && collider.kind == "Mesh")
            { error = $"{context} Mesh Collider requires a Model component."; return false; }
            var hasActiveBody = rigidbody != null && rigidbody.mode != "Static";
            if (hasActiveBody && hasScaledAncestor)
            { error = $"{context} has an active Rigidbody beneath a scaled parent transform; that hierarchy is unsupported."; return false; }
            if (hasActiveBody) activeBodyCount++;
            var childHasScaledAncestor = hasScaledAncestor || !IsUnitScale(node.Transform.scale);

            for (var i = 0; i < node.children.Length; i++)
            {
                if (!ValidateObjectNode(node.children[i], $"{context}/child[{i}]", packageDirectory, nodeIds, componentIds, modelPaths, activeNodes, visitedNodes, childHasScaledAncestor, depth + 1, out var childBodies, out error)) return false;
                activeBodyCount += childBodies;
                if (activeBodyCount > 1) { error = $"{context}: only one non-Static Rigidbody may own a node subtree."; return false; }
            }
            activeNodes.Remove(node);
            return true;
        }

        private static bool ValidateTransform(Object3DRuntimeTransform transform, string context, out string error)
        {
            error = string.Empty;
            if (transform.position == null || transform.rotationEulerDegrees == null || transform.scale == null
                || !IsFinite(transform.position) || !IsFinite(transform.rotationEulerDegrees) || !IsPositiveScale(transform.scale))
            { error = $"{context} Transform must have finite position/rotation and positive finite scale."; return false; }
            return true;
        }

        private static bool ValidateModel(Object3DRuntimeModel model, string context, string packageDirectory,
            Dictionary<string, string> modelPaths, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(model.path) || !string.Equals(Path.GetExtension(model.path), ".glb", StringComparison.OrdinalIgnoreCase)
                || !IsFinitePositive(model.scale) || (model.upAxis != "X" && model.upAxis != "Y" && model.upAxis != "Z"))
            { error = $"{context} Model has an invalid GLB path, scale, or upAxis."; return false; }
            if (!TryResolveContained(packageDirectory, packageDirectory, model.path, out var resolvedModelPath, out error))
            { error = $"{context} Model path '{model.path}' is invalid: {error}"; return false; }
            if (!File.Exists(resolvedModelPath)) { error = $"{context} Model GLB is missing: {resolvedModelPath}."; return false; }
            if (modelPaths.TryGetValue(model.path, out var existingPath)
                && !string.Equals(existingPath, resolvedModelPath, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            { error = $"{context} Model path '{model.path}' resolves ambiguously."; return false; }
            modelPaths[model.path] = resolvedModelPath;
            return true;
        }

        private static bool ValidateCollider(Object3DRuntimeCollider collider, string context, out string error)
        {
            error = string.Empty;
            if (collider.kind != "None" && collider.kind != "Sphere" && collider.kind != "Box" && collider.kind != "Capsule" && collider.kind != "Mesh")
            { error = $"{context} Collider kind '{collider.kind}' is unsupported."; return false; }
            if (collider.sourceFrame != "ModelLocal" && collider.sourceFrame != "NodeLocal")
            { error = $"{context} Collider sourceFrame '{collider.sourceFrame}' is unsupported."; return false; }
            if (collider.kind == "None")
            {
                if (collider.isTrigger || HasPrimitiveDimensions(collider)) { error = $"{context} Collider None cannot define a trigger or dimensions."; return false; }
                return true;
            }
            if (collider.kind == "Sphere")
            {
                if (!IsFiniteVector(collider.center) || !IsFinitePositive(collider.radius)) { error = $"{context} Sphere Collider requires a finite center and positive finite radius."; return false; }
            }
            else if (collider.kind == "Box")
            {
                if (!IsFiniteVector(collider.center) || !IsPositiveVector(collider.size)) { error = $"{context} Box Collider requires a finite center and positive finite size."; return false; }
            }
            else if (collider.kind == "Capsule")
            {
                if (!IsFiniteVector(collider.center) || !IsFinitePositive(collider.radius) || !IsFinitePositive(collider.height)
                    || collider.height < collider.radius * 2f || (collider.axis != "X" && collider.axis != "Y" && collider.axis != "Z"))
                { error = $"{context} Capsule Collider requires finite center, positive radius, height at least twice its radius, and axis X/Y/Z."; return false; }
            }
            else if (HasPrimitiveDimensions(collider))
            { error = $"{context} Mesh Collider cannot define primitive dimensions."; return false; }
            return true;
        }

        private static bool ValidateRigidbody(Object3DRuntimeRigidbody rigidbody, string context, out string error)
        {
            error = string.Empty;
            if (rigidbody.mode != "Static" && rigidbody.mode != "Dynamic" && rigidbody.mode != "Kinematic")
            { error = $"{context} Rigidbody mode '{rigidbody.mode}' is unsupported."; return false; }
            if (rigidbody.mode == "Dynamic" && !IsFinitePositive(rigidbody.mass))
            { error = $"{context} Dynamic Rigidbody mass must be positive and finite."; return false; }
            if (rigidbody.mode != "Dynamic" && rigidbody.mass != 0)
            { error = $"{context} {rigidbody.mode} Rigidbody cannot specify mass."; return false; }
            return true;
        }

        private static bool HasPrimitiveDimensions(Object3DRuntimeCollider collider)
        {
            return collider.center != null && collider.center.Length > 0 || collider.size != null && collider.size.Length > 0
                || collider.radius != 0 || collider.height != 0 || !string.IsNullOrEmpty(collider.axis);
        }

        private static bool IsFiniteVector(float[] values)
        {
            return values != null && values.Length == 3 && IsFinite(values[0]) && IsFinite(values[1]) && IsFinite(values[2]);
        }

        private static bool IsPositiveVector(float[] values)
        {
            return IsFiniteVector(values) && values[0] > 0 && values[1] > 0 && values[2] > 0;
        }

        private static bool IsPositiveScale(RuntimeVector3Definition value)
        {
            return IsFinite(value) && value.x > 0 && value.y > 0 && value.z > 0;
        }

        private static bool IsUnitScale(RuntimeVector3Definition value)
        {
            return value != null && value.x == 1 && value.y == 1 && value.z == 1;
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool LooksLikeJsonObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            var trimmed = json.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
        }

        private static bool IsCompositionId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (var i = 0; i < id.Length; i++)
            {
                var value = id[i];
                if (!((value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || (value >= '0' && value <= '9') || value == '_' || value == '-')) return false;
            }
            return true;
        }

        private static bool IsFinite(RuntimeVector3Definition value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsSupportedEmulationPlatform(string platform)
        {
            return platform == "None" || platform == "Impact" || platform == "MPU5" || platform == "Epoch"
                || platform == "MPU3" || platform == "MaygayM1" || platform == "Scorpion4";
        }

        private static bool TryResolveContained(string root, string baseDir, string relative, out string resolved, out string error)
        {
            resolved = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(relative))
            {
                error = "path is empty.";
                return false;
            }

            if (Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0 || relative.StartsWith("\\", StringComparison.Ordinal))
            {
                error = "rooted paths are not allowed.";
                return false;
            }

            try
            {
                resolved = Path.GetFullPath(Path.Combine(baseDir, relative.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar)));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            { error = $"invalid package path: {exception.Message}"; return false; }
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(fullRoot, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                error = "path traversal outside the build root is not allowed.";
                return false;
            }

            return true;
        }
    }
}
