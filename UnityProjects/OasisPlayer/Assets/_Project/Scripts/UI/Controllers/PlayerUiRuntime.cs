using System;
using OasisPlayer.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace OasisPlayer.UI.Controllers
{
    public sealed class PlayerUiRuntime : MonoBehaviour
    {
        private const string GraphicsSettingsViewPath = "UI/Uxml/GraphicsSettings";
        private const string ThemePath = "UI/Styles/OasisPlayerTheme";
        private const string RuntimeThemePath = "UI/Styles/OasisPlayerRuntimeTheme";
        private const string PanelSettingsPath = "UI/PanelSettings/OasisRuntimePanelSettings";

        private UIDocument _document;
        private VisualTreeAsset _graphicsView;
        private StyleSheet _theme;
        private ThemeStyleSheet _runtimeTheme;
        private PanelSettings _panelSettingsAsset;
        private GraphicsSettingsController _controller;
        private bool _open;
        private bool _previousCursorVisible;
        private bool _themeAttached;
        private CursorLockMode _previousLockMode;

        private void Awake()
        {
            _graphicsView = Resources.Load<VisualTreeAsset>(GraphicsSettingsViewPath);
            _theme = Resources.Load<StyleSheet>(ThemePath);
            _runtimeTheme = Resources.Load<ThemeStyleSheet>(RuntimeThemePath);
            _panelSettingsAsset = Resources.Load<PanelSettings>(PanelSettingsPath);
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = CreateRuntimePanelSettings(_panelSettingsAsset, _runtimeTheme);
            _document.visualTreeAsset = null;
            _document.enabled = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_open)
                {
                    _controller?.Cancel();
                    return;
                }

                OpenGraphicsSettings();
            }
        }

        private void OpenGraphicsSettings()
        {
            if (_graphicsView == null)
            {
                Debug.LogError($"Oasis Player UI could not load Resources/{GraphicsSettingsViewPath}.uxml.");
                return;
            }

            if (_theme == null)
            {
                Debug.LogError($"Oasis Player UI could not load Resources/{ThemePath}.uss. The Graphics Settings menu cannot be shown without its runtime stylesheet.");
                return;
            }

            if (_document.panelSettings == null)
            {
                Debug.LogError($"Oasis Player UI could not load Resources/{PanelSettingsPath}.asset. Open the Unity Editor once so OasisRuntimePanelSettingsGenerator can create it before entering Play Mode or building the Player.");
                return;
            }

            if (_document.panelSettings.themeStyleSheet == null)
            {
                Debug.LogError($"Oasis Player UI could not load Resources/{RuntimeThemePath}.tss. PanelSettings requires a Theme Style Sheet for UI Toolkit controls to render correctly.");
                return;
            }

            _previousCursorVisible = UnityEngine.Cursor.visible;
            _previousLockMode = UnityEngine.Cursor.lockState;
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;

            _document.enabled = true;
            _document.visualTreeAsset = null;

            var root = _document.rootVisualElement;
            root.Clear();
            if (!_themeAttached)
            {
                root.styleSheets.Add(_theme);
                _themeAttached = true;
            }
            root.pickingMode = PickingMode.Position;
            _graphicsView.CloneTree(root);

            Debug.Log($"Oasis Player UI diagnostics: viewLoaded=True stylesheetLoaded=True runtimeThemeLoaded=True panelSettingsLoaded=True rootChildCount={root.childCount} graphicsRoot={Exists<VisualElement>(root, "graphics-settings-root")} lampExposure={Exists<Slider>(root, "lamp-exposure")} bloomEnabled={Exists<Toggle>(root, "bloom-enabled")} applyButton={Exists<Button>(root, "apply-button")}");

            ValidateConstructedTree(root);

            _open = true;
            _controller = new GraphicsSettingsController(PlayerSettingsService.EnsureGlobal(), CloseGraphicsSettings);
            _controller.Bind(root);
            Debug.Log("Oasis Player UI diagnostics: GraphicsSettingsController.Bind completed.");
        }

        private void CloseGraphicsSettings()
        {
            _document.rootVisualElement.Clear();
            _document.enabled = false;
            _controller = null;
            _open = false;
            UnityEngine.Cursor.visible = _previousCursorVisible;
            UnityEngine.Cursor.lockState = _previousLockMode;
        }

        private static PanelSettings CreateRuntimePanelSettings(PanelSettings panelSettingsAsset, ThemeStyleSheet runtimeTheme)
        {
            if (panelSettingsAsset == null) return null;

            var panelSettings = Instantiate(panelSettingsAsset);
            panelSettings.name = "Oasis Runtime Panel Settings";
            panelSettings.themeStyleSheet = runtimeTheme != null ? runtimeTheme : panelSettings.themeStyleSheet;
            panelSettings.targetDisplay = 0;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = short.MaxValue;
            return panelSettings;
        }

        private static void ValidateConstructedTree(VisualElement root)
        {
            Require<VisualElement>(root, "graphics-settings-root");
            Require<Slider>(root, "lamp-exposure");
            Require<Toggle>(root, "bloom-enabled");
            Require<Button>(root, "apply-button");
        }

        private static bool Exists<T>(VisualElement root, string name) where T : VisualElement
        {
            return root.Q<T>(name) != null;
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element != null) return element;

            throw new InvalidOperationException($"Graphics Settings UXML is missing required {typeof(T).Name} '{name}'.");
        }
    }
}
