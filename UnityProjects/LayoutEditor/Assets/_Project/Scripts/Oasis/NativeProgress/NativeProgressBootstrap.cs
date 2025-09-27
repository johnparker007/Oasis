using UnityEngine;

namespace Oasis.NativeProgress
{
    [DisallowMultipleComponent]
    public sealed class NativeProgressBootstrap : MonoBehaviour
    {
        private bool _windowCreated;

        private void Awake()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!NativeProgressWindow.EnsureWindowCreated(out string errorMessage))
            {
                Debug.LogError($"Failed to create native progress window: {errorMessage}");
                return;
            }

            _windowCreated = true;
#else
            Debug.Log("Native progress window bootstrap is only active in Windows standalone builds.");
#endif
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_windowCreated)
            {
                NativeProgressWindow.CloseWindow();
                _windowCreated = false;
            }
#endif
        }
    }
}
