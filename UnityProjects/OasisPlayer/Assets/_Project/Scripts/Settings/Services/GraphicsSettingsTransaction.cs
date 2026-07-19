namespace OasisPlayer.Settings
{
    public sealed class GraphicsSettingsTransaction
    {
        private readonly PlayerSettingsService _settings;
        private PlayerGraphicsSettings _baseline;
        private PlayerGraphicsSettings _editable;

        public GraphicsSettingsTransaction(PlayerSettingsService settings)
        {
            _settings = settings;
        }

        public bool IsBloomIntensityEnabled => _editable != null && _editable.BloomEnabled;
        public PlayerGraphicsSettings Baseline => _baseline != null ? _baseline.Clone() : null;
        public PlayerGraphicsSettings Editable => _editable != null ? _editable.Clone() : null;

        public void Open()
        {
            _baseline = _settings.Graphics;
            _editable = _baseline.Clone();
        }

        public PlayerGraphicsSettings SetLampExposure(float value)
        {
            _editable.LampExposureStops = value;
            return Preview();
        }

        public PlayerGraphicsSettings SetBloomEnabled(bool value)
        {
            _editable.BloomEnabled = value;
            return Preview();
        }

        public PlayerGraphicsSettings SetBloomIntensity(float value)
        {
            if (!_editable.BloomEnabled) return _editable.Clone();
            _editable.BloomIntensity = value;
            return Preview();
        }

        public PlayerGraphicsSettings RestoreDefaults()
        {
            _editable = _settings.DefaultsForGraphics();
            return Preview();
        }

        public void Cancel()
        {
            _settings.PreviewGraphics(_baseline);
        }

        public bool Apply()
        {
            var saved = _settings.ApplyGraphics(_editable);
            if (saved) _baseline = _editable.Clone();
            return saved;
        }

        private PlayerGraphicsSettings Preview()
        {
            _editable.Validate();
            _settings.PreviewGraphics(_editable);
            return _editable.Clone();
        }
    }
}
