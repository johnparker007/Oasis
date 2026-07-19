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
                if (!TryRender(face, out var warning)) machine.AddWarning(warning);
            }
        }

        public bool TryRender(RuntimeFace face, out string warning)
        {
            warning = string.Empty;
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

            if (!TryResolveRenderer(face, out var renderer, out warning)) return false;
            if (!TryResolveMaterialSlot(face, renderer, out var slotIndex, out warning)) return false;
            if (!_materialFactory.TryCreate(face, out var runtimeMaterial, out warning)) return false;

            var originalMaterials = renderer.sharedMaterials;
            var replacementMaterials = (Material[])originalMaterials.Clone();
            replacementMaterials[slotIndex] = runtimeMaterial;
            renderer.sharedMaterials = replacementMaterials;

            face.SetRenderBinding(new RuntimeFaceRenderBinding(renderer, originalMaterials, slotIndex, runtimeMaterial, null));
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
                : $"Cabinet target '{TargetId(face)}' for Runtime Face '{FaceId(face)}' has {usableCount} usable renderers; static Face rendering skipped to avoid ambiguous material replacement.";
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

            warning = $"Cabinet target '{TargetId(face)}' for Runtime Face '{FaceId(face)}' has {materials.Length} material slots; static Face rendering skipped because the intended slot cannot be determined safely.";
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
        public const string BaseMapProperty = "_BaseMap";
        public const string MainTexProperty = "_MainTex";

        public bool TryCreate(RuntimeFace face, out Material material, out string warning)
        {
            material = null;
            warning = string.Empty;
            if (face == null || face.Artwork == null || face.Artwork.Texture == null)
            {
                warning = "Runtime Face material creation failed because artwork texture is invalid.";
                return false;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                warning = $"Runtime Face '{(face.Reference != null ? face.Reference.faceId : "<unknown>")}' could not render because no compatible Lit shader was available.";
                return false;
            }

            material = new Material(shader);
            material.name = $"RuntimeFace_{(face.Reference != null ? face.Reference.faceId : "Face")}_Static";
            AssignTexture(material, face.Artwork.Texture);
            ConfigureColor(material);
            ConfigureTransparency(material);
            return true;
        }

        private static void ConfigureColor(Material material)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        }

        private static void ConfigureTransparency(Material material)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void AssignTexture(Material material, Texture texture)
        {
            if (material.HasProperty(BaseMapProperty)) material.SetTexture(BaseMapProperty, texture);
            if (material.HasProperty(MainTexProperty)) material.SetTexture(MainTexProperty, texture);
            SetScaleOffset(material, BaseMapProperty);
            SetScaleOffset(material, MainTexProperty);
        }

        private static void SetScaleOffset(Material material, string propertyName)
        {
            if (!material.HasProperty(propertyName)) return;
            material.SetTextureScale(propertyName, Vector2.one);
            material.SetTextureOffset(propertyName, Vector2.zero);
        }
    }

    public sealed class RuntimeFaceRenderBinding
    {
        public RuntimeFaceRenderBinding(Renderer renderer, Material[] originalMaterials, int materialSlotIndex, Material runtimeMaterial, Texture2D generatedTexture)
        {
            Renderer = renderer;
            OriginalMaterials = originalMaterials != null ? (Material[])originalMaterials.Clone() : Array.Empty<Material>();
            MaterialSlotIndex = materialSlotIndex;
            RuntimeMaterial = runtimeMaterial;
            GeneratedTexture = generatedTexture;
        }

        public Renderer Renderer { get; private set; }
        public Material[] OriginalMaterials { get; private set; }
        public int MaterialSlotIndex { get; private set; }
        public Material RuntimeMaterial { get; private set; }
        public Texture2D GeneratedTexture { get; private set; }

        public void Dispose()
        {
            if (Renderer != null && OriginalMaterials != null) Renderer.sharedMaterials = (Material[])OriginalMaterials.Clone();
            DestroyOwned(RuntimeMaterial);
            DestroyOwned(GeneratedTexture);
            Renderer = null;
            OriginalMaterials = Array.Empty<Material>();
            RuntimeMaterial = null;
            GeneratedTexture = null;
        }

        private static void DestroyOwned(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
