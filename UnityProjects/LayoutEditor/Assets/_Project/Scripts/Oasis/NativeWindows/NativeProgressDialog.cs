using System;
using UnityEngine;

namespace NativeWindowsUI
{
    /// <summary>
    /// Helper that ensures a singleton instance of the native progress dialog manager exists.
    /// </summary>
    public abstract class NativeProgressDialog : MonoBehaviour
    {
        public static NativeProgressDialog Instance { get; protected set; }

        /// <summary>
        /// Creates the progress dialog GameObject when needed.
        /// </summary>
        public static NativeProgressDialog EnsureInstance()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("NativeProgressDialog");
            DontDestroyOnLoad(go);

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            Instance = go.AddComponent<NativeProgressDialogWindowsOS>();
#else
            Instance = go.AddComponent<StubNativeProgressDialog>();
#endif
            return Instance;
        }

        /// <summary>Shows the progress dialog with the specified title and message.</summary>
        public abstract void Show(string title, string message, Action onCancel = null, bool blockOwnerWindow = false);

        /// <summary>Hides the progress dialog.</summary>
        public abstract void Hide();

        /// <summary>Updates the progress percentage (0-100).</summary>
        public abstract void SetProgress(float percent);

        /// <summary>Updates the descriptive status text displayed underneath the bar.</summary>
        public abstract void SetStatusText(string text);

        /// <summary>Updates the OS window title while the dialog is visible.</summary>
        public abstract void SetWindowTitle(string title);

        /// <summary>Whether the native dialog window is currently visible.</summary>
        public abstract bool IsVisible { get; }

        /// <summary>Convenience wrapper that ensures the dialog exists and shows it.</summary>
        public static void ShowDialog(string title, string message, Action onCancel = null, bool blockOwnerWindow = false)
        {
            EnsureInstance().Show(title, message, onCancel, blockOwnerWindow);
        }

        /// <summary>Convenience wrapper that hides the dialog if it exists.</summary>
        public static void HideDialog()
        {
            Instance?.Hide();
        }

        /// <summary>Convenience wrapper for updating the progress percentage.</summary>
        public static void SetProgressValue(float percent)
        {
            Instance?.SetProgress(percent);
        }

        /// <summary>Convenience wrapper for updating the descriptive text.</summary>
        public static void SetStatus(string text)
        {
            Instance?.SetStatusText(text);
        }

        /// <summary>Convenience wrapper for updating the window title.</summary>
        public static void SetTitle(string title)
        {
            Instance?.SetWindowTitle(title);
        }
    }
}


