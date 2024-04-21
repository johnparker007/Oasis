using MfmeTools.UnityStructWrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MfmeTools.WindowCapture.NativeMethods;

namespace MfmeTools.Mfme
{
    public static class MfmeScraper
    {
        public class MfmeWindow
        {
            public IntPtr Handle = IntPtr.Zero;
            public RECT Rect = new RECT();

            public Color32[] GetPixels(int x, int y, int width, int height)
            {
                // TODO
                return null;
            }
        }

        public static MfmeWindow SplashScreen = new MfmeWindow();
        public static MfmeWindow MainForm = new MfmeWindow();
        public static MfmeWindow Properties = new MfmeWindow();

        public static MfmeWindow CurrentWindow = null;


    }
}
