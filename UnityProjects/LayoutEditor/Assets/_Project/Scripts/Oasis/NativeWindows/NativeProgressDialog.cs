#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Oasis.NativeWindows
{
    internal static class NativeProgressDialog
    {
        private const string WindowClassName = "Oasis.NativeProgressDialog";

        private static readonly object s_classRegistrationLock = new object();
        private static bool s_isClassRegistered;
        private static readonly WndProc s_wndProc = WindowProc;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
            [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetModuleHandleW")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Ensures the hidden progress dialog host window class is registered and returns a freshly created window handle.
        /// </summary>
        /// <param name="windowName">Optional title for diagnostic purposes.</param>
        /// <returns>The handle to the created window.</returns>
        public static IntPtr CreateHostWindow(string windowName = "")
        {
            EnsureClassRegistered();

            IntPtr hwnd = CreateWindowEx(
                0,
                WindowClassName,
                windowName ?? string.Empty,
                0,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            if (hwnd == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create native progress dialog host window.");
            }

            return hwnd;
        }

        private static void EnsureClassRegistered()
        {
            if (s_isClassRegistered)
            {
                return;
            }

            lock (s_classRegistrationLock)
            {
                if (s_isClassRegistered)
                {
                    return;
                }

                var windowClass = new WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                    style = 0,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProc),
                    hInstance = GetModuleHandle(null),
                    hIcon = IntPtr.Zero,
                    hCursor = IntPtr.Zero,
                    hbrBackground = IntPtr.Zero,
                    lpszMenuName = null,
                    lpszClassName = WindowClassName,
                    hIconSm = IntPtr.Zero,
                };

                ushort atom = RegisterClassEx(ref windowClass);
                if (atom == 0)
                {
                    int error = Marshal.GetLastWin32Error();

                    // ERROR_CLASS_ALREADY_EXISTS (1410) is benign when multiple threads race to register.
                    if (error != 1410)
                    {
                        throw new Win32Exception(error, "Failed to register native progress dialog window class.");
                    }
                }

                s_isClassRegistered = true;
            }
        }

        private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }


        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
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
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
            public IntPtr hIconSm;
        }
    }
}
#endif
