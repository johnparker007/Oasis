using System;
using OasisPlayer.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace OasisPlayer.UI.Controllers
{
    public sealed class GraphicsSettingsController
    {
        private readonly Action _close;
        private readonly GraphicsSettingsTransaction _transaction;
        private Slider _lampExposure;
        private Label _lampExposureValue;
        private Toggle _bloomEnabled;
        private VisualElement _bloomIntensityRow;
        private Slider _bloomIntensity;
        private Label _bloomIntensityValue;

        public GraphicsSettingsController(PlayerSettingsService settings, Action close)
        {
            _close = close;
            _transaction = new GraphicsSettingsTransaction(settings);
        }

        public bool IsBloomIntensityEnabled => _transaction.IsBloomIntensityEnabled;
        public PlayerGraphicsSettings Baseline => _transaction.Baseline;
        public PlayerGraphicsSettings Editable => _transaction.Editable;

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
            SetControls(_transaction.Editable);
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
            _transaction.Open();
        }

        public void SetLampExposure(float value)
        {
            SetControls(_transaction.SetLampExposure(value));
        }

        public void SetBloomEnabled(bool value)
        {
            SetControls(_transaction.SetBloomEnabled(value));
        }

        public void SetBloomIntensity(float value)
        {
            SetControls(_transaction.SetBloomIntensity(value));
        }

        public void Cancel()
        {
            _transaction.Cancel();
            _close?.Invoke();
        }

        public bool Apply()
        {
            var saved = _transaction.Apply();
            _close?.Invoke();
            return saved;
        }

        public void RestoreDefaults()
        {
            SetControls(_transaction.RestoreDefaults());
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
            var editable = _transaction.Editable;
            if (editable == null) return;
            if (_lampExposureValue != null) _lampExposureValue.text = $"{editable.LampExposureStops:0.0} stops";
            if (_bloomIntensityValue != null) _bloomIntensityValue.text = editable.BloomIntensity.ToString("0.0");
        }

        private void UpdateEnabledStates()
        {
            var enabled = _transaction.IsBloomIntensityEnabled;
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
