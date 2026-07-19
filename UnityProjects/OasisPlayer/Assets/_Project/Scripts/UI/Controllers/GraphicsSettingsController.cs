using System;
using OasisPlayer.Settings;
using UnityEngine;
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
        private VisualElement _bloomIntensityRow;
        private Slider _bloomIntensity;
        private Label _bloomIntensityValue;

        public GraphicsSettingsController(PlayerSettingsService settings, Action close)
        {
            _settings = settings;
            _close = close;
        }

        public bool IsBloomIntensityEnabled => _editable != null && _editable.BloomEnabled;
        public PlayerGraphicsSettings Baseline => _baseline != null ? _baseline.Clone() : null;
        public PlayerGraphicsSettings Editable => _editable != null ? _editable.Clone() : null;

        public void Bind(VisualElement root)
        {
            Open();
            _lampExposure = root.Q<Slider>("lamp-exposure");
            _lampExposureValue = root.Q<Label>("lamp-exposure-value");
            _bloomEnabled = root.Q<Toggle>("bloom-enabled");
            _bloomIntensityRow = root.Q<VisualElement>("bloom-intensity-row");
            _bloomIntensity = root.Q<Slider>("bloom-intensity");
            _bloomIntensityValue = root.Q<Label>("bloom-intensity-value");

            ConfigureRanges();
            SetControls(_editable);
            _lampExposure.RegisterValueChangedCallback(e => SetLampExposure(e.newValue));
            _bloomEnabled.RegisterValueChangedCallback(e => SetBloomEnabled(e.newValue));
            _bloomIntensity.RegisterValueChangedCallback(e => SetBloomIntensity(e.newValue));
            root.Q<Button>("close-button").clicked += Cancel;
            root.Q<Button>("restore-defaults-button").clicked += RestoreDefaults;
            root.Q<Button>("cancel-button").clicked += Cancel;
            root.Q<Button>("apply-button").clicked += () => Apply();
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _lampExposure.Focus();
        }

        public void Open()
        {
            _baseline = _settings.Graphics;
            _editable = _baseline.Clone();
        }

        public void SetLampExposure(float value)
        {
            _editable.LampExposureStops = value;
            Preview();
        }

        public void SetBloomEnabled(bool value)
        {
            _editable.BloomEnabled = value;
            Preview();
        }

        public void SetBloomIntensity(float value)
        {
            if (!_editable.BloomEnabled) return;
            _editable.BloomIntensity = value;
            Preview();
        }

        public void Cancel()
        {
            _settings.PreviewGraphics(_baseline);
            _close?.Invoke();
        }

        public bool Apply()
        {
            var saved = _settings.ApplyGraphics(_editable);
            if (saved) _baseline = _editable.Clone();
            _close?.Invoke();
            return saved;
        }

        public void RestoreDefaults()
        {
            _editable = _settings.DefaultsForGraphics();
            SetControls(_editable);
            Preview();
        }

        private void Preview()
        {
            _editable.Validate();
            UpdateValueLabels();
            UpdateEnabledStates();
            _settings.PreviewGraphics(_editable);
        }

        private void ConfigureRanges()
        {
            _lampExposure.lowValue = PlayerGraphicsSettings.MinLampExposureStops;
            _lampExposure.highValue = PlayerGraphicsSettings.MaxLampExposureStops;
            _bloomIntensity.lowValue = PlayerGraphicsSettings.MinBloomIntensity;
            _bloomIntensity.highValue = PlayerGraphicsSettings.MaxBloomIntensity;
        }

        private void SetControls(PlayerGraphicsSettings settings)
        {
            settings.Validate();
            if (_lampExposure != null) _lampExposure.SetValueWithoutNotify(settings.LampExposureStops);
            if (_bloomEnabled != null) _bloomEnabled.SetValueWithoutNotify(settings.BloomEnabled);
            if (_bloomIntensity != null) _bloomIntensity.SetValueWithoutNotify(settings.BloomIntensity);
            UpdateValueLabels();
            UpdateEnabledStates();
        }

        private void UpdateValueLabels()
        {
            if (_lampExposureValue != null) _lampExposureValue.text = $"{_editable.LampExposureStops:0.0} stops";
            if (_bloomIntensityValue != null) _bloomIntensityValue.text = _editable.BloomIntensity.ToString("0.0");
        }

        private void UpdateEnabledStates()
        {
            var enabled = _editable.BloomEnabled;
            if (_bloomIntensity != null) _bloomIntensity.SetEnabled(enabled);
            if (_bloomIntensityRow == null) return;
            _bloomIntensityRow.EnableInClassList("oasis-disabled-row", !enabled);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape) return;
            evt.StopPropagation();
            Cancel();
        }
    }
}
