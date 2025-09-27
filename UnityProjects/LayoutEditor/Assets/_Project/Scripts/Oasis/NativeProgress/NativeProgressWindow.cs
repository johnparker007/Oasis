using System;
using System.Runtime.InteropServices;
namespace Oasis.NativeProgress
{
    internal static class NativeProgressWindow
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const string WindowClassName = "OasisNativeProgressWindow";
        private const int CS_HREDRAW = 0x0002;
        private const int CS_VREDRAW = 0x0001;
        private const int WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const int WS_VISIBLE = 0x10000000;
        private const int CW_USEDEFAULT = unchecked((int)0x80000000);
        private const int SW_SHOWNORMAL = 1;
        private const int IDC_ARROW = 32512;
        private const int COLOR_WINDOW = 5;

        private static ushort _classAtom;
        private static IntPtr _instanceHandle = IntPtr.Zero;
        private static IntPtr _windowHandle = IntPtr.Zero;
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
                return false;
            }

            _windowHandle = CreateWindowEx(
                0,
                WindowClassName,
                "Oasis Progress",
                WS_OVERLAPPEDWINDOW | WS_VISIBLE,
                CW_USEDEFAULT,
                CW_USEDEFAULT,
                480,
                120,
                IntPtr.Zero,
                IntPtr.Zero,
                _instanceHandle,
                IntPtr.Zero);

            if (_windowHandle == IntPtr.Zero)
            {
                int lastError = Marshal.GetLastWin32Error();
                UnregisterWindowClass();
                errorMessage = $"CreateWindowEx failed with error {lastError}";
                return false;
            }

            ShowWindow(_windowHandle, SW_SHOWNORMAL);
            UpdateWindow(_windowHandle);

            errorMessage = null;
            return true;
        }

        public static void CloseWindow()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                DestroyWindowNative(_windowHandle);
                _windowHandle = IntPtr.Zero;
            }

            UnregisterWindowClass();
            _wndProc = null;
            _instanceHandle = IntPtr.Zero;
        }

        private static void UnregisterWindowClass()
        {
            if (_classAtom != 0 && _instanceHandle != IntPtr.Zero)
            {
                UnregisterClass(WindowClassName, _instanceHandle);
                _classAtom = 0;
            }
        }

        private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return DefWindowProc(hWnd, msg, wParam, lParam);
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
#else
        public static bool EnsureWindowCreated(out string errorMessage)
        {
            errorMessage = "Native progress window is only supported in Windows standalone builds.";
            return false;
        }

        public static void CloseWindow()
        {
        }
#endif
    }
}
