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

        private GameObject _documentHost;
        private UIDocument _document;
        private VisualTreeAsset _graphicsView;
        private StyleSheet _theme;
        private ThemeStyleSheet _runtimeTheme;
        private PanelSettings _panelSettingsAsset;
        private GraphicsSettingsController _controller;
        private bool _open;
        private bool _previousCursorVisible;
        private bool _themeAttached;
        private bool _escapeConsumed;
        private int _openVersion;
        private CursorLockMode _previousLockMode;

        private void Awake()
        {
            _graphicsView = Resources.Load<VisualTreeAsset>(GraphicsSettingsViewPath);
            _theme = Resources.Load<StyleSheet>(ThemePath);
            _runtimeTheme = Resources.Load<ThemeStyleSheet>(RuntimeThemePath);
            _panelSettingsAsset = Resources.Load<PanelSettings>(PanelSettingsPath);

            _documentHost = new GameObject("Oasis Runtime UI Document");
            _documentHost.transform.SetParent(transform, false);
            _documentHost.SetActive(false);

            _document = _documentHost.AddComponent<UIDocument>();
            _document.panelSettings = CreateRuntimePanelSettings(_panelSettingsAsset, _runtimeTheme);
            _document.visualTreeAsset = null;
            _document.sortingOrder = short.MaxValue;
            _document.enabled = true;

            if (_document.panelSettings != null)
            {
                _documentHost.SetActive(true);
                var root = _document.rootVisualElement;
                FillPanel(root);
                root.Clear();
                root.pickingMode = PickingMode.Ignore;
            }
        }

        private void Update()
        {
            if (_escapeConsumed)
            {
                if (!Input.GetKey(KeyCode.Escape)) _escapeConsumed = false;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_open)
                {
                    _escapeConsumed = true;
                    if (_controller != null) _controller.Cancel();
                    else CloseGraphicsSettings();
                    return;
                }

                _escapeConsumed = true;
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

            if (!_documentHost.activeSelf) _documentHost.SetActive(true);
            _document.sortingOrder = short.MaxValue;

            var root = _document.rootVisualElement;
            root.Clear();
            FillPanel(root);
            root.pickingMode = PickingMode.Position;

            _open = true;
            _openVersion++;
            var openVersion = _openVersion;
            _document.visualTreeAsset = _graphicsView;
            root.schedule.Execute(() => CompleteOpen(openVersion)).ExecuteLater(0);
        }

        private void CompleteOpen(int openVersion)
        {
            if (!_open || openVersion != _openVersion) return;

            var root = _document.rootVisualElement;
            FillPanel(root);
            AttachTheme(root);

            var graphicsRoot = Require<VisualElement>(root, "graphics-settings-root");
            AttachTheme(graphicsRoot);
            FillPanel(graphicsRoot);
            ApplyCriticalLayoutFallback(root, graphicsRoot);

            foreach (var child in root.Children())
            {
                FillPanel(child);
            }

            Debug.Log($"Oasis Player UI diagnostics: viewLoaded=True stylesheetLoaded=True runtimeThemeLoaded=True panelSettingsLoaded=True sourceAssetAssigned={_document.visualTreeAsset != null} sortOrder={_document.sortingOrder} rootChildCount={root.childCount} graphicsRoot={Exists<VisualElement>(root, "graphics-settings-root")} lampExposure={Exists<Slider>(root, "lamp-exposure")} bloomEnabled={Exists<Toggle>(root, "bloom-enabled")} applyButton={Exists<Button>(root, "apply-button")}");

            ValidateConstructedTree(root);

            _controller = new GraphicsSettingsController(PlayerSettingsService.EnsureGlobal(), CloseGraphicsSettings);
            _controller.Bind(root);
            Debug.Log("Oasis Player UI diagnostics: GraphicsSettingsController.Bind completed.");
        }

        private void CloseGraphicsSettings()
        {
            _openVersion++;
            _document.visualTreeAsset = null;
            var root = _document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
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

        private static void ApplyCriticalLayoutFallback(VisualElement root, VisualElement graphicsRoot)
        {
            graphicsRoot.style.alignItems = Align.Center;
            graphicsRoot.style.justifyContent = Justify.Center;
            graphicsRoot.style.paddingLeft = 24f;
            graphicsRoot.style.paddingRight = 24f;
            graphicsRoot.style.paddingTop = 24f;
            graphicsRoot.style.paddingBottom = 24f;
            graphicsRoot.style.backgroundColor = new Color(0f, 0f, 0f, 0.58f);

            var panel = root.Q<VisualElement>(className: "oasis-panel");
            if (panel != null)
            {
                panel.style.width = 680f;
                panel.style.paddingLeft = 28f;
                panel.style.paddingRight = 28f;
                panel.style.paddingTop = 28f;
                panel.style.paddingBottom = 28f;
                panel.style.backgroundColor = new Color(0.086f, 0.094f, 0.118f, 0.96f);
                panel.style.color = new Color(0.92f, 0.93f, 0.96f, 1f);
            }

            var header = root.Q<VisualElement>(className: "oasis-header");
            if (header != null)
            {
                header.style.flexDirection = FlexDirection.Row;
                header.style.justifyContent = Justify.SpaceBetween;
                header.style.alignItems = Align.FlexStart;
                header.style.marginBottom = 16f;
            }

            var title = root.Q<Label>(className: "oasis-title");
            if (title != null)
            {
                title.style.fontSize = 30f;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            root.Query<VisualElement>(className: "oasis-row").ForEach(row =>
            {
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 8f;
                row.style.marginBottom = 8f;
            });

            root.Query<Label>().ForEach(label => label.style.color = new Color(0.92f, 0.93f, 0.96f, 1f));
            root.Query<Label>(className: "oasis-section").ForEach(label =>
            {
                label.style.fontSize = 17f;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = new Color(0.65f, 0.81f, 1f, 1f);
                label.style.marginTop = 16f;
                label.style.marginBottom = 8f;
            });
            root.Query<Label>(className: "oasis-label").ForEach(label => label.style.width = 190f);
            root.Query<Slider>(className: "oasis-slider").ForEach(slider => slider.style.width = 320f);
            root.Query<Label>(className: "oasis-value").ForEach(label => label.style.width = 90f);

            var buttons = root.Q<VisualElement>(className: "oasis-buttons");
            if (buttons != null)
            {
                buttons.style.flexDirection = FlexDirection.Row;
                buttons.style.justifyContent = Justify.FlexEnd;
                buttons.style.marginTop = 28f;
                buttons.Query<Button>().ForEach(button =>
                {
                    button.style.width = 130f;
                    button.style.flexGrow = 0f;
                    button.style.marginLeft = 10f;
                });
            }
        }

        private void AttachTheme(VisualElement element)
        {
            if (!element.styleSheets.Contains(_theme)) element.styleSheets.Add(_theme);
            _themeAttached = true;
        }

        private static void FillPanel(VisualElement element)
        {
            element.style.flexGrow = 1f;
            element.style.width = Length.Percent(100f);
            element.style.height = Length.Percent(100f);
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
