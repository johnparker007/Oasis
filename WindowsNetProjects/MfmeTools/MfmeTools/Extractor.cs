using MfmeTools.Mfme;
using System.Diagnostics;
using WindowsInput;
using System.Threading;

namespace MfmeTools
{
    using WindowCapture;

    public class Extractor
    {
        public struct Options
        {
            public string SourceLayoutPath;
            public bool UseCachedLampImages;
            public bool UseCachedReelImages;
            public bool ScrapeLamps5To8;
            public bool ScrapeLamps9To12;
        }

        private Process _mfmeProcess = null;
        private Process _dllProcess = null;

        public void StartExtraction(Options options)
        {
            OutputLog.Log("Starting Extraction");
            OutputLog.Log("Extraction source layout: " + options.SourceLayoutPath);

            Program.LayoutCopier.CopyToMfmeTools(options.SourceLayoutPath);
            OutputLog.Log("Copied source layout to MFME Tools");


            // XXX TEST
            //StartCoroutine
            InputSimulator inputSimulator = new InputSimulator();

            LoadAndExtractCurrentLayout(inputSimulator, options);
        }

        private void LoadAndExtractCurrentLayout(InputSimulator inputSimulator, Options options)
        {
            LaunchMfmeAndDll();

            WindowCapture.WindowCapture.Reset();

            OutputLog.Log("Waiting for matching MFME.exe window handles...");

            CaptureMFMESplashScreenWindow();
            CaptureMFMEMainFormWindow();












            //            OutputLog.LogError("JP TEST Sending Win+M minimise keystroke combo");
            //            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LWIN, WindowsInput.Native.VirtualKeyCode.VK_M);

            //            Thread.Sleep(5000);

            //inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_J);
            //Thread.Sleep(50);

            //inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_O);
            //Thread.Sleep(50);

            //inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_H);
            //Thread.Sleep(50);

            //inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_N);
            //Thread.Sleep(50);

            //inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LWIN, WindowsInput.Native.VirtualKeyCode.VK_M);
            //Thread.Sleep(50);


            //yield return null;
        }

        private void LaunchMfmeAndDll()
        {
            _mfmeProcess = MfmeController.LaunchMFMEExeWithLayout();
            OutputLog.Log($"MFME exe launched (Process id: {_mfmeProcess.Id})");

            _dllProcess = MfmeController.LaunchMFMEDllInjector(_mfmeProcess);
            OutputLog.Log($"MFME dll injector launched (Process id: {_dllProcess.Id})");
        }

        private void CaptureMFMESplashScreenWindow()
        {
            const int kRetryCount = 1000;
            for (int retry = 0; retry < kRetryCount; ++retry)
            {
                WindowCapture.WindowCapture.FindSplashscreenWindow((uint)_mfmeProcess.Id);
                if (WindowCapture.WindowCapture.SplashscreenWindowFound)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            if (WindowCapture.WindowCapture.SplashscreenWindowFound)
            {
                OutputLog.Log("MFME.exe splashscreen window handle found");
                OutputLog.Log("Splash title: "
                    + WindowCapture.WindowCapture.GetWindowText(WindowCapture.WindowCapture.SplashscreenWindowHandle));
            }
            else
            {
                OutputLog.LogError("MFME.exe splashscreen window handle could not be found!");
            }
        }

        private void CaptureMFMEMainFormWindow()
        {
            const int kRetryCount = 1000;
            for (int retry = 0; retry < kRetryCount; ++retry)
            {
                WindowCapture.WindowCapture.FindMainformWindow((uint)_mfmeProcess.Id);
                if (WindowCapture.WindowCapture.MainFormWindowFound)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            if (WindowCapture.WindowCapture.MainFormWindowFound)
            {
                OutputLog.Log("MFME.exe mainform window handle found");
                OutputLog.Log("Mainform title: "
                    + WindowCapture.WindowCapture.GetWindowText(WindowCapture.WindowCapture.MainFormWindowHandle));
            }
            else
            {
                OutputLog.LogError("MFME.exe mainform window handle could not be found!");
            }

            Thread.Sleep(100);

            OutputLog.Log("Mainform title after 100ms sleep: "
                + WindowCapture.WindowCapture.GetWindowText(WindowCapture.WindowCapture.MainFormWindowHandle));

        }



    }
}
