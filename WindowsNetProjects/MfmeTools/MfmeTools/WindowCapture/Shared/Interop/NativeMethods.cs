using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MfmeTools.WindowCapture.Shared.Interop
{
    public static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(HandleRef hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(System.IntPtr hWnd, System.IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);


        //        RECT rect;
        //        GetWindowRect(hwnd, &rect);
        //        RECT itself is a structure

        //typedef struct _RECT
        //        {
        //            LONG left;
        //            LONG top;
        //            LONG right;
        //            LONG bottom;
        //        }
        //        RECT;



        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public RECT(System.Drawing.Rectangle r)
                : this(r.Left, r.Top, r.Right, r.Bottom)
            {
            }

            public int X
            {
                get
                {
                    return Left;
                }
                set
                {
                    Right -= (Left - value);
                    Left = value;
                }
            }

            public int Y
            {
                get
                {
                    return Top;
                }
                set
                {
                    Bottom -= (Top - value);
                    Top = value;
                }
            }

            public int Height
            {
                get
                {
                    return Bottom - Top;
                }
                set
                {
                    Bottom = value + Top;
                }
            }

            public int Width
            {
                get
                {
                    return Right - Left;
                }
                set
                {
                    Right = value + Left;
                }
            }

            public static implicit operator System.Drawing.Rectangle(RECT r)
            {
                return new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height);
            }

            public static implicit operator RECT(System.Drawing.Rectangle r)
            {
                return new RECT(r);
            }
        }


        //To get the window bounds excluding the drop shadow, use DwmGetWindowAttribute,
        //specifying DWMWA_EXTENDED_FRAME_BOUNDS.Note that unlike the Window Rect, the
        //DWM Extended Frame Bounds are not adjusted for DPI.Getting the extended frame
        //bounds can only be done after the window has been shown at least once.

    }
}