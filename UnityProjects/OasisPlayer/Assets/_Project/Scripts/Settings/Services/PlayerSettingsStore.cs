using System;
using System.IO;
using UnityEngine;

namespace OasisPlayer.Settings
{
    public sealed class PlayerSettingsStore
    {
        public string SettingsPath { get; }

        public PlayerSettingsStore(string settingsPath = null)
        {
            SettingsPath = string.IsNullOrWhiteSpace(settingsPath)
                ? Path.Combine(Application.persistentDataPath, "player-settings.json")
                : settingsPath;
        }

        public PlayerSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return PlayerSettings.Defaults();
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonUtility.FromJson<PlayerSettings>(json) ?? PlayerSettings.Defaults();
                settings.Validate();
                return settings;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Oasis Player settings could not be loaded from '{SettingsPath}'; defaults will be used. {ex.Message}");
                return PlayerSettings.Defaults();
            }
        }

        public bool Save(PlayerSettings settings)
        {
            try
            {
                var copy = (settings ?? PlayerSettings.Defaults()).Clone();
                copy.Validate();
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, JsonUtility.ToJson(copy, true));
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Oasis Player settings could not be saved to '{SettingsPath}'. {ex.Message}");
                return false;
            }
        }
    }
}
