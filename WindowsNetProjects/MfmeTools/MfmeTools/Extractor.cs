using Oasis.MfmeTools.Shared.Mfme;
using System.Diagnostics;
using WindowsInput;
using System.Threading;
using static Oasis.MfmeTools.WindowCapture.Shared.Interop.NativeMethods;
using static Oasis.MfmeTools.Shared.Mfme.MFMEConstants;
using System;
using static Oasis.MfmeTools.Shared.Mfme.MfmeExtractor;
using Oasis.MfmeTools.Shared.Extract;
using System.IO;
using Newtonsoft.Json;

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

            // In ArcadeSim Extractor, I would extract the Configuration here,
            // such as 8x MPU4 Lamp Columns bytes, then the config for the platform
            // such as MPU4 fields, Scorpion fields etc.  Not sure if this will
            // be relevant to this tool, at least initially (since all this is
            // handled in the MAME driver).

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
// OASIS - TODO:
                //    // workaround, as once text box has been clicked in, the flashing cursor in top-left is picked up as non-blank,
                //    // even if text box is empty
                //    const int kIgnoreLeftmostPixelColumnCount = 6;
                //    if (!MFMEScraper.IsImageBoxBlank(EmulatorScraper,
                //        MFMEScraperConstants.kComponentTextBox_X + kIgnoreLeftmostPixelColumnCount, MFMEScraperConstants.kComponentTextBox_Y,
                //        MFMEScraperConstants.kComponentTextBox_Width - kIgnoreLeftmostPixelColumnCount, MFMEScraperConstants.kComponentTextBox_Height,
                //        false, false))
                //    {
                //        // TOIMPROVE this offset to get the mouse cursor to select inside the text box should be done better:
                //        const int kOffsetToClickInsideTextBox = 6;
                //        yield return MFMEAutomation.GetTextCoroutine(inputSimulator, this, EmulatorScraper,
                //            MFMEScraperConstants.kComponentTextBox_X + kOffsetToClickInsideTextBox,
                //            MFMEScraperConstants.kComponentTextBox_Y + kOffsetToClickInsideTextBox);
                //        textBoxText = GUIUtility.systemCopyBuffer;
                //    }

                ComponentStandardData componentStandardData = new ComponentStandardData(
                    componentXText, componentYText, componentWidthText, componentHeightText, angleText, textBoxText, zOrder);

                MFMEComponentType mfmeComponentType = MFMEAutomation.GetMFMEComponentType(mfmeComponentTypeText);
                switch (mfmeComponentType)
                {
                    case MFMEComponentType.Background:
                        ExtractComponentProcessor.ProcessBackground(inputSimulator, componentStandardData);
                        break;
                    //        case MFMEComponentType.Lamp:
                    //            ExtractComponentProcessor.ProcessLamp(inputSimulator, componentStandardData);
                    //            break;
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



    }
}
