using MfmeTools.Mfme;
using System.Diagnostics;
using WindowsInput;
using System.Threading;

namespace MfmeTools
{
    using System.Runtime.InteropServices;
    using WindowCapture;
    using static MfmeTools.WindowCapture.NativeMethods;

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
            GetMFMEMainFormWindowRect();

            MFMEAutomation.ToggleEditMode(inputSimulator);

            // In ArcadeSim Extractor, I would extract the Configuration here,
            // such as 8x MPU4 Lamp Columns bytes, then the config for the platform
            // such as MPU4 fields, Scorpion fields etc.  Not sure if this will
            // be relevant to this tool, at least initially (since all this is
            // handled in the MAME driver).

            MFMEAutomation.CopyOffLampsToBackground(inputSimulator);

            MFMEAutomation.OpenPropertiesWindow(inputSimulator, true);
            CaptureMFMEPropertiesWindow();
            GetMFMEPropertiesWindowRect();










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

        private void CaptureMFMEPropertiesWindow()
        {
            const int kRetryCount = 1000;
            for (int retry = 0; retry < kRetryCount; ++retry)
            {
                WindowCapture.WindowCapture.FindPropertiesWindow((uint)_mfmeProcess.Id);
                if (WindowCapture.WindowCapture.PropertiesWindowFound)
                {
                    break;
                }

                Thread.Sleep(50);
            }

            if (WindowCapture.WindowCapture.PropertiesWindowFound)
            {
                OutputLog.Log("MFME.exe properties window handle found");
                OutputLog.Log("Properties title: "
                    + WindowCapture.WindowCapture.GetWindowText(WindowCapture.WindowCapture.PropertiesWindowHandle));
            }
            else
            {
                OutputLog.LogError("MFME.exe properties window handle could not be found!");
            }
        }

        private void GetMFMEMainFormWindowRect()
        {
            const bool kDebugOutput = false;

            WindowCapture.WindowCapture.MainFormWindowRect =
                WindowCapture.WindowCapture.GetWindowRect(this, WindowCapture.WindowCapture.MainFormWindowHandle);

            if (kDebugOutput)
            {
                OutputLog.Log($"Mainform rect: " +
                    $"{WindowCapture.WindowCapture.MainFormWindowRect.X}, " +
                    $"{WindowCapture.WindowCapture.MainFormWindowRect.Y}, " +
                    $"{WindowCapture.WindowCapture.MainFormWindowRect.Width}, " +
                    $"{WindowCapture.WindowCapture.MainFormWindowRect.Height}");
            }
        }

        private void GetMFMEPropertiesWindowRect()
        {
            const bool kDebugOutput = false;

            WindowCapture.WindowCapture.PropertiesWindowRect =
                WindowCapture.WindowCapture.GetWindowRect(this, WindowCapture.WindowCapture.PropertiesWindowHandle);

            if(kDebugOutput)
            {
                OutputLog.Log($"Properties rect: " +
                    $"{WindowCapture.WindowCapture.PropertiesWindowRect.X}, " +
                    $"{WindowCapture.WindowCapture.PropertiesWindowRect.Y}, " +
                    $"{WindowCapture.WindowCapture.PropertiesWindowRect.Width}, " +
                    $"{WindowCapture.WindowCapture.PropertiesWindowRect.Height}");
            }
        }


    }
}
