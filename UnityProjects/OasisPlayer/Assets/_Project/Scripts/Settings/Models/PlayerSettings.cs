using System;

namespace OasisPlayer.Settings
{
    [Serializable]
    public sealed class PlayerSettings
    {
        public int Version = 1;
        public PlayerGraphicsSettings Graphics = PlayerGraphicsSettings.Defaults();

        public static PlayerSettings Defaults()
        {
            return new PlayerSettings();
        }

        public PlayerSettings Clone()
        {
            return new PlayerSettings
            {
                Version = Version,
                Graphics = Graphics != null ? Graphics.Clone() : PlayerGraphicsSettings.Defaults()
            };
        }

        public void Validate()
        {
            if (Version <= 0) Version = 1;
            if (Graphics == null) Graphics = PlayerGraphicsSettings.Defaults();
            Graphics.Validate();
        }
    }
}
