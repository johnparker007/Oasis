using System;
using UnityEngine;

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
        private const float DefaultEmissionStrength = 1.75f;
        private const float DefaultLampLift = 0.35f;

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
            // Keep the base artwork at authored brightness while adding a separate lamp emission pass.
            // LampLift lets masked lamps remain visible over dark artwork even though runtime exports do
            // not yet include per-lamp colours or illuminated artwork textures.
            if (material.HasProperty(RuntimeFaceShaderProperties.StaticBrightness)) material.SetFloat(RuntimeFaceShaderProperties.StaticBrightness, DefaultStaticBrightness);
            if (material.HasProperty(RuntimeFaceShaderProperties.MaskStrength)) material.SetFloat(RuntimeFaceShaderProperties.MaskStrength, DefaultMaskStrength);
            if (material.HasProperty(RuntimeFaceShaderProperties.EmissionStrength)) material.SetFloat(RuntimeFaceShaderProperties.EmissionStrength, DefaultEmissionStrength);
            if (material.HasProperty(RuntimeFaceShaderProperties.LampLift)) material.SetFloat(RuntimeFaceShaderProperties.LampLift, DefaultLampLift);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return true;
        }

        private static void BindTextures(Material material, RuntimeFace face)
        {
            AssignTexture(material, RuntimeFaceShaderProperties.ArtworkTexture, face.Artwork.Texture);
            AssignTexture(material, RuntimeFaceShaderProperties.MaskTexture, face.Mask.Texture);
            if (face.TrayId != null) AssignTexture(material, RuntimeFaceShaderProperties.TrayIdTexture, face.TrayId.Texture);
            if (face.LampIds0 != null) AssignTexture(material, RuntimeFaceShaderProperties.LampIds0Texture, face.LampIds0.Texture);
            if (face.LampWeights0 != null) AssignTexture(material, RuntimeFaceShaderProperties.LampWeights0Texture, face.LampWeights0.Texture);
        }

        private static void AssignTexture(Material material, int propertyId, Texture texture)
        {
            if (!material.HasProperty(propertyId)) return;
            material.SetTexture(propertyId, texture);
            material.SetTextureScale(propertyId, Vector2.one);
            material.SetTextureOffset(propertyId, Vector2.zero);
        }
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
