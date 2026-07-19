using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace OasisPlayer.Editor
{
    [InitializeOnLoad]
    internal static class OasisRuntimePanelSettingsGenerator
    {
        private const string PanelSettingsPath = "Assets/Resources/UI/PanelSettings/OasisRuntimePanelSettings.asset";
        private const string RuntimeThemePath = "Assets/Resources/UI/Styles/OasisPlayerRuntimeTheme.tss";

        static OasisRuntimePanelSettingsGenerator()
        {
            EditorApplication.delayCall += EnsurePanelSettingsAsset;
        }

        private static void EnsurePanelSettingsAsset()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath);
            if (theme == null)
            {
                Debug.LogError($"Oasis Player UI could not create runtime PanelSettings because '{RuntimeThemePath}' is missing or failed to import.");
                return;
            }

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PanelSettingsPath));
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            Configure(panelSettings, theme);
            EditorUtility.SetDirty(panelSettings);
            AssetDatabase.SaveAssets();
        }

        private static void Configure(PanelSettings panelSettings, ThemeStyleSheet theme)
        {
            panelSettings.themeStyleSheet = theme;
            panelSettings.targetDisplay = 0;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = short.MaxValue;
        }
    }
}
