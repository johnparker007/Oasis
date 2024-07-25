using Oasis.MfmeTools.Shared.Mfme;
using System.Diagnostics;
using WindowsInput;
using System.Threading;
using static Oasis.MfmeTools.WindowCapture.Shared.Interop.NativeMethods;
using static Oasis.MfmeTools.Shared.Mfme.MFMEConstants;
using System;
using static Oasis.MfmeTools.Mfme.MfmeExtractor;
using Oasis.MfmeTools.Shared.Extract;
using System.IO;
using Newtonsoft.Json;
using Oasis.MfmeTools.Mfme;
using System.Windows.Forms;

namespace Oasis.MfmeTools
{
    public class Extractor
    {
        public struct Options
        {
            public string SourceLayoutPath;
            public bool UseCachedBackgroundImage;
            public bool UseCachedLampImages;
            public bool UseCachedButtonImages;
            public bool UseCachedReelImages;
            public bool UseCachedBitmapImages;
            public bool ScrapeLamps5To8;
            public bool ScrapeLamps9To12;
        }

        public static Layout Layout;

        private Process _mfmeProcess = null;
        private Process _dllProcess = null;

        private MFMEComponentType _previousComponentType = MFMEComponentType.None;


        public void StartExtraction(Options options)
        {
            OutputLog.Log("Starting Extraction");
            OutputLog.Log("Extraction source layout: " + options.SourceLayoutPath);

            Layout = new Layout() { ASName = Path.GetFileNameWithoutExtension(options.SourceLayoutPath) };

            Program.LayoutCopier.CopyToMfmeTools(options.SourceLayoutPath);
            OutputLog.Log("Copied source layout to MFME Tools");

            FileSystem.Setup(options.SourceLayoutPath,
                options.UseCachedBackgroundImage,
                options.UseCachedReelImages,
                options.UseCachedLampImages,
                options.UseCachedButtonImages,
                options.UseCachedBitmapImages);
            OutputLog.Log("Extract filesystem set up");

            InputSimulator inputSimulator = new InputSimulator();

            LoadAndExtractCurrentLayout(inputSimulator);
        }

        private void LoadAndExtractCurrentLayout(InputSimulator inputSimulator)
        {
            LaunchMfmeAndDll();

            WindowCapture.WindowCapture.Reset();

            OutputLog.Log("Waiting for matching MFME.exe window handles...");

            CaptureMFMESplashScreenWindow();

            CaptureMFMEMainFormWindow();
            GetMFMEMainFormClientRect();

            MFMEAutomation.ToggleEditMode(inputSimulator);

            ExtractConfiguration(inputSimulator);

            MFMEAutomation.CopyOffLampsToBackground(inputSimulator);

            MFMEAutomation.OpenPropertiesWindow(inputSimulator, true);
            CaptureMFMEPropertiesWindow();
            GetMFMEPropertiesClientRect();

            MfmeScraper.CurrentWindow = MfmeScraper.Properties;
            MfmeScraper.Initialise();

            DelphiFontScraper.Initialise();

            MFMEAutomation.ClickPropertiesComponentPreviousUntilOnFirstComponent(inputSimulator);

            // begin the component scraping loop:
            _previousComponentType = MFMEComponentType.None;
            int zOrder = 0;
            do
            {
                string mfmeComponentTypeText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentTypeTabX, MFMEScraperConstants.kComponentTypeTabY);

                // do this first, as we may park the cursor in Angle input field as part of a workaround fix
                // for an MFME bug (present in Pook Indiana Jones DX as an example:
                string angleText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentAngle_X, MFMEScraperConstants.kComponentAngle_Y);

                string componentXText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentPositionX_X, MFMEScraperConstants.kComponentPositionX_Y);

                string componentYText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentPositionY_X, MFMEScraperConstants.kComponentPositionY_Y);

                string componentWidthText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentPositionWidth_X, MFMEScraperConstants.kComponentPositionWidth_Y);

                if (_previousComponentType == MFMEComponentType.AlphaNew)
                {
                    MFMEAutomation.DoWorkaroundFixForMFMEComponentHeightBugAfterAlphaNewComponent(
                        inputSimulator);
                }

                string componentHeightText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentPositionHeight_X, MFMEScraperConstants.kComponentPositionHeight_Y);

                string textBoxText = "";
                // workaround, as once text box has been clicked in, the flashing cursor in top-left is picked up as non-blank,
                // even if text box is empty
                const int kIgnoreLeftmostPixelColumnCount = 6;
                if (!MfmeScraper.IsImageBoxBlank(
                    MFMEScraperConstants.kComponentTextBox_X + kIgnoreLeftmostPixelColumnCount, MFMEScraperConstants.kComponentTextBox_Y,
                    MFMEScraperConstants.kComponentTextBox_Width - kIgnoreLeftmostPixelColumnCount, MFMEScraperConstants.kComponentTextBox_Height,
                    false, false))
                {
                    // TOIMPROVE this offset to get the mouse cursor to select inside the text box should be done better:
                    const int kOffsetToClickInsideTextBox = 6;
                    MFMEAutomation.GetText(inputSimulator,  
                        MFMEScraperConstants.kComponentTextBox_X + kOffsetToClickInsideTextBox,
                        MFMEScraperConstants.kComponentTextBox_Y + kOffsetToClickInsideTextBox);

                    //textBoxText = TryGetClipboardText();
                    textBoxText = GetTextFromClipboard();
                }

                ComponentStandardData componentStandardData = new ComponentStandardData(
                    componentXText, componentYText, componentWidthText, componentHeightText, angleText, textBoxText, zOrder);

                MFMEComponentType mfmeComponentType = MFMEAutomation.GetMFMEComponentType(mfmeComponentTypeText);
                switch (mfmeComponentType)
                {
                    case MFMEComponentType.Background:
                        ExtractComponentProcessor.ProcessBackground(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.Lamp:
                        ExtractComponentProcessor.ProcessLamp(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.Reel:
                        ExtractComponentProcessor.ProcessReel(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.DotAlpha:
                        ExtractComponentProcessor.ProcessDotAlpha(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.MatrixAlpha:
                        ExtractComponentProcessor.ProcessMatrixAlpha(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.SevenSegment:
                        ExtractComponentProcessor.ProcessSevenSegment(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.AlphaNew:
                        ExtractComponentProcessor.ProcessAlphaNew(inputSimulator, componentStandardData);
                        break;
                    //case MFMEComponentType.Alpha:
                    //    ExtractComponentProcessor.ProcessAlpha(inputSimulator, componentStandardData);
                    //    break;
                    case MFMEComponentType.Checkbox:
                        ExtractComponentProcessor.ProcessCheckbox(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.RgbLed:
                        ExtractComponentProcessor.ProcessRgbLed(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.Led:
                        ExtractComponentProcessor.ProcessLed(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.Frame:
                        ExtractComponentProcessor.ProcessFrame(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.Label:
                        ExtractComponentProcessor.ProcessLabel(inputSimulator, componentStandardData);
                        break;
                    //        case MFMEComponentType.Button:
                    //            ExtractComponentProcessor.ProcessButton(inputSimulator, componentStandardData);
                    //            break;
                    //        case MFMEComponentType.BandReel:
                    //            ExtractComponentProcessor.ProcessBandReel(inputSimulator, componentStandardData);
                    //            break;
                    //        case MFMEComponentType.DiscReel:
                    //            ExtractComponentProcessor.ProcessDiscReel(inputSimulator, componentStandardData);
                    //            break;
                    //        case MFMEComponentType.FlipReel:
                    //            ExtractComponentProcessor.ProcessFlipReel(inputSimulator, componentStandardData);
                    //            break;
                    //        case MFMEComponentType.JpmBonusReel:
                    //            ExtractComponentProcessor.ProcessJpmBonusReel(inputSimulator, componentStandardData);
                    //            break;
                    case MFMEComponentType.BfmAlpha:
                        ExtractComponentProcessor.ProcessBfmAlpha(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.ProconnMatrix:
                        ExtractComponentProcessor.ProcessProconnMatrix(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.EpochAlpha:
                        ExtractComponentProcessor.ProcessEpochAlpha(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.IgtVfd:
                        ExtractComponentProcessor.ProcessIgtVfd(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.Plasma:
                        ExtractComponentProcessor.ProcessPlasma(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.DotMatrix:
                        ExtractComponentProcessor.ProcessDotMatrix(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.BfmLed:
                        ExtractComponentProcessor.ProcessBfmLed(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.BfmColourLed:
                        ExtractComponentProcessor.ProcessBfmColourLed(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.AceMatrix:
                        ExtractComponentProcessor.ProcessAceMatrix(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.EpochMatrix:
                        ExtractComponentProcessor.ProcessEpochMatrix(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.BarcrestBwbVideo:
                        ExtractComponentProcessor.ProcessBarcrestBwbVideo(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.BfmVideo:
                        ExtractComponentProcessor.ProcessBfmVideo(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.AceVideo:
                        ExtractComponentProcessor.ProcessAceVideo(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.MaygayVideo:
                        ExtractComponentProcessor.ProcessMaygayVideo(inputSimulator, componentStandardData);
                        break;
                    //        case MFMEComponentType.PrismLamp:
                    //            ExtractComponentProcessor.ProcessPrismLamp(inputSimulator, componentStandardData);
                    //            break;
                    //        case MFMEComponentType.Bitmap:
                    //            ExtractComponentProcessor.ProcessBitmap(inputSimulator, componentStandardData);
                    //            break;
                    case MFMEComponentType.SevenSegmentBlock:
                        ExtractComponentProcessor.ProcessSevenSegmentBlock(inputSimulator, componentStandardData);
                        break;
                    case MFMEComponentType.Border:
                        ExtractComponentProcessor.ProcessBorder(inputSimulator, componentStandardData);
                        break;

                    default:
                        OutputLog.LogError($"Skipping ComponentType {mfmeComponentType}, zOrder {zOrder}");
                        break;
                }

// OASIS - MAYBE TODO?  Really should try a more detailed scrape of font info so we don't need these
// crappy 'SnapshotRenders' any more.  It was for scraping classic layout text to work out text flow
// without knowing font style/size etc.  Could do with something more advanced now this Extractor is
// entering its releasable form.
                //    yield return WriteSnapshotTextRender(Extractor.Layout.Components.Last());
                //Thread.Sleep(100); // missed scraping a text field on Adders (Hold 1) since adding this code to write the SnapshotTextRender

                _previousComponentType = mfmeComponentType;
                ++zOrder;

                MFMEAutomation.ClickPropertiesComponentNext(inputSimulator);
            }
            while (!MFMEAutomation.PreviousComponentNavigationTimedOut);

            FileSystem.SaveLayout(Layout);

            // PROB TO REMOVE - FROM THE OLD ARCADE SIM BIG CLASSIC EXTRACTION ATTEMPT
            //if (zOrder == Extractor.Layout.Components.Count && Extractor.Layout.Components.Count > 0)
            //{
            //    string extractCompletedMarkerFilePath = Path.Combine(
            //        Converter.GetOutputDirectoryPath(MachineConfiguration), kExtractCompletedFilename);

            //    File.WriteAllBytes(extractCompletedMarkerFilePath, new byte[0]);
            //}

            MfmeController.KillMFMEProcessIfNotExited(_mfmeProcess);
        }

        // TODO move this out to a Clipboard helper class if it works:
        // TOIMPROVE - this is still really rather slow, though it does work
        static string TryGetClipboardText(int retryCount = 200, int retryDelayMilliseconds = 10)
        {
            bool success = false;
            int attempts = 0;
            string text = null;

            while (!success && attempts < retryCount)
            {
                try
                {
                    // just as slow with this ContainsText check commented out
                    //if (Clipboard.ContainsText())
                    {
                        text = Clipboard.GetText();
                        success = true;
                    }
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    attempts++;
                    Thread.Sleep(retryDelayMilliseconds); // Wait for a short time before retrying
                }
            }

            if (!success)
            {
                Console.WriteLine("Failed to get clipboard text after multiple attempts.");
            }

            return text;
        }

        internal static string GetTextFromClipboard()
        {
            string clipText = "";

            RunAsSTAThread(
           () =>
           {
               clipText = Clipboard.GetText();
           });

            return clipText;
        }

        internal static void RunAsSTAThread(Action goForIt)
        {
            AutoResetEvent @event = new AutoResetEvent(false);
            Thread thread = new Thread(
                () =>
                {
                    goForIt();
                    @event.Set();
                });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            @event.WaitOne();
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
                    + WindowCapture.WindowCapture.GetWindowText(MfmeScraper.SplashScreen.Handle));
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
                    + WindowCapture.WindowCapture.GetWindowText(MfmeScraper.MainForm.Handle));
            }
            else
            {
                OutputLog.LogError("MFME.exe mainform window handle could not be found!");
            }

            Thread.Sleep(100);

            OutputLog.Log("Mainform title after 100ms sleep: "
                + WindowCapture.WindowCapture.GetWindowText(MfmeScraper.MainForm.Handle));

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
                    + WindowCapture.WindowCapture.GetWindowText(MfmeScraper.Properties.Handle));
            }
            else
            {
                OutputLog.LogError("MFME.exe properties window handle could not be found!");
            }
        }

        private void GetMFMEMainFormClientRect()
        {
            const bool kDebugOutput = false;

            MfmeScraper.MainForm.Rect =
                WindowCapture.WindowCapture.GetWindowRect(this, MfmeScraper.MainForm.Handle);

            RemoveTitleBarAndBorders(ref MfmeScraper.MainForm.Rect);

            if (kDebugOutput)
            {
                OutputLog.Log($"Mainform rect: " +
                    $"{MfmeScraper.MainForm.Rect.X}, " +
                    $"{MfmeScraper.MainForm.Rect.Y}, " +
                    $"{MfmeScraper.MainForm.Rect.Width}, " +
                    $"{MfmeScraper.MainForm.Rect.Height}");
            }
        }

        private void GetMFMEPropertiesClientRect()
        {
            const bool kDebugOutput = false;

            MfmeScraper.Properties.Rect =
                WindowCapture.WindowCapture.GetWindowRect(this, MfmeScraper.Properties.Handle);

            RemoveTitleBarAndBorders(ref MfmeScraper.Properties.Rect);

            if (kDebugOutput)
            {
                OutputLog.Log($"Properties rect: " +
                    $"{MfmeScraper.Properties.Rect.X}, " +
                    $"{MfmeScraper.Properties.Rect.Y}, " +
                    $"{MfmeScraper.Properties.Rect.Width}, " +
                    $"{MfmeScraper.Properties.Rect.Height}");
            }
        }

        private void RemoveTitleBarAndBorders(ref RECT rect)
        {
            //rect.Y += MfmeScraper.kMfmeWindowTitlebarHeight;
            //rect.Height -= MfmeScraper.kMfmeWindowTitlebarHeight;
        }

        private void ExtractConfiguration(InputSimulator inputSimulator)
        {
            //ExtractComponentBackground extractBackground = new ExtractComponentBackground(componentStandardData);
            //extractBackground.BmpImageFilename = Path.GetFileName(saveFullPath);

            //Extractor.Layout.Components.Add(extractBackground);

            //Extractor.Layout.BackgroundImageSize.X = componentStandardData.Size.x;
            //Extractor.Layout.BackgroundImageSize.Y = componentStandardData.Size.y;





            //            if (MachineConfiguration.Platform == MachineConfigurationData.PlatformType.MPU4)
            //            {
            //                yield return ExtractMPU4CharacteriserLampData(inputSimulator);
            //            }

            //            bool scrapingSetup = false;
            //            switch (MachineConfiguration.Platform)
            //            {
            //                case MachineConfigurationData.PlatformType.MPU4:
            //                    scrapingSetup = true;
            //                    break;
            //                default:
            //                    break;
            //            }

            //            if (scrapingSetup)
            //            {
            //                EmulatorScraper.SetScrapeChildIfFound(true, "Game Configuration");

            //                // move mouse to top of screen so it doesn't hover over fields and break scraping
            //                //inputSimulator.Mouse.MoveMouseBy(0, -2000);
            //                inputSimulator.Mouse.MoveMouseTo(1, 1);

            //                // open configuration window
            //                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_C);

            //                yield return new WaitForSeconds(2f);
            //            }

            //            switch (MachineConfiguration.Platform)
            //            {
            //                case MachineConfigurationData.PlatformType.MPU4:
            //                    yield return ExtractConfigurationPageMPU4(inputSimulator);
            //                    break;
            //                case MachineConfigurationData.PlatformType.Scorpion1:
            //                    yield return ExtractConfigurationPageScorpion1(inputSimulator);
            //                    break;
            //                case MachineConfigurationData.PlatformType.Scorpion2:
            //                    yield return ExtractConfigurationPageScorpion2(inputSimulator);
            //                    break;
            //                case MachineConfigurationData.PlatformType.MPS2:
            //                    yield return ExtractConfigurationPageMPS2(inputSimulator);
            //                    break;
            //                case MachineConfigurationData.PlatformType.M1AB:
            //                    yield return ExtractConfigurationPageM1AB(inputSimulator);
            //                    break;




            //                default:
            //                    break;
            //            }

            //            if (scrapingSetup)
            //            {
            //                // close configuration window
            //                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.F4);
            //                yield return new WaitForSeconds(2f);

            //                EmulatorScraper.SetScrapeChildIfFound(false);
            //            }

            //            yield return null;
        }

        private void ExtractMPU4CharacteriserLampData(InputSimulator inputSimulator)
        {

        }

        //            if (MachineConfiguration.Platform == MachineConfigurationData.PlatformType.MPU4)
        //            {
        //                yield return ExtractMPU4CharacteriserLampData(inputSimulator);
        //            }

        //            bool scrapingSetup = false;
        //            switch (MachineConfiguration.Platform)
        //            {
        //                case MachineConfigurationData.PlatformType.MPU4:
        //                    scrapingSetup = true;
        //                    break;
        //                default:
        //                    break;
        //            }

        //            if (scrapingSetup)
        //            {
        //                EmulatorScraper.SetScrapeChildIfFound(true, "Game Configuration");

        //                // move mouse to top of screen so it doesn't hover over fields and break scraping
        //                //inputSimulator.Mouse.MoveMouseBy(0, -2000);
        //                inputSimulator.Mouse.MoveMouseTo(1, 1);

        //                // open configuration window
        //                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_C);

        //                yield return new WaitForSeconds(2f);
        //            }

        //            switch (MachineConfiguration.Platform)
        //            {
        //                case MachineConfigurationData.PlatformType.MPU4:
        //                    yield return ExtractConfigurationPageMPU4(inputSimulator);
        //                    break;
        //                case MachineConfigurationData.PlatformType.Scorpion1:
        //                    yield return ExtractConfigurationPageScorpion1(inputSimulator);
        //                    break;
        //                case MachineConfigurationData.PlatformType.Scorpion2:
        //                    yield return ExtractConfigurationPageScorpion2(inputSimulator);
        //                    break;
        //                case MachineConfigurationData.PlatformType.MPS2:
        //                    yield return ExtractConfigurationPageMPS2(inputSimulator);
        //                    break;
        //                case MachineConfigurationData.PlatformType.M1AB:
        //                    yield return ExtractConfigurationPageM1AB(inputSimulator);
        //                    break;




        //                default:
        //                    break;
        //            }

        //            if (scrapingSetup)
        //            {
        //                // close configuration window
        //                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.F4);
        //                yield return new WaitForSeconds(2f);

        //                EmulatorScraper.SetScrapeChildIfFound(false);
        //            }

        //            yield return null;
        //        }

        //        private IEnumerator ExtractMPU4CharacteriserLampData(InputSimulator inputSimulator)
        //        {
        //            EmulatorScraper.SetScrapeChildIfFound(false);

        //            ExtractConfigurationMPU4 configuration = new ExtractConfigurationMPU4();

        //            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_D);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            // can't use shortcut as sometimes it changes from *C*haracteriser, to C*h*aracteriser, so Downs are safer:
        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //            yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

        //            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LSHIFT, WindowsInput.Native.VirtualKeyCode.TAB);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //            for (int characteriserLamp = 0; characteriserLamp < ExtractConfigurationMPU4.kCharacteriserLampCount; ++characteriserLamp)
        //            {
        //                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.TAB);
        //                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_C);
        //                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                configuration.CharacteriserLamps[characteriserLamp] = GUIUtility.systemCopyBuffer;
        //            }

        //            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.F4);
        //            yield return new WaitForSeconds(MFMEAutomation.kLongDelay);

        //            Extractor.Layout.Configuration = configuration;

        //            EmulatorScraper.SetScrapeChildIfFound(true);
        //        }

        //        private IEnumerator ExtractConfigurationPageMPU4(InputSimulator inputSimulator)
        //        {
        //            ExtractConfigurationMPU4 configuration = (ExtractConfigurationMPU4)Extractor.Layout.Configuration;

        //            configuration.MeterElements[0].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInType1_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterInType1_DropdownY);

        //            configuration.MeterElements[1].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInType2_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterInType2_DropdownY);

        //            configuration.MeterElements[2].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInType3_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterInType3_DropdownY);

        //            configuration.MeterElements[0].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier1_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier1_DropdownY);

        //            configuration.MeterElements[1].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier2_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier2_DropdownY);

        //            configuration.MeterElements[2].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier3_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier3_DropdownY);

        //            configuration.MeterElements[3].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutType1_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterOutType1_DropdownY);

        //            configuration.MeterElements[4].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutType2_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterOutType2_DropdownY);

        //            configuration.MeterElements[5].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutType3_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterOutType3_DropdownY);

        //            configuration.MeterElements[3].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier1_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier1_DropdownY);

        //            configuration.MeterElements[4].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier2_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier2_DropdownY);

        //            configuration.MeterElements[5].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier3_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier3_DropdownY);

        //            configuration.Stake = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Stake_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4Stake_DropdownY);

        //            configuration.Prize = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Prize_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4Prize_DropdownY);

        //            configuration.Percentage = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Percentage_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4Percentage_DropdownY);

        //            if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4VolumeControlAuto_RadioButtonTopLeftX),
        //                MFMEScraperConstants.kConfigurationMPU4VolumeControlAuto_RadioButtonTopLeftY))
        //            {
        //                configuration.VolumeControl = MFMEExtract.ExtractConfigurationMPU4.VolumeControlType.Auto;
        //            }
        //            else
        //            {
        //                configuration.VolumeControl = MFMEExtract.ExtractConfigurationMPU4.VolumeControlType.Manual;
        //            }

        //            configuration.RomPagingAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4ROMPaging_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4ROMPaging_DropdownY);

        //            if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4LVDNo_RadioButtonTopLeftX),
        //                MFMEScraperConstants.kConfigurationMPU4LVDNo_RadioButtonTopLeftY))
        //            {
        //                configuration.LVD = MFMEExtract.ExtractConfigurationMPU4.LVDType.No;
        //            }
        //            else
        //            {
        //                configuration.LVD = MFMEExtract.ExtractConfigurationMPU4.LVDType.Yes;
        //            }

        //            if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4DisplayReel_RadioButtonTopLeftX),
        //                MFMEScraperConstants.kConfigurationMPU4DisplayReel_RadioButtonTopLeftY))
        //            {
        //                configuration.Display = MFMEExtract.ExtractConfigurationMPU4.DisplayType.Reel;
        //            }
        //            else
        //            {
        //                configuration.Display = MFMEExtract.ExtractConfigurationMPU4.DisplayType.Video;
        //            }

        //            if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4LampTestPass_RadioButtonTopLeftX),
        //                MFMEScraperConstants.kConfigurationMPU4LampTestPass_RadioButtonTopLeftY))
        //            {
        //                configuration.LampTest = MFMEExtract.ExtractConfigurationMPU4.LampTestType.Pass;
        //            }
        //            else
        //            {
        //                configuration.LampTest = MFMEExtract.ExtractConfigurationMPU4.LampTestType.Fail;
        //            }

        //            configuration.PayoutAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Payout_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4Payout_DropdownY);

        //            configuration.ExtenderAux1AsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4ExtenderAux1_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4ExtenderAux1_DropdownY);

        //            configuration.SevenSegDisplayAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SevenSegDisplay_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4SevenSegDisplay_DropdownY);

        //            configuration.ReelsAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Reels_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4Reels_DropdownY);

        //            configuration.SoundAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Sound_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4Sound_DropdownY);

        //            configuration.EncryptionAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Encryption_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4Encryption_DropdownY);

        //            configuration.CharacterAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Character_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4Character_DropdownY);

        //            configuration.DataPakAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4DataPak_DropdownX),
        //                MFMEScraperConstants.kConfigurationMPU4DataPak_DropdownY);

        //            configuration.SwitchServiceAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchService_X),
        //                MFMEScraperConstants.kConfigurationMPU4SwitchService_Y);

        //            configuration.SwitchCashAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchCash_X),
        //                MFMEScraperConstants.kConfigurationMPU4SwitchCash_Y);

        //            configuration.SwitchRefillAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchRefill_X),
        //                MFMEScraperConstants.kConfigurationMPU4SwitchRefill_Y);

        //            configuration.SwitchTestAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchTest_X),
        //                MFMEScraperConstants.kConfigurationMPU4SwitchTest_Y);

        //            configuration.SwitchTopUpAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchTopUp_X),
        //                MFMEScraperConstants.kConfigurationMPU4SwitchTopUp_Y);

        //            configuration.Aux1Invert = MFMEScraper.GetCheckboxValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Aux1Invert_CheckboxX),
        //                MFMEScraperConstants.kConfigurationMPU4Aux1Invert_CheckboxY);

        //            configuration.Aux2Invert = MFMEScraper.GetCheckboxValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Aux2Invert_CheckboxX),
        //                MFMEScraperConstants.kConfigurationMPU4Aux2Invert_CheckboxY);

        //            configuration.DoorInvert = MFMEScraper.GetCheckboxValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4DoorInvert_CheckboxX),
        //                MFMEScraperConstants.kConfigurationMPU4DoorInvert_CheckboxY);


        //            if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4AlphaCableNormal_RadioButtonTopLeftX),
        //                MFMEScraperConstants.kConfigurationMPU4AlphaCableNormal_RadioButtonTopLeftY))
        //            {
        //                configuration.AlphaCable = MFMEExtract.ExtractConfigurationMPU4.AlphaCableType.Normal;
        //            }
        //            else
        //            {
        //                configuration.AlphaCable = MFMEExtract.ExtractConfigurationMPU4.AlphaCableType.CR;
        //            }

        //            if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4ModType2_RadioButtonTopLeftX),
        //                MFMEScraperConstants.kConfigurationMPU4ModType2_RadioButtonTopLeftY))
        //            {
        //                configuration.ModType = MFMEExtract.ExtractConfigurationMPU4.ModTypes.Two;
        //            }
        //            else
        //            {
        //                configuration.ModType = MFMEExtract.ExtractConfigurationMPU4.ModTypes.Four;
        //            }

        //            if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4CabinetStyleDefault_RadioButtonTopLeftX),
        //                MFMEScraperConstants.kConfigurationMPU4CabinetStyleDefault_RadioButtonTopLeftY))
        //            {
        //                configuration.CabinetStyle = MFMEExtract.ExtractConfigurationMPU4.CabinetStyleType.Default;
        //            }
        //            else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                 MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4CabinetStyleRio_RadioButtonTopLeftX),
        //                 MFMEScraperConstants.kConfigurationMPU4CabinetStyleRio_RadioButtonTopLeftY))
        //            {
        //                configuration.CabinetStyle = MFMEExtract.ExtractConfigurationMPU4.CabinetStyleType.Rio;
        //            }
        //            else
        //            {
        //                configuration.CabinetStyle = MFMEExtract.ExtractConfigurationMPU4.CabinetStyleType.Genesis;
        //            }

        //            yield return null;
        //        }

        //        private IEnumerator ExtractConfigurationPageScorpion1(InputSimulator inputSimulator)
        //        {
        //            ExtractConfigurationScorpion1 configuration = new ExtractConfigurationScorpion1();

        //            configuration.MeterElements[0].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInType1_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterInType1_DropdownY);

        //            configuration.MeterElements[1].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInType2_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterInType2_DropdownY);

        //            configuration.MeterElements[2].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInType3_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterInType3_DropdownY);

        //            configuration.MeterElements[0].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier1_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier1_DropdownY);

        //            configuration.MeterElements[1].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier2_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier2_DropdownY);

        //            configuration.MeterElements[2].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier3_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier3_DropdownY);

        //            configuration.MeterElements[3].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutType1_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterOutType1_DropdownY);

        //            configuration.MeterElements[4].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutType2_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterOutType2_DropdownY);

        //            configuration.MeterElements[5].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutType3_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterOutType3_DropdownY);

        //            configuration.MeterElements[3].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier1_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier1_DropdownY);

        //            configuration.MeterElements[4].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier2_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier2_DropdownY);

        //            configuration.MeterElements[5].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier3_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier3_DropdownY);

        //            configuration.Stake = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1Stake_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1Stake_DropdownY);

        //            configuration.Prize = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1Prize_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1Prize_DropdownY);

        //            configuration.Percentage = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1Percentage_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1Percentage_DropdownY);

        //            configuration.EncryptionAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1Encryption_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1Encryption_DropdownY);

        //            configuration.SwitchServiceAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchService_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchService_Y);

        //            configuration.SwitchCashAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchCash_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchCash_Y);

        //            configuration.SwitchRefillAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchRefill_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchRefill_Y);

        //            configuration.SwitchTestAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchTest_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchTest_Y);

        //            configuration.SwitchPaySense1AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense1_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense1_Y);

        //            configuration.SwitchPaySense2AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense2_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense2_Y);

        //            configuration.SwitchPaySense3AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense3_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense3_Y);

        //            configuration.SwitchPaySense4AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense4_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense4_Y);

        //            configuration.SwitchDMBusyAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchDMBusy_X),
        //                MFMEScraperConstants.kConfigurationScorpion1SwitchDMBusy_Y);

        //            configuration.DataPakAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1DataPak_DropdownX),
        //                MFMEScraperConstants.kConfigurationScorpion1DataPak_DropdownY);

        //            if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SampledSoundNEC_RadioButtonTopLeftX),
        //                MFMEScraperConstants.kConfigurationScorpion1SampledSoundNEC_RadioButtonTopLeftY))
        //            {
        //                configuration.SampledSound = ExtractConfigurationScorpion1.SampledSoundType.NEC;
        //            }
        //            else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
        //                 MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SampledSoundOKI_RadioButtonTopLeftX),
        //                 MFMEScraperConstants.kConfigurationScorpion1SampledSoundOKI_RadioButtonTopLeftY))
        //            {
        //                configuration.SampledSound = ExtractConfigurationScorpion1.SampledSoundType.OKI;
        //            }
        //            else
        //            {
        //                configuration.SampledSound = ExtractConfigurationScorpion1.SampledSoundType.Global;
        //            }

        //            Extractor.Layout.Configuration = configuration;

        //            yield return null;
        //        }

        //        private IEnumerator ExtractConfigurationPageScorpion2(InputSimulator inputSimulator)
        //        {
        //            ExtractConfigurationScorpion2 configuration = new ExtractConfigurationScorpion2();

        //            Extractor.Layout.Configuration = configuration;

        //            yield return null;
        //        }

        //        private IEnumerator ExtractConfigurationPageMPS2(InputSimulator inputSimulator)
        //        {
        //            ExtractConfigurationMPS2 configuration = new ExtractConfigurationMPS2();

        //            Extractor.Layout.Configuration = configuration;

        //            yield return null;
        //        }

        //        private IEnumerator ExtractConfigurationPageM1AB(InputSimulator inputSimulator)
        //        {
        //            ExtractConfigurationM1AB configuration = new ExtractConfigurationM1AB();

        //            Extractor.Layout.Configuration = configuration;

        //            yield return null;
        //        }

    }
}
