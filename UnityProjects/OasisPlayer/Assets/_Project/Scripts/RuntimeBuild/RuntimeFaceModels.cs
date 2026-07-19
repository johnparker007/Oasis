using System.Collections.Generic;
using UnityEngine;

namespace OasisPlayer.RuntimeBuild
{
    public sealed class RuntimeMachine
    {
        private readonly List<RuntimeFace> _faces = new List<RuntimeFace>();
        private readonly List<string> _warnings = new List<string>();

        public RuntimeMachine(ResolvedRuntimeBuild build, GameObject cabinet)
        {
            Build = build;
            Cabinet = cabinet;
        }

        public ResolvedRuntimeBuild Build { get; private set; }
        public GameObject Cabinet { get; private set; }
        public IReadOnlyList<RuntimeFace> Faces { get { return _faces; } }
        public IReadOnlyList<string> Warnings { get { return _warnings; } }

        public void RegisterFace(RuntimeFace face)
        {
            if (face != null) _faces.Add(face);
        }

        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning)) _warnings.Add(warning);
        }

        public void UnloadAssets()
        {
            foreach (var face in _faces)
            {
                face.UnloadAssets();
            }

            _faces.Clear();
            _warnings.Clear();
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

        public void SetRenderBinding(RuntimeFaceRenderBinding renderBinding)
        {
            RenderBinding = renderBinding;
        }

        public void UnloadAssets()
        {
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
