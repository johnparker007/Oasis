using MfmeTools.Mfme;
using System.Diagnostics;
using WindowsInput;
using System.Threading;
using static MfmeTools.WindowCapture.Shared.Interop.NativeMethods;
using static MfmeTools.Mfme.MFMEConstants;
using System;

namespace MfmeTools
{
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

        private MFMEComponentType _previousComponentType = MFMEComponentType.None;


        public void StartExtraction(Options options)
        {
            OutputLog.Log("Starting Extraction");
            OutputLog.Log("Extraction source layout: " + options.SourceLayoutPath);

            Program.LayoutCopier.CopyToMfmeTools(options.SourceLayoutPath);
            OutputLog.Log("Copied source layout to MFME Tools");

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






            _previousComponentType = MFMEComponentType.None;
            int zOrder = 0;
            do
            {

                // OASIS TODO - port to Delphi font scraper:
                //    string mfmeComponentTypeTextOCR = MFMEAutomation.GetText(
                //        tesseractDriver, EmulatorScraper,
                //        MFMEScraperConstants.kComponentTypeTabX, MFMEScraperConstants.kComponentTypeTabY,
                //        MFMEScraperConstants.kComponentTypeTabWidth, MFMEScraperConstants.kComponentTypeTabHeight);

                //    Converter.MFMEComponentType mfmeComponentType = MFMEAutomation.GetMFMEComponentType(EmulatorScraper, mfmeComponentTypeTextOCR);

                string componentXText = "0";
                string componentYText = "0";
                string componentWidthText = "0";
                string componentHeightText = "0";
                string angleText = "0";
                string textBoxText = "";

Console.WriteLine("Get X");
                componentXText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentPositionX_X, MFMEScraperConstants.kComponentPositionX_Y);

Console.WriteLine("Get Y");
                componentYText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentPositionY_X, MFMEScraperConstants.kComponentPositionY_Y);

Console.WriteLine("Get Width");
                componentWidthText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentPositionWidth_X, MFMEScraperConstants.kComponentPositionWidth_Y);

                // OASIS TODO: reimplement:
                //if (_previousComponentType == Converter.MFMEComponentType.AlphaNew)
                //{
                //    yield return MFMEAutomation.DoWorkaroundFixForMFMEComponentHeightBugAfterAlphaNewComponent(
                //        inputSimulator, this, EmulatorScraper);
                //}

Console.WriteLine("Get Height");
                componentHeightText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentPositionHeight_X, MFMEScraperConstants.kComponentPositionHeight_Y);

Console.WriteLine("Get Angle");
                angleText = DelphiFontScraper.GetFieldCharacters(
                    MFMEScraperConstants.kComponentAngle_X, MFMEScraperConstants.kComponentAngle_Y);


                Console.WriteLine($"x: {componentXText}, y: {componentYText}, width: {componentWidthText}, height: {componentHeightText}, angle: {angleText}");


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

                //    ComponentStandardData componentStandardData = new ComponentStandardData(
                //        componentXText, componentYText, componentWidthText, componentHeightText, angleText, textBoxText, zOrder);

                //    switch (mfmeComponentType)
                //    {
                //        case Converter.MFMEComponentType.Background:
                //            yield return ProcessBackground(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Lamp:
                //            yield return ProcessLamp(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Reel:
                //            yield return ProcessReel(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.DotAlpha:
                //            yield return ProcessDotAlpha(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.MatrixAlpha:
                //            yield return ProcessMatrixAlpha(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.SevenSegment:
                //            yield return ProcessSevenSegment(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.AlphaNew:
                //            yield return ProcessAlphaNew(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Alpha:
                //            yield return ProcessAlpha(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Checkbox:
                //            yield return ProcessCheckbox(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.RgbLed:
                //            yield return ProcessRgbLed(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Led:
                //            yield return ProcessLed(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Frame:
                //            yield return ProcessFrame(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Label:
                //            yield return ProcessLabel(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Button:
                //            yield return ProcessButton(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.BandReel:
                //            yield return ProcessBandReel(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.DiscReel:
                //            yield return ProcessDiscReel(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.FlipReel:
                //            yield return ProcessFlipReel(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.JpmBonusReel:
                //            yield return ProcessJpmBonusReel(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.BfmAlpha:
                //            yield return ProcessBfmAlpha(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.ProconnMatrix:
                //            yield return ProcessProconnMatrix(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.EpochAlpha:
                //            yield return ProcessEpochAlpha(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.IgtVfd:
                //            yield return ProcessIgtVfd(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Plasma:
                //            yield return ProcessPlasma(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.DotMatrix:
                //            yield return ProcessDotMatrix(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.BfmLed:
                //            yield return ProcessBfmLed(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.BfmColourLed:
                //            yield return ProcessBfmColourLed(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.AceMatrix:
                //            yield return ProcessAceMatrix(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.EpochMatrix:
                //            yield return ProcessEpochMatrix(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.BarcrestBwbVideo:
                //            yield return ProcessBarcrestBwbVideo(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.BfmVideo:
                //            yield return ProcessBfmVideo(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.AceVideo:
                //            yield return ProcessAceVideo(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.MaygayVideo:
                //            yield return ProcessMaygayVideo(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.PrismLamp:
                //            yield return ProcessPrismLamp(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Bitmap:
                //            yield return ProcessBitmap(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.SevenSegmentBlock:
                //            yield return ProcessSevenSegmentBlock(inputSimulator, componentStandardData);
                //            break;
                //        case Converter.MFMEComponentType.Border:
                //            yield return ProcessBorder(inputSimulator, componentStandardData);
                //            break;

                //        default:
                //            UnityEngine.Debug.LogError("MFME Extractor: skipping MFMEComponentType " + mfmeComponentType
                //                + " (Z Order: " + zOrder + ")");
                //            break;
                //    }

                //    yield return WriteSnapshotTextRender(Extractor.Layout.Components.Last());

                //    yield return new WaitForSeconds(0.1f); // missed scraping a text field on Adders (Hold 1) since adding this code to write the SnapshotTextRender

                //    _previousComponentType = mfmeComponentType;
                //    ++zOrder;

                //    yield return MFMEAutomation.ClickPropertiesComponentNext(inputSimulator, this, EmulatorScraper);
            }
            while (!MFMEAutomation.PreviousComponentNavigationTimedOut);

            //Extractor.SaveLayout(OutputDirectoryPath);

            //if (zOrder == Extractor.Layout.Components.Count && Extractor.Layout.Components.Count > 0)
            //{
            //    string extractCompletedMarkerFilePath = Path.Combine(
            //        Converter.GetOutputDirectoryPath(MachineConfiguration), kExtractCompletedFilename);

            //    File.WriteAllBytes(extractCompletedMarkerFilePath, new byte[0]);
            //}

            //MFMEAutomation.KillMFMEProcessIfNotExited(_mfmeProcess);







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
