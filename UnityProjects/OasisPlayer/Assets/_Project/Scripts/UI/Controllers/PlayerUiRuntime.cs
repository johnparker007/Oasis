using System;
using OasisPlayer.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace OasisPlayer.UI.Controllers
{
    public sealed class PlayerUiRuntime : MonoBehaviour
    {
        private const string GraphicsSettingsViewPath = "UI/Uxml/GraphicsSettings";
        private const string PanelSettingsPath = "UI/PanelSettings/OasisRuntimePanelSettings";

        private GameObject _documentHost;
        private UIDocument _document;
        private VisualTreeAsset _graphicsView;
        private PanelSettings _panelSettingsAsset;
        private GraphicsSettingsController _controller;
        private bool _open;
        private bool _previousCursorVisible;
        private int _openVersion;
        private CursorLockMode _previousLockMode;

        private void Awake()
        {
            _graphicsView = Resources.Load<VisualTreeAsset>(GraphicsSettingsViewPath);
            _panelSettingsAsset = Resources.Load<PanelSettings>(PanelSettingsPath);

            _documentHost = new GameObject("Oasis Runtime UI Document");
            _documentHost.transform.SetParent(transform, false);
            _documentHost.SetActive(false);

            _document = _documentHost.AddComponent<UIDocument>();
            _document.panelSettings = CreateRuntimePanelSettings(_panelSettingsAsset);
            _document.visualTreeAsset = null;
            _document.sortingOrder = short.MaxValue;
            _document.enabled = true;

            if (_document.panelSettings != null)
            {
                _documentHost.SetActive(true);
                CloseDocumentRoot();
            }
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (_open) CancelAndClose();
            else OpenGraphicsSettings();
        }

        private void OpenGraphicsSettings()
        {
            if (_graphicsView == null)
            {
                Debug.LogError($"Oasis Player UI could not load Resources/{GraphicsSettingsViewPath}.uxml.");
                return;
            }

            if (_document.panelSettings == null)
            {
                Debug.LogError($"Oasis Player UI could not load Resources/{PanelSettingsPath}.asset.");
                return;
            }

            _previousCursorVisible = UnityEngine.Cursor.visible;
            _previousLockMode = UnityEngine.Cursor.lockState;
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;

            if (!_documentHost.activeSelf) _documentHost.SetActive(true);
            _document.sortingOrder = short.MaxValue;
            _document.visualTreeAsset = _graphicsView;
            _document.rootVisualElement.pickingMode = PickingMode.Position;

            _open = true;
            _openVersion++;
            var openVersion = _openVersion;

            // UIDocument instantiates its assigned VisualTreeAsset asynchronously during the panel update.
            // Bind on the next scheduled panel pass so required UXML elements exist before querying them.
            _document.rootVisualElement.schedule.Execute(() => CompleteOpen(openVersion)).ExecuteLater(0);
        }

        private void CompleteOpen(int openVersion)
        {
            if (!_open || openVersion != _openVersion) return;

            var root = _document.rootVisualElement;
            ValidateConstructedTree(root);

            _controller = new GraphicsSettingsController(PlayerSettingsService.EnsureGlobal(), CloseGraphicsSettings);
            _controller.Bind(root);
        }

        private void CancelAndClose()
        {
            if (_controller != null) _controller.Cancel();
            else CloseGraphicsSettings();
        }

        private void CloseGraphicsSettings()
        {
            _openVersion++;
            _document.visualTreeAsset = null;
            CloseDocumentRoot();
            _controller = null;
            _open = false;
            UnityEngine.Cursor.visible = _previousCursorVisible;
            UnityEngine.Cursor.lockState = _previousLockMode;
        }

        private void CloseDocumentRoot()
        {
            var root = _document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;
        }

        private static PanelSettings CreateRuntimePanelSettings(PanelSettings panelSettingsAsset)
        {
            if (panelSettingsAsset == null) return null;

            var panelSettings = Instantiate(panelSettingsAsset);
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

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element != null) return element;

            throw new InvalidOperationException($"Graphics Settings UXML is missing required {typeof(T).Name} '{name}'.");
        }
    }
}
