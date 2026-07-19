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
        {
            Reference = reference;
            Manifest = manifest;
            CabinetTarget = cabinetTarget;
            Artwork = artwork;
            Mask = mask;
        }

        public MachineRuntimeFaceReference Reference { get; private set; }
        public FaceRuntimeManifest Manifest { get; private set; }
        public Transform CabinetTarget { get; private set; }
        public RuntimeTextureAsset Artwork { get; private set; }
        public RuntimeTextureAsset Mask { get; private set; }

        public void UnloadAssets()
        {
            if (Artwork != null) Artwork.Unload();
            if (Mask != null) Mask.Unload();
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
                UnityEngine.Object.Destroy(Texture);
                Texture = null;
            }
        }
    }
}
