using UnityEngine;
using Oasis.NativeMenus;

namespace Oasis.NativeMenuNEW
{
    [DisallowMultipleComponent]
    public sealed class NativeMenuBootstrap : MonoBehaviour
    {
        [SerializeField]
        private SelectionHandler selectionHandler;

        private NativeMenuManager _manager;
        private INativeMenuPlatform _platform;

        private void Awake()
        {
            if (selectionHandler == null)
            {
                selectionHandler = FindObjectOfType<SelectionHandler>();
            }

            if (selectionHandler == null)
            {
                Debug.LogError("NativeMenuBootstrap requires a SelectionHandler reference.");
                enabled = false;
                return;
            }

            _manager = new NativeMenuManager();
            _manager.LoadMenu(NativeMenuDefinition.BuildDefaultMenu(selectionHandler));
            NativeMenuRegistry.Register(_manager);

            _platform = NativeMenuPlatformFactory.Create();
            _platform.Initialize(_manager);
        }

        private void OnDestroy()
        {
            _platform?.Dispose();
            _platform = null;

            if (_manager != null)
            {
                NativeMenuRegistry.Unregister(_manager);
                _manager = null;
            }
        }
    }
}
