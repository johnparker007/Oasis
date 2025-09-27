using System;
using System.Runtime.InteropServices;

namespace Oasis.NativeProgress
{
    internal static class NativeProgressWindow
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const string WindowClassName = "OasisNativeProgressWindow";
        private const int CS_HREDRAW = 0x0002;
        private const int CS_VREDRAW = 0x0001;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_CHILD = 0x40000000;
        private const int WS_TABSTOP = 0x00010000;
        private const int WS_GROUP = 0x00020000;
        private const int BS_PUSHBUTTON = 0x00000000;
        private const int SS_LEFT = 0x00000000;
        private const int WS_EX_DLGMODALFRAME = 0x00000001;
        private const int CW_USEDEFAULT = unchecked((int)0x80000000);
        private const int SW_SHOWNORMAL = 1;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int IDC_ARROW = 32512;
        private const int COLOR_WINDOW = 5;
        private const int PBM_SETMARQUEE = 0x0400 + 103;
        private const int PBM_SETRANGE32 = 0x0400 + 6;
        private const int PBM_SETPOS = 0x0400 + 2;
        private const int WM_SETFONT = 0x0030;
        private const int WM_SIZE = 0x0005;
        private const int WM_DESTROY = 0x0002;
        private const int WM_COMMAND = 0x0111;
        private const int WM_CTLCOLORSTATIC = 0x0138;
        private const int BN_CLICKED = 0;
        private const int DEFAULT_GUI_FONT = 17;
        private const int CancelButtonId = 1001;
        private const uint ICC_PROGRESS_CLASS = 0x00000020;
        private const int TRANSPARENT = 1;
        private const int COLOR_WINDOWTEXT = 8;
        private const int ProgressRange = 1000;
        private const int PBS_SMOOTH = 0x00000001;
        private const int PBS_MARQUEE = 0x00000008;

        private static ushort _classAtom;
        private static IntPtr _instanceHandle = IntPtr.Zero;
        private static IntPtr _windowHandle = IntPtr.Zero;
        private static IntPtr _unityWindowHandle = IntPtr.Zero;
        private static IntPtr _progressHandle = IntPtr.Zero;
        private static IntPtr _labelHandle = IntPtr.Zero;
        private static IntPtr _cancelButtonHandle = IntPtr.Zero;
        private static bool _commonControlsInitialized;
        private static bool _isMarqueeMode = true;
        private static int _currentProgressValue = -1;
        private static IntPtr _windowBackgroundBrush = IntPtr.Zero;
        private static WndProc _wndProc;

        public static bool EnsureWindowCreated(out string errorMessage)
        {
            if (_windowHandle != IntPtr.Zero)
            {
                errorMessage = null;
                return true;
            }

            _instanceHandle = GetModuleHandle(null);
            if (_instanceHandle == IntPtr.Zero)
            {
                errorMessage = $"GetModuleHandle failed with error {Marshal.GetLastWin32Error()}";
                return false;
            }

            _unityWindowHandle = GetActiveWindow();
            if (_unityWindowHandle == IntPtr.Zero)
            {
                _unityWindowHandle = GetForegroundWindow();
            }

            if (_unityWindowHandle == IntPtr.Zero)
            {
                errorMessage = "Unable to determine Unity window handle.";
                return false;
            }

            _wndProc = WindowProcedure;

            var windowClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = _wndProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = _instanceHandle,
                hIcon = IntPtr.Zero,
                hCursor = LoadCursor(IntPtr.Zero, new IntPtr(IDC_ARROW)),
                hbrBackground = new IntPtr(COLOR_WINDOW + 1),
                lpszMenuName = null,
                lpszClassName = WindowClassName,
                hIconSm = IntPtr.Zero
            };

            _classAtom = RegisterClassEx(ref windowClass);
            if (_classAtom == 0)
            {
                errorMessage = $"RegisterClassEx failed with error {Marshal.GetLastWin32Error()}";
                _wndProc = null;
                _unityWindowHandle = IntPtr.Zero;
                return false;
            }

            _windowHandle = CreateWindowEx(
                WS_EX_DLGMODALFRAME,
                WindowClassName,
                "Oasis Progress",
                WS_POPUP | WS_CAPTION | WS_VISIBLE,
                CW_USEDEFAULT,
                CW_USEDEFAULT,
                500,
                150,
                _unityWindowHandle,
                IntPtr.Zero,
                _instanceHandle,
                IntPtr.Zero);

            if (_windowHandle == IntPtr.Zero)
            {
                int lastError = Marshal.GetLastWin32Error();
                UnregisterWindowClass();
                errorMessage = $"CreateWindowEx failed with error {lastError}";
                _unityWindowHandle = IntPtr.Zero;
                return false;
            }

            ShowWindow(_windowHandle, SW_SHOWNORMAL);
            UpdateWindow(_windowHandle);

            if (!EnsureChildControls(out errorMessage))
            {
                CloseWindow();
                return false;
            }

            EnableWindow(_unityWindowHandle, false);
            SetForegroundWindow(_windowHandle);

            errorMessage = null;
            return true;
        }

        public static bool UpdateContent(string windowTitle, string statusText, bool cancelEnabled, float? progress = null)
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return false;
            }

            if (windowTitle != null)
            {
                SetWindowText(_windowHandle, windowTitle);
            }

            if (_labelHandle != IntPtr.Zero)
            {
                SetWindowText(_labelHandle, statusText ?? string.Empty);
            }

            if (_cancelButtonHandle != IntPtr.Zero)
            {
                ShowWindow(_cancelButtonHandle, cancelEnabled ? SW_SHOW : SW_HIDE);
                EnableWindow(_cancelButtonHandle, cancelEnabled);
            }

            UpdateProgressInternal(progress);

            return true;
        }

        public static bool UpdateProgress(float normalizedProgress)
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return false;
            }

            UpdateProgressInternal(normalizedProgress);
            return true;
        }

        public static void CloseWindow()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                if (_unityWindowHandle != IntPtr.Zero)
                {
                    EnableWindow(_unityWindowHandle, true);
                    SetForegroundWindow(_unityWindowHandle);
                }

                DestroyWindowNative(_windowHandle);
                _windowHandle = IntPtr.Zero;
            }

            UnregisterWindowClass();
            _wndProc = null;
            _instanceHandle = IntPtr.Zero;
            _unityWindowHandle = IntPtr.Zero;
            _progressHandle = IntPtr.Zero;
            _labelHandle = IntPtr.Zero;
            _cancelButtonHandle = IntPtr.Zero;
            _isMarqueeMode = true;
            _currentProgressValue = -1;
            _windowBackgroundBrush = IntPtr.Zero;
        }

        private static void UnregisterWindowClass()
        {
            if (_classAtom != 0 && _instanceHandle != IntPtr.Zero)
            {
                UnregisterClass(WindowClassName, _instanceHandle);
                _classAtom = 0;
            }
        }

        private static bool EnsureChildControls(out string errorMessage)
        {
            if (_progressHandle != IntPtr.Zero && _labelHandle != IntPtr.Zero && _cancelButtonHandle != IntPtr.Zero)
            {
                errorMessage = null;
                UpdateChildLayout();
                return true;
            }

            if (!EnsureCommonControls(out errorMessage))
            {
                return false;
            }

            IntPtr fontHandle = GetStockObject(DEFAULT_GUI_FONT);

            _progressHandle = CreateWindowEx(
                0,
                "msctls_progress32",
                null,
                WS_CHILD | WS_VISIBLE | PBS_SMOOTH | PBS_MARQUEE,
                0,
                0,
                0,
                0,
                _windowHandle,
                IntPtr.Zero,
                _instanceHandle,
                IntPtr.Zero);

            if (_progressHandle == IntPtr.Zero)
            {
                errorMessage = $"Failed to create progress bar control. Error: {Marshal.GetLastWin32Error()}";
                return false;
            }

            SendMessage(_progressHandle, PBM_SETMARQUEE, new IntPtr(1), new IntPtr(0));
            SendMessage(_progressHandle, WM_SETFONT, fontHandle, new IntPtr(1));
            _isMarqueeMode = true;
            _currentProgressValue = -1;

            _labelHandle = CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                WS_CHILD | WS_VISIBLE | SS_LEFT,
                0,
                0,
                0,
                0,
                _windowHandle,
                IntPtr.Zero,
                _instanceHandle,
                IntPtr.Zero);

            if (_labelHandle == IntPtr.Zero)
            {
                errorMessage = $"Failed to create status label control. Error: {Marshal.GetLastWin32Error()}";
                return false;
            }

            SendMessage(_labelHandle, WM_SETFONT, fontHandle, new IntPtr(1));

            _cancelButtonHandle = CreateWindowEx(
                0,
                "BUTTON",
                "Cancel",
                WS_CHILD | WS_TABSTOP | WS_GROUP | BS_PUSHBUTTON,
                0,
                0,
                0,
                0,
                _windowHandle,
                new IntPtr(CancelButtonId),
                _instanceHandle,
                IntPtr.Zero);

            if (_cancelButtonHandle == IntPtr.Zero)
            {
                errorMessage = $"Failed to create cancel button control. Error: {Marshal.GetLastWin32Error()}";
                return false;
            }

            SendMessage(_cancelButtonHandle, WM_SETFONT, fontHandle, new IntPtr(1));
            ShowWindow(_cancelButtonHandle, SW_HIDE);

            UpdateChildLayout();

            errorMessage = null;
            return true;
        }

        private static bool EnsureCommonControls(out string errorMessage)
        {
            if (_commonControlsInitialized)
            {
                errorMessage = null;
                return true;
            }

            var controls = new INITCOMMONCONTROLSEX
            {
                dwSize = (uint)Marshal.SizeOf(typeof(INITCOMMONCONTROLSEX)),
                dwICC = ICC_PROGRESS_CLASS,
            };

            if (!InitCommonControlsEx(ref controls))
            {
                errorMessage = $"InitCommonControlsEx failed with error {Marshal.GetLastWin32Error()}";
                return false;
            }

            _commonControlsInitialized = true;
            errorMessage = null;
            return true;
        }

        private static void UpdateProgressInternal(float? normalizedProgress)
        {
            if (_progressHandle == IntPtr.Zero)
            {
                return;
            }

            if (normalizedProgress.HasValue)
            {
                float clamped = normalizedProgress.Value;
                if (clamped < 0f)
                {
                    clamped = 0f;
                }
                else if (clamped > 1f)
                {
                    clamped = 1f;
                }

                int progressValue = (int)(clamped * ProgressRange);

                if (_isMarqueeMode)
                {
                    SendMessage(_progressHandle, PBM_SETMARQUEE, IntPtr.Zero, IntPtr.Zero);
                    SendMessage(_progressHandle, PBM_SETRANGE32, IntPtr.Zero, new IntPtr(ProgressRange));
                    _isMarqueeMode = false;
                }

                if (progressValue != _currentProgressValue)
                {
                    SendMessage(_progressHandle, PBM_SETPOS, new IntPtr(progressValue), IntPtr.Zero);
                    _currentProgressValue = progressValue;
                }
            }
            else if (!_isMarqueeMode)
            {
                SendMessage(_progressHandle, PBM_SETMARQUEE, new IntPtr(1), IntPtr.Zero);
                _currentProgressValue = -1;
                _isMarqueeMode = true;
            }
        }

        private static void UpdateChildLayout()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            if (!GetClientRect(_windowHandle, out RECT clientRect))
            {
                return;
            }

            int width = clientRect.Right - clientRect.Left;
            int padding = 16;
            int verticalSpacing = 10;
            int progressHeight = 22;
            int buttonWidth = 90;
            int buttonHeight = 26;
            int labelHeight = 20;

            int progressX = padding;
            int progressY = padding;
            int progressWidth = width - (padding * 2);

            if (_progressHandle != IntPtr.Zero)
            {
                MoveWindow(_progressHandle, progressX, progressY, progressWidth, progressHeight, true);
            }

            int labelAreaY = progressY + progressHeight + verticalSpacing;
            int buttonX = width - padding - buttonWidth;
            int labelWidth = buttonX - progressX - verticalSpacing;
            if (labelWidth < 50)
            {
                labelWidth = progressWidth;
            }

            if (_labelHandle != IntPtr.Zero)
            {
                MoveWindow(_labelHandle, progressX, labelAreaY + ((buttonHeight - labelHeight) / 2), labelWidth, labelHeight, true);
            }

            if (_cancelButtonHandle != IntPtr.Zero)
            {
                MoveWindow(_cancelButtonHandle, buttonX, labelAreaY, buttonWidth, buttonHeight, true);
            }
        }

        private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_SIZE:
                    UpdateChildLayout();
                    break;
                case WM_COMMAND:
                    int commandId = LowWord(wParam);
                    int notificationCode = HighWord(wParam);
                    if (commandId == CancelButtonId && notificationCode == BN_CLICKED)
                    {
                        CloseWindow();
                        return IntPtr.Zero;
                    }

                    break;
                case WM_CTLCOLORSTATIC:
                    if (lParam == _labelHandle)
                    {
                        SetBkMode(wParam, TRANSPARENT);
                        SetTextColor(wParam, GetSysColor(COLOR_WINDOWTEXT));
                        if (_windowBackgroundBrush == IntPtr.Zero)
                        {
                            _windowBackgroundBrush = GetSysColorBrush(COLOR_WINDOW);
                        }

                        return _windowBackgroundBrush;
                    }

                    break;
                case WM_DESTROY:
                    _progressHandle = IntPtr.Zero;
                    _labelHandle = IntPtr.Zero;
                    _cancelButtonHandle = IntPtr.Zero;
                    break;
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static int LowWord(IntPtr value)
        {
            return (int)((long)value & 0xFFFF);
        }

        private static int HighWord(IntPtr value)
        {
            return (int)(((long)value >> 16) & 0xFFFF);
        }

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public WndProc lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INITCOMMONCONTROLSEX
        {
            public uint dwSize;
            public uint dwICC;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int X,
            int Y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindowNative(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("comctl32.dll", SetLastError = true)]
        private static extern bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX lpInitCtrls);

        [DllImport("gdi32.dll")]
        private static extern IntPtr GetStockObject(int fnObject);

        [DllImport("gdi32.dll")]
        private static extern int SetBkMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll")]
        private static extern int SetTextColor(IntPtr hdc, int crColor);

        [DllImport("user32.dll")]
        private static extern int GetSysColor(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSysColorBrush(int nIndex);
#else
        public static bool EnsureWindowCreated(out string errorMessage)
        {
            errorMessage = "Native progress window is only supported on Windows.";
            return false;
        }

        public static bool UpdateContent(string windowTitle, string statusText, bool cancelEnabled, float? progress = null)
        {
            return false;
        }

        public static bool UpdateProgress(float normalizedProgress)
        {
            return false;
        }

        public static void CloseWindow()
        {
        }
#endif
    }
}
