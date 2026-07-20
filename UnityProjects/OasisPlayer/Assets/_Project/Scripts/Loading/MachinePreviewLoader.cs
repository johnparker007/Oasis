using System;
using System.Linq;
using System.Threading.Tasks;
using OasisPlayer.RuntimeBuild;
using UnityEngine;

namespace OasisPlayer.Loading
{
    public sealed class MachinePreviewLoader
    {
        private readonly ICabinetModelLoader _modelLoader;
        private readonly RuntimeFaceLoader _faceLoader;
        private readonly RuntimeFaceRenderer _faceRenderer;
        private readonly RuntimeReelRenderer _reelRenderer = new RuntimeReelRenderer();
        private GameObject _current;
        private RuntimeMachine _runtimeMachine;

        public MachinePreviewLoader(ICabinetModelLoader modelLoader)
            : this(modelLoader, new RuntimeFaceLoader(new PngRuntimeTextureAssetLoader()), new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory()))
        {
        }

        public MachinePreviewLoader(ICabinetModelLoader modelLoader, RuntimeFaceLoader faceLoader)
            : this(modelLoader, faceLoader, new RuntimeFaceRenderer(new RuntimeFaceMaterialFactory()))
        {
        }

        public MachinePreviewLoader(ICabinetModelLoader modelLoader, RuntimeFaceLoader faceLoader, RuntimeFaceRenderer faceRenderer)
        {
            _modelLoader = modelLoader;
            _faceLoader = faceLoader;
            _faceRenderer = faceRenderer;
        }

        public async Task<RuntimeMachine> LoadAsync(ResolvedRuntimeBuild build)
        {
            Unload();
            var spawns = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.parent == null && t.name == "MachineSpawn")
                .ToArray();
            if (spawns.Length != 1)
            {
                throw new InvalidOperationException(spawns.Length == 0
                    ? "MachinePreview scene is missing root transform 'MachineSpawn'."
                    : "MachinePreview scene contains duplicate root transforms named 'MachineSpawn'.");
            }

            var correctionRoot = new GameObject("OasisCabinetCorrectionRoot");
            correctionRoot.transform.SetParent(spawns[0], false);
            correctionRoot.transform.localScale = Vector3.one * Mathf.Max(0.0001f, build.Cabinet.scale);
            correctionRoot.transform.localRotation = build.Cabinet.upAxis == "Z" ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            _current = correctionRoot;
            var cabinet = await _modelLoader.LoadAsync(build.GlbPath, correctionRoot.transform);
            var machine = new RuntimeMachine(build, cabinet);
            _runtimeMachine = machine;
            _faceLoader.LoadFaces(machine);
            _faceRenderer.RenderFaces(machine);
            _reelRenderer.RenderReels(machine);
            var updater = correctionRoot.AddComponent<RuntimeMachineLampUpdater>();
            updater.Initialize(machine);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var controls = correctionRoot.AddComponent<RuntimeLampDevelopmentControls>();
            controls.Initialize(machine);
#endif
            foreach (var warning in machine.Warnings) Debug.LogWarning(warning);
            return machine;
        }

        public void Unload()
        {
            if (_runtimeMachine != null)
            {
                _runtimeMachine.UnloadAssets();
                _runtimeMachine = null;
            }

            if (_current != null)
            {
                _modelLoader.Unload(_current);
                _current = null;
            }
        }
    }
}
