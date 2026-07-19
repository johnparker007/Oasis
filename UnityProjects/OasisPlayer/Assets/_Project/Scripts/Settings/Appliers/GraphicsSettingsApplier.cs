using OasisPlayer.RuntimeBuild;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OasisPlayer.Settings
{
    public sealed class GraphicsSettingsApplier : MonoBehaviour
    {
        private PlayerSettingsService _settings;
        private Bloom _bloom;

        private void Awake()
        {
            _settings = PlayerSettingsService.EnsureGlobal();
            _settings.GraphicsChanged += Apply;
            EnsureBloomVolume();
            Apply(_settings.Graphics);
        }

        private void OnDestroy()
        {
            if (_settings != null) _settings.GraphicsChanged -= Apply;
        }

        private void Apply(PlayerGraphicsSettings graphics)
        {
            RuntimeFaceMaterialRegistry.Apply(graphics);
            if (_bloom == null) EnsureBloomVolume();
            if (_bloom != null)
            {
                _bloom.active = graphics.BloomEnabled;
                _bloom.intensity.overrideState = true;
                _bloom.intensity.value = graphics.BloomIntensity;
            }
        }

        private void EnsureBloomVolume()
        {
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (volume != null && volume.profile != null && volume.profile.TryGet(out _bloom)) return;
            }

            var go = new GameObject("Oasis Global Graphics Volume");
            DontDestroyOnLoad(go);
            var volumeComponent = go.AddComponent<Volume>();
            volumeComponent.isGlobal = true;
            volumeComponent.priority = 100f;
            volumeComponent.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volumeComponent.profile.name = "Oasis Runtime Graphics Volume";
            _bloom = volumeComponent.profile.Add<Bloom>(true);
        }
    }
}
