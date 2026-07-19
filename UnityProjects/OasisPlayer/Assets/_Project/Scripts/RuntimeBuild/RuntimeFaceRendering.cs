using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OasisPlayer.RuntimeBuild
{
    public sealed class RuntimeFaceRenderer
    {
        private readonly RuntimeFaceMaterialFactory _materialFactory;

        public RuntimeFaceRenderer(RuntimeFaceMaterialFactory materialFactory)
        {
            _materialFactory = materialFactory ?? throw new ArgumentNullException(nameof(materialFactory));
        }

        public void RenderFaces(RuntimeMachine machine)
        {
            if (machine == null) throw new ArgumentNullException(nameof(machine));

            foreach (var face in machine.Faces)
            {
                if (!TryRender(machine, face, out var warning)) machine.AddWarning(warning);
            }
        }

        public bool TryRender(RuntimeMachine machine, RuntimeFace face, out string warning)
        {
            warning = string.Empty;
            if (machine == null) throw new ArgumentNullException(nameof(machine));

            if (face == null)
            {
                warning = "Runtime Face rendering skipped because the Face was null.";
                return false;
            }

            if (face.RenderBinding != null)
            {
                warning = $"Runtime Face '{FaceId(face)}' was already rendered; duplicate render request was skipped.";
                return false;
            }

            if (face.Artwork == null || face.Artwork.Texture == null)
            {
                warning = $"Runtime Face '{FaceId(face)}' has no valid artwork texture.";
                return false;
            }

            if (face.Mask == null || face.Mask.Texture == null)
            {
                warning = $"Runtime Face '{FaceId(face)}' has no valid mask texture.";
                return false;
            }

            if (!TryResolveRenderer(face, out var renderer, out warning)) return false;
            if (!TryResolveMaterialSlot(face, renderer, out var slotIndex, out warning)) return false;
            if (!_materialFactory.TryCreate(face, machine.LampStateTexture, out var runtimeMaterial, out warning)) return false;

            var originalMaterials = renderer.sharedMaterials;
            var replacementMaterials = (Material[])originalMaterials.Clone();
            replacementMaterials[slotIndex] = runtimeMaterial;
            renderer.sharedMaterials = replacementMaterials;

            if (Debug.isDebugBuild)
            {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                var assignedMaterial = renderer.sharedMaterials != null && slotIndex >= 0 && slotIndex < renderer.sharedMaterials.Length ? renderer.sharedMaterials[slotIndex] : null;
                Debug.Log($"Oasis Face renderer binding: faceId='{FaceId(face)}', cabinetFaceTargetId='{TargetId(face)}', target='{(face.CabinetTarget != null ? face.CabinetTarget.name : "<none>")}', mesh='{(meshFilter != null && meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "<none>")}', renderer='{renderer.name}', runtimeMaterialInstanceId={(runtimeMaterial != null ? runtimeMaterial.GetInstanceID() : 0)}, rendererMaterialInstanceId={(assignedMaterial != null ? assignedMaterial.GetInstanceID() : 0)}.");
            }

            face.SetRenderBinding(new RuntimeFaceRenderBinding(renderer, originalMaterials, slotIndex, runtimeMaterial));
            return true;
        }

        private static bool TryResolveRenderer(RuntimeFace face, out Renderer renderer, out string warning)
        {
            renderer = null;
            warning = string.Empty;
            if (face.CabinetTarget == null)
            {
                warning = $"Runtime Face '{FaceId(face)}' has no resolved cabinet target.";
                return false;
            }

            var renderers = face.CabinetTarget.GetComponentsInChildren<Renderer>(true);
            var usableCount = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i] is SkinnedMeshRenderer) continue;
                renderer = renderers[i];
                usableCount++;
            }

            if (usableCount == 1) return true;
            renderer = null;
            warning = usableCount == 0
                ? $"Cabinet target '{TargetId(face)}' for Runtime Face '{FaceId(face)}' has no usable renderer."
                : $"Cabinet target '{TargetId(face)}' for Runtime Face '{FaceId(face)}' has {usableCount} usable renderers; Face rendering skipped to avoid ambiguous material replacement.";
            return false;
        }

        private static bool TryResolveMaterialSlot(RuntimeFace face, Renderer renderer, out int slotIndex, out string warning)
        {
            slotIndex = -1;
            warning = string.Empty;
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                warning = $"Cabinet target '{TargetId(face)}' for Runtime Face '{FaceId(face)}' has no material slots.";
                return false;
            }

            if (materials.Length == 1)
            {
                slotIndex = 0;
                return true;
            }

            warning = $"Cabinet target '{TargetId(face)}' for Runtime Face '{FaceId(face)}' has {materials.Length} material slots; Face rendering skipped because the intended slot cannot be determined safely.";
            return false;
        }

        private static string FaceId(RuntimeFace face)
        {
            return face.Reference != null && !string.IsNullOrWhiteSpace(face.Reference.faceId) ? face.Reference.faceId.Trim() : "<unknown>";
        }

        private static string TargetId(RuntimeFace face)
        {
            return face.Reference != null && !string.IsNullOrWhiteSpace(face.Reference.cabinetFaceTargetId) ? face.Reference.cabinetFaceTargetId.Trim() : "<unknown>";
        }
    }

    public sealed class RuntimeFaceMaterialFactory
    {
        private const float DefaultStaticBrightness = 1f;
        private const float DefaultMaskStrength = 1f;
        private const float DefaultLampExposureStops = 2.5f;
        private const float DefaultBaseAmbientStrength = 1f;
        private const float DefaultBaseMainLightStrength = 1f;
        private const float DefaultBaseAdditionalLightStrength = 1f;

        private readonly string _shaderName;

        public RuntimeFaceMaterialFactory()
            : this(RuntimeFaceShaderProperties.ShaderName)
        {
        }

        public RuntimeFaceMaterialFactory(string shaderName)
        {
            _shaderName = string.IsNullOrWhiteSpace(shaderName) ? RuntimeFaceShaderProperties.ShaderName : shaderName;
        }

        public bool TryCreate(RuntimeFace face, RuntimeLampStateTexture lampStateTexture, out Material material, out string warning)
        {
            material = null;
            warning = string.Empty;
            if (face == null || face.Artwork == null || face.Artwork.Texture == null || face.Mask == null || face.Mask.Texture == null)
            {
                warning = "Runtime Face material creation failed because required Face textures are invalid.";
                return false;
            }

            var frontSide = face.Reference != null ? RuntimeFaceFrontSideExtensions.Parse(face.Reference.frontSide) : RuntimeFaceFrontSide.Normal;
            var shader = Shader.Find(_shaderName);
            if (shader == null)
            {
                warning = $"Runtime Face '{(face.Reference != null ? face.Reference.faceId : "<unknown>")}' could not render because the dedicated '{_shaderName}' shader was unavailable.";
                return false;
            }

            material = new Material(shader);
            material.name = $"RuntimeFace_{(face.Reference != null ? face.Reference.faceId : "Face")}_OasisFace";
            BindTextures(material, face);
            if (lampStateTexture != null && lampStateTexture.Texture != null) AssignTexture(material, RuntimeFaceShaderProperties.LampStateTexture, lampStateTexture.Texture);
            // Keep the base artwork scene-lit while the shader applies GEGL-style photographic
            // exposure to lit lamp regions without requiring illuminated artwork textures in the
            // runtime export.
            if (material.HasProperty(RuntimeFaceShaderProperties.StaticBrightness)) material.SetFloat(RuntimeFaceShaderProperties.StaticBrightness, DefaultStaticBrightness);
            if (material.HasProperty(RuntimeFaceShaderProperties.MaskStrength)) material.SetFloat(RuntimeFaceShaderProperties.MaskStrength, DefaultMaskStrength);
            if (material.HasProperty(RuntimeFaceShaderProperties.LampExposureStops)) material.SetFloat(RuntimeFaceShaderProperties.LampExposureStops, DefaultLampExposureStops);
            if (material.HasProperty(RuntimeFaceShaderProperties.BaseAmbientStrength)) material.SetFloat(RuntimeFaceShaderProperties.BaseAmbientStrength, DefaultBaseAmbientStrength);
            if (material.HasProperty(RuntimeFaceShaderProperties.BaseMainLightStrength)) material.SetFloat(RuntimeFaceShaderProperties.BaseMainLightStrength, DefaultBaseMainLightStrength);
            if (material.HasProperty(RuntimeFaceShaderProperties.BaseAdditionalLightStrength)) material.SetFloat(RuntimeFaceShaderProperties.BaseAdditionalLightStrength, DefaultBaseAdditionalLightStrength);
            if (!ApplyFrontSide(material, face, frontSide, out warning))
            {
                DestroyOwned(material);
                material = null;
                return false;
            }
            if (!ApplyTextureOrientation(material, face, out warning))
            {
                DestroyOwned(material);
                material = null;
                return false;
            }
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return true;
        }


        private static bool ApplyFrontSide(Material material, RuntimeFace face, RuntimeFaceFrontSide frontSide, out string warning)
        {
            warning = string.Empty;
            if (!RequireProperty(material, RuntimeFaceShaderProperties.CullMode, RuntimeFaceShaderProperties.CullModeName, face, out warning)) return false;
            if (!RequireProperty(material, RuntimeFaceShaderProperties.NormalSign, RuntimeFaceShaderProperties.NormalSignName, face, out warning)) return false;

            var orientation = ResolveUnityOrientation(frontSide);
            material.SetInt(RuntimeFaceShaderProperties.CullMode, (int)orientation.CullMode);
            material.SetFloat(RuntimeFaceShaderProperties.NormalSign, orientation.NormalSign);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"Oasis Face orientation material: faceId='{(face != null && face.Reference != null ? face.Reference.faceId : "<unknown>")}', cabinetFaceTargetId='{(face != null && face.Reference != null ? face.Reference.cabinetFaceTargetId : "<unknown>")}', rawFrontSide='{(face != null && face.Reference != null ? face.Reference.frontSide : string.Empty)}', parsedFrontSide={frontSide}, isInverted={frontSide == RuntimeFaceFrontSide.Inverted}, shader='{material.shader.name}', hasCull={material.HasProperty(RuntimeFaceShaderProperties.CullMode)}, cull={material.GetInt(RuntimeFaceShaderProperties.CullMode)}, hasNormalSign={material.HasProperty(RuntimeFaceShaderProperties.NormalSign)}, normalSign={material.GetFloat(RuntimeFaceShaderProperties.NormalSign)}, target='{(face != null && face.CabinetTarget != null ? face.CabinetTarget.name : "<none>")}', materialInstanceId={material.GetInstanceID()}.");
            }
            return true;
        }

        private static RuntimeFaceUnityOrientation ResolveUnityOrientation(RuntimeFaceFrontSide frontSide)
        {
            // The Editor preview defines the authoring semantics. Unity's imported GLB Face target
            // winding is opposite to the Editor preview winding, so Player maps the semantic front
            // side to the opposite Unity cull state while keeping the lighting normal aligned with
            // the side that remains visible.
            return frontSide == RuntimeFaceFrontSide.Inverted
                ? new RuntimeFaceUnityOrientation(CullMode.Back, 1f)
                : new RuntimeFaceUnityOrientation(CullMode.Front, -1f);
        }

        private static bool ApplyTextureOrientation(Material material, RuntimeFace face, out string warning)
        {
            warning = string.Empty;
            if (!RequireProperty(material, RuntimeFaceShaderProperties.FaceRotationQuarterTurns, RuntimeFaceShaderProperties.FaceRotationQuarterTurnsName, face, out warning)) return false;
            if (!RequireProperty(material, RuntimeFaceShaderProperties.FaceFlipHorizontal, RuntimeFaceShaderProperties.FaceFlipHorizontalName, face, out warning)) return false;

            var orientation = RuntimeFaceTextureOrientation.FromReference(face != null ? face.Reference : null);
            material.SetInt(RuntimeFaceShaderProperties.FaceRotationQuarterTurns, orientation.UnityUvQuarterTurns);
            material.SetFloat(RuntimeFaceShaderProperties.FaceFlipHorizontal, orientation.FlipHorizontal ? 1f : 0f);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"Oasis Face texture orientation material: faceId='{(face != null && face.Reference != null ? face.Reference.faceId : "<unknown>")}', rawFaceRotation={(face != null && face.Reference != null ? face.Reference.faceRotation : 0)}, editorRotation={orientation.EditorRotationDegrees}, unityUvQuarterTurns={material.GetInt(RuntimeFaceShaderProperties.FaceRotationQuarterTurns)}, flipHorizontal={material.GetFloat(RuntimeFaceShaderProperties.FaceFlipHorizontal)}, materialInstanceId={material.GetInstanceID()}.");
            }
            return true;
        }

        private static bool RequireProperty(Material material, int propertyId, string propertyName, RuntimeFace face, out string warning)
        {
            warning = string.Empty;
            if (material.HasProperty(propertyId)) return true;
            warning = $"Runtime Face '{(face != null && face.Reference != null ? face.Reference.faceId : "<unknown>")}' could not apply front-side orientation because shader '{material.shader.name}' does not expose required property '{propertyName}'.";
            return false;
        }

        private static void BindTextures(Material material, RuntimeFace face)
        {
            AssignTexture(material, RuntimeFaceShaderProperties.ArtworkTexture, face.Artwork.Texture);
            AssignTexture(material, RuntimeFaceShaderProperties.MaskTexture, face.Mask.Texture);
            if (face.TrayId != null) AssignTexture(material, RuntimeFaceShaderProperties.TrayIdTexture, face.TrayId.Texture);
            if (face.LampIds0 != null) AssignTexture(material, RuntimeFaceShaderProperties.LampIds0Texture, face.LampIds0.Texture);
            if (face.LampWeights0 != null) AssignTexture(material, RuntimeFaceShaderProperties.LampWeights0Texture, face.LampWeights0.Texture);
        }

        private static void DestroyOwned(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        private static void AssignTexture(Material material, int propertyId, Texture texture)
        {
            if (!material.HasProperty(propertyId)) return;
            material.SetTexture(propertyId, texture);
            material.SetTextureScale(propertyId, Vector2.one);
            material.SetTextureOffset(propertyId, Vector2.zero);
        }
    }

    public readonly struct RuntimeFaceTextureOrientation
    {
        public RuntimeFaceTextureOrientation(int rotationDegrees, bool flipHorizontal)
        {
            EditorRotationDegrees = NormalizeRotation(rotationDegrees);
            UnityUvQuarterTurns = ResolveUnityUvQuarterTurns(EditorRotationDegrees);
            FlipHorizontal = flipHorizontal;
        }

        public int EditorRotationDegrees { get; }
        public int UnityUvQuarterTurns { get; }
        public bool FlipHorizontal { get; }

        public static RuntimeFaceTextureOrientation FromReference(MachineRuntimeFaceReference reference)
        {
            return reference != null
                ? new RuntimeFaceTextureOrientation(reference.faceRotation, reference.faceFlipHorizontal)
                : new RuntimeFaceTextureOrientation(0, false);
        }

        public Vector2 TransformUv(Vector2 uv)
        {
            Vector2 rotated;
            switch (UnityUvQuarterTurns)
            {
                case 1:
                    rotated = new Vector2(1f - uv.y, uv.x);
                    break;
                case 2:
                    rotated = new Vector2(1f - uv.x, 1f - uv.y);
                    break;
                case 3:
                    rotated = new Vector2(uv.y, 1f - uv.x);
                    break;
                default:
                    rotated = uv;
                    break;
            }

            return FlipHorizontal ? new Vector2(1f - rotated.x, rotated.y) : rotated;
        }

        private static int NormalizeRotation(int rotationDegrees)
        {
            return rotationDegrees == 90 || rotationDegrees == 180 || rotationDegrees == 270 ? rotationDegrees : 0;
        }

        private static int ResolveUnityUvQuarterTurns(int editorRotationDegrees)
        {
            // Editor rotation is semantic. The Editor rotates by reordering physical quad corners
            // while keeping fixed texture coordinates; Player rotates imported GLB UVs whose
            // baseline and positive rotation direction differ from the Editor preview convention.
            // Convert Editor quarter turns through the reflected Unity UV mapping rather than
            // treating the serialized value as a direct Unity UV rotation.
            var editorQuarterTurns = editorRotationDegrees / 90;
            return (1 - editorQuarterTurns + 4) % 4;
        }
    }

    internal readonly struct RuntimeFaceUnityOrientation
    {
        public RuntimeFaceUnityOrientation(CullMode cullMode, float normalSign)
        {
            CullMode = cullMode;
            NormalSign = normalSign;
        }

        public CullMode CullMode { get; }
        public float NormalSign { get; }
    }

    public sealed class RuntimeFaceRenderBinding
    {
        public RuntimeFaceRenderBinding(Renderer renderer, Material[] originalMaterials, int materialSlotIndex, Material runtimeMaterial)
        {
            Renderer = renderer;
            OriginalMaterials = originalMaterials != null ? (Material[])originalMaterials.Clone() : Array.Empty<Material>();
            MaterialSlotIndex = materialSlotIndex;
            RuntimeMaterial = runtimeMaterial;
        }

        public Renderer Renderer { get; private set; }
        public Material[] OriginalMaterials { get; private set; }
        public int MaterialSlotIndex { get; private set; }
        public Material RuntimeMaterial { get; private set; }
        public bool HasDynamicState { get; private set; }

        public void MarkDynamicStateDirty()
        {
            HasDynamicState = true;
        }

        public void BindLampState(RuntimeLampStateTexture lampStateTexture)
        {
            if (RuntimeMaterial != null && lampStateTexture != null && lampStateTexture.Texture != null && RuntimeMaterial.HasProperty(RuntimeFaceShaderProperties.LampStateTexture))
            {
                RuntimeMaterial.SetTexture(RuntimeFaceShaderProperties.LampStateTexture, lampStateTexture.Texture);
            }
        }

        public void ApplyDynamicState()
        {
            HasDynamicState = false;
        }

        public void Dispose()
        {
            if (Renderer != null && OriginalMaterials != null) Renderer.sharedMaterials = (Material[])OriginalMaterials.Clone();
            DestroyOwned(RuntimeMaterial);
            Renderer = null;
            OriginalMaterials = Array.Empty<Material>();
            RuntimeMaterial = null;
            HasDynamicState = false;
        }

        private static void DestroyOwned(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
