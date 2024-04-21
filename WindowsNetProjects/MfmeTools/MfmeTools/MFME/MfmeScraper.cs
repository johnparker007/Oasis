using MfmeTools.UnityStructWrappers;
using MfmeTools.WindowCapture;
using MfmeTools.WindowCapture.BitBlt;
using MfmeTools.WindowCapture.Shared.Interfaces;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MfmeTools.Mfme
{
    public static class MfmeScraper
    {
        public static MfmeWindow SplashScreen = new MfmeWindow(new BitBlt());
        public static MfmeWindow MainForm = new MfmeWindow(new BitBlt());
        public static MfmeWindow Properties = new MfmeWindow(new BitBlt());

        public static MfmeWindow CurrentWindow = null;

        public static Device Device = null;

        public static void Initialise()
        {

        }
    }
}
