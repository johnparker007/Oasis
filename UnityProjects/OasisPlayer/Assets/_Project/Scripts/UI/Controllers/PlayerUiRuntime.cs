using OasisPlayer.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace OasisPlayer.UI.Controllers
{
    public sealed class PlayerUiRuntime : MonoBehaviour
    {
        private UIDocument _document;
        private VisualTreeAsset _graphicsView;
        private GraphicsSettingsController _controller;
        private bool _open;
        private bool _previousCursorVisible;
        private CursorLockMode _previousLockMode;

        private void Awake()
        {
            _graphicsView = Resources.Load<VisualTreeAsset>("UI/Uxml/GraphicsSettings");
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _document.panelSettings.name = "Oasis Runtime Panel Settings";
            _document.enabled = false;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_open) _controller?.Cancel();
                else OpenGraphicsSettings();
            }
        }

        private void OpenGraphicsSettings()
        {
            if (_graphicsView == null)
            {
                Debug.LogError("Oasis Player UI could not load Resources/UI/Uxml/GraphicsSettings.uxml.");
                return;
            }

            _previousCursorVisible = Cursor.visible;
            _previousLockMode = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            _document.visualTreeAsset = _graphicsView;
            _document.enabled = true;
            _open = true;
            _controller = new GraphicsSettingsController(PlayerSettingsService.EnsureGlobal(), CloseGraphicsSettings);
            _document.rootVisualElement.pickingMode = PickingMode.Position;
            _controller.Bind(_document.rootVisualElement);
        }

        private void CloseGraphicsSettings()
        {
            _document.rootVisualElement.Clear();
            _document.enabled = false;
            _controller = null;
            _open = false;
            Cursor.visible = _previousCursorVisible;
            Cursor.lockState = _previousLockMode;
        }
    }
}
