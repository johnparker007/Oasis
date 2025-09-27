using UnityEngine;

namespace Oasis.NativeProgress
{
    [DisallowMultipleComponent]
    public sealed class NativeProgressBootstrap : MonoBehaviour
    {
        [SerializeField]
        private string _windowTitle = "Compiling Scripts";

        [SerializeField, TextArea]
        private string _statusText = "Postprocessing 1 for Assembly-CSharp-Editor";

        [SerializeField]
        private bool _cancelAvailable = true;

        private bool _windowCreated;

        private void Awake()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!NativeProgressWindow.EnsureWindowCreated(out string errorMessage))
            {
                Debug.LogError($"Failed to create native progress window: {errorMessage}");
                return;
            }

            _windowCreated = true;
#else
            Debug.Log("Native progress window bootstrap is only active on Windows platforms.");
#endif
        }

        private void Start()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_windowCreated)
            {
                NativeProgressWindow.UpdateContent(_windowTitle, _statusText, _cancelAvailable);
            }
#endif
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_windowCreated)
            {
                NativeProgressWindow.CloseWindow();
                _windowCreated = false;
            }
#endif
        }
    }
}
