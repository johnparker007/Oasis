using MfmeTools.UnityStructWrappers;
using MfmeTools.WindowCapture;
using MfmeTools.WindowCapture.BitBlt;
using MfmeTools.WindowCapture.Shared.Interfaces;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using Resource = SharpDX.Direct3D11.Resource;

namespace MfmeTools.Mfme
{
    public static class MfmeScraper
    {
        public static readonly int kMfmeWindowTitlebarHeight = 30;

        public static MfmeWindow SplashScreen = new MfmeWindow(new BitBlt());
        public static MfmeWindow MainForm = new MfmeWindow(new BitBlt());
        public static MfmeWindow Properties = new MfmeWindow(new BitBlt());

        public static MfmeWindow CurrentWindow = null;

        // JP not sure if these need to be member variable yet:
        public static Device Device = null;
        public static SwapChain SwapChain = null;
        public static Factory Factory = null;

        public static void Initialise()
        {
            // JP not looking to actually output the image, can a dummy Form object work?
            Form form = new Form();

            // create a Device and SwapChain
            var swapChainDescription = new SwapChainDescription
            {
                BufferCount = 2,
                Flags = SwapChainFlags.None,
                IsWindowed = true,
                ModeDescription = new ModeDescription(form.ClientSize.Width, form.ClientSize.Height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                OutputHandle = form.Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport, swapChainDescription, out Device, out SwapChain);

            using var swapChain1 = SwapChain.QueryInterface<SwapChain1>();

            // ignore all Windows events
            using var Factory = swapChain1.GetParent<Factory>();
            Factory.MakeWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAll);


            // JP not sure if should set up all windows here:
            Properties.StartCapture();
        }

        public static void ScrapeCurrentWindow()
        {

        }
    }
}
