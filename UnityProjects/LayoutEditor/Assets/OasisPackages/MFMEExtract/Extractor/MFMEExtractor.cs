using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
//using WindowsInput;
using MFMEExtract;
using System;
using System.Linq;

public class MFMEExtractor : MonoBehaviour
{
    public struct ComponentStandardData
    {
        public Vector2Int Position;
        public Vector2Int Size;
        public string AngleAsText;
        public string TextBoxText;
        public int ZOrder;

        public ComponentStandardData(string x, string y, string width, string height, string angle, string textBoxText, int zOrder)
        {
            Position = new Vector2Int(int.Parse(x), int.Parse(y));
            Size = new Vector2Int(int.Parse(width), int.Parse(height));
            AngleAsText = angle;
            TextBoxText = textBoxText;
            ZOrder = zOrder;
        }
    }

    public const string kExtractCompletedFilename = "ExtractCompleted";
    public const string kExtractFailedFilename = "ExtractFailed";

//    public MachineConfigurationData[] MachineConfigurations;

//    // Options:
//    [Header("Cached Images")]
//    public bool DontUseExistingBackgrounds;
//    public bool DontUseExistingReels;
//    public bool DontUseExistingLamps;

//    [Header("Text Layouts")]
//    public bool SnapshotTextRenderSavePngPreview;
//    public bool SnapshotTextRenderSaveConverterImage;

//    public Converter Converter;
//    public MFMEDataLayoutBuilder MFMEDataLayoutBuilder;

//    public EmulatorScraper EmulatorScraper;

//    private Process _mfmeProcess = null;
//    private Converter.MFMEComponentType _previousComponentType = Converter.MFMEComponentType.Alpha;

//    public MachineConfigurationData MachineConfiguration
//    {
//        get;
//        private set;
//    }

//    public string OutputDirectoryName
//    {
//        get
//        {
//            return GetOutputDirectoryName(MachineConfiguration);
//        }
//    }

//    public string OutputDirectoryPath
//    {
//        get
//        {
//            return GetOutputDirectoryPath(MachineConfiguration);
//        }
//    }


//    private void Start()
//    {
//        MFMEScraper.Initialise();

//        Coroutine extractorBatchCoroutine = StartCoroutine(ExtractorBatchCoroutine());
//    }

//    public static string GetOutputDirectoryName(MachineConfigurationData machineConfigurationData)
//    {
//        return machineConfigurationData.Name + "_Output";
//    }

//    public static string GetOutputDirectoryPath(MachineConfigurationData machineConfigurationData)
//    {
//        string outputDirectoryPath = Path.Combine(Converter.kMFMEConverterOutputPath, GetOutputDirectoryName(machineConfigurationData));
//        outputDirectoryPath = outputDirectoryPath.Replace("/", "\\");
//        return outputDirectoryPath;
//    }

//    public static string GetConverterImageFilename(ExtractComponentBase extractComponentBase, bool png = false)
//    {
//        string extension = png ? ".png" : ".converterImage";
//        return MFMEExtractorConstants.kTextSnapshotName + extractComponentBase.ZOrder + extension;
//    }

//    private IEnumerator ExtractorBatchCoroutine()
//    {
//        InputSimulator inputSimulator = new InputSimulator();

//        foreach (MachineConfigurationData machineConfigurationData in MachineConfigurations)
//        {
//            MachineConfiguration = machineConfigurationData;

//            yield return StartCoroutine(ExtractorCoroutine(inputSimulator));
//        }

//        yield return StartCoroutine(MFMEAutomation.RestoreUnityEditorWindow(inputSimulator));
//    }

//    private IEnumerator ExtractorCoroutine(InputSimulator inputSimulator)
//    {
//        UnityEngine.Debug.LogError("Starting ExtractorCoroutine for " + MachineConfiguration.Name);

//        Extractor.NewLayout(MachineConfiguration.Name);

//        //        yield return StartCoroutine(MFMEAutomation.MinimiseUnity(inputSimulator));
//        yield return StartCoroutine(MFMEAutomation.CenterMouseOnScreen(inputSimulator));

//        yield return StartCoroutine(CreateOutputDirectoryIfNotFound());


//        EmulatorScraper.UwcWindowTexture.partialWindowTitle = MachineConfiguration.FullWindowTitle;
//        UnityEngine.Debug.LogError("EmulatorScraper.UwcWindowTexture.partialWindowTitle = " + EmulatorScraper.UwcWindowTexture.partialWindowTitle);
//        if (EmulatorScraper.UwcWindowTexture.partialWindowTitle == "")
//        {
//            EmulatorScraper.UwcWindowTexture.partialWindowTitle = "MFME 20.1";
//            UnityEngine.Debug.LogError("Changed to mfme");
//        }
//        //EmulatorScraper.ScrapingEnabled = false;

//        EmulatorScraper.UwcWindowTexture.createChildWindows = true;
//        EmulatorScraper.ScrapingEnabled = true;

//#if UNITY_EDITOR 
//        string mfmeGameFilePath = MachineConfiguration.MFMEGameFileAbsolutePath;
//        string mfmeGameFilename = Path.GetFileName(mfmeGameFilePath);
//        DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(mfmeGameFilePath));
//        string mfmeGameDirectoryName = directoryInfo.Name;

//        string commandLineArguments;
//        if (MachineConfiguration.ClassicForMAME)
//        {
//            commandLineArguments = Path.Combine("Layouts", "_CLASSICS_For_MAME", mfmeGameDirectoryName, mfmeGameFilename);
//        }
//        else
//        {
//            commandLineArguments = Path.Combine("Layouts", mfmeGameDirectoryName, mfmeGameFilename);
//        }

//        _mfmeProcess = EmulatorController.StartEmulator(MFMEExeHelper.EditorMFMERootPath, MFMEExeHelper.MFMEExeFilename, commandLineArguments);
//#endif

//        TesseractDriver tesseractDriver = new TesseractDriver();
//        // don't need to pass callback function as we always wait when first loading layout which easily gives enough time
//        tesseractDriver.Setup(null);

//        yield return new WaitForSeconds(Converter.kDelayBeforeClearingStartupPopups);

//        yield return StartCoroutine(MFMEAutomation.ClearStartupPopups(inputSimulator));

//        yield return StartCoroutine(MFMEAutomation.StopEmulationFromDebugMenu(inputSimulator));

//        yield return StartCoroutine(MFMEAutomation.EnableEditModeFromDesignMenu(inputSimulator));

//        yield return ExtractConfiguration(inputSimulator);

//        // hopefully won't need to do this now, as it's flakey if there are 'Frame' objects underneath etc:
//        //yield return StartCoroutine(MFMEAutomation.DragDownFromTopLeftCornerToMoveUnwantedCheckboxEtc(inputSimulator, EmulatorScraper));

//        yield return StartCoroutine(MFMEAutomation.CopyOffLampsToBackground(inputSimulator));

//        yield return StartCoroutine(MFMEAutomation.OpenPropertiesWindow(inputSimulator, this, EmulatorScraper, true));

//        yield return new WaitForSeconds(2.0f);

//        EmulatorScraper.UwcWindowTexture.createChildWindows = false; // IMPORTANT - this fixes crashing!

//        // sometimes fails getting very first component type (OCR text scraper gets blank due to this error:
//        //GetWindowPixels(190, 4, 31, 90, 19) failed.
//        //UnityEngine.Debug:LogErrorFormat(String, Object[])
//        //uWindowCapture.Lib:GetWindowPixels(Int32, Color32[], Int32, Int32, Int32, Int32)(at Assets / uWindowCapture / Assets / uWindowCapture / Scripts / UwcLib.cs:288)
//        // ... so having a delay here may help:
//        yield return new WaitForSeconds(0.5f);

//        yield return MFMEAutomation.ClickPropertiesComponentPreviousUntilOnFirstComponent(inputSimulator, this, EmulatorScraper);

//        _previousComponentType = Converter.MFMEComponentType.None;
//        int zOrder = 0;
//        do
//        {
//            string mfmeComponentTypeTextOCR = MFMEAutomation.GetText(
//                tesseractDriver, EmulatorScraper,
//                MFMEScraperConstants.kComponentTypeTabX, MFMEScraperConstants.kComponentTypeTabY,
//                MFMEScraperConstants.kComponentTypeTabWidth, MFMEScraperConstants.kComponentTypeTabHeight);

//            Converter.MFMEComponentType mfmeComponentType = MFMEAutomation.GetMFMEComponentType(EmulatorScraper, mfmeComponentTypeTextOCR);

//            string componentXText = "0";
//            string componentYText = "0";
//            string componentWidthText = "0";
//            string componentHeightText = "0";
//            string angleText = "0";
//            string textBoxText = "";

//            componentXText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//                MFMEScraperConstants.kComponentPositionX_X, MFMEScraperConstants.kComponentPositionX_Y);

//            componentYText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//                MFMEScraperConstants.kComponentPositionY_X, MFMEScraperConstants.kComponentPositionY_Y);

//            componentWidthText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//                MFMEScraperConstants.kComponentPositionWidth_X, MFMEScraperConstants.kComponentPositionWidth_Y);

//            if (_previousComponentType == Converter.MFMEComponentType.AlphaNew)
//            {
//                yield return MFMEAutomation.DoWorkaroundFixForMFMEComponentHeightBugAfterAlphaNewComponent(
//                    inputSimulator, this, EmulatorScraper);
//            }

//            componentHeightText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//                MFMEScraperConstants.kComponentPositionHeight_X, MFMEScraperConstants.kComponentPositionHeight_Y);

//            angleText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//                MFMEScraperConstants.kComponentAngle_X, MFMEScraperConstants.kComponentAngle_Y);

//            // workaround, as once text box has been clicked in, the flashing cursor in top-left is picked up as non-blank,
//            // even if text box is empty
//            const int kIgnoreLeftmostPixelColumnCount = 6;
//            if (!MFMEScraper.IsImageBoxBlank(EmulatorScraper,
//                MFMEScraperConstants.kComponentTextBox_X + kIgnoreLeftmostPixelColumnCount, MFMEScraperConstants.kComponentTextBox_Y,
//                MFMEScraperConstants.kComponentTextBox_Width - kIgnoreLeftmostPixelColumnCount, MFMEScraperConstants.kComponentTextBox_Height,
//                false, false))
//            {
//                // TOIMPROVE this offset to get the mouse cursor to select inside the text box should be done better:
//                const int kOffsetToClickInsideTextBox = 6;
//                yield return MFMEAutomation.GetTextCoroutine(inputSimulator, this, EmulatorScraper,
//                    MFMEScraperConstants.kComponentTextBox_X + kOffsetToClickInsideTextBox,
//                    MFMEScraperConstants.kComponentTextBox_Y + kOffsetToClickInsideTextBox);
//                textBoxText = GUIUtility.systemCopyBuffer;
//            }

//            ComponentStandardData componentStandardData = new ComponentStandardData(
//                componentXText, componentYText, componentWidthText, componentHeightText, angleText, textBoxText, zOrder);

//            switch (mfmeComponentType)
//            {
//                case Converter.MFMEComponentType.Background:
//                    yield return ProcessBackground(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Lamp:
//                    yield return ProcessLamp(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Reel:
//                    yield return ProcessReel(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.DotAlpha:
//                    yield return ProcessDotAlpha(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.MatrixAlpha:
//                    yield return ProcessMatrixAlpha(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.SevenSegment:
//                    yield return ProcessSevenSegment(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.AlphaNew:
//                    yield return ProcessAlphaNew(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Alpha:
//                    yield return ProcessAlpha(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Checkbox:
//                    yield return ProcessCheckbox(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.RgbLed:
//                    yield return ProcessRgbLed(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Led:
//                    yield return ProcessLed(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Frame:
//                    yield return ProcessFrame(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Label:
//                    yield return ProcessLabel(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Button:
//                    yield return ProcessButton(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.BandReel:
//                    yield return ProcessBandReel(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.DiscReel:
//                    yield return ProcessDiscReel(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.FlipReel:
//                    yield return ProcessFlipReel(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.JpmBonusReel:
//                    yield return ProcessJpmBonusReel(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.BfmAlpha:
//                    yield return ProcessBfmAlpha(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.ProconnMatrix:
//                    yield return ProcessProconnMatrix(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.EpochAlpha:
//                    yield return ProcessEpochAlpha(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.IgtVfd:
//                    yield return ProcessIgtVfd(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Plasma:
//                    yield return ProcessPlasma(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.DotMatrix:
//                    yield return ProcessDotMatrix(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.BfmLed:
//                    yield return ProcessBfmLed(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.BfmColourLed:
//                    yield return ProcessBfmColourLed(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.AceMatrix:
//                    yield return ProcessAceMatrix(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.EpochMatrix:
//                    yield return ProcessEpochMatrix(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.BarcrestBwbVideo:
//                    yield return ProcessBarcrestBwbVideo(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.BfmVideo:
//                    yield return ProcessBfmVideo(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.AceVideo:
//                    yield return ProcessAceVideo(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.MaygayVideo:
//                    yield return ProcessMaygayVideo(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.PrismLamp:
//                    yield return ProcessPrismLamp(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Bitmap:
//                    yield return ProcessBitmap(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.SevenSegmentBlock:
//                    yield return ProcessSevenSegmentBlock(inputSimulator, componentStandardData);
//                    break;
//                case Converter.MFMEComponentType.Border:
//                    yield return ProcessBorder(inputSimulator, componentStandardData);
//                    break;

//                default:
//                    UnityEngine.Debug.LogError("MFME Extractor: skipping MFMEComponentType " + mfmeComponentType
//                        + " (Z Order: " + zOrder + ")");
//                    break;
//            }

//            yield return WriteSnapshotTextRender(Extractor.Layout.Components.Last());

//            yield return new WaitForSeconds(0.1f); // missed scraping a text field on Adders (Hold 1) since adding this code to write the SnapshotTextRender

//            _previousComponentType = mfmeComponentType;
//            ++zOrder;

//            yield return MFMEAutomation.ClickPropertiesComponentNext(inputSimulator, this, EmulatorScraper);
//        }
//        while (!MFMEAutomation.PreviousComponentNavigationTimedOut);

//        Extractor.SaveLayout(OutputDirectoryPath);

//        if (zOrder == Extractor.Layout.Components.Count && Extractor.Layout.Components.Count > 0)
//        {
//            string extractCompletedMarkerFilePath = Path.Combine(
//                Converter.GetOutputDirectoryPath(MachineConfiguration), kExtractCompletedFilename);

//            File.WriteAllBytes(extractCompletedMarkerFilePath, new byte[0]);
//        }

//        MFMEAutomation.KillMFMEProcessIfNotExited(_mfmeProcess);
//    }

//    private IEnumerator CreateOutputDirectoryIfNotFound()
//    {
//        if (!Directory.Exists(OutputDirectoryPath))
//        {
//            Directory.CreateDirectory(OutputDirectoryPath);
//            yield return new WaitForSeconds(MFMEAutomation.kLongDelay);
//        }
//    }

//    private IEnumerator WriteSnapshotTextRender(ExtractComponentBase extractComponentBase)
//    {
//        if (!SnapshotTextRenderSavePngPreview && !SnapshotTextRenderSaveConverterImage)
//        {
//            yield break;
//        }

//        int x = Mathf.RoundToInt(MFMEScraperConstants.kPropertiesOverlayImage_CenterX - (extractComponentBase.Size.X / 2.0f));
//        int y = Mathf.RoundToInt(MFMEScraperConstants.kPropertiesOverlayImage_CenterY - (extractComponentBase.Size.Y / 2.0f));

//        Vector2Int Position = new Vector2Int(
//            Mathf.Max(x, MFMEScraperConstants.kPropertiesOverlayImage_TopLeftX),
//            Mathf.Max(y, MFMEScraperConstants.kPropertiesOverlayImage_TopLeftY));

//        // TODO need to get this sorted properly globally at some point - the screen capture grabs some pixels to the left and one above the topleft of
//        // the properties window
//        Position.x += 8;
//        Position.y += 1;

//        Vector2Int ScrapeSize = new Vector2Int(
//            Mathf.Min(extractComponentBase.Size.X, MFMEScraperConstants.kPropertiesOverlayImage_Width),
//            Mathf.Min(extractComponentBase.Size.Y, MFMEScraperConstants.kPropertiesOverlayImage_Height));

//        Color32[] scrapedPixelBlockColor32 =
//            EmulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixels(Position.x, Position.y, ScrapeSize.x, ScrapeSize.y);

//        ConverterImage converterImage = new ConverterImage(scrapedPixelBlockColor32, ScrapeSize.x, ScrapeSize.y);

//        string outputPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName);
//        outputPath = outputPath.Replace("/", "\\");
//        if (SnapshotTextRenderSavePngPreview)
//        {
//            string pngFilename = GetConverterImageFilename(extractComponentBase, true);
//            File.WriteAllBytes(Path.Combine(outputPath, pngFilename), converterImage.GetAsPNGBytes());
//        }

//        if (SnapshotTextRenderSaveConverterImage)
//        {
//            string converterImageFilename = GetConverterImageFilename(extractComponentBase);
//            converterImage.SaveToJSONFormat(Path.Combine(outputPath, converterImageFilename));
//        }
//    }

//    private IEnumerator ProcessBackground(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        // TODO - acquire window width / height from right click menu, set as ComponentBackground width/height

//        bool backgroundBitmapBlank = MFMEScraper.IsImageBoxBlank(EmulatorScraper,
//                        MFMEScraperConstants.kPropertiesBackgroundImage_X, MFMEScraperConstants.kPropertiesBackgroundImage_Y,
//                        MFMEScraperConstants.kPropertiesBackgroundImage_Width, MFMEScraperConstants.kPropertiesBackgroundImage_Height,
//                        true, false);


//        string saveFullPath = "";

//        if (!backgroundBitmapBlank)
//        {
//            saveFullPath = Path.Combine(OutputDirectoryPath, "background.bmp");
//            saveFullPath = saveFullPath.Replace("/", "\\");

//            // save bmp image from MFME
//            if ((DontUseExistingBackgrounds || !File.Exists(saveFullPath)) && !MachineConfiguration.ClassicForMAME)
//            {
//                FileHelper.DeleteFileAndMetafileIfFound(saveFullPath);

//                yield return MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper,
//                    MFMEScraperConstants.kPropertiesBackgroundImage_CenterX, MFMEScraperConstants.kPropertiesBackgroundImage_CenterY);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                // wait for file requester to intialise
//                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);

//                GUIUtility.systemCopyBuffer = saveFullPath;

//                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
//                yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
//            }
//        }

//        ExtractComponentBackground extractBackground = new ExtractComponentBackground(componentStandardData);
//        extractBackground.BmpImageFilename = Path.GetFileName(saveFullPath);

//        Extractor.Layout.Components.Add(extractBackground);

//        //ConverterImage converterImage = new ConverterImage(saveFullPath, null, false);
//        //Extractor.Layout.BackgroundImageSize.X = converterImage.Width;
//        //Extractor.Layout.BackgroundImageSize.Y = converterImage.Height;

//        Extractor.Layout.BackgroundImageSize.X = componentStandardData.Size.x;
//        Extractor.Layout.BackgroundImageSize.Y = componentStandardData.Size.y;
//    }

//    private IEnumerator ProcessReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentReel extractReel = new ExtractComponentReel(componentStandardData);

//        extractReel.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelNumber_X, MFMEScraperConstants.kPropertiesReelNumber_Y));

//        extractReel.Stops = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelStops_X, MFMEScraperConstants.kPropertiesReelStops_Y));

//        extractReel.HalfSteps = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelHalfSteps_X, MFMEScraperConstants.kPropertiesReelHalfSteps_Y));

//        extractReel.Resolution = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelResolution_X, MFMEScraperConstants.kPropertiesReelResolution_Y));

//        extractReel.BandOffset = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelBandOffset_X, MFMEScraperConstants.kPropertiesReelBandOffset_Y));

//        extractReel.OptoTab = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelOptoTab_X, MFMEScraperConstants.kPropertiesReelOptoTab_Y));

//        extractReel.Height = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelHeight_X, MFMEScraperConstants.kPropertiesReelHeight_Y));

//        extractReel.WidthDiff = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelWidthDiff_X, MFMEScraperConstants.kPropertiesReelWidthDiff_Y));

//        extractReel.Horizontal = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesReelHorizontalCheckbox_X, MFMEScraperConstants.kPropertiesReelHorizontalCheckbox_Y);

//        extractReel.Reversed = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesReelReversedCheckbox_X, MFMEScraperConstants.kPropertiesReelReversedCheckbox_Y);

//        extractReel.Lamps = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesReelLampsCheckbox_X, MFMEScraperConstants.kPropertiesReelLampsCheckbox_Y);

//        extractReel.LampsLEDs = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesReelLampsLEDsCheckbox_X, MFMEScraperConstants.kPropertiesReelLampsLEDsCheckbox_Y);

//        extractReel.Mirrored = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesReelMirroredCheckbox_X, MFMEScraperConstants.kPropertiesReelMirroredCheckbox_Y);

//        for (int reelLampIndex = 0; reelLampIndex < ComponentReel.kReelLampCount; ++reelLampIndex)
//        {
//            Vector2Int propertiesReelLampNumberPosition = MFMEScraperConstants.GetPropertiesReelLampNumber_XY(reelLampIndex);

//            extractReel.LampNumbersAsStrings[reelLampIndex] = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//                propertiesReelLampNumberPosition.x, propertiesReelLampNumberPosition.y);
//        }

//        extractReel.WinLinesCount = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelWinLinesCount_X, MFMEScraperConstants.kPropertiesReelWinLinesCount_Y));

//        extractReel.WinLinesOffset = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesReelWinLinesOffset_X, MFMEScraperConstants.kPropertiesReelWinLinesOffset_Y));

//        string reelImagefilename = "reel_"
//            + extractReel.Number
//            + ".bmp";

//        string reelImageSaveFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, reelImagefilename);
//        reelImageSaveFullPath = reelImageSaveFullPath.Replace("/", "\\");

//        string reelOverlayImagefilename = "reeloverlay_"
//            + extractReel.Number
//            + ".bmp";

//        string reelOverlaySaveFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, reelOverlayImagefilename);
//        reelOverlaySaveFullPath = reelOverlaySaveFullPath.Replace("/", "\\");

//        // save bmp image from MFME
//        if (DontUseExistingReels || !File.Exists(reelImageSaveFullPath))
//        {
//            FileHelper.DeleteFileAndMetafileIfFound(reelImageSaveFullPath);

//            // save reel band image
//            yield return MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper,
//                MFMEScraperConstants.kPropertiesReelImage_CenterX, MFMEScraperConstants.kPropertiesReelImage_CenterY);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay); // wait for file requester to intialise

//            GUIUtility.systemCopyBuffer = reelImageSaveFullPath;

//            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
//            yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//            yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
//        }

//        // save bmp image from MFME
//        if ((DontUseExistingReels || !File.Exists(reelOverlaySaveFullPath)) && !MachineConfiguration.ClassicForMAME)
//        {
//            FileHelper.DeleteFileAndMetafileIfFound(reelOverlaySaveFullPath);

//            // save reel overlay image if present
//            yield return MFMEAutomation.LeftClickAtPosition(inputSimulator, this, EmulatorScraper,
//                MFMEScraperConstants.kPropertiesOverlayTab_CenterX, MFMEScraperConstants.kPropertiesOverlayTab_CenterY);

//            // is it just blank checkerboard?
//            if (!MFMEScraper.IsImageBoxBlank(
//                EmulatorScraper, MFMEScraperConstants.kPropertiesOverlayImage_TopLeftX, MFMEScraperConstants.kPropertiesOverlayImage_TopLeftY,
//                MFMEScraperConstants.kPropertiesOverlayImage_Width, MFMEScraperConstants.kPropertiesOverlayImage_Height, true))
//            {
//                yield return MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper,
//                    MFMEScraperConstants.kPropertiesOverlayImage_CenterX, MFMEScraperConstants.kPropertiesOverlayImage_CenterY);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay); // wait for file requester to intialise

//                GUIUtility.systemCopyBuffer = reelOverlaySaveFullPath;

//                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
//                yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
//            }
//        }

//        extractReel.BandBmpImageFilename = Path.GetFileName(reelImageSaveFullPath);

//        extractReel.HasOverlay = File.Exists(reelOverlaySaveFullPath);
//        if (extractReel.HasOverlay)
//        {
//            extractReel.OverlayBmpImageFilename = Path.GetFileName(reelOverlaySaveFullPath);
//        }

//        Extractor.Layout.Components.Add(extractReel);
//    }

//    private IEnumerator ProcessBandReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentBandReel extractBandReel = new ExtractComponentBandReel(componentStandardData);

//        extractBandReel.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBandReelNumber_X, MFMEScraperConstants.kPropertiesBandReelNumber_Y));

//        extractBandReel.Stops = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBandReelStops_X, MFMEScraperConstants.kPropertiesBandReelStops_Y));

//        extractBandReel.HalfSteps = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBandReelHalfSteps_X, MFMEScraperConstants.kPropertiesBandReelHalfSteps_Y));

//        extractBandReel.View = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBandReelView_X, MFMEScraperConstants.kPropertiesBandReelView_Y));

//        extractBandReel.Offset = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBandReelOffset_X, MFMEScraperConstants.kPropertiesBandReelOffset_Y));

//        extractBandReel.Spacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBandReelSpacing_X, MFMEScraperConstants.kPropertiesBandReelSpacing_Y));

//        extractBandReel.OptoTab = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBandReelOptoTab_X, MFMEScraperConstants.kPropertiesBandReelOptoTab_Y));

//        extractBandReel.Reversed = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelReversedCheckbox_X, MFMEScraperConstants.kPropertiesBandReelReversedCheckbox_Y);

//        extractBandReel.Inverted = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelInvertedCheckbox_X, MFMEScraperConstants.kPropertiesBandReelInvertedCheckbox_Y);

//        extractBandReel.Vertical = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelVerticalCheckbox_X, MFMEScraperConstants.kPropertiesBandReelVerticalCheckbox_Y);

//        extractBandReel.Opaque = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelOpaqueCheckbox_X, MFMEScraperConstants.kPropertiesBandReelOpaqueCheckbox_Y);

//        extractBandReel.Lamps = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelLampsCheckbox_X, MFMEScraperConstants.kPropertiesBandReelLampsCheckbox_Y);

//        extractBandReel.Custom = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelCustomCheckbox_X, MFMEScraperConstants.kPropertiesBandReelCustomCheckbox_Y);

//        // TODO
//        //public string[] LampNumbersAsStrings = new string[ComponentBandReel.kReelLampCount];

//        //public bool HasOverlay;

//        //public string BandBmpImageFilename;
//        //public string OverlayBmpImageFilename;

//        yield return null;

//        Extractor.Layout.Components.Add(extractBandReel);
//    }

//    private IEnumerator ProcessDiscReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentDiscReel extractDiscReel = new ExtractComponentDiscReel(componentStandardData);

//        extractDiscReel.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelNumber_X, MFMEScraperConstants.kPropertiesDiscReelNumber_Y));

//        extractDiscReel.Stops = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelStops_X, MFMEScraperConstants.kPropertiesDiscReelStops_Y));

//        extractDiscReel.HalfSteps = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelHalfSteps_X, MFMEScraperConstants.kPropertiesDiscReelHalfSteps_Y));

//        extractDiscReel.Resolution = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelResolution_X, MFMEScraperConstants.kPropertiesDiscReelResolution_Y));

//        extractDiscReel.Offset = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelOffset_X, MFMEScraperConstants.kPropertiesDiscReelOffset_Y));

//        extractDiscReel.OptoTab = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelOptoTab_X, MFMEScraperConstants.kPropertiesDiscReelOptoTab_Y));

//        extractDiscReel.Bounce = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelBounce_X, MFMEScraperConstants.kPropertiesDiscReelBounce_Y));

//        extractDiscReel.Lamps = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelLampsCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelLampsCheckbox_Y);

//        extractDiscReel.Reversed = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelReversedCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelReversedCheckbox_Y);

//        extractDiscReel.Inverted = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelInvertedCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelInvertedCheckbox_Y);

//        extractDiscReel.Transparent = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelTransparentCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelTransparentCheckbox_Y);

//        extractDiscReel.OuterH = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelOuterH_X, MFMEScraperConstants.kPropertiesDiscReelOuterH_Y));

//        extractDiscReel.OuterL = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelOuterL_X, MFMEScraperConstants.kPropertiesDiscReelOuterL_Y));

//        extractDiscReel.OuterLampSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelOuterLampSize_X, MFMEScraperConstants.kPropertiesDiscReelOuterLampSize_Y));

//        extractDiscReel.InnerH = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelInnerH_X, MFMEScraperConstants.kPropertiesDiscReelInnerH_Y));

//        extractDiscReel.InnerL = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelInnerL_X, MFMEScraperConstants.kPropertiesDiscReelInnerL_Y));

//        extractDiscReel.InnerLampSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelInnerLampSize_X, MFMEScraperConstants.kPropertiesDiscReelInnerLampSize_Y));

//        extractDiscReel.LampPositionsLamps = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelLampPositionsLamps_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsLamps_Y));

//        extractDiscReel.LampPositionsLamp = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelLampPositionsLamp_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsLamp_Y));

//        extractDiscReel.LampPositionsNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelLampPositionsNumber_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsNumber_Y);

//        extractDiscReel.LampPositionsOffset = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelLampPositionsOffset_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsOffset_Y));

//        extractDiscReel.LampPositionsGap = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDiscReelLampPositionsGapCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsGapCheckbox_Y);

//        // TODO images:
//        //public string DiscBmpImageFilename;
//        //public string DiscOverlayBmpImageFilename;
//        //public string LampPositionsBmpImageFilename;

//        //public string OuterMask1BmpImageFilename;
//        //public string OuterMask2BmpImageFilename;

//        //public string InnerMask1BmpImageFilename;
//        //public string InnerMask2BmpImageFilename;

//        //public bool HasOverlay;
//        //public string OverlayBmpImageFilename;


//        yield return null;

//        Extractor.Layout.Components.Add(extractDiscReel);
//    }

//    private IEnumerator ProcessFlipReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentFlipReel extractFlipReel = new ExtractComponentFlipReel(componentStandardData);

//        extractFlipReel.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelNumber_X, MFMEScraperConstants.kPropertiesFlipReelNumber_Y));

//        extractFlipReel.Stops = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelStops_X, MFMEScraperConstants.kPropertiesFlipReelStops_Y));

//        extractFlipReel.HalfSteps = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelHalfSteps_X, MFMEScraperConstants.kPropertiesFlipReelHalfSteps_Y));

//        extractFlipReel.Offset = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelOffset_X, MFMEScraperConstants.kPropertiesFlipReelOffset_Y));

//        extractFlipReel.Reversed = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelReversedCheckbox_X, MFMEScraperConstants.kPropertiesFlipReelReversedCheckbox_Y);

//        extractFlipReel.Inverted = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelInvertedCheckbox_X, MFMEScraperConstants.kPropertiesFlipReelInvertedCheckbox_Y);

//        Color borderColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelBorderColourbox_X, MFMEScraperConstants.kPropertiesFlipReelBorderColourbox_Y);
//        extractFlipReel.BorderColour = new ColorJSON(borderColor);

//        extractFlipReel.BorderWidth = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelBorderWidth_X, MFMEScraperConstants.kPropertiesFlipReelBorderWidth_Y));

//        extractFlipReel.Lamps = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelLampsCheckbox_X, MFMEScraperConstants.kPropertiesFlipReelLampsCheckbox_Y);

//        extractFlipReel.Lamp1AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelLamp1_X, MFMEScraperConstants.kPropertiesFlipReelLamp1_Y);

//        extractFlipReel.Lamp2AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelLamp2_X, MFMEScraperConstants.kPropertiesFlipReelLamp2_Y);

//        extractFlipReel.Lamp3AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFlipReelLamp3_X, MFMEScraperConstants.kPropertiesFlipReelLamp3_Y);

//        // TODO images:
//        //public string BandBmpImageFilename;

//        //public string LampMask1BmpImageFilename;
//        //public string LampMask2BmpImageFilename;
//        //public string LampMask3BmpImageFilename;

//        //public bool HasOverlay;
//        //public string OverlayBmpImageFilename;


//        yield return null;

//        Extractor.Layout.Components.Add(extractFlipReel);
//    }


//    private IEnumerator ProcessJpmBonusReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentJpmBonusReel extractJPMBonusReel = new ExtractComponentJpmBonusReel(componentStandardData);

//        extractJPMBonusReel.Lamp1AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesJPMBonusReelLamp1_X, MFMEScraperConstants.kPropertiesJPMBonusReelLamp1_Y);

//        extractJPMBonusReel.Lamp2AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesJPMBonusReelLamp2_X, MFMEScraperConstants.kPropertiesJPMBonusReelLamp2_Y);

//        extractJPMBonusReel.Lamp3AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesJPMBonusReelLamp3_X, MFMEScraperConstants.kPropertiesJPMBonusReelLamp3_Y);

//        extractJPMBonusReel.Lamp4AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesJPMBonusReelLamp4_X, MFMEScraperConstants.kPropertiesJPMBonusReelLamp4_Y);

//        extractJPMBonusReel.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesJPMBonusReelNumber_X, MFMEScraperConstants.kPropertiesJPMBonusReelNumber_Y));

//        extractJPMBonusReel.SymbolPos = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesJPMBonusReelSymbolPos_X, MFMEScraperConstants.kPropertiesJPMBonusReelSymbolPos_Y));

//        // TODO images:
//        //public string Lamp1OnImageBmpImageFilename;
//        //public string Lamp2OnImageBmpImageFilename;
//        //public string Lamp3OnImageBmpImageFilename;
//        //public string Lamp4OnImageBmpImageFilename;

//        //public string MaskBmpImageFilename;
//        //public string BackgroundBmpImageFilename;

//        //public bool HasOverlay;
//        //public string OverlayBmpImageFilename;


//        yield return null;

//        Extractor.Layout.Components.Add(extractJPMBonusReel);
//    }

//    private IEnumerator ProcessBfmAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentBfmAlpha extractBfmAlpha = new ExtractComponentBfmAlpha(componentStandardData);

//        extractBfmAlpha.Reversed = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBFMAlphaReversedCheckbox_X, MFMEScraperConstants.kPropertiesBFMAlphaReversedCheckbox_Y);

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBFMAlphaColourColorbox_X, MFMEScraperConstants.kPropertiesBFMAlphaColourColorbox_Y);
//        extractBfmAlpha.Colour = new ColorJSON(color);

//        extractBfmAlpha.OffLevel = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBFMAlphaOffLevel_X, MFMEScraperConstants.kPropertiesBFMAlphaOffLevel_Y));

//        extractBfmAlpha.DigitWidth = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBFMAlphaDigitWidth_X, MFMEScraperConstants.kPropertiesBFMAlphaDigitWidth_Y));

//        extractBfmAlpha.Columns = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBFMAlphaColumns_X, MFMEScraperConstants.kPropertiesBFMAlphaColumns_Y));

//        yield return null;

//        Extractor.Layout.Components.Add(extractBfmAlpha);
//    }

//    private IEnumerator ProcessProconnMatrix(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentProconnMatrix extractProconnMatrix = new ExtractComponentProconnMatrix(componentStandardData);

//        extractProconnMatrix.DotSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesProconnMatrixDotSize_X, MFMEScraperConstants.kPropertiesProconnMatrixDotSize_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesProconnMatrixOnColourColorbox_X, MFMEScraperConstants.kPropertiesProconnMatrixOnColourColorbox_Y);
//        extractProconnMatrix.OnColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesProconnMatrixOffColourColorbox_X, MFMEScraperConstants.kPropertiesProconnMatrixOffColourColorbox_Y);
//        extractProconnMatrix.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesProconnMatrixBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesProconnMatrixBackgroundColourColorbox_Y);
//        extractProconnMatrix.BackgroundColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractProconnMatrix);
//    }

//    private IEnumerator ProcessEpochAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentEpochAlpha extractEpochAlpha = new ExtractComponentEpochAlpha(componentStandardData);

//        extractEpochAlpha.XSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochAlphaXSize_X, MFMEScraperConstants.kPropertiesEpochAlphaXSize_Y));

//        extractEpochAlpha.YSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochAlphaYSize_X, MFMEScraperConstants.kPropertiesEpochAlphaYSize_Y));

//        extractEpochAlpha.DotSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochAlphaDotSpacing_X, MFMEScraperConstants.kPropertiesEpochAlphaDotSpacing_Y));

//        extractEpochAlpha.DigitSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochAlphaDigitSpacing_X, MFMEScraperConstants.kPropertiesEpochAlphaDigitSpacing_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochAlphaOnColourColorbox_X, MFMEScraperConstants.kPropertiesEpochAlphaOnColourColorbox_Y);
//        extractEpochAlpha.OnColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochAlphaOffColourColorbox_X, MFMEScraperConstants.kPropertiesEpochAlphaOffColourColorbox_Y);
//        extractEpochAlpha.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochAlphaBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesEpochAlphaBackgroundColourColorbox_Y);
//        extractEpochAlpha.BackgroundColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractEpochAlpha);
//    }

//    private IEnumerator ProcessIgtVfd(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentIgtVfd extractIgtVfd = new ExtractComponentIgtVfd(componentStandardData);

//        extractIgtVfd.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesIgtVfdNumber_X, MFMEScraperConstants.kPropertiesIgtVfdNumber_Y));

//        extractIgtVfd.DotSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesIgtVfdDotSize_X, MFMEScraperConstants.kPropertiesIgtVfdDotSize_Y));

//        extractIgtVfd.DotSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesIgtVfdDotSpacing_X, MFMEScraperConstants.kPropertiesIgtVfdDotSpacing_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesIgtVfdOnColourColorbox_X, MFMEScraperConstants.kPropertiesIgtVfdOnColourColorbox_Y);
//        extractIgtVfd.OnColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesIgtVfdOffColourColorbox_X, MFMEScraperConstants.kPropertiesIgtVfdOffColourColorbox_Y);
//        extractIgtVfd.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesIgtVfdBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesIgtVfdBackgroundColourColorbox_Y);
//        extractIgtVfd.BackgroundColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractIgtVfd);
//    }

//    private IEnumerator ProcessPlasma(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentPlasma extractPlasma = new ExtractComponentPlasma(componentStandardData);

//        extractPlasma.DotSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPlasmaDotSize_X, MFMEScraperConstants.kPropertiesPlasmaDotSize_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPlasmaOnColourColorbox_X, MFMEScraperConstants.kPropertiesPlasmaOnColourColorbox_Y);
//        extractPlasma.OnColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPlasmaOffColourColorbox_X, MFMEScraperConstants.kPropertiesPlasmaOffColourColorbox_Y);
//        extractPlasma.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPlasmaBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesPlasmaBackgroundColourColorbox_Y);
//        extractPlasma.BackgroundColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractPlasma);
//    }

//    private IEnumerator ProcessDotMatrix(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentDotMatrix extractDotMatrix = new ExtractComponentDotMatrix(componentStandardData);

//        extractDotMatrix.DotSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotMatrixDotSize_X, MFMEScraperConstants.kPropertiesDotMatrixDotSize_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotMatrixOnColourColorbox_X, MFMEScraperConstants.kPropertiesDotMatrixOnColourColorbox_Y);
//        extractDotMatrix.OnColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotMatrixOffColourColorbox_X, MFMEScraperConstants.kPropertiesDotMatrixOffColourColorbox_Y);
//        extractDotMatrix.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotMatrixBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesDotMatrixBackgroundColourColorbox_Y);
//        extractDotMatrix.BackgroundColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractDotMatrix);
//    }

//    private IEnumerator ProcessBfmLed(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentBfmLed extractBfmLed = new ExtractComponentBfmLed(componentStandardData);

//        extractBfmLed.XSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmLedXSize_X, MFMEScraperConstants.kPropertiesBfmLedXSize_Y));

//        extractBfmLed.YSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmLedYSize_X, MFMEScraperConstants.kPropertiesBfmLedYSize_Y));

//        extractBfmLed.DigitSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmLedDigitSpacing_X, MFMEScraperConstants.kPropertiesBfmLedDigitSpacing_Y));

//        extractBfmLed.LedSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmLedLedSize_X, MFMEScraperConstants.kPropertiesBfmLedLedSize_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmLedOnColourColorbox_X, MFMEScraperConstants.kPropertiesBfmLedOnColourColorbox_Y);
//        extractBfmLed.OnColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmLedOffColourColorbox_X, MFMEScraperConstants.kPropertiesBfmLedOffColourColorbox_Y);
//        extractBfmLed.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmLedBackColourColorbox_X, MFMEScraperConstants.kPropertiesBfmLedBackColourColorbox_Y);
//        extractBfmLed.BackColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractBfmLed);
//    }

//    private IEnumerator ProcessBfmColourLed(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentBfmColourLed extractBfmColourLed = new ExtractComponentBfmColourLed(componentStandardData);

//        extractBfmColourLed.DotSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmColourLedDotSize_X, MFMEScraperConstants.kPropertiesBfmColourLedDotSize_Y));

//        extractBfmColourLed.Spacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmColourLedSpacing_X, MFMEScraperConstants.kPropertiesBfmColourLedSpacing_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmColourLedOffColourColorbox_X, MFMEScraperConstants.kPropertiesBfmColourLedOffColourColorbox_Y);
//        extractBfmColourLed.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmColourLedBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesBfmColourLedBackgroundColourColorbox_Y);
//        extractBfmColourLed.BackgroundColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractBfmColourLed);
//    }

//    private IEnumerator ProcessAceMatrix(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentAceMatrix extractAceMatrix = new ExtractComponentAceMatrix(componentStandardData);

//        extractAceMatrix.DotSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAceMatrixDotSize_X, MFMEScraperConstants.kPropertiesAceMatrixDotSize_Y));

//        extractAceMatrix.Flip180 = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAceMatrixFlip180Checkbox_X, MFMEScraperConstants.kPropertiesAceMatrixFlip180Checkbox_Y);

//        extractAceMatrix.Vertical = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAceMatrixVerticalCheckbox_X, MFMEScraperConstants.kPropertiesAceMatrixVerticalCheckbox_Y);

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAceMatrixOnColourColorbox_X, MFMEScraperConstants.kPropertiesAceMatrixOnColourColorbox_Y);
//        extractAceMatrix.OnColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAceMatrixOffColourColorbox_X, MFMEScraperConstants.kPropertiesAceMatrixOffColourColorbox_Y);
//        extractAceMatrix.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAceMatrixBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesAceMatrixBackgroundColourColorbox_Y);
//        extractAceMatrix.BackgroundColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractAceMatrix);
//    }

//    private IEnumerator ProcessEpochMatrix(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentEpochMatrix extractEpochMatrix = new ExtractComponentEpochMatrix(componentStandardData);

//        extractEpochMatrix.DotSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochMatrixDotSize_X, MFMEScraperConstants.kPropertiesEpochMatrixDotSize_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochMatrixOffColourColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixOffColourColorbox_Y);
//        extractEpochMatrix.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochMatrixOnColourLoColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixOnColourLoColorbox_Y);
//        extractEpochMatrix.OnColourLo = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochMatrixOnColourMedColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixOnColourMedColorbox_Y);
//        extractEpochMatrix.OnColourMed = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochMatrixOnColourHiColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixOnColourHiColorbox_Y);
//        extractEpochMatrix.OnColourHi = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesEpochMatrixBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixBackgroundColourColorbox_Y);
//        extractEpochMatrix.BackgroundColour = new ColorJSON(color);

//        yield return null;

//        Extractor.Layout.Components.Add(extractEpochMatrix);
//    }

//    private IEnumerator ProcessBarcrestBwbVideo(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentBarcrestBwbVideo extractBarcrestBwbVideo = new ExtractComponentBarcrestBwbVideo(componentStandardData);

//        extractBarcrestBwbVideo.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBarcrestBwbVideoNumber_X, MFMEScraperConstants.kPropertiesBarcrestBwbVideoNumber_Y));

//        extractBarcrestBwbVideo.LeftSkew = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBarcrestBwbVideoLeftSkew_X, MFMEScraperConstants.kPropertiesBarcrestBwbVideoLeftSkew_Y));

//        extractBarcrestBwbVideo.RightSkew = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBarcrestBwbVideoRightSkew_X, MFMEScraperConstants.kPropertiesBarcrestBwbVideoRightSkew_Y));

//        yield return null;

//        Extractor.Layout.Components.Add(extractBarcrestBwbVideo);
//    }

//    private IEnumerator ProcessBfmVideo(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentBfmVideo extractBfmVideo = new ExtractComponentBfmVideo(componentStandardData);

//        extractBfmVideo.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmVideoNumber_X, MFMEScraperConstants.kPropertiesBfmVideoNumber_Y));

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmVideo600x800VRadioButton_TopLeftX, MFMEScraperConstants.kPropertiesBfmVideo600x800VRadioButton_TopLeftY))
//        {
//            extractBfmVideo.Resolution = ExtractComponentBfmVideo.ResolutionType._600x800V;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmVideo480x640VRadioButton_TopLeftX, MFMEScraperConstants.kPropertiesBfmVideo480x640VRadioButton_TopLeftY))
//        {
//            extractBfmVideo.Resolution = ExtractComponentBfmVideo.ResolutionType._480x640V;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmVideo800x600HRadioButton_TopLeftX, MFMEScraperConstants.kPropertiesBfmVideo800x600HRadioButton_TopLeftY))
//        {
//            extractBfmVideo.Resolution = ExtractComponentBfmVideo.ResolutionType._800x600H;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBfmVideo640x480HRadioButton_TopLeftX, MFMEScraperConstants.kPropertiesBfmVideo640x480HRadioButton_TopLeftY))
//        {
//            extractBfmVideo.Resolution = ExtractComponentBfmVideo.ResolutionType._640x480H;
//        }
//        else
//        {
//            UnityEngine.Debug.LogError("Couldn't find a set radio button!");
//        }

//        yield return null;

//        Extractor.Layout.Components.Add(extractBfmVideo);
//    }

//    private IEnumerator ProcessAceVideo(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentAceVideo extractAceVideo = new ExtractComponentAceVideo(componentStandardData);

//        // this component has no properties outside of ComponentStandardData

//        yield return null;

//        Extractor.Layout.Components.Add(extractAceVideo);
//    }

//    private IEnumerator ProcessMaygayVideo(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentMaygayVideo extractMaygayVideo = new ExtractComponentMaygayVideo(componentStandardData);

//        extractMaygayVideo.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesMaygayVideoNumber_X, MFMEScraperConstants.kPropertiesMaygayVideoNumber_Y));

//        extractMaygayVideo.Vertical = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesMaygayVideoVerticalCheckbox_X, MFMEScraperConstants.kPropertiesMaygayVideoVerticalCheckbox_Y);

//        string qualityDropdownCharacters = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesMaygayVideoQualityDropdown_X, MFMEScraperConstants.kPropertiesMaygayVideoQualityDropdown_Y);
//        extractMaygayVideo.Quality = qualityDropdownCharacters;

//        yield return null;

//        Extractor.Layout.Components.Add(extractMaygayVideo);
//    }

//    private IEnumerator ProcessPrismLamp(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentPrismLamp extractPrismLamp = new ExtractComponentPrismLamp(componentStandardData);

//        extractPrismLamp.Lamp1NumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPrismLampLamp1Number_X, MFMEScraperConstants.kPropertiesPrismLampLamp1Number_Y);

//        extractPrismLamp.Lamp2NumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPrismLampLamp2Number_X, MFMEScraperConstants.kPropertiesPrismLampLamp2Number_Y);

//        extractPrismLamp.HorzSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPrismLampHorzSpacing_X, MFMEScraperConstants.kPropertiesPrismLampHorzSpacing_Y));

//        extractPrismLamp.VertSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPrismLampVertSpacing_X, MFMEScraperConstants.kPropertiesPrismLampVertSpacing_Y));

//        extractPrismLamp.Tilt = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPrismLampTilt_X, MFMEScraperConstants.kPropertiesPrismLampTilt_Y));

//        extractPrismLamp.Style = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPrismLampStyleCheckbox_X, MFMEScraperConstants.kPropertiesPrismLampStyleCheckbox_Y);

//        extractPrismLamp.Horizontal = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPrismLampHorizontalCheckbox_X, MFMEScraperConstants.kPropertiesPrismLampHorizontalCheckbox_Y);

//        extractPrismLamp.CentreLine = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesPrismLampCentreLineCheckbox_X, MFMEScraperConstants.kPropertiesPrismLampCentreLineCheckbox_Y);

//        // TODO images:
//        //public string Lamp1ImageBmpFilename;
//        //public string Lamp1MaskBmpFilename;
//        //public string Lamp2ImageBmpFilename;
//        //public string Lamp2MaskBmpFilename;
//        //public string OffImageBmpFilename;

//        yield return null;

//        Extractor.Layout.Components.Add(extractPrismLamp);
//    }

//    private IEnumerator ProcessBitmap(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentBitmap extractBitmap = new ExtractComponentBitmap(componentStandardData);

//        extractBitmap.Transparent = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBitmapTransparentCheckbox_X, MFMEScraperConstants.kPropertiesBitmapTransparentCheckbox_Y);

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBitmapStretchFilterNearestRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterNearestRadioButton_Y))
//        {
//            extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Nearest;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBitmapStretchFilterDraftRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterDraftRadioButton_Y))
//        {
//            extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Draft;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBitmapStretchFilterLinearRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterLinearRadioButton_Y))
//        {
//            extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Linear;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBitmapStretchFilterCosineRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterCosineRadioButton_Y))
//        {
//            extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Cosine;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBitmapStretchFilterSplineRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterSplineRadioButton_Y))
//        {
//            extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Spline;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBitmapStretchFilterLanczosRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterLanczosRadioButton_Y))
//        {
//            extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Lanczos;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBitmapStretchFilterMitchellRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterMitchellRadioButton_Y))
//        {
//            extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Mitchell;
//        }

//        // TODO image:
//        //public string ImageBmpFilename;

//        yield return null;

//        Extractor.Layout.Components.Add(extractBitmap);
//    }

//    private IEnumerator ProcessSevenSegmentBlock(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentSevenSegmentBlock extractSevenSegmentBlock = new ExtractComponentSevenSegmentBlock(componentStandardData);

//        extractSevenSegmentBlock.Width = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockWidth_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockWidth_Y));

//        extractSevenSegmentBlock.Height = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockHeight_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockHeight_Y));

//        extractSevenSegmentBlock.Columns = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockColumns_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockColumns_Y));

//        extractSevenSegmentBlock.Rows = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockRows_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockRows_Y));

//        extractSevenSegmentBlock.RowSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockRowSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockRowSpacing_Y));

//        extractSevenSegmentBlock.ColumnSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockColumnSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockColumnSpacing_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockOnColourColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockOnColourColorbox_Y);
//        extractSevenSegmentBlock.OnColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockOffColourColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockOffColourColorbox_Y);
//        extractSevenSegmentBlock.OffColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockBackColourColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockBackColourColorbox_Y);
//        extractSevenSegmentBlock.BackColour = new ColorJSON(color);

//        extractSevenSegmentBlock.TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockTypeDropdown_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockTypeDropdown_Y);

//        extractSevenSegmentBlock.DPRight = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDPRightCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockDPRightCheckbox_Y);

//        extractSevenSegmentBlock.FourteenSegment = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlock14SegmentCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlock14SegmentCheckbox_Y);

//        extractSevenSegmentBlock.Thickness = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockThickness_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockThickness_Y));

//        extractSevenSegmentBlock.Spacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockSpacing_Y));

//        extractSevenSegmentBlock.HorzSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockHorzSizePercent_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockHorzSizePercent_Y));

//        extractSevenSegmentBlock.VertSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockVertSizePercent_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockVertSizePercent_Y));

//        extractSevenSegmentBlock.Offset = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockOffset_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockOffset_Y));

//        extractSevenSegmentBlock.Angle = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockAngle_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockAngle_Y));

//        extractSevenSegmentBlock.Slant = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockSlant_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockSlant_Y));

//        extractSevenSegmentBlock.Chop = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockChop_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockChop_Y));

//        extractSevenSegmentBlock.Centre = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockCenter_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockCenter_Y));

//        extractSevenSegmentBlock.Centre = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockCenter_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockCenter_Y));

//        int digitCount = extractSevenSegmentBlock.Rows * extractSevenSegmentBlock.Columns;
//        int currentDigitIndex = 0;
//        int remainingDigits;
//        do
//        {
//            // this click on the left arrow is necessary to work around MFME bug.  If previous component is a 7seg block with > 4,
//            // then have scrolled to the right by clicking right arrows, on th next properties page for the nest
//            // component, clicking right arrow jumps from 1 -> 3 instead of 1 -> 2:
//            if (currentDigitIndex == 0)
//            {
//                yield return MFMEAutomation.LeftClickAtPosition(inputSimulator, this, EmulatorScraper,
//                    MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitTabLeftArrowCenter_X,
//                    MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitTabLeftArrowCenter_Y,
//                    MFMEAutomation.kLongDelay);
//            }

//            yield return ProcessSevenSegmentBlockReadDigit(inputSimulator, extractSevenSegmentBlock.DigitElements[currentDigitIndex]);
//            ++currentDigitIndex;

//            remainingDigits = digitCount - currentDigitIndex;

//            int xClickPosition = 0;
//            int yClickPosition = 0;
//            if (remainingDigits > 3)
//            {
//                xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitTabRightArrowCenter_X;
//                yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitTabRightArrowCenter_Y;

//                yield return MFMEAutomation.LeftClickAtPosition(inputSimulator, this, EmulatorScraper,
//                    xClickPosition, yClickPosition, MFMEAutomation.kLongDelay);

//                // and now need to click on tab number 1 to make it active
//                xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit1TabCenter_X;
//                yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit1TabCenter_Y;
//            }
//            else if (remainingDigits == 3)
//            {
//                xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit2TabCenter_X;
//                yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit2TabCenter_Y;
//            }
//            else if (remainingDigits == 2)
//            {
//                xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit3TabCenter_X;
//                yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit3TabCenter_Y;
//            }
//            else if (remainingDigits == 1)
//            {
//                xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit4TabCenter_X;
//                yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit4TabCenter_Y;
//            }

//            if (remainingDigits > 0)
//            {
//                yield return MFMEAutomation.LeftClickAtPosition(inputSimulator, this, EmulatorScraper,
//                    xClickPosition, yClickPosition, MFMEAutomation.kLongDelay);
//            }
//        }
//        while (remainingDigits > 0);

//        yield return null;

//        Extractor.Layout.Components.Add(extractSevenSegmentBlock);
//    }

//    private IEnumerator ProcessSevenSegmentBlockReadDigit(InputSimulator inputSimulator,
//        ExtractComponentSevenSegmentBlock.DigitElement digitElement)
//    {
//        digitElement.NumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitNumber_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitNumber_Y);

//        digitElement.Programmable = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_Y);

//        digitElement.Visible = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitVisibleCheckbox_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitVisibleCheckbox_Y);

//        digitElement.DPOn = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitDPOnCheckbox_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitDPOnCheckbox_Y);

//        digitElement.DPOff = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitDPOffCheckbox_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitDPOffCheckbox_Y);

//        digitElement.AutoDP = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitAutoDPCheckbox_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitAutoDPCheckbox_Y);

//        digitElement.ZeroOn = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitZeroOnCheckbox_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitZeroOnCheckbox_Y);

//        digitElement.ProgrammableSegment1LampNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_Y);

//        digitElement.ProgrammableSegment2LampNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment2Lamp_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment2Lamp_Y);

//        digitElement.ProgrammableSegment3LampNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment3Lamp_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment3Lamp_Y);

//        digitElement.ProgrammableSegment4LampNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment4Lamp_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment4Lamp_Y);

//        digitElement.ProgrammableSegment5LampNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment5Lamp_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment5Lamp_Y);

//        digitElement.ProgrammableSegment6LampNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment6Lamp_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment6Lamp_Y);

//        digitElement.ProgrammableSegment7LampNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment7Lamp_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment7Lamp_Y);

//        digitElement.ProgrammableSegment8LampNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment8Lamp_X,
//            MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment8Lamp_Y);

//        yield return null;
//    }

//    private IEnumerator ProcessBorder(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentBorder extractBorder = new ExtractComponentBorder(componentStandardData);

//        extractBorder.BorderWidth = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBorderBorderWidth_X, MFMEScraperConstants.kPropertiesBorderBorderWidth_Y));

//        extractBorder.Spacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBorderSpacing_X, MFMEScraperConstants.kPropertiesBorderSpacing_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBorderOuterColorColorbox_X, MFMEScraperConstants.kPropertiesBorderOuterColorColorbox_Y);
//        extractBorder.OuterColour = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBorderInnerColorColorbox_X, MFMEScraperConstants.kPropertiesBorderInnerColorColorbox_Y);
//        extractBorder.InnerColour = new ColorJSON(color);

//        extractBorder.Outer = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBorderOuterCheckbox_X, MFMEScraperConstants.kPropertiesBorderOuterCheckbox_Y);

//        extractBorder.Inner = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesBorderInnerCheckbox_X, MFMEScraperConstants.kPropertiesBorderInnerCheckbox_Y);

//        yield return null;

//        Extractor.Layout.Components.Add(extractBorder);
//    }

//    private IEnumerator ProcessSevenSegment(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentSevenSegment extractSevenSegment = new ExtractComponentSevenSegment(componentStandardData);

//        extractSevenSegment.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentNumber_X, MFMEScraperConstants.kPropertiesSevenSegmentNumber_Y));

//        extractSevenSegment.DPRight = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentDPRightCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentDPRightCheckbox_Y);

//        extractSevenSegment.Alpha = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentAlphaCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentAlphaCheckbox_Y);

//        extractSevenSegment.DPOff = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentDPOffCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentDPOffCheckbox_Y);

//        extractSevenSegment.DPOn = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentDPOnCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentDPOnCheckbox_Y);

//        extractSevenSegment.AutoDP = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentAutoDPCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentAutoDPCheckbox_Y);

//        extractSevenSegment.SixteenSegment = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentSixteenSegmentCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentSixteenSegmentCheckbox_Y);

//        extractSevenSegment.ZeroOn = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentZeroOnCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentZeroOnCheckbox_Y);

//        extractSevenSegment.TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentTypeDropdown_X, MFMEScraperConstants.kPropertiesSevenSegmentTypeDropdown_Y);

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentSegmentOnColorColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentSegmentOnColorColorbox_Y);
//        extractSevenSegment.SegmentOnColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentSegmentOffColorColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentSegmentOffColorColorbox_Y);
//        extractSevenSegment.SegmentOffColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentSegmentBackgroundColorColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentSegmentBackgroundColorColorbox_Y);
//        extractSevenSegment.SegmentBackgroundColor = new ColorJSON(color);

//        extractSevenSegment.Thickness = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentThickness_X, MFMEScraperConstants.kPropertiesSevenSegmentThickness_Y));

//        extractSevenSegment.Spacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentSpacing_Y));

//        extractSevenSegment.HorzSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentHorzSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentHorzSpacing_Y));

//        extractSevenSegment.VertSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentVertSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentVertSpacing_Y));

//        extractSevenSegment.Offset = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentOffset_X, MFMEScraperConstants.kPropertiesSevenSegmentOffset_Y));

//        extractSevenSegment.Angle = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentAngle_X, MFMEScraperConstants.kPropertiesSevenSegmentAngle_Y));

//        extractSevenSegment.Slant = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentSlant_X, MFMEScraperConstants.kPropertiesSevenSegmentSlant_Y));

//        extractSevenSegment.Chop = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentChop_X, MFMEScraperConstants.kPropertiesSevenSegmentChop_Y));

//        extractSevenSegment.Centre = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentCentre_X, MFMEScraperConstants.kPropertiesSevenSegmentCentre_Y));

//        extractSevenSegment.LampsProgrammable = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLampsProgrammableCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentLampsProgrammableCheckbox_Y);

//        extractSevenSegment.Lamps1AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps1_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps1_Y);

//        extractSevenSegment.Lamps2AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps2_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps2_Y);

//        extractSevenSegment.Lamps3AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps3_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps3_Y);

//        extractSevenSegment.Lamps4AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps4_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps4_Y);

//        extractSevenSegment.Lamps5AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps5_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps5_Y);

//        extractSevenSegment.Lamps6AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps6_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps6_Y);

//        extractSevenSegment.Lamps7AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps7_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps7_Y);

//        extractSevenSegment.Lamps8AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps8_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps8_Y);

//        extractSevenSegment.Lamps9AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps9_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps9_Y);

//        extractSevenSegment.Lamps10AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps10_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps10_Y);

//        extractSevenSegment.Lamps11AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps11_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps11_Y);

//        extractSevenSegment.Lamps12AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps12_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps12_Y);

//        extractSevenSegment.Lamps13AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps13_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps13_Y);

//        extractSevenSegment.Lamps14AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps14_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps14_Y);

//        extractSevenSegment.Lamps15AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps15_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps15_Y);

//        extractSevenSegment.Lamps16AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesSevenSegmentLamps16_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps16_Y);

//        yield return null;

//        Extractor.Layout.Components.Add(extractSevenSegment);
//    }

//    private IEnumerator ProcessAlphaNew(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentAlphaNew extractAlphaNew = new ExtractComponentAlphaNew(componentStandardData);

//        extractAlphaNew.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaNewNumber_X, MFMEScraperConstants.kPropertiesAlphaNewNumber_Y));

//        // charset
//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaNewOldCharset_X, MFMEScraperConstants.kPropertiesAlphaNewOldCharset_Y))
//        {
//            extractAlphaNew.CharacterSet = ComponentSegmentAlpha.MFMECharacterSetType.OldCharset;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaNewOKI1937Charset_X, MFMEScraperConstants.kPropertiesAlphaNewOKI1937Charset_Y))
//        {
//            extractAlphaNew.CharacterSet = ComponentSegmentAlpha.MFMECharacterSetType.OKI1937;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaNewBFMCharset_X, MFMEScraperConstants.kPropertiesAlphaNewBFMCharset_Y))
//        {
//            extractAlphaNew.CharacterSet = ComponentSegmentAlpha.MFMECharacterSetType.BFMCharset;
//        }
//        else
//        {
//            UnityEngine.Debug.LogError("ERROR could not find a radio button set for Segment Alpha charset type!");
//            extractAlphaNew.CharacterSet = ComponentSegmentAlpha.MFMECharacterSetType.OKI1937;
//        }

//        // 16 seg
//        extractAlphaNew.SixteenSegment = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaNew16SegCheckbox_X, MFMEScraperConstants.kPropertiesAlphaNew16SegCheckbox_Y);

//        // reversed
//        extractAlphaNew.Reversed = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaNewReversedCheckbox_X, MFMEScraperConstants.kPropertiesAlphaNewReversedCheckbox_Y);

//        // alpha on color
//        Color onColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaNewOnColourbox_X, MFMEScraperConstants.kPropertiesAlphaNewOnColourbox_Y);
//        extractAlphaNew.OnColor = new ColorJSON(onColor);

//        Extractor.Layout.Components.Add(extractAlphaNew);

//        yield return null;
//    }

//    private IEnumerator ProcessAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentAlpha extractAlpha = new ExtractComponentAlpha(componentStandardData);

//        extractAlpha.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaNumber_X, MFMEScraperConstants.kPropertiesAlphaNumber_Y));

//        extractAlpha.Reversed = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaReversedCheckbox_X, MFMEScraperConstants.kPropertiesAlphaReversedCheckbox_Y);

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaOnColourbox_X, MFMEScraperConstants.kPropertiesAlphaOnColourbox_Y);
//        extractAlpha.Color = new ColorJSON(color);

//        extractAlpha.DigitWidth = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaDigitWidth_X, MFMEScraperConstants.kPropertiesAlphaDigitWidth_Y));

//        extractAlpha.Columns = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesAlphaColumns_X, MFMEScraperConstants.kPropertiesAlphaColumns_Y));

//        // save bmp of alpha:
//        string alphaImagefilename = "alpha_"
//            + extractAlpha.Number
//            + ".bmp";

//        string alphaImageSaveFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, alphaImagefilename);
//        alphaImageSaveFullPath = alphaImageSaveFullPath.Replace("/", "\\");

//        // save bmp image from MFME
//        if (!File.Exists(alphaImageSaveFullPath) && !MachineConfiguration.ClassicForMAME)
//        {
//            FileHelper.DeleteFileAndMetafileIfFound(alphaImageSaveFullPath);

//            // save alpha image
//            yield return MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper,
//                MFMEScraperConstants.kPropertiesAlphaImage_CenterX, MFMEScraperConstants.kPropertiesAlphaImage_CenterY);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay); // wait for file requester to intialise

//            GUIUtility.systemCopyBuffer = alphaImageSaveFullPath;

//            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
//            yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//            yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
//        }

//        extractAlpha.BmpImageFilename = alphaImagefilename;

//        Extractor.Layout.Components.Add(extractAlpha);

//        yield return null;
//    }

//    private IEnumerator ProcessDotAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentDotAlpha extractDotAlpha = new ExtractComponentDotAlpha(componentStandardData);

//        extractDotAlpha.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaNumber_X, MFMEScraperConstants.kPropertiesDotAlphaNumber_Y));

//        extractDotAlpha.XSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaXSize_X, MFMEScraperConstants.kPropertiesDotAlphaXSize_Y));

//        extractDotAlpha.YSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaYSize_X, MFMEScraperConstants.kPropertiesDotAlphaYSize_Y));

//        extractDotAlpha.DotSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaDotSpacing_X, MFMEScraperConstants.kPropertiesDotAlphaDotSpacing_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaOnColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaOnColourbox_Y);
//        extractDotAlpha.OnColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaOffColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaOffColourbox_Y);
//        extractDotAlpha.OffColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaBackgroundColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaBackgroundColourbox_Y);
//        extractDotAlpha.BackgroundColor = new ColorJSON(color);

//        Extractor.Layout.Components.Add(extractDotAlpha);

//        yield return null;
//    }

//    private IEnumerator ProcessMatrixAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentMatrixAlpha extractMatrixAlpha = new ExtractComponentMatrixAlpha(componentStandardData);

//        extractMatrixAlpha.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesMatrixAlphaNumber_X, MFMEScraperConstants.kPropertiesMatrixAlphaNumber_Y));

//        extractMatrixAlpha.XSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesMatrixAlphaXSize_X, MFMEScraperConstants.kPropertiesMatrixAlphaXSize_Y));

//        extractMatrixAlpha.YSize = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesMatrixAlphaYSize_X, MFMEScraperConstants.kPropertiesMatrixAlphaYSize_Y));

//        extractMatrixAlpha.DotSpacing = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesMatrixAlphaDotSpacing_X, MFMEScraperConstants.kPropertiesMatrixAlphaDotSpacing_Y));

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaOnColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaOnColourbox_Y);
//        extractMatrixAlpha.OnColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaOffColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaOffColourbox_Y);
//        extractMatrixAlpha.OffColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesDotAlphaBackgroundColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaBackgroundColourbox_Y);
//        extractMatrixAlpha.BackgroundColor = new ColorJSON(color);

//        Extractor.Layout.Components.Add(extractMatrixAlpha);

//        yield return null;
//    }

//    private IEnumerator ProcessLamp(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentLamp extractComponentLamp = new ExtractComponentLamp(componentStandardData);

//        extractComponentLamp.NoOutline = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampNoOutlineCheckbox_X, MFMEScraperConstants.kPropertiesLampNoOutlineCheckbox_Y);

//        extractComponentLamp.Graphic = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampGraphicCheckbox_X, MFMEScraperConstants.kPropertiesLampGraphicCheckbox_Y);

//        extractComponentLamp.Transparent = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampTransparentCheckbox_X, MFMEScraperConstants.kPropertiesLampTransparentCheckbox_Y);

//        extractComponentLamp.Blend = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampBlendCheckbox_X, MFMEScraperConstants.kPropertiesLampBlendCheckbox_Y);

//        extractComponentLamp.Inverted = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampInvertedCheckbox_X, MFMEScraperConstants.kPropertiesLampInvertedCheckbox_Y);

//        extractComponentLamp.ClickAll = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampClickAllCheckbox_X, MFMEScraperConstants.kPropertiesLampClickAllCheckbox_Y);

//        extractComponentLamp.LED = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampLEDCheckbox_X, MFMEScraperConstants.kPropertiesLampLEDCheckbox_Y);

//        extractComponentLamp.LockOut = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampLockOutCheckbox_X, MFMEScraperConstants.kPropertiesLampLockOutCheckbox_Y);

//        extractComponentLamp.RGB = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampRGBCheckbox_X, MFMEScraperConstants.kPropertiesLampRGBCheckbox_Y);

//        extractComponentLamp.PreserveAspectRatio = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesLampPreserveAspectRatioCheckbox_X, MFMEScraperConstants.kPropertiesLampPreserveAspectRatioCheckbox_Y);


//        extractComponentLamp.ButtonNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampButtonNumber_X, MFMEScraperConstants.kPropertiesLampButtonNumber_Y);

//        extractComponentLamp.CoinNote = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampCoinNote_X, MFMEScraperConstants.kPropertiesLampCoinNote_Y);

//        extractComponentLamp.Effect = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampEffect_X, MFMEScraperConstants.kPropertiesLampEffect_Y);

//        extractComponentLamp.InhibitLampAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampInhibitLamp_X, MFMEScraperConstants.kPropertiesLampInhibitLamp_Y);

//        extractComponentLamp.Shortcut1 = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampShortcut1_X, MFMEScraperConstants.kPropertiesLampShortcut1_Y);

//        extractComponentLamp.Shortcut2 = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampShortcut2_X, MFMEScraperConstants.kPropertiesLampShortcut2_Y);

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampTextColourColourbox_X, MFMEScraperConstants.kPropertiesLampTextColourColourbox_Y);
//        extractComponentLamp.TextColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampOutlineColourColourbox_X, MFMEScraperConstants.kPropertiesLampOutlineColourColourbox_Y);
//        extractComponentLamp.OutlineColor = new ColorJSON(color);

//        extractComponentLamp.XOff = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampXOff_X, MFMEScraperConstants.kPropertiesLampXOff_Y));

//        extractComponentLamp.YOff = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampYOff_X, MFMEScraperConstants.kPropertiesLampYOff_Y));

//        extractComponentLamp.Shape = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//                MFMEScraperConstants.kPropertiesLampShape_X, MFMEScraperConstants.kPropertiesLampShape_Y);

//        extractComponentLamp.ShapeParameter1 = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampShapeParameter1_X, MFMEScraperConstants.kPropertiesLampShapeParameter1_Y);

//        extractComponentLamp.ShapeParameter2 = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLampShapeParameter2_X, MFMEScraperConstants.kPropertiesLampShapeParameter2_Y);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//                    MFMEScraperConstants.kPropertiesLampOffImageColourbox_X, MFMEScraperConstants.kPropertiesLampOffImageColourbox_Y);
//        extractComponentLamp.OffImageColor = new ColorJSON(color);

//        // TODO only scraping the first 4 lamps for now:
//        //for(int lampElementIndex = 0; lampElementIndex < ExtractComponentLamp.kLampElementCount; ++lampElementIndex)
//        for (int lampElementIndex = 0; lampElementIndex < 4; ++lampElementIndex)
//        {
//            yield return ProcessLampElement(inputSimulator, componentStandardData, lampElementIndex, extractComponentLamp);
//        }

//        Extractor.Layout.Components.Add(extractComponentLamp);

//        yield return null;
//    }

//    private IEnumerator ProcessLampElement(InputSimulator inputSimulator, ComponentStandardData componentStandardData,
//        int lampElementIndex, ExtractComponentLamp extractComponentLamp)
//    {
//        int lampIndexX;
//        int lampIndexY;

//        int lampOnColorboxX;
//        int lampOnColorboxY;

//        int lampImageCenterX;
//        int lampImageCenterY;
//        int lampImageTopLeftX;
//        int lampImageTopLeftY;

//        int lampMaskImageCenterX;
//        int lampMaskImageCenterY;
//        int lampMaskImageTopLeftX;
//        int lampMaskImageTopLeftY;

//        GetLampData(lampElementIndex, out lampIndexX, out lampIndexY, out lampOnColorboxX, out lampOnColorboxY,
//            out lampImageCenterX, out lampImageCenterY, out lampImageTopLeftX, out lampImageTopLeftY);

//        GetLampMaskData(lampElementIndex, out lampMaskImageCenterX, out lampMaskImageCenterY, out lampMaskImageTopLeftX, out lampMaskImageTopLeftY);

//        extractComponentLamp.LampElements[lampElementIndex].NumberAsText = MFMEScraper.GetFieldCharacters(EmulatorScraper, lampIndexX, lampIndexY);

//        int lampIndex;
//        try
//        {
//            lampIndex = int.Parse(extractComponentLamp.LampElements[lampElementIndex].NumberAsText);
//        }
//        catch (Exception)
//        {
//            // not a valid number in the lamp index field, skip this lamp
//            yield break;
//        }

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper, lampOnColorboxX, lampOnColorboxY);
//        extractComponentLamp.LampElements[lampElementIndex].OnColor = new ColorJSON(color);

//        if (MFMEScraper.IsImageBoxBlank(EmulatorScraper, lampImageTopLeftX, lampImageTopLeftY,
//            MFMEScraperConstants.kPropertiesLampImage_Width, MFMEScraperConstants.kPropertiesLampImage_Height, false))
//        {
//            yield break; // TOIMPROVE later, will need to implement blended lamps that do the technique of reusing image from 1st (or earlier?) spot
//        }

//        // TOIMPROVE - maybe doing away with the On1, On2 etc terminology?  LEDs are Red, Green, Blue etc, plus never use Off lamp type
//        Converter.MFMELampType lampType = (Converter.MFMELampType)(lampElementIndex + 1);

//        string lampImagefilename = "lamp_"
//            + lampIndex + "_"
//            + componentStandardData.Position.x + "_"
//            + componentStandardData.Position.y + "_"
//            + componentStandardData.Size.x + "_"
//            + componentStandardData.Size.y + "_"
//            + lampType.ToString()
//            + ".bmp";

//        string lampMaskImageFilename = Path.GetFileNameWithoutExtension(lampImagefilename) + "_mask.bmp";

//        string saveFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, lampImagefilename);
//        saveFullPath = saveFullPath.Replace("/", "\\");

//        string saveMaskFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, lampMaskImageFilename);
//        saveMaskFullPath = saveMaskFullPath.Replace("/", "\\");

//        // save lamp bmp image from MFME
//        if ((DontUseExistingLamps || !File.Exists(saveFullPath)) && !MachineConfiguration.ClassicForMAME)
//        {
//            FileHelper.DeleteFileAndMetafileIfFound(saveFullPath);

//            yield return MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper, lampImageCenterX, lampImageCenterY);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_S);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            // TIMPROVE: DO this on all other right click menus where shortcut exists!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

//            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//            //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            // wait for file requester to intialise
//            yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);

//            GUIUtility.systemCopyBuffer = saveFullPath;

//            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
//            yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//            yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
//        }

//        // TODO a check will be needed when more work is done on this, as we can sometimes have no lamp image, just 
//        // a lamp mask image etc... for now assuming always a lamp
//        extractComponentLamp.LampElements[lampElementIndex].BmpImageFilename = lampImagefilename;

//        // save lamp mask bmp image from MFME if present
//        if (!MFMEScraper.IsImageBoxBlank(EmulatorScraper, lampMaskImageTopLeftX, lampMaskImageTopLeftY,
//            MFMEScraperConstants.kPropertiesLampImage_Width, MFMEScraperConstants.kPropertiesLampImage_Height, false))
//        {
//            extractComponentLamp.LampElements[lampElementIndex].BmpMaskImageFilename = lampMaskImageFilename;

//            if ((DontUseExistingLamps || !File.Exists(saveMaskFullPath)) && !MachineConfiguration.ClassicForMAME)
//            {
//                FileHelper.DeleteFileAndMetafileIfFound(saveMaskFullPath);

//                yield return MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper, lampMaskImageCenterX, lampMaskImageCenterY);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_S);
//                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//                // wait for file requester to intialise
//                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);

//                GUIUtility.systemCopyBuffer = saveMaskFullPath;

//                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
//                yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
//            }
//        }

//        yield return null;
//    }

//    private void GetLampData(int lampElementIndex, out int lampIndexX, out int lampIndexY,
//        out int lampOnColorboxX, out int lampOnColorboxY,
//        out int lampImageCenterX, out int lampImageCenterY, out int lampImageTopLeftX, out int lampImageTopLeftY)
//    {
//        const int kXShiftPixels = 152;
//        const int kYShiftPixels = 196;

//        lampIndexX = MFMEScraperConstants.kPropertiesLamp1Index_X;
//        lampIndexY = MFMEScraperConstants.kPropertiesLamp1Index_Y;
//        lampOnColorboxX = MFMEScraperConstants.kPropertiesLamp1OnColourbox_X;
//        lampOnColorboxY = MFMEScraperConstants.kPropertiesLamp1OnColourbox_Y;
//        lampImageCenterX = MFMEScraperConstants.kPropertiesLamp1Image_CenterX;
//        lampImageCenterY = MFMEScraperConstants.kPropertiesLamp1Image_CenterY;
//        lampImageTopLeftX = MFMEScraperConstants.kPropertiesLamp1Image_TopLeftX;
//        lampImageTopLeftY = MFMEScraperConstants.kPropertiesLamp1Image_TopLeftY;

//        switch (lampElementIndex)
//        {
//            case 0:
//            case 4:
//            case 8:
//                // no offset required, base reference scrape coordinates are for On1 lamp 
//                break;
//            case 1:
//            case 5:
//            case 9:
//                lampIndexX += kXShiftPixels;
//                lampImageCenterX += kXShiftPixels;
//                lampImageTopLeftX += kXShiftPixels;
//                break;
//            case 2:
//            case 6:
//            case 10:
//                lampIndexX -= 1; // This is because the On3 panel is incorrectly aligned in MFME - it's 1px to the left of where it should be
//                lampIndexY += kYShiftPixels;
//                lampImageCenterX -= 1; // This is because the On3 panel is incorrectly aligned in MFME - it's 1px to the left of where it should be
//                lampImageTopLeftX -= 1; // This is because the On3 panel is incorrectly aligned in MFME - it's 1px to the left of where it should be
//                lampImageCenterY += kYShiftPixels + 2;// this is because lower lamp image boxes are offset down by a further 2 pixels due to MFME mistake
//                lampImageTopLeftY += kYShiftPixels + 2;// this is because lower lamp image boxes are offset down by a further 2 pixels due to MFME mistake
//                break;
//            case 3:
//            case 7:
//            case 11:
//                lampIndexX += kXShiftPixels;
//                lampImageCenterX += kXShiftPixels;
//                lampImageTopLeftX += kXShiftPixels;
//                lampIndexY += kYShiftPixels;
//                lampImageCenterY += kYShiftPixels;
//                lampImageTopLeftY += kYShiftPixels;
//                break;
//            default:
//                UnityEngine.Debug.LogError("Lamp element index out of range");
//                break;
//        }
//    }

//    private void GetLampMaskData(int lampElementIndex, /*out int lampIndexX, out int lampIndexY,*/
//        out int lampMaskImageCenterX, out int lampMaskImageCenterY, out int lampMaskImageTopLeftX, out int lampMaskImageTopLeftY)
//    {
//        GetLampData(lampElementIndex, out int lampIndexX, out int lampIndexY, out int _, out int _,
//            out lampMaskImageCenterX, out lampMaskImageCenterY,
//            out lampMaskImageTopLeftX, out lampMaskImageTopLeftY);

//        // correct image coordinates to mask image box
//        switch (lampElementIndex)
//        {
//            case 0:
//            case 4:
//            case 8:
//                lampMaskImageCenterY += 70;
//                lampMaskImageTopLeftY += 70;
//                break;
//            case 1:
//            case 5:
//            case 9:
//                lampMaskImageCenterY += 70;
//                lampMaskImageTopLeftY += 70;
//                break;
//            case 2:
//            case 6:
//            case 10:
//                lampMaskImageCenterY += 68;
//                lampMaskImageTopLeftY += 68;
//                break;
//            case 3:
//            case 7:
//            case 11:
//                lampMaskImageCenterY += 70;
//                lampMaskImageTopLeftY += 70;
//                break;
//            default:
//                UnityEngine.Debug.LogError("Lamp element index out of range");
//                break;
//        }
//    }

//    private IEnumerator ProcessCheckbox(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentCheckbox extractCheckbox = new ExtractComponentCheckbox(componentStandardData);

//        extractCheckbox.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesCheckboxNumber_X, MFMEScraperConstants.kPropertiesCheckboxNumber_Y));

//        Color textColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesCheckboxTextColourbox_X, MFMEScraperConstants.kPropertiesCheckboxTextColourbox_Y);
//        extractCheckbox.TextColor = new ColorJSON(textColor);

//        // TODO needs to be redon with the MS Sans Serif scraper:
//        //if (!SkipCopyingLabelTextToIncreaseSpeed)
//        //{
//        //    yield return MFMEAutomation.GetTextCoroutine(inputSimulator, this, EmulatorScraper,
//        //        MFMEScraperConstants.kComponentTextBox_X, MFMEScraperConstants.kComponentTextBox_Y);
//        //    extractCheckbox.Text = GUIUtility.systemCopyBuffer;
//        //}

//        const int kReferenceFixedMinimumWidth = 15; // use this with width to caculate X offset to scrape checkbox state on/off pixel from
//        const int kReferenceTopLeftCheckboxPreviewXOffset = 641;
//        const int kReferenceTopLeftCheckboxPreviewYOffset = 224;

//        int checkboxWidth = extractCheckbox.Size.X;
//        int widthDifferenceToReference = checkboxWidth - kReferenceFixedMinimumWidth;
//        int xOffsetFromReference = Mathf.RoundToInt((float)widthDifferenceToReference / 2);
//        int topLeftInteriorPixelX = kReferenceTopLeftCheckboxPreviewXOffset - xOffsetFromReference;

//        extractCheckbox.State = MFMEScraper.GetCheckboxPropertiesResultPanePreviewValue(EmulatorScraper,
//            topLeftInteriorPixelX, kReferenceTopLeftCheckboxPreviewYOffset);

//        Extractor.Layout.Components.Add(extractCheckbox);

//        yield return null;
//    }

//    private IEnumerator ProcessRgbLed(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentRgbLed extractRgbLed = new ExtractComponentRgbLed(componentStandardData);

//        extractRgbLed.Number = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedNumber_Y));

//        extractRgbLed.RedLedNumberAsText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedRedLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedRedLedNumber_Y);

//        extractRgbLed.GreenLedNumberAsText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedGreenLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedGreenLedNumber_Y);

//        extractRgbLed.BlueLedNumberAsText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedBlueLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedBlueLedNumber_Y);

//        extractRgbLed.WhiteLedNumberAsText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedWhiteLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedWhiteLedNumber_Y);

//        extractRgbLed.MuxLED = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedMuxLEDCheckbox_X, MFMEScraperConstants.kPropertiesRGBLedMuxLEDCheckbox_Y);

//        extractRgbLed.NoOutline = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedNoOutlineCheckbox_X, MFMEScraperConstants.kPropertiesRGBLedNoOutlineCheckbox_Y);

//        extractRgbLed.NoShadow = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedNoShadowCheckbox_X, MFMEScraperConstants.kPropertiesRGBLedNoShadowCheckbox_Y);

//        extractRgbLed.Style = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedStyleDropdown_X, MFMEScraperConstants.kPropertiesRGBLedStyleDropdown_Y);

//        Color adjustedColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedAdjustedOffColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedOffColourbox_Y);
//        extractRgbLed.AdjustedColorOff = new ColorJSON(adjustedColor);

//        adjustedColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedAdjustedRedColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedRedColourbox_Y);
//        extractRgbLed.AdjustedColorRed = new ColorJSON(adjustedColor);

//        adjustedColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedAdjustedGreenColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedGreenColourbox_Y);
//        extractRgbLed.AdjustedColorGreen = new ColorJSON(adjustedColor);

//        adjustedColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedAdjustedRedGreenColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedRedGreenColourbox_Y);
//        extractRgbLed.AdjustedColorRedGreen = new ColorJSON(adjustedColor);

//        adjustedColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedAdjustedBlueColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedBlueColourbox_Y);
//        extractRgbLed.AdjustedColorBlue = new ColorJSON(adjustedColor);

//        adjustedColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedAdjustedRedBlueColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedRedBlueColourbox_Y);
//        extractRgbLed.AdjustedColorRedBlue = new ColorJSON(adjustedColor);

//        adjustedColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedAdjustedGreenBlueColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedGreenBlueColourbox_Y);
//        extractRgbLed.AdjustedColorGreenBlue = new ColorJSON(adjustedColor);

//        adjustedColor = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesRGBLedAdjustedRedGreenBlueColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedRedGreenBlueColourbox_Y);
//        extractRgbLed.AdjustedColorRedGreenBlue = new ColorJSON(adjustedColor);

//        Extractor.Layout.Components.Add(extractRgbLed);

//        yield return null;
//    }

//    private IEnumerator ProcessLed(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentLed extractLed = new ExtractComponentLed(componentStandardData);

//        extractLed.NumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedNumber_X, MFMEScraperConstants.kPropertiesLedNumber_Y);

//        extractLed.Led = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedLedCheckbox_X, MFMEScraperConstants.kPropertiesLedLedCheckbox_Y);

//        extractLed.DigitAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedDigit_X, MFMEScraperConstants.kPropertiesLedDigit_Y);

//        extractLed.Segment = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedSegmentDropdown_X, MFMEScraperConstants.kPropertiesLedSegmentDropdown_Y);

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedOnColourbox_X, MFMEScraperConstants.kPropertiesLedOnColourbox_Y);
//        extractLed.OnColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedOffColourbox_X, MFMEScraperConstants.kPropertiesLedOffColourbox_Y);
//        extractLed.OffColor = new ColorJSON(color);

//        extractLed.NoOutline = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedNoOutlineCheckbox_X, MFMEScraperConstants.kPropertiesLedNoOutlineCheckbox_Y);

//        extractLed.NoShadow = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedNoShadowCheckbox_X, MFMEScraperConstants.kPropertiesLedNoShadowCheckbox_Y);

//        extractLed.Style = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLedStyleDropdown_X, MFMEScraperConstants.kPropertiesLedStyleDropdown_Y);

//        Extractor.Layout.Components.Add(extractLed);

//        yield return null;
//    }

//    private IEnumerator ProcessFrame(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentFrame extractFrame = new ExtractComponentFrame(componentStandardData);

//        extractFrame.ShapeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFrameShapeDropdown_X, MFMEScraperConstants.kPropertiesFrameShapeDropdown_Y);

//        extractFrame.BevelAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesFrameBevelDropdown_X, MFMEScraperConstants.kPropertiesFrameBevelDropdown_Y);

//        Extractor.Layout.Components.Add(extractFrame);

//        yield return null;
//    }

//    private IEnumerator ProcessLabel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentLabel extractLabel = new ExtractComponentLabel(componentStandardData);

//        extractLabel.LampNumberAsText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLabelLampNumber_X, MFMEScraperConstants.kPropertiesLabelLampNumber_Y);

//        extractLabel.Transparent = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesLabelTransparentCheckbox_X, MFMEScraperConstants.kPropertiesLabelTransparentCheckbox_Y);

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//                    MFMEScraperConstants.kPropertiesLabelTextColourbox_X, MFMEScraperConstants.kPropertiesLabelTextColourbox_Y);
//        extractLabel.TextColor = new ColorJSON(color);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//                    MFMEScraperConstants.kPropertiesLabelBackgroundColourbox_X, MFMEScraperConstants.kPropertiesLabelBackgroundColourbox_Y);
//        extractLabel.BackgroundColor = new ColorJSON(color);

//        Extractor.Layout.Components.Add(extractLabel);

//        yield return null;
//    }

//    private IEnumerator ProcessButton(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
//    {
//        ExtractComponentButton extractButton = new ExtractComponentButton(componentStandardData);

//        // TODO Extract the lamp image and mask image if present

//        extractButton.LampElements[0].NumberAsText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonLampElement0LampNumber_X, MFMEScraperConstants.kPropertiesButtonLampElement0LampNumber_Y);

//        Color color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonLampElement0Color_X, MFMEScraperConstants.kPropertiesButtonLampElement0Color_Y);
//        extractButton.LampElements[0].OnColor = new ColorJSON(color);

//        extractButton.LampElements[1].NumberAsText = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonLampElement1LampNumber_X, MFMEScraperConstants.kPropertiesButtonLampElement1LampNumber_Y);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonLampElement1Color_X, MFMEScraperConstants.kPropertiesButtonLampElement1Color_Y);
//        extractButton.LampElements[1].OnColor = new ColorJSON(color);

//        extractButton.ButtonNumberAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonButtonNumber_X, MFMEScraperConstants.kPropertiesButtonButtonNumber_Y);

//        extractButton.CoinNote = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonCoinNote_X, MFMEScraperConstants.kPropertiesButtonCoinNote_Y);

//        extractButton.Effect = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonEffect_X, MFMEScraperConstants.kPropertiesButtonEffect_Y);

//        extractButton.InhibitLampAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonInhibitLamp_X, MFMEScraperConstants.kPropertiesButtonInhibitLamp_Y);

//        extractButton.Shortcut1 = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonShortcut1_X, MFMEScraperConstants.kPropertiesButtonShortcut1_Y);

//        extractButton.Shortcut2 = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonShortcut2_X, MFMEScraperConstants.kPropertiesButtonShortcut2_Y);

//        extractButton.Graphic = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesButtonGraphicCheckbox_X, MFMEScraperConstants.kPropertiesButtonGraphicCheckbox_Y);

//        extractButton.Inverted = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesButtonInvertedCheckbox_X, MFMEScraperConstants.kPropertiesButtonInvertedCheckbox_Y);

//        extractButton.Split = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesButtonSplitCheckbox_X, MFMEScraperConstants.kPropertiesButtonSplitCheckbox_Y);

//        extractButton.LockOut = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesButtonLockOutCheckbox_X, MFMEScraperConstants.kPropertiesButtonLockOutCheckbox_Y);

//        extractButton.LED = MFMEScraper.GetCheckboxValue(
//            EmulatorScraper, MFMEScraperConstants.kPropertiesButtonLEDCheckbox_X, MFMEScraperConstants.kPropertiesButtonLEDCheckbox_Y);

//        extractButton.XOff = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonXOff_X, MFMEScraperConstants.kPropertiesButtonXOff_Y));

//        extractButton.YOff = int.Parse(MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonYOff_X, MFMEScraperConstants.kPropertiesButtonYOff_Y));

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//                    MFMEScraperConstants.kPropertiesButtonTextColourbox_X, MFMEScraperConstants.kPropertiesButtonTextColourbox_Y);
//        extractButton.TextColor = new ColorJSON(color);

//        extractButton.ShapeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraperConstants.kPropertiesButtonShapeDropdown_X, MFMEScraperConstants.kPropertiesButtonShapeDropdown_Y);

//        color = MFMEScraper.GetColorboxValue(EmulatorScraper,
//                    MFMEScraperConstants.kPropertiesButtonOffImageColourbox_X, MFMEScraperConstants.kPropertiesButtonOffImageColourbox_Y);
//        extractButton.OffImageColor = new ColorJSON(color);

//        Extractor.Layout.Components.Add(extractButton);

//        yield return null;
//    }

//    private IEnumerator ExtractConfiguration(InputSimulator inputSimulator)
//    {
//        if (MachineConfiguration.Platform == MachineConfigurationData.PlatformType.MPU4)
//        {
//            yield return ExtractMPU4CharacteriserLampData(inputSimulator);
//        }

//        bool scrapingSetup = false;
//        switch (MachineConfiguration.Platform)
//        {
//            case MachineConfigurationData.PlatformType.MPU4:
//                scrapingSetup = true;
//                break;
//            default:
//                break;
//        }

//        if (scrapingSetup)
//        {
//            EmulatorScraper.SetScrapeChildIfFound(true, "Game Configuration");

//            // move mouse to top of screen so it doesn't hover over fields and break scraping
//            //inputSimulator.Mouse.MoveMouseBy(0, -2000);
//            inputSimulator.Mouse.MoveMouseTo(1, 1);

//            // open configuration window
//            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_C);

//            yield return new WaitForSeconds(2f);
//        }

//        switch (MachineConfiguration.Platform)
//        {
//            case MachineConfigurationData.PlatformType.MPU4:
//                yield return ExtractConfigurationPageMPU4(inputSimulator);
//                break;
//            case MachineConfigurationData.PlatformType.Scorpion1:
//                yield return ExtractConfigurationPageScorpion1(inputSimulator);
//                break;
//            case MachineConfigurationData.PlatformType.Scorpion2:
//                yield return ExtractConfigurationPageScorpion2(inputSimulator);
//                break;
//            case MachineConfigurationData.PlatformType.MPS2:
//                yield return ExtractConfigurationPageMPS2(inputSimulator);
//                break;
//            case MachineConfigurationData.PlatformType.M1AB:
//                yield return ExtractConfigurationPageM1AB(inputSimulator);
//                break;




//            default:
//                break;
//        }

//        if (scrapingSetup)
//        {
//            // close configuration window
//            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.F4);
//            yield return new WaitForSeconds(2f);

//            EmulatorScraper.SetScrapeChildIfFound(false);
//        }

//        yield return null;
//    }

//    private IEnumerator ExtractMPU4CharacteriserLampData(InputSimulator inputSimulator)
//    {
//        EmulatorScraper.SetScrapeChildIfFound(false);

//        ExtractConfigurationMPU4 configuration = new ExtractConfigurationMPU4();

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_D);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        // can't use shortcut as sometimes it changes from *C*haracteriser, to C*h*aracteriser, so Downs are safer:
//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//        yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LSHIFT, WindowsInput.Native.VirtualKeyCode.TAB);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//        for (int characteriserLamp = 0; characteriserLamp < ExtractConfigurationMPU4.kCharacteriserLampCount; ++characteriserLamp)
//        {
//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.TAB);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_C);
//            yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

//            configuration.CharacteriserLamps[characteriserLamp] = GUIUtility.systemCopyBuffer;
//        }

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.F4);
//        yield return new WaitForSeconds(MFMEAutomation.kLongDelay);

//        Extractor.Layout.Configuration = configuration;

//        EmulatorScraper.SetScrapeChildIfFound(true);
//    }

//    private IEnumerator ExtractConfigurationPageMPU4(InputSimulator inputSimulator)
//    {
//        ExtractConfigurationMPU4 configuration = (ExtractConfigurationMPU4)Extractor.Layout.Configuration;

//        configuration.MeterElements[0].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInType1_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterInType1_DropdownY);

//        configuration.MeterElements[1].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInType2_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterInType2_DropdownY);

//        configuration.MeterElements[2].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInType3_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterInType3_DropdownY);

//        configuration.MeterElements[0].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier1_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier1_DropdownY);

//        configuration.MeterElements[1].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier2_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier2_DropdownY);

//        configuration.MeterElements[2].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier3_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterInMultiplier3_DropdownY);

//        configuration.MeterElements[3].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutType1_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterOutType1_DropdownY);

//        configuration.MeterElements[4].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutType2_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterOutType2_DropdownY);

//        configuration.MeterElements[5].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutType3_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterOutType3_DropdownY);

//        configuration.MeterElements[3].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier1_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier1_DropdownY);

//        configuration.MeterElements[4].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier2_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier2_DropdownY);

//        configuration.MeterElements[5].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier3_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4MeterOutMultiplier3_DropdownY);

//        configuration.Stake = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Stake_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4Stake_DropdownY);

//        configuration.Prize = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Prize_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4Prize_DropdownY);

//        configuration.Percentage = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Percentage_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4Percentage_DropdownY);

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4VolumeControlAuto_RadioButtonTopLeftX),
//            MFMEScraperConstants.kConfigurationMPU4VolumeControlAuto_RadioButtonTopLeftY))
//        {
//            configuration.VolumeControl = MFMEExtract.ExtractConfigurationMPU4.VolumeControlType.Auto;
//        }
//        else
//        {
//            configuration.VolumeControl = MFMEExtract.ExtractConfigurationMPU4.VolumeControlType.Manual;
//        }

//        configuration.RomPagingAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4ROMPaging_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4ROMPaging_DropdownY);

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4LVDNo_RadioButtonTopLeftX),
//            MFMEScraperConstants.kConfigurationMPU4LVDNo_RadioButtonTopLeftY))
//        {
//            configuration.LVD = MFMEExtract.ExtractConfigurationMPU4.LVDType.No;
//        }
//        else
//        {
//            configuration.LVD = MFMEExtract.ExtractConfigurationMPU4.LVDType.Yes;
//        }

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4DisplayReel_RadioButtonTopLeftX),
//            MFMEScraperConstants.kConfigurationMPU4DisplayReel_RadioButtonTopLeftY))
//        {
//            configuration.Display = MFMEExtract.ExtractConfigurationMPU4.DisplayType.Reel;
//        }
//        else
//        {
//            configuration.Display = MFMEExtract.ExtractConfigurationMPU4.DisplayType.Video;
//        }

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4LampTestPass_RadioButtonTopLeftX),
//            MFMEScraperConstants.kConfigurationMPU4LampTestPass_RadioButtonTopLeftY))
//        {
//            configuration.LampTest = MFMEExtract.ExtractConfigurationMPU4.LampTestType.Pass;
//        }
//        else
//        {
//            configuration.LampTest = MFMEExtract.ExtractConfigurationMPU4.LampTestType.Fail;
//        }

//        configuration.PayoutAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Payout_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4Payout_DropdownY);

//        configuration.ExtenderAux1AsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4ExtenderAux1_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4ExtenderAux1_DropdownY);

//        configuration.SevenSegDisplayAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SevenSegDisplay_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4SevenSegDisplay_DropdownY);

//        configuration.ReelsAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Reels_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4Reels_DropdownY);

//        configuration.SoundAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Sound_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4Sound_DropdownY);

//        configuration.EncryptionAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Encryption_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4Encryption_DropdownY);

//        configuration.CharacterAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Character_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4Character_DropdownY);

//        configuration.DataPakAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4DataPak_DropdownX),
//            MFMEScraperConstants.kConfigurationMPU4DataPak_DropdownY);

//        configuration.SwitchServiceAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchService_X),
//            MFMEScraperConstants.kConfigurationMPU4SwitchService_Y);

//        configuration.SwitchCashAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchCash_X),
//            MFMEScraperConstants.kConfigurationMPU4SwitchCash_Y);

//        configuration.SwitchRefillAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchRefill_X),
//            MFMEScraperConstants.kConfigurationMPU4SwitchRefill_Y);

//        configuration.SwitchTestAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchTest_X),
//            MFMEScraperConstants.kConfigurationMPU4SwitchTest_Y);

//        configuration.SwitchTopUpAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4SwitchTopUp_X),
//            MFMEScraperConstants.kConfigurationMPU4SwitchTopUp_Y);

//        configuration.Aux1Invert = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Aux1Invert_CheckboxX),
//            MFMEScraperConstants.kConfigurationMPU4Aux1Invert_CheckboxY);

//        configuration.Aux2Invert = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4Aux2Invert_CheckboxX),
//            MFMEScraperConstants.kConfigurationMPU4Aux2Invert_CheckboxY);

//        configuration.DoorInvert = MFMEScraper.GetCheckboxValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4DoorInvert_CheckboxX),
//            MFMEScraperConstants.kConfigurationMPU4DoorInvert_CheckboxY);


//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4AlphaCableNormal_RadioButtonTopLeftX),
//            MFMEScraperConstants.kConfigurationMPU4AlphaCableNormal_RadioButtonTopLeftY))
//        {
//            configuration.AlphaCable = MFMEExtract.ExtractConfigurationMPU4.AlphaCableType.Normal;
//        }
//        else
//        {
//            configuration.AlphaCable = MFMEExtract.ExtractConfigurationMPU4.AlphaCableType.CR;
//        }

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4ModType2_RadioButtonTopLeftX),
//            MFMEScraperConstants.kConfigurationMPU4ModType2_RadioButtonTopLeftY))
//        {
//            configuration.ModType = MFMEExtract.ExtractConfigurationMPU4.ModTypes.Two;
//        }
//        else
//        {
//            configuration.ModType = MFMEExtract.ExtractConfigurationMPU4.ModTypes.Four;
//        }

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4CabinetStyleDefault_RadioButtonTopLeftX),
//            MFMEScraperConstants.kConfigurationMPU4CabinetStyleDefault_RadioButtonTopLeftY))
//        {
//            configuration.CabinetStyle = MFMEExtract.ExtractConfigurationMPU4.CabinetStyleType.Default;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//             MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationMPU4CabinetStyleRio_RadioButtonTopLeftX),
//             MFMEScraperConstants.kConfigurationMPU4CabinetStyleRio_RadioButtonTopLeftY))
//        {
//            configuration.CabinetStyle = MFMEExtract.ExtractConfigurationMPU4.CabinetStyleType.Rio;
//        }
//        else
//        {
//            configuration.CabinetStyle = MFMEExtract.ExtractConfigurationMPU4.CabinetStyleType.Genesis;
//        }

//        yield return null;
//    }

//    private IEnumerator ExtractConfigurationPageScorpion1(InputSimulator inputSimulator)
//    {
//        ExtractConfigurationScorpion1 configuration = new ExtractConfigurationScorpion1();

//        configuration.MeterElements[0].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInType1_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterInType1_DropdownY);

//        configuration.MeterElements[1].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInType2_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterInType2_DropdownY);

//        configuration.MeterElements[2].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInType3_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterInType3_DropdownY);

//        configuration.MeterElements[0].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier1_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier1_DropdownY);

//        configuration.MeterElements[1].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier2_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier2_DropdownY);

//        configuration.MeterElements[2].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier3_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterInMultiplier3_DropdownY);

//        configuration.MeterElements[3].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutType1_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterOutType1_DropdownY);

//        configuration.MeterElements[4].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutType2_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterOutType2_DropdownY);

//        configuration.MeterElements[5].TypeAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutType3_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterOutType3_DropdownY);

//        configuration.MeterElements[3].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier1_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier1_DropdownY);

//        configuration.MeterElements[4].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier2_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier2_DropdownY);

//        configuration.MeterElements[5].MultiplierAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier3_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1MeterOutMultiplier3_DropdownY);

//        configuration.Stake = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1Stake_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1Stake_DropdownY);

//        configuration.Prize = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1Prize_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1Prize_DropdownY);

//        configuration.Percentage = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1Percentage_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1Percentage_DropdownY);

//        configuration.EncryptionAsString = MFMEScraper.GetDropdownCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1Encryption_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1Encryption_DropdownY);

//        configuration.SwitchServiceAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchService_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchService_Y);

//        configuration.SwitchCashAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchCash_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchCash_Y);

//        configuration.SwitchRefillAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchRefill_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchRefill_Y);

//        configuration.SwitchTestAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchTest_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchTest_Y);

//        configuration.SwitchPaySense1AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense1_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense1_Y);

//        configuration.SwitchPaySense2AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense2_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense2_Y);

//        configuration.SwitchPaySense3AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense3_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense3_Y);

//        configuration.SwitchPaySense4AsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense4_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchPaysense4_Y);

//        configuration.SwitchDMBusyAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SwitchDMBusy_X),
//            MFMEScraperConstants.kConfigurationScorpion1SwitchDMBusy_Y);

//        configuration.DataPakAsString = MFMEScraper.GetFieldCharacters(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1DataPak_DropdownX),
//            MFMEScraperConstants.kConfigurationScorpion1DataPak_DropdownY);

//        if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//            MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SampledSoundNEC_RadioButtonTopLeftX),
//            MFMEScraperConstants.kConfigurationScorpion1SampledSoundNEC_RadioButtonTopLeftY))
//        {
//            configuration.SampledSound = ExtractConfigurationScorpion1.SampledSoundType.NEC;
//        }
//        else if (MFMEScraper.GetRadioButtonValue(EmulatorScraper,
//             MFMEScraper.GetGameConfigurationX(MFMEScraperConstants.kConfigurationScorpion1SampledSoundOKI_RadioButtonTopLeftX),
//             MFMEScraperConstants.kConfigurationScorpion1SampledSoundOKI_RadioButtonTopLeftY))
//        {
//            configuration.SampledSound = ExtractConfigurationScorpion1.SampledSoundType.OKI;
//        }
//        else
//        {
//            configuration.SampledSound = ExtractConfigurationScorpion1.SampledSoundType.Global;
//        }

//        Extractor.Layout.Configuration = configuration;

//        yield return null;
//    }

//    private IEnumerator ExtractConfigurationPageScorpion2(InputSimulator inputSimulator)
//    {
//        ExtractConfigurationScorpion2 configuration = new ExtractConfigurationScorpion2();

//        Extractor.Layout.Configuration = configuration;

//        yield return null;
//    }

//    private IEnumerator ExtractConfigurationPageMPS2(InputSimulator inputSimulator)
//    {
//        ExtractConfigurationMPS2 configuration = new ExtractConfigurationMPS2();

//        Extractor.Layout.Configuration = configuration;

//        yield return null;
//    }

//    private IEnumerator ExtractConfigurationPageM1AB(InputSimulator inputSimulator)
//    {
//        ExtractConfigurationM1AB configuration = new ExtractConfigurationM1AB();

//        Extractor.Layout.Configuration = configuration;

//        yield return null;
//    }




}