using System;

namespace OasisPlayer.Settings
{
    public sealed class PlayerSettingsService
    {
        public static PlayerSettingsService Instance { get; private set; }
        private readonly PlayerSettingsStore _store;
        private PlayerSettings _active;

        public event Action<PlayerGraphicsSettings> GraphicsChanged;
        public PlayerGraphicsSettings Graphics => _active.Graphics.Clone();

        public PlayerSettingsService(PlayerSettingsStore store = null)
        {
            _store = store ?? new PlayerSettingsStore();
            _active = _store.Load();
            _active.Validate();
        }

        public static PlayerSettingsService EnsureGlobal(PlayerSettingsStore store = null)
        {
            return Instance ??= new PlayerSettingsService(store);
        }

        public void PreviewGraphics(PlayerGraphicsSettings graphics)
        {
            SetGraphics(graphics);
        }

        public bool ApplyGraphics(PlayerGraphicsSettings graphics)
        {
            SetGraphics(graphics);
            return _store.Save(_active);
        }

        public PlayerGraphicsSettings DefaultsForGraphics()
        {
            return PlayerGraphicsSettings.Defaults();
        }

        private void SetGraphics(PlayerGraphicsSettings graphics)
        {
            _active.Graphics = (graphics ?? PlayerGraphicsSettings.Defaults()).Clone();
            _active.Validate();
            GraphicsChanged?.Invoke(_active.Graphics.Clone());
        }
    }
}
