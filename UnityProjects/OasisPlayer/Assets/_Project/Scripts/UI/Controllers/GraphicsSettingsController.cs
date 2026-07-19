using System;
using OasisPlayer.Settings;
using UnityEngine.UIElements;

namespace OasisPlayer.UI.Controllers
{
    public sealed class GraphicsSettingsController
    {
        private readonly PlayerSettingsService _settings;
        private readonly Action _close;
        private PlayerGraphicsSettings _baseline;
        private PlayerGraphicsSettings _editable;
        private Slider _lampExposure;
        private Label _lampExposureValue;
        private Toggle _bloomEnabled;
        private Slider _bloomIntensity;
        private Label _bloomIntensityValue;

        public GraphicsSettingsController(PlayerSettingsService settings, Action close)
        {
            _settings = settings;
            _close = close;
        }

        public void Bind(VisualElement root)
        {
            _baseline = _settings.Graphics;
            _editable = _baseline.Clone();
            _lampExposure = root.Q<Slider>("lamp-exposure");
            _lampExposureValue = root.Q<Label>("lamp-exposure-value");
            _bloomEnabled = root.Q<Toggle>("bloom-enabled");
            _bloomIntensity = root.Q<Slider>("bloom-intensity");
            _bloomIntensityValue = root.Q<Label>("bloom-intensity-value");

            SetControls(_editable);
            _lampExposure.RegisterValueChangedCallback(e => { _editable.LampExposureStops = e.newValue; Preview(); });
            _bloomEnabled.RegisterValueChangedCallback(e => { _editable.BloomEnabled = e.newValue; Preview(); });
            _bloomIntensity.RegisterValueChangedCallback(e => { _editable.BloomIntensity = e.newValue; Preview(); });
            root.Q<Button>("restore-defaults-button").clicked += RestoreDefaults;
            root.Q<Button>("cancel-button").clicked += Cancel;
            root.Q<Button>("apply-button").clicked += Apply;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _lampExposure.Focus();
        }

        public void Cancel()
        {
            _settings.PreviewGraphics(_baseline);
            _close?.Invoke();
        }

        private void Apply()
        {
            if (_settings.ApplyGraphics(_editable)) _baseline = _editable.Clone();
            _close?.Invoke();
        }

        private void RestoreDefaults()
        {
            _editable = _settings.DefaultsForGraphics();
            SetControls(_editable);
            Preview();
        }

        private void Preview()
        {
            _editable.Validate();
            UpdateValueLabels();
            _settings.PreviewGraphics(_editable);
        }

        private void SetControls(PlayerGraphicsSettings settings)
        {
            _lampExposure.SetValueWithoutNotify(settings.LampExposureStops);
            _bloomEnabled.SetValueWithoutNotify(settings.BloomEnabled);
            _bloomIntensity.SetValueWithoutNotify(settings.BloomIntensity);
            UpdateValueLabels();
        }

        private void UpdateValueLabels()
        {
            _lampExposureValue.text = $"{_editable.LampExposureStops:0.0} stops";
            _bloomIntensityValue.text = _editable.BloomIntensity.ToString("0.0");
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != UnityEngine.KeyCode.Escape) return;
            evt.StopPropagation();
            Cancel();
        }
    }
}
