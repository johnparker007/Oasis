using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace OasisPlayer.Loading
{
    public interface ICabinetModelLoader
    {
        Task<GameObject> LoadAsync(string glbPath, Transform parent);
        void Unload(GameObject root);
    }

    public sealed class GltfFastCabinetModelLoader : ICabinetModelLoader
    {
        public async Task<GameObject> LoadAsync(string glbPath, Transform parent)
        {
            var sessionRoot = new GameObject("CabinetRuntimeModel");
            sessionRoot.transform.SetParent(parent, false);
            var gltf = new GltfImport();
            var loaded = await gltf.Load(glbPath);
            if (!loaded)
            {
                Object.Destroy(sessionRoot);
                throw new System.InvalidOperationException($"glTFast failed to load GLB: {glbPath}");
            }

            var instantiated = await gltf.InstantiateMainSceneAsync(sessionRoot.transform);
            if (!instantiated)
            {
                Object.Destroy(sessionRoot);
                throw new System.InvalidOperationException($"glTFast failed to instantiate GLB: {glbPath}");
            }

            return sessionRoot;
        }

        public void Unload(GameObject root)
        {
            if (root != null) Object.Destroy(root);
        }
    }
}
