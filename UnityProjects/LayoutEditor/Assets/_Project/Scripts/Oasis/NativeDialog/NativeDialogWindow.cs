using System;
using System.Runtime.InteropServices;

namespace Oasis.NativeDialog
{
    public static class NativeDialogWindow
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const string WindowClassName = "OasisNativeDialogWindow";
        private const int CS_HREDRAW = 0x0002;
        private const int CS_VREDRAW = 0x0001;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_CHILD = 0x40000000;
        private const int WS_TABSTOP = 0x00010000;
        private const int WS_GROUP = 0x00020000;
        private const int WS_EX_DLGMODALFRAME = 0x00000001;
        private const int BS_PUSHBUTTON = 0x00000000;
        private const int SS_LEFT = 0x00000000;
        private const int SS_NOPREFIX = 0x00000080;
        private const int SS_ICON = 0x00000003;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int CW_USEDEFAULT = unchecked((int)0x80000000);
        private const int WM_DESTROY = 0x0002;
        private const int WM_COMMAND = 0x0111;
        private const int WM_CLOSE = 0x0010;
        private const int WM_SIZE = 0x0005;
        private const int WM_SETFONT = 0x0030;
        private const int WM_CTLCOLORSTATIC = 0x0138;
        private const int BN_CLICKED = 0;
        private const int DEFAULT_GUI_FONT = 17;
        private const int TRANSPARENT = 1;
        private const int COLOR_WINDOW = 5;
        private const int COLOR_WINDOWTEXT = 8;
        private const int IDC_ARROW = 32512;
        private const int IDI_INFORMATION = 32516;
        private const int IDI_WARNING = 32515;
        private const int IDI_ERROR = 32513;
        private const int STM_SETIMAGE = 0x0172;
        private const int IMAGE_ICON = 1;
        private const int OkButtonId = 2001;
        private const int CancelButtonId = 2002;
        private const uint PM_REMOVE = 0x0001;
        private const int DT_CALCRECT = 0x00000400;
        private const int DT_WORDBREAK = 0x00000010;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int GWL_STYLE = -16;
        private const int WM_QUIT = 0x0012;

        private const int BaseWidth = 420;
        private const int BaseHeight = 200;

        [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static ushort _classAtom;
        private static WndProc _wndProc;
        private static IntPtr _instanceHandle = IntPtr.Zero;
        private static IntPtr _windowHandle = IntPtr.Zero;
        private static IntPtr _unityWindowHandle = IntPtr.Zero;
        private static IntPtr _labelHandle = IntPtr.Zero;
        private static IntPtr _iconHandle = IntPtr.Zero;
        private static IntPtr _okButtonHandle = IntPtr.Zero;
        private static IntPtr _cancelButtonHandle = IntPtr.Zero;
        private static IntPtr _windowBackgroundBrush = IntPtr.Zero;
        private static NativeDialogOptions _currentOptions;
        private static NativeDialogIcon _currentIcon = NativeDialogIcon.None;

        public static bool ShowDialog(NativeDialogOptions options, out string errorMessage)
        {
            if (options == null)
            {
                errorMessage = "Options must not be null.";
                return false;
            }

            if (_windowHandle != IntPtr.Zero)
            {
                CloseInternal();
            }

            _instanceHandle = GetModuleHandleW(null);
            if (_instanceHandle == IntPtr.Zero)
            {
                errorMessage = $"GetModuleHandleW failed with error {Marshal.GetLastWin32Error()}";
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

            if (!EnsureWindowClass(out errorMessage))
            {
                return false;
            }

            _currentOptions = options;

            int windowStyle = WS_POPUP | WS_CAPTION | WS_VISIBLE;
            if (options.ShowCloseButton)
            {
                windowStyle |= WS_SYSMENU;
            }

            _windowHandle = CreateWindowExW(
                WS_EX_DLGMODALFRAME,
                WindowClassName,
                options.Title ?? string.Empty,
                windowStyle,
                CW_USEDEFAULT,
                CW_USEDEFAULT,
                BaseWidth,
                BaseHeight,
                _unityWindowHandle,
                IntPtr.Zero,
                _instanceHandle,
                IntPtr.Zero);

            if (_windowHandle == IntPtr.Zero)
            {
                int lastError = Marshal.GetLastWin32Error();
                UnregisterWindowClass();
                errorMessage = $"CreateWindowExW failed with error {lastError}";
                ResetState();
                return false;
            }

            if (!EnsureChildControls(out errorMessage))
            {
                CloseInternal();
                return false;
            }

            UpdateContent();
            CenterWindowOnUnityWindow();
            ShowWindow(_windowHandle, SW_SHOWNORMAL);
            UpdateWindow(_windowHandle);

            EnableWindow(_unityWindowHandle, false);
            SetForegroundWindow(_windowHandle);

            errorMessage = null;
            return true;
        }

        public static void CloseDialog()
        {
            CloseDialog(NativeDialogResult.Closed);
        }

        private static void CloseDialog(NativeDialogResult result)
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            NativeDialogOptions options = _currentOptions;
            CloseInternal();

            if (options != null)
            {
                switch (result)
                {
                    case NativeDialogResult.Ok:
                        options.OnOkClicked?.Invoke();
                        break;
                    case NativeDialogResult.Cancel:
                        options.OnCancelClicked?.Invoke();
                        break;
                    case NativeDialogResult.Closed:
                        options.OnClosed?.Invoke();
                        break;
                }
            }
        }

        private static void CloseInternal()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                bool unityWindowStillExists = _unityWindowHandle != IntPtr.Zero && IsWindow(_unityWindowHandle);
                if (unityWindowStillExists)
                {
                    EnableWindow(_unityWindowHandle, true);
                    SetForegroundWindow(_unityWindowHandle);
                }

                DestroyWindow(_windowHandle);

                if (unityWindowStillExists)
                {
                    PeekMessage(out MSG _, IntPtr.Zero, WM_QUIT, WM_QUIT, PM_REMOVE);
                }
            }

            ResetState();
            UnregisterWindowClass();
        }

        private static void ResetState()
        {
            _windowHandle = IntPtr.Zero;
            _unityWindowHandle = IntPtr.Zero;
            _labelHandle = IntPtr.Zero;
            _iconHandle = IntPtr.Zero;
            _okButtonHandle = IntPtr.Zero;
            _cancelButtonHandle = IntPtr.Zero;
            _windowBackgroundBrush = IntPtr.Zero;
            _currentOptions = null;
            _currentIcon = NativeDialogIcon.None;
            _instanceHandle = IntPtr.Zero;
        }

        private static bool EnsureWindowClass(out string errorMessage)
        {
            if (_classAtom != 0)
            {
                errorMessage = null;
                return true;
            }

            _wndProc = WindowProcedure;
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEXW)),
                style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = _wndProc,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = _instanceHandle,
                hIcon = IntPtr.Zero,
                hCursor = LoadCursorW(IntPtr.Zero, new IntPtr(IDC_ARROW)),
                hbrBackground = new IntPtr(COLOR_WINDOW + 1),
                lpszMenuName = null,
                lpszClassName = WindowClassName,
                hIconSm = IntPtr.Zero,
            };

            _classAtom = RegisterClassExW(ref windowClass);
            if (_classAtom == 0)
            {
                errorMessage = $"RegisterClassExW failed with error {Marshal.GetLastWin32Error()}";
                _wndProc = null;
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static void UnregisterWindowClass()
        {
            if (_classAtom != 0 && _instanceHandle != IntPtr.Zero)
            {
                UnregisterClassW(WindowClassName, _instanceHandle);
                _classAtom = 0;
                _wndProc = null;
            }
        }

        private static bool EnsureChildControls(out string errorMessage)
        {
            IntPtr fontHandle = GetStockObject(DEFAULT_GUI_FONT);

            _labelHandle = CreateWindowExW(
                0,
                "STATIC",
                string.Empty,
                WS_CHILD | WS_VISIBLE | SS_LEFT | SS_NOPREFIX,
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
                errorMessage = $"Failed to create label control. Error: {Marshal.GetLastWin32Error()}";
                return false;
            }

            SendMessage(_labelHandle, WM_SETFONT, fontHandle, new IntPtr(1));

            _iconHandle = CreateWindowExW(
                0,
                "STATIC",
                IntPtr.Zero,
                WS_CHILD | SS_ICON,
                0,
                0,
                0,
                0,
                _windowHandle,
                IntPtr.Zero,
                _instanceHandle,
                IntPtr.Zero);

            if (_iconHandle == IntPtr.Zero)
            {
                errorMessage = $"Failed to create icon control. Error: {Marshal.GetLastWin32Error()}";
                return false;
            }

            _okButtonHandle = CreateWindowExW(
                0,
                "BUTTON",
                "OK",
                WS_CHILD | WS_TABSTOP | WS_GROUP | BS_PUSHBUTTON,
                0,
                0,
                0,
                0,
                _windowHandle,
                new IntPtr(OkButtonId),
                _instanceHandle,
                IntPtr.Zero);

            if (_okButtonHandle == IntPtr.Zero)
            {
                errorMessage = $"Failed to create OK button. Error: {Marshal.GetLastWin32Error()}";
                return false;
            }

            SendMessage(_okButtonHandle, WM_SETFONT, fontHandle, new IntPtr(1));

            _cancelButtonHandle = CreateWindowExW(
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
                errorMessage = $"Failed to create Cancel button. Error: {Marshal.GetLastWin32Error()}";
                return false;
            }

            SendMessage(_cancelButtonHandle, WM_SETFONT, fontHandle, new IntPtr(1));
            ShowWindow(_cancelButtonHandle, SW_HIDE);

            errorMessage = null;
            return true;
        }

        private static void UpdateContent()
        {
            if (_windowHandle == IntPtr.Zero || _currentOptions == null)
            {
                return;
            }

            SetWindowTextW(_windowHandle, _currentOptions.Title ?? string.Empty);
            SetWindowTextW(_labelHandle, _currentOptions.Message ?? string.Empty);

            if (_currentOptions.ShowOkButton)
            {
                ShowWindow(_okButtonHandle, SW_SHOW);
                EnableWindow(_okButtonHandle, true);
            }
            else
            {
                ShowWindow(_okButtonHandle, SW_HIDE);
                EnableWindow(_okButtonHandle, false);
            }

            if (_currentOptions.ShowCancelButton)
            {
                ShowWindow(_cancelButtonHandle, SW_SHOW);
                EnableWindow(_cancelButtonHandle, true);
            }
            else
            {
                ShowWindow(_cancelButtonHandle, SW_HIDE);
                EnableWindow(_cancelButtonHandle, false);
            }

            UpdateIcon();
            UpdateLayout();
            UpdateCloseButtonState();
        }

        private static void UpdateIcon()
        {
            if (_iconHandle == IntPtr.Zero)
            {
                return;
            }

            NativeDialogIcon desiredIcon = _currentOptions?.Icon ?? NativeDialogIcon.None;
            if (desiredIcon == NativeDialogIcon.None)
            {
                ShowWindow(_iconHandle, SW_HIDE);
                _currentIcon = NativeDialogIcon.None;
                SendMessage(_iconHandle, STM_SETIMAGE, new IntPtr(IMAGE_ICON), IntPtr.Zero);
                return;
            }

            if (desiredIcon == _currentIcon)
            {
                ShowWindow(_iconHandle, SW_SHOW);
                return;
            }

            IntPtr iconHandle = IntPtr.Zero;
            switch (desiredIcon)
            {
                case NativeDialogIcon.Information:
                    iconHandle = LoadIconW(IntPtr.Zero, new IntPtr(IDI_INFORMATION));
                    break;
                case NativeDialogIcon.Warning:
                    iconHandle = LoadIconW(IntPtr.Zero, new IntPtr(IDI_WARNING));
                    break;
                case NativeDialogIcon.Error:
                    iconHandle = LoadIconW(IntPtr.Zero, new IntPtr(IDI_ERROR));
                    break;
            }

            SendMessage(_iconHandle, STM_SETIMAGE, new IntPtr(IMAGE_ICON), iconHandle);
            ShowWindow(_iconHandle, SW_SHOW);
            _currentIcon = desiredIcon;
        }

        private static void UpdateCloseButtonState()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            int style = GetWindowLong(_windowHandle, GWL_STYLE);
            bool hasClose = (style & WS_SYSMENU) == WS_SYSMENU;

            if (_currentOptions?.ShowCloseButton == true && !hasClose)
            {
                SetWindowLong(_windowHandle, GWL_STYLE, style | WS_SYSMENU);
                SetWindowPos(_windowHandle, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            else if (_currentOptions?.ShowCloseButton == false && hasClose)
            {
                SetWindowLong(_windowHandle, GWL_STYLE, style & ~WS_SYSMENU);
                SetWindowPos(_windowHandle, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
        }

        private static void UpdateLayout()
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
            int iconSize = 32;
            int spacing = 12;
            int buttonWidth = 90;
            int buttonHeight = 26;
            int buttonSpacing = 8;
            bool iconVisible = _currentOptions?.Icon != NativeDialogIcon.None;

            int textX = padding + (iconVisible ? iconSize + spacing : 0);
            int textWidth = width - textX - padding;
            if (textWidth < 50)
            {
                textWidth = width - (padding * 2);
                textX = padding;
            }

            int textHeight = Math.Max(MeasureTextHeight(_currentOptions?.Message ?? string.Empty, textWidth), iconVisible ? iconSize : 0);
            if (textHeight < iconSize && iconVisible)
            {
                textHeight = iconSize;
            }

            int currentY = padding;

            if (iconVisible)
            {
                MoveWindow(_iconHandle, padding, currentY, iconSize, iconSize, true);
            }

            MoveWindow(_labelHandle, textX, currentY, textWidth, textHeight, true);

            currentY += textHeight + spacing;

            int buttonCount = 0;
            if (_currentOptions?.ShowOkButton == true) buttonCount++;
            if (_currentOptions?.ShowCancelButton == true) buttonCount++;
            if (buttonCount == 0) buttonCount = 1;

            int buttonsWidth = (buttonWidth * buttonCount) + (buttonSpacing * (buttonCount - 1));
            int buttonStartX = Math.Max(padding, width - padding - buttonsWidth);

            int nextButtonX = buttonStartX;

            if (_currentOptions?.ShowOkButton == true)
            {
                MoveWindow(_okButtonHandle, nextButtonX, currentY, buttonWidth, buttonHeight, true);
                nextButtonX += buttonWidth + buttonSpacing;
            }

            if (_currentOptions?.ShowCancelButton == true)
            {
                MoveWindow(_cancelButtonHandle, nextButtonX, currentY, buttonWidth, buttonHeight, true);
            }

            int requiredHeight = currentY + buttonHeight + padding;
            RECT windowRect;
            if (GetWindowRect(_windowHandle, out windowRect))
            {
                int currentHeight = windowRect.Bottom - windowRect.Top;
                int newHeight = requiredHeight + (currentHeight - (clientRect.Bottom - clientRect.Top));
                if (Math.Abs(newHeight - currentHeight) > 2)
                {
                    SetWindowPos(_windowHandle, IntPtr.Zero, 0, 0, windowRect.Right - windowRect.Left, newHeight,
                        SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
                }
            }
        }

        private static int MeasureTextHeight(string text, int width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0)
            {
                return 0;
            }

            IntPtr hdc = GetDC(_labelHandle != IntPtr.Zero ? _labelHandle : _windowHandle);
            if (hdc == IntPtr.Zero)
            {
                return 0;
            }

            RECT rect = new RECT { Left = 0, Top = 0, Right = width, Bottom = 0 };
            DrawTextW(hdc, text, text.Length, ref rect, DT_CALCRECT | DT_WORDBREAK);
            ReleaseDC(_labelHandle != IntPtr.Zero ? _labelHandle : _windowHandle, hdc);
            return rect.Bottom - rect.Top;
        }

        private static void CenterWindowOnUnityWindow()
        {
            if (_windowHandle == IntPtr.Zero || _unityWindowHandle == IntPtr.Zero)
            {
                return;
            }

            if (!GetWindowRect(_unityWindowHandle, out RECT unityRect))
            {
                return;
            }

            if (!GetWindowRect(_windowHandle, out RECT windowRect))
            {
                return;
            }

            int unityWidth = unityRect.Right - unityRect.Left;
            int unityHeight = unityRect.Bottom - unityRect.Top;
            int windowWidth = windowRect.Right - windowRect.Left;
            int windowHeight = windowRect.Bottom - windowRect.Top;

            int x = unityRect.Left + ((unityWidth - windowWidth) / 2);
            int y = unityRect.Top + ((unityHeight - windowHeight) / 2);

            SetWindowPos(_windowHandle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private static void HandleCommand(int commandId, int notificationCode)
        {
            if (notificationCode != BN_CLICKED)
            {
                return;
            }

            if (commandId == OkButtonId)
            {
                CloseDialog(NativeDialogResult.Ok);
            }
            else if (commandId == CancelButtonId)
            {
                CloseDialog(NativeDialogResult.Cancel);
            }
        }

        private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_COMMAND:
                    HandleCommand(LowWord(wParam), HighWord(wParam));
                    return IntPtr.Zero;
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
                case WM_CLOSE:
                    CloseDialog(NativeDialogResult.Closed);
                    return IntPtr.Zero;
                case WM_SIZE:
                    UpdateLayout();
                    break;
                case WM_DESTROY:
                    _labelHandle = IntPtr.Zero;
                    _iconHandle = IntPtr.Zero;
                    _okButtonHandle = IntPtr.Zero;
                    _cancelButtonHandle = IntPtr.Zero;
                    break;
            }

            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        private static int LowWord(IntPtr value) => (int)((long)value & 0xFFFF);
        private static int HighWord(IntPtr value) => (int)(((long)value >> 16) & 0xFFFF);

        private enum NativeDialogResult
        {
            Ok,
            Cancel,
            Closed,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
            public uint lPrivate;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEXW
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

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "RegisterClassExW", SetLastError = true)]
        private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "UnregisterClassW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(
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

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "DefWindowProcW")]
        private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "DestroyWindow", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "LoadCursorW", SetLastError = true)]
        private static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "LoadIconW", SetLastError = true)]
        private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "SetWindowTextW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int fnObject);
        [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr hdc, int mode);
        [DllImport("gdi32.dll")] private static extern int SetTextColor(IntPtr hdc, int crColor);
        [DllImport("user32.dll")] private static extern int GetSysColor(int nIndex);
        [DllImport("user32.dll")] private static extern IntPtr GetSysColorBrush(int nIndex);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int DrawTextW(IntPtr hdc, string lpchText, int cchText, ref RECT lprc, int dwDTFormat);
        [DllImport("kernel32.dll", ExactSpelling = true, EntryPoint = "GetModuleHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string lpModuleName);
#else
        public static bool ShowDialog(NativeDialogOptions options, out string errorMessage)
        {
            errorMessage = "Native dialogs are only supported on Windows.";
            return false;
        }

        public static void CloseDialog()
        {
        }
#endif
    }
}
