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
            Instance = go.AddComponent<WindowsNativeProgressDialog>();
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

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;

namespace NativeWindowsUI
{
    /// <summary>
    /// Windows-specific implementation that renders a native dialog containing
    /// a progress bar, descriptive text label and Cancel button.
    /// </summary>
    internal sealed class WindowsNativeProgressDialog : NativeProgressDialog
    {
        const string WindowClassName = "OasisNativeProgressDialog";
        const int WindowWidth = 420;
        const int WindowHeight = 160;
        const int Margin = 20;
        const int LabelHeight = 20;
        const int ProgressHeight = 22;
        const int ButtonWidth = 96;
        const int ButtonHeight = 28;
        const int LabelYOffset = Margin + ProgressHeight + 10;
        const int ButtonYOffset = LabelYOffset + LabelHeight + 16;

        const uint WS_OVERLAPPED = 0x00000000;
        const uint WS_CAPTION = 0x00C00000;
        const uint WS_SYSMENU = 0x00080000;
        const uint WS_MINIMIZEBOX = 0x00020000;
        const uint WS_VISIBLE = 0x10000000;
        const uint WS_CHILD = 0x40000000;
        const uint WS_TABSTOP = 0x00010000;
        const uint BS_PUSHBUTTON = 0x00000000;
        const uint BS_DEFPUSHBUTTON = 0x00000001;

        const int CW_USEDEFAULT = unchecked((int)0x80000000);

        const int IDC_ARROW = 32512;
        const int COLOR_WINDOW = 5;

        const int ID_CANCEL = 2; // standard IDCANCEL
        const int ID_LABEL = 1002;
        const int ID_PROGRESS = 1003;

        const uint WM_DESTROY = 0x0002;
        const uint WM_CLOSE = 0x0010;
        const uint WM_COMMAND = 0x0111;
        const uint WM_SETFONT = 0x0030;

        const uint PBM_SETRANGE = 0x0401;
        const uint PBM_SETPOS = 0x0402;

        const uint PM_REMOVE = 0x0001;

        const uint CS_HREDRAW = 0x0002;
        const uint CS_VREDRAW = 0x0001;

        const uint ICC_PROGRESS_CLASS = 0x00000020;

        delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        static bool s_classRegistered;
        static WndProcDelegate s_wndProcDelegate;

        IntPtr _window;
        IntPtr _progressBar;
        IntPtr _label;
        IntPtr _cancelButton;
        IntPtr _ownerWindow;
        bool _ownerDisabled;
        bool _cancelInvoked;
        Action _cancelHandler;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureCommonControls();
            EnsureWindowClass();
        }

        void OnDestroy()
        {
            if (_window != IntPtr.Zero)
            {
                DestroyWindow(_window);
                _window = IntPtr.Zero;
            }

            if (_ownerDisabled && _ownerWindow != IntPtr.Zero)
            {
                EnableWindow(_ownerWindow, true);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Update()
        {
            PumpMessages();
        }

        public override void Show(string title, string message, Action onCancel = null, bool blockOwnerWindow = false)
        {
            EnsureWindow();

            _cancelHandler = onCancel;
            _cancelInvoked = false;

            SetWindowText(_window, title ?? string.Empty);
            SetStatusText(message ?? string.Empty);
            SetCancelButtonEnabled(true);
            SetProgress(0f);

            if (blockOwnerWindow)
            {
                _ownerWindow = GetUnityWindow();
                if (_ownerWindow != IntPtr.Zero)
                {
                    EnableWindow(_ownerWindow, false);
                    _ownerDisabled = true;
                }
            }
            else
            {
                _ownerDisabled = false;
                _ownerWindow = GetUnityWindow();
            }

            CenterWindowRelativeToOwner();
            ShowWindow(_window, ShowWindowCommands.Show);
            SetForegroundWindow(_window);
            UpdateWindow(_window);
        }

        public override void Hide()
        {
            if (_window == IntPtr.Zero) return;

            ShowWindow(_window, ShowWindowCommands.Hide);
            if (_ownerDisabled && _ownerWindow != IntPtr.Zero)
            {
                EnableWindow(_ownerWindow, true);
                _ownerDisabled = false;
            }
        }

        public override void SetProgress(float percent)
        {
            if (_progressBar == IntPtr.Zero) return;
            var clamped = Mathf.Clamp(Mathf.RoundToInt(percent), 0, 100);
            SendMessage(_progressBar, PBM_SETPOS, (IntPtr)clamped, IntPtr.Zero);
        }

        public override void SetStatusText(string text)
        {
            if (_label == IntPtr.Zero) return;
            SetWindowText(_label, text ?? string.Empty);
        }

        public override void SetWindowTitle(string title)
        {
            if (_window == IntPtr.Zero) return;
            SetWindowText(_window, title ?? string.Empty);
        }

        public override bool IsVisible => _window != IntPtr.Zero && IsWindowVisible(_window);

        static void EnsureCommonControls()
        {
            INITCOMMONCONTROLSEX icc = new INITCOMMONCONTROLSEX
            {
                dwSize = (uint)Marshal.SizeOf(typeof(INITCOMMONCONTROLSEX)),
                dwICC = ICC_PROGRESS_CLASS
            };

            if (!InitCommonControlsEx(ref icc))
            {
                Debug.LogWarning("Failed to initialize Windows common controls; native progress dialog may not render correctly.");
            }
        }

        void EnsureWindow()
        {
            if (_window != IntPtr.Zero) return;

            var owner = GetUnityWindow();
            IntPtr creationOwner = owner;
            _window = CreateWindowEx(0, WindowClassName, string.Empty,
                WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
                CW_USEDEFAULT, CW_USEDEFAULT, WindowWidth, WindowHeight,
                creationOwner, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

            if (_window == IntPtr.Zero && creationOwner != IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Debug.LogWarning($"Failed to create native progress dialog with owner window (error {error}); retrying without owner.");
                creationOwner = IntPtr.Zero;
                _window = CreateWindowEx(0, WindowClassName, string.Empty,
                    WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
                    CW_USEDEFAULT, CW_USEDEFAULT, WindowWidth, WindowHeight,
                    creationOwner, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            }

            if (_window == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to create native progress dialog window (Win32 error {error}).");
            }

            _ownerWindow = owner;

            // Child controls
            _progressBar = CreateWindowEx(0, "msctls_progress32", string.Empty,
                WS_CHILD | WS_VISIBLE,
                Margin, Margin, WindowWidth - (Margin * 2), ProgressHeight,
                _window, (IntPtr)ID_PROGRESS, IntPtr.Zero, IntPtr.Zero);

            _label = CreateWindowEx(0, "static", string.Empty,
                WS_CHILD | WS_VISIBLE,
                Margin, LabelYOffset, WindowWidth - (Margin * 2), LabelHeight,
                _window, (IntPtr)ID_LABEL, IntPtr.Zero, IntPtr.Zero);

            _cancelButton = CreateWindowEx(0, "button", "Cancel",
                WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON | BS_DEFPUSHBUTTON,
                WindowWidth - Margin - ButtonWidth, ButtonYOffset,
                ButtonWidth, ButtonHeight,
                _window, (IntPtr)ID_CANCEL, IntPtr.Zero, IntPtr.Zero);

            SendMessage(_progressBar, PBM_SETRANGE, IntPtr.Zero, (IntPtr)((100 << 16) | 0));

            var font = GetStockObject(StockObject.DEFAULT_GUI_FONT);
            if (font != IntPtr.Zero)
            {
                SendMessage(_label, WM_SETFONT, font, (IntPtr)1);
                SendMessage(_cancelButton, WM_SETFONT, font, (IntPtr)1);
            }
        }

        static void EnsureWindowClass()
        {
            if (s_classRegistered) return;

            s_wndProcDelegate = ProgressWndProc;

            var wndClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProcDelegate),
                hInstance = GetModuleHandle(null),
                hCursor = LoadCursor(IntPtr.Zero, (IntPtr)IDC_ARROW),
                hbrBackground = (IntPtr)(COLOR_WINDOW + 1),
                lpszClassName = WindowClassName
            };

            if (RegisterClassEx(ref wndClass) == 0)
            {
                throw new InvalidOperationException("Failed to register progress dialog window class.");
            }

            s_classRegistered = true;
        }

        void PumpMessages()
        {
            if (_window == IntPtr.Zero) return;

            while (PeekMessage(out MSG msg, _window, 0, 0, PM_REMOVE))
            {
                if (!IsDialogMessage(_window, ref msg))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
        }

        static IntPtr ProgressWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (Instance is WindowsNativeProgressDialog dialog)
            {
                return dialog.HandleWndProc(hWnd, msg, wParam, lParam);
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        IntPtr HandleWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_COMMAND:
                    int id = LOWORD(wParam);
                    if (id == ID_CANCEL)
                    {
                        HandleCancelRequest();
                        return IntPtr.Zero;
                    }
                    break;
                case WM_CLOSE:
                    HandleCancelRequest();
                    return IntPtr.Zero;
                case WM_DESTROY:
                    if (_ownerDisabled && _ownerWindow != IntPtr.Zero)
                    {
                        EnableWindow(_ownerWindow, true);
                        _ownerDisabled = false;
                    }
                    _window = IntPtr.Zero;
                    break;
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        void HandleCancelRequest()
        {
            if (_cancelInvoked)
                return;

            _cancelInvoked = true;
            EnableWindow(_cancelButton, false);

            if (_cancelHandler != null)
            {
                try
                {
                    _cancelHandler.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            else
            {
                Hide();
            }
        }

        void CenterWindowRelativeToOwner()
        {
            if (_window == IntPtr.Zero) return;

            RECT windowRect;
            GetWindowRect(_window, out windowRect);

            int width = windowRect.Right - windowRect.Left;
            int height = windowRect.Bottom - windowRect.Top;

            RECT ownerRect;
            if (_ownerWindow != IntPtr.Zero && GetWindowRect(_ownerWindow, out ownerRect))
            {
                int ownerWidth = ownerRect.Right - ownerRect.Left;
                int ownerHeight = ownerRect.Bottom - ownerRect.Top;
                int x = ownerRect.Left + (ownerWidth - width) / 2;
                int y = ownerRect.Top + (ownerHeight - height) / 2;
                SetWindowPos(_window, IntPtr.Zero, x, y, 0, 0, SetWindowPosFlags.NoSize | SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate);
            }
            else
            {
                var screenWidth = GetSystemMetrics(SystemMetric.SM_CXSCREEN);
                var screenHeight = GetSystemMetrics(SystemMetric.SM_CYSCREEN);
                int x = (screenWidth - width) / 2;
                int y = (screenHeight - height) / 2;
                SetWindowPos(_window, IntPtr.Zero, x, y, 0, 0, SetWindowPosFlags.NoSize | SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate);
            }
        }

        void SetCancelButtonEnabled(bool enabled)
        {
            if (_cancelButton == IntPtr.Zero) return;
            EnableWindow(_cancelButton, enabled);
        }

        static IntPtr GetUnityWindow()
        {
            var hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero)
                hwnd = GetForegroundWindow();
            return hwnd;
        }

        static int LOWORD(IntPtr value) => (int)((ulong)value & 0xFFFF);

        #region Win32 interop
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int X, int Y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

        [DllImport("user32.dll")]
        static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(SystemMetric smIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG lpmsg);

        [DllImport("user32.dll")]
        static extern bool IsDialogMessage(IntPtr hDlg, ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("comctl32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX lpInitCtrls);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("gdi32.dll")]
        static extern IntPtr GetStockObject(StockObject fnObject);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        #endregion

        enum ShowWindowCommands
        {
            Hide = 0,
            Show = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INITCOMMONCONTROLSEX
        {
            public uint dwSize;
            public uint dwICC;
        }

        enum StockObject
        {
            DEFAULT_GUI_FONT = 17
        }

        [Flags]
        enum SetWindowPosFlags : uint
        {
            NoSize = 0x0001,
            NoMove = 0x0002,
            NoZOrder = 0x0004,
            NoRedraw = 0x0008,
            NoActivate = 0x0010,
            DrawFrame = 0x0020,
            FrameChanged = 0x0020,
            ShowWindow = 0x0040,
            HideWindow = 0x0080,
            NoOwnerZOrder = 0x0200,
            NoSendChanging = 0x0400,
            DeferErase = 0x2000,
            AsyncWindowPos = 0x4000
        }

        enum SystemMetric
        {
            SM_CXSCREEN = 0,
            SM_CYSCREEN = 1
        }
    }
}
#else
namespace NativeWindowsUI
{
    /// <summary>
    /// Non-Windows stub that keeps API calls valid whilst providing no-op behaviour.
    /// </summary>
    internal sealed class StubNativeProgressDialog : NativeProgressDialog
    {
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void Show(string title, string message, Action onCancel = null, bool blockOwnerWindow = false)
        {
            Debug.LogWarning("Native progress dialog is only available on Windows platforms.");
        }

        public override void Hide() { }
        public override void SetProgress(float percent) { }
        public override void SetStatusText(string text) { }
        public override void SetWindowTitle(string title) { }
        public override bool IsVisible => false;
    }
}
#endif
