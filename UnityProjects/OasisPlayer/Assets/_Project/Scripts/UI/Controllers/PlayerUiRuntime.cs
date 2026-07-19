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

        private UIDocument _document;
        private VisualTreeAsset _graphicsView;
        private StyleSheet _theme;
        private GraphicsSettingsController _controller;
        private bool _open;
        private bool _previousCursorVisible;
        private bool _themeAttached;
        private CursorLockMode _previousLockMode;

        private void Awake()
        {
            _graphicsView = Resources.Load<VisualTreeAsset>(GraphicsSettingsViewPath);
            _theme = Resources.Load<StyleSheet>(ThemePath);
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = CreateRuntimePanelSettings();
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

            Debug.Log($"Oasis Player UI diagnostics: viewLoaded=True stylesheetLoaded=True rootChildCount={root.childCount} graphicsRoot={Exists<VisualElement>(root, "graphics-settings-root")} lampExposure={Exists<Slider>(root, "lamp-exposure")} bloomEnabled={Exists<Toggle>(root, "bloom-enabled")} applyButton={Exists<Button>(root, "apply-button")}");

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

        private static PanelSettings CreateRuntimePanelSettings()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "Oasis Runtime Panel Settings";
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
