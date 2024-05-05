using Oasis.MfmeTools.Shared.ExtractComponents;
using Oasis.MfmeTools.Shared.JsonDataStructures;
using Oasis.MfmeTools.UnityWrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsInput;
using static Oasis.MfmeTools.Shared.Mfme.MFMEConstants;
using static Oasis.MfmeTools.Shared.Mfme.MfmeExtractor;

namespace Oasis.MfmeTools.Shared.Mfme
{
    public static class ExtractComponentProcessor
    {
        //        public static void ProcessBackground(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            // TODO - acquire window width / height from right click menu, set as ComponentBackground width/height

        //            bool backgroundBitmapBlank = MfmeScraper.IsImageBoxBlank(
        //                            MFMEScraperConstants.kPropertiesBackgroundImage_X, MFMEScraperConstants.kPropertiesBackgroundImage_Y,
        //                            MFMEScraperConstants.kPropertiesBackgroundImage_Width, MFMEScraperConstants.kPropertiesBackgroundImage_Height,
        //                            true, false);


        //            string saveFullPath = "";

        //            if (!backgroundBitmapBlank)
        //            {
        //                saveFullPath = Path.Combine(OutputDirectoryPath, "background.bmp");
        //                saveFullPath = saveFullPath.Replace("/", "\\");

        //                // save bmp image from MFME
        //                if ((DontUseExistingBackgrounds || !File.Exists(saveFullPath)) && !MachineConfiguration.ClassicForMAME)
        //                {
        //                    FileHelper.DeleteFileAndMetafileIfFound(saveFullPath);

        //                    MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper,
        //                        MFMEScraperConstants.kPropertiesBackgroundImage_CenterX, MFMEScraperConstants.kPropertiesBackgroundImage_CenterY);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    // wait for file requester to intialise
        //                    yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);

        //                    GUIUtility.systemCopyBuffer = saveFullPath;

        //                    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
        //                    yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
        //                }
        //            }

        //            ExtractComponentBackground extractBackground = new ExtractComponentBackground(componentStandardData);
        //            extractBackground.BmpImageFilename = Path.GetFileName(saveFullPath);

        //            Extractor.Layout.Components.Add(extractBackground);

        //            //ConverterImage converterImage = new ConverterImage(saveFullPath, null, false);
        //            //Extractor.Layout.BackgroundImageSize.X = converterImage.Width;
        //            //Extractor.Layout.BackgroundImageSize.Y = converterImage.Height;

        //            Extractor.Layout.BackgroundImageSize.X = componentStandardData.Size.x;
        //            Extractor.Layout.BackgroundImageSize.Y = componentStandardData.Size.y;
        //        }

        //        public static void ProcessReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentReel extractReel = new ExtractComponentReel(componentStandardData);

        //            extractReel.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelNumber_X, MFMEScraperConstants.kPropertiesReelNumber_Y));

        //            extractReel.Stops = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelStops_X, MFMEScraperConstants.kPropertiesReelStops_Y));

        //            extractReel.HalfSteps = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelHalfSteps_X, MFMEScraperConstants.kPropertiesReelHalfSteps_Y));

        //            extractReel.Resolution = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelResolution_X, MFMEScraperConstants.kPropertiesReelResolution_Y));

        //            extractReel.BandOffset = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelBandOffset_X, MFMEScraperConstants.kPropertiesReelBandOffset_Y));

        //            extractReel.OptoTab = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelOptoTab_X, MFMEScraperConstants.kPropertiesReelOptoTab_Y));

        //            extractReel.Height = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelHeight_X, MFMEScraperConstants.kPropertiesReelHeight_Y));

        //            extractReel.WidthDiff = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelWidthDiff_X, MFMEScraperConstants.kPropertiesReelWidthDiff_Y));

        //            extractReel.Horizontal = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesReelHorizontalCheckbox_X, MFMEScraperConstants.kPropertiesReelHorizontalCheckbox_Y);

        //            extractReel.Reversed = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesReelReversedCheckbox_X, MFMEScraperConstants.kPropertiesReelReversedCheckbox_Y);

        //            extractReel.Lamps = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesReelLampsCheckbox_X, MFMEScraperConstants.kPropertiesReelLampsCheckbox_Y);

        //            extractReel.LampsLEDs = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesReelLampsLEDsCheckbox_X, MFMEScraperConstants.kPropertiesReelLampsLEDsCheckbox_Y);

        //            extractReel.Mirrored = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesReelMirroredCheckbox_X, MFMEScraperConstants.kPropertiesReelMirroredCheckbox_Y);

        //            for (int reelLampIndex = 0; reelLampIndex < ComponentReel.kReelLampCount; ++reelLampIndex)
        //            {
        //                Vector2Int propertiesReelLampNumberPosition = MFMEScraperConstants.GetPropertiesReelLampNumber_XY(reelLampIndex);

        //                extractReel.LampNumbersAsStrings[reelLampIndex] = DelphiFontScraper.GetFieldCharacters(
        //                    propertiesReelLampNumberPosition.x, propertiesReelLampNumberPosition.y);
        //            }

        //            extractReel.WinLinesCount = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelWinLinesCount_X, MFMEScraperConstants.kPropertiesReelWinLinesCount_Y));

        //            extractReel.WinLinesOffset = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesReelWinLinesOffset_X, MFMEScraperConstants.kPropertiesReelWinLinesOffset_Y));

        //            string reelImagefilename = "reel_"
        //                + extractReel.Number
        //                + ".bmp";

        //            string reelImageSaveFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, reelImagefilename);
        //            reelImageSaveFullPath = reelImageSaveFullPath.Replace("/", "\\");

        //            string reelOverlayImagefilename = "reeloverlay_"
        //                + extractReel.Number
        //                + ".bmp";

        //            string reelOverlaySaveFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, reelOverlayImagefilename);
        //            reelOverlaySaveFullPath = reelOverlaySaveFullPath.Replace("/", "\\");

        //            // save bmp image from MFME
        //            if (DontUseExistingReels || !File.Exists(reelImageSaveFullPath))
        //            {
        //                FileHelper.DeleteFileAndMetafileIfFound(reelImageSaveFullPath);

        //                // save reel band image
        //                MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper,
        //                    MFMEScraperConstants.kPropertiesReelImage_CenterX, MFMEScraperConstants.kPropertiesReelImage_CenterY);

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

        //                GUIUtility.systemCopyBuffer = reelImageSaveFullPath;

        //                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
        //                yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

        //                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
        //            }

        //            // save bmp image from MFME
        //            if ((DontUseExistingReels || !File.Exists(reelOverlaySaveFullPath)) && !MachineConfiguration.ClassicForMAME)
        //            {
        //                FileHelper.DeleteFileAndMetafileIfFound(reelOverlaySaveFullPath);

        //                // save reel overlay image if present
        //                MFMEAutomation.LeftClickAtPosition(inputSimulator,
        //                    MFMEScraperConstants.kPropertiesOverlayTab_CenterX, MFMEScraperConstants.kPropertiesOverlayTab_CenterY);

        //                // is it just blank checkerboard?
        //                if (!MfmeScraper.IsImageBoxBlank(
        //                    EmulatorScraper, MFMEScraperConstants.kPropertiesOverlayImage_TopLeftX, MFMEScraperConstants.kPropertiesOverlayImage_TopLeftY,
        //                    MFMEScraperConstants.kPropertiesOverlayImage_Width, MFMEScraperConstants.kPropertiesOverlayImage_Height, true))
        //                {
        //                    MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper,
        //                        MFMEScraperConstants.kPropertiesOverlayImage_CenterX, MFMEScraperConstants.kPropertiesOverlayImage_CenterY);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay); // wait for file requester to intialise

        //                    GUIUtility.systemCopyBuffer = reelOverlaySaveFullPath;

        //                    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
        //                    yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
        //                }
        //            }

        //            extractReel.BandBmpImageFilename = Path.GetFileName(reelImageSaveFullPath);

        //            extractReel.HasOverlay = File.Exists(reelOverlaySaveFullPath);
        //            if (extractReel.HasOverlay)
        //            {
        //                extractReel.OverlayBmpImageFilename = Path.GetFileName(reelOverlaySaveFullPath);
        //            }

        //            Extractor.Layout.Components.Add(extractReel);
        //        }

        //        public static void ProcessBandReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentBandReel extractBandReel = new ExtractComponentBandReel(componentStandardData);

        //            extractBandReel.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesBandReelNumber_X, MFMEScraperConstants.kPropertiesBandReelNumber_Y));

        //            extractBandReel.Stops = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesBandReelStops_X, MFMEScraperConstants.kPropertiesBandReelStops_Y));

        //            extractBandReel.HalfSteps = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesBandReelHalfSteps_X, MFMEScraperConstants.kPropertiesBandReelHalfSteps_Y));

        //            extractBandReel.View = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesBandReelView_X, MFMEScraperConstants.kPropertiesBandReelView_Y));

        //            extractBandReel.Offset = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesBandReelOffset_X, MFMEScraperConstants.kPropertiesBandReelOffset_Y));

        //            extractBandReel.Spacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesBandReelSpacing_X, MFMEScraperConstants.kPropertiesBandReelSpacing_Y));

        //            extractBandReel.OptoTab = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesBandReelOptoTab_X, MFMEScraperConstants.kPropertiesBandReelOptoTab_Y));

        //            extractBandReel.Reversed = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelReversedCheckbox_X, MFMEScraperConstants.kPropertiesBandReelReversedCheckbox_Y);

        //            extractBandReel.Inverted = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelInvertedCheckbox_X, MFMEScraperConstants.kPropertiesBandReelInvertedCheckbox_Y);

        //            extractBandReel.Vertical = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelVerticalCheckbox_X, MFMEScraperConstants.kPropertiesBandReelVerticalCheckbox_Y);

        //            extractBandReel.Opaque = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelOpaqueCheckbox_X, MFMEScraperConstants.kPropertiesBandReelOpaqueCheckbox_Y);

        //            extractBandReel.Lamps = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelLampsCheckbox_X, MFMEScraperConstants.kPropertiesBandReelLampsCheckbox_Y);

        //            extractBandReel.Custom = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesBandReelCustomCheckbox_X, MFMEScraperConstants.kPropertiesBandReelCustomCheckbox_Y);

        //            // TODO
        //            //public string[] LampNumbersAsStrings = new string[ComponentBandReel.kReelLampCount];

        //            //public bool HasOverlay;

        //            //public string BandBmpImageFilename;
        //            //public string OverlayBmpImageFilename;

        //            

        //            Extractor.Layout.Components.Add(extractBandReel);
        //        }

        //        public static void ProcessDiscReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentDiscReel extractDiscReel = new ExtractComponentDiscReel(componentStandardData);

        //            extractDiscReel.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelNumber_X, MFMEScraperConstants.kPropertiesDiscReelNumber_Y));

        //            extractDiscReel.Stops = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelStops_X, MFMEScraperConstants.kPropertiesDiscReelStops_Y));

        //            extractDiscReel.HalfSteps = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelHalfSteps_X, MFMEScraperConstants.kPropertiesDiscReelHalfSteps_Y));

        //            extractDiscReel.Resolution = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelResolution_X, MFMEScraperConstants.kPropertiesDiscReelResolution_Y));

        //            extractDiscReel.Offset = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelOffset_X, MFMEScraperConstants.kPropertiesDiscReelOffset_Y));

        //            extractDiscReel.OptoTab = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelOptoTab_X, MFMEScraperConstants.kPropertiesDiscReelOptoTab_Y));

        //            extractDiscReel.Bounce = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelBounce_X, MFMEScraperConstants.kPropertiesDiscReelBounce_Y));

        //            extractDiscReel.Lamps = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesDiscReelLampsCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelLampsCheckbox_Y);

        //            extractDiscReel.Reversed = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesDiscReelReversedCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelReversedCheckbox_Y);

        //            extractDiscReel.Inverted = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesDiscReelInvertedCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelInvertedCheckbox_Y);

        //            extractDiscReel.Transparent = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesDiscReelTransparentCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelTransparentCheckbox_Y);

        //            extractDiscReel.OuterH = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelOuterH_X, MFMEScraperConstants.kPropertiesDiscReelOuterH_Y));

        //            extractDiscReel.OuterL = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelOuterL_X, MFMEScraperConstants.kPropertiesDiscReelOuterL_Y));

        //            extractDiscReel.OuterLampSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelOuterLampSize_X, MFMEScraperConstants.kPropertiesDiscReelOuterLampSize_Y));

        //            extractDiscReel.InnerH = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelInnerH_X, MFMEScraperConstants.kPropertiesDiscReelInnerH_Y));

        //            extractDiscReel.InnerL = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelInnerL_X, MFMEScraperConstants.kPropertiesDiscReelInnerL_Y));

        //            extractDiscReel.InnerLampSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelInnerLampSize_X, MFMEScraperConstants.kPropertiesDiscReelInnerLampSize_Y));

        //            extractDiscReel.LampPositionsLamps = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelLampPositionsLamps_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsLamps_Y));

        //            extractDiscReel.LampPositionsLamp = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelLampPositionsLamp_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsLamp_Y));

        //            extractDiscReel.LampPositionsNumberAsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelLampPositionsNumber_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsNumber_Y);

        //            extractDiscReel.LampPositionsOffset = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesDiscReelLampPositionsOffset_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsOffset_Y));

        //            extractDiscReel.LampPositionsGap = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesDiscReelLampPositionsGapCheckbox_X, MFMEScraperConstants.kPropertiesDiscReelLampPositionsGapCheckbox_Y);

        //            // TODO images:
        //            //public string DiscBmpImageFilename;
        //            //public string DiscOverlayBmpImageFilename;
        //            //public string LampPositionsBmpImageFilename;

        //            //public string OuterMask1BmpImageFilename;
        //            //public string OuterMask2BmpImageFilename;

        //            //public string InnerMask1BmpImageFilename;
        //            //public string InnerMask2BmpImageFilename;

        //            //public bool HasOverlay;
        //            //public string OverlayBmpImageFilename;


        //            

        //            Extractor.Layout.Components.Add(extractDiscReel);
        //        }

        //        public static void ProcessFlipReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentFlipReel extractFlipReel = new ExtractComponentFlipReel(componentStandardData);

        //            extractFlipReel.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesFlipReelNumber_X, MFMEScraperConstants.kPropertiesFlipReelNumber_Y));

        //            extractFlipReel.Stops = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesFlipReelStops_X, MFMEScraperConstants.kPropertiesFlipReelStops_Y));

        //            extractFlipReel.HalfSteps = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesFlipReelHalfSteps_X, MFMEScraperConstants.kPropertiesFlipReelHalfSteps_Y));

        //            extractFlipReel.Offset = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesFlipReelOffset_X, MFMEScraperConstants.kPropertiesFlipReelOffset_Y));

        //            extractFlipReel.Reversed = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesFlipReelReversedCheckbox_X, MFMEScraperConstants.kPropertiesFlipReelReversedCheckbox_Y);

        //            extractFlipReel.Inverted = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesFlipReelInvertedCheckbox_X, MFMEScraperConstants.kPropertiesFlipReelInvertedCheckbox_Y);

        //            Color bordercolor = new Color(MfmeScraper.GetColorboxValue(
        //                MFMEScraperConstants.kPropertiesFlipReelBorderColourbox_X, MFMEScraperConstants.kPropertiesFlipReelBorderColourbox_Y);
        //            extractFlipReel.BorderColour = new ColorJSON(borderColor);

        //            extractFlipReel.BorderWidth = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesFlipReelBorderWidth_X, MFMEScraperConstants.kPropertiesFlipReelBorderWidth_Y));

        //            extractFlipReel.Lamps = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesFlipReelLampsCheckbox_X, MFMEScraperConstants.kPropertiesFlipReelLampsCheckbox_Y);

        //            extractFlipReel.Lamp1AsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesFlipReelLamp1_X, MFMEScraperConstants.kPropertiesFlipReelLamp1_Y);

        //            extractFlipReel.Lamp2AsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesFlipReelLamp2_X, MFMEScraperConstants.kPropertiesFlipReelLamp2_Y);

        //            extractFlipReel.Lamp3AsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesFlipReelLamp3_X, MFMEScraperConstants.kPropertiesFlipReelLamp3_Y);

        //            // TODO images:
        //            //public string BandBmpImageFilename;

        //            //public string LampMask1BmpImageFilename;
        //            //public string LampMask2BmpImageFilename;
        //            //public string LampMask3BmpImageFilename;

        //            //public bool HasOverlay;
        //            //public string OverlayBmpImageFilename;


        //            

        //            Extractor.Layout.Components.Add(extractFlipReel);
        //        }


        //        public static void ProcessJpmBonusReel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentJpmBonusReel extractJPMBonusReel = new ExtractComponentJpmBonusReel(componentStandardData);

        //            extractJPMBonusReel.Lamp1AsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesJPMBonusReelLamp1_X, MFMEScraperConstants.kPropertiesJPMBonusReelLamp1_Y);

        //            extractJPMBonusReel.Lamp2AsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesJPMBonusReelLamp2_X, MFMEScraperConstants.kPropertiesJPMBonusReelLamp2_Y);

        //            extractJPMBonusReel.Lamp3AsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesJPMBonusReelLamp3_X, MFMEScraperConstants.kPropertiesJPMBonusReelLamp3_Y);

        //            extractJPMBonusReel.Lamp4AsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesJPMBonusReelLamp4_X, MFMEScraperConstants.kPropertiesJPMBonusReelLamp4_Y);

        //            extractJPMBonusReel.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesJPMBonusReelNumber_X, MFMEScraperConstants.kPropertiesJPMBonusReelNumber_Y));

        //            extractJPMBonusReel.SymbolPos = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesJPMBonusReelSymbolPos_X, MFMEScraperConstants.kPropertiesJPMBonusReelSymbolPos_Y));

        //            // TODO images:
        //            //public string Lamp1OnImageBmpImageFilename;
        //            //public string Lamp2OnImageBmpImageFilename;
        //            //public string Lamp3OnImageBmpImageFilename;
        //            //public string Lamp4OnImageBmpImageFilename;

        //            //public string MaskBmpImageFilename;
        //            //public string BackgroundBmpImageFilename;

        //            //public bool HasOverlay;
        //            //public string OverlayBmpImageFilename;


        //            

        //            Extractor.Layout.Components.Add(extractJPMBonusReel);
        //        }

        public static void ProcessBfmAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentBfmAlpha extractBfmAlpha = new ExtractComponentBfmAlpha(componentStandardData);

            extractBfmAlpha.Reversed = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesBFMAlphaReversedCheckbox_X, MFMEScraperConstants.kPropertiesBFMAlphaReversedCheckbox_Y);

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesBFMAlphaColourColorbox_X, MFMEScraperConstants.kPropertiesBFMAlphaColourColorbox_Y));
            extractBfmAlpha.Colour = new ColorJSON(color);

            extractBfmAlpha.OffLevel = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBFMAlphaOffLevel_X, MFMEScraperConstants.kPropertiesBFMAlphaOffLevel_Y));

            extractBfmAlpha.DigitWidth = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBFMAlphaDigitWidth_X, MFMEScraperConstants.kPropertiesBFMAlphaDigitWidth_Y));

            extractBfmAlpha.Columns = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBFMAlphaColumns_X, MFMEScraperConstants.kPropertiesBFMAlphaColumns_Y));

            Extractor.Layout.Components.Add(extractBfmAlpha);
        }

        public static void ProcessProconnMatrix(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentProconnMatrix extractProconnMatrix = new ExtractComponentProconnMatrix(componentStandardData);

            extractProconnMatrix.DotSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesProconnMatrixDotSize_X, MFMEScraperConstants.kPropertiesProconnMatrixDotSize_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesProconnMatrixOnColourColorbox_X, MFMEScraperConstants.kPropertiesProconnMatrixOnColourColorbox_Y));
            extractProconnMatrix.OnColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesProconnMatrixOffColourColorbox_X, MFMEScraperConstants.kPropertiesProconnMatrixOffColourColorbox_Y));
            extractProconnMatrix.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesProconnMatrixBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesProconnMatrixBackgroundColourColorbox_Y));
            extractProconnMatrix.BackgroundColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractProconnMatrix);
        }

        public static void ProcessEpochAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentEpochAlpha extractEpochAlpha = new ExtractComponentEpochAlpha(componentStandardData);

            extractEpochAlpha.XSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesEpochAlphaXSize_X, MFMEScraperConstants.kPropertiesEpochAlphaXSize_Y));

            extractEpochAlpha.YSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesEpochAlphaYSize_X, MFMEScraperConstants.kPropertiesEpochAlphaYSize_Y));

            extractEpochAlpha.DotSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesEpochAlphaDotSpacing_X, MFMEScraperConstants.kPropertiesEpochAlphaDotSpacing_Y));

            extractEpochAlpha.DigitSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesEpochAlphaDigitSpacing_X, MFMEScraperConstants.kPropertiesEpochAlphaDigitSpacing_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesEpochAlphaOnColourColorbox_X, MFMEScraperConstants.kPropertiesEpochAlphaOnColourColorbox_Y));
            extractEpochAlpha.OnColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesEpochAlphaOffColourColorbox_X, MFMEScraperConstants.kPropertiesEpochAlphaOffColourColorbox_Y));
            extractEpochAlpha.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesEpochAlphaBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesEpochAlphaBackgroundColourColorbox_Y));
            extractEpochAlpha.BackgroundColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractEpochAlpha);
        }

        public static void ProcessIgtVfd(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentIgtVfd extractIgtVfd = new ExtractComponentIgtVfd(componentStandardData);

            extractIgtVfd.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesIgtVfdNumber_X, MFMEScraperConstants.kPropertiesIgtVfdNumber_Y));

            extractIgtVfd.DotSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesIgtVfdDotSize_X, MFMEScraperConstants.kPropertiesIgtVfdDotSize_Y));

            extractIgtVfd.DotSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesIgtVfdDotSpacing_X, MFMEScraperConstants.kPropertiesIgtVfdDotSpacing_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesIgtVfdOnColourColorbox_X, MFMEScraperConstants.kPropertiesIgtVfdOnColourColorbox_Y));
            extractIgtVfd.OnColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesIgtVfdOffColourColorbox_X, MFMEScraperConstants.kPropertiesIgtVfdOffColourColorbox_Y));
            extractIgtVfd.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesIgtVfdBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesIgtVfdBackgroundColourColorbox_Y));
            extractIgtVfd.BackgroundColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractIgtVfd);
        }

        public static void ProcessPlasma(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentPlasma extractPlasma = new ExtractComponentPlasma(componentStandardData);

            extractPlasma.DotSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesPlasmaDotSize_X, MFMEScraperConstants.kPropertiesPlasmaDotSize_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesPlasmaOnColourColorbox_X, MFMEScraperConstants.kPropertiesPlasmaOnColourColorbox_Y));
            extractPlasma.OnColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesPlasmaOffColourColorbox_X, MFMEScraperConstants.kPropertiesPlasmaOffColourColorbox_Y));
            extractPlasma.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesPlasmaBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesPlasmaBackgroundColourColorbox_Y));
            extractPlasma.BackgroundColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractPlasma);
        }

        public static void ProcessDotMatrix(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentDotMatrix extractDotMatrix = new ExtractComponentDotMatrix(componentStandardData);

            extractDotMatrix.DotSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesDotMatrixDotSize_X, MFMEScraperConstants.kPropertiesDotMatrixDotSize_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotMatrixOnColourColorbox_X, MFMEScraperConstants.kPropertiesDotMatrixOnColourColorbox_Y));
            extractDotMatrix.OnColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotMatrixOffColourColorbox_X, MFMEScraperConstants.kPropertiesDotMatrixOffColourColorbox_Y));
            extractDotMatrix.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotMatrixBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesDotMatrixBackgroundColourColorbox_Y));
            extractDotMatrix.BackgroundColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractDotMatrix);
        }

        public static void ProcessBfmLed(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentBfmLed extractBfmLed = new ExtractComponentBfmLed(componentStandardData);

            extractBfmLed.XSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBfmLedXSize_X, MFMEScraperConstants.kPropertiesBfmLedXSize_Y));

            extractBfmLed.YSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBfmLedYSize_X, MFMEScraperConstants.kPropertiesBfmLedYSize_Y));

            extractBfmLed.DigitSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBfmLedDigitSpacing_X, MFMEScraperConstants.kPropertiesBfmLedDigitSpacing_Y));

            extractBfmLed.LedSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBfmLedLedSize_X, MFMEScraperConstants.kPropertiesBfmLedLedSize_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesBfmLedOnColourColorbox_X, MFMEScraperConstants.kPropertiesBfmLedOnColourColorbox_Y));
            extractBfmLed.OnColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesBfmLedOffColourColorbox_X, MFMEScraperConstants.kPropertiesBfmLedOffColourColorbox_Y));
            extractBfmLed.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesBfmLedBackColourColorbox_X, MFMEScraperConstants.kPropertiesBfmLedBackColourColorbox_Y));
            extractBfmLed.BackColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractBfmLed);
        }

        public static void ProcessBfmColourLed(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentBfmColourLed extractBfmColourLed = new ExtractComponentBfmColourLed(componentStandardData);

            extractBfmColourLed.DotSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBfmColourLedDotSize_X, MFMEScraperConstants.kPropertiesBfmColourLedDotSize_Y));

            extractBfmColourLed.Spacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBfmColourLedSpacing_X, MFMEScraperConstants.kPropertiesBfmColourLedSpacing_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesBfmColourLedOffColourColorbox_X, MFMEScraperConstants.kPropertiesBfmColourLedOffColourColorbox_Y));
            extractBfmColourLed.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesBfmColourLedBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesBfmColourLedBackgroundColourColorbox_Y));
            extractBfmColourLed.BackgroundColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractBfmColourLed);
        }

        public static void ProcessAceMatrix(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentAceMatrix extractAceMatrix = new ExtractComponentAceMatrix(componentStandardData);

            extractAceMatrix.DotSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesAceMatrixDotSize_X, MFMEScraperConstants.kPropertiesAceMatrixDotSize_Y));

            extractAceMatrix.Flip180 = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesAceMatrixFlip180Checkbox_X, MFMEScraperConstants.kPropertiesAceMatrixFlip180Checkbox_Y);

            extractAceMatrix.Vertical = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesAceMatrixVerticalCheckbox_X, MFMEScraperConstants.kPropertiesAceMatrixVerticalCheckbox_Y);

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesAceMatrixOnColourColorbox_X, MFMEScraperConstants.kPropertiesAceMatrixOnColourColorbox_Y));
            extractAceMatrix.OnColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesAceMatrixOffColourColorbox_X, MFMEScraperConstants.kPropertiesAceMatrixOffColourColorbox_Y));
            extractAceMatrix.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesAceMatrixBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesAceMatrixBackgroundColourColorbox_Y));
            extractAceMatrix.BackgroundColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractAceMatrix);
        }

        public static void ProcessEpochMatrix(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentEpochMatrix extractEpochMatrix = new ExtractComponentEpochMatrix(componentStandardData);

            extractEpochMatrix.DotSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesEpochMatrixDotSize_X, MFMEScraperConstants.kPropertiesEpochMatrixDotSize_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesEpochMatrixOffColourColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixOffColourColorbox_Y));
            extractEpochMatrix.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesEpochMatrixOnColourLoColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixOnColourLoColorbox_Y));
            extractEpochMatrix.OnColourLo = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesEpochMatrixOnColourMedColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixOnColourMedColorbox_Y));
            extractEpochMatrix.OnColourMed = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesEpochMatrixOnColourHiColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixOnColourHiColorbox_Y));
            extractEpochMatrix.OnColourHi = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesEpochMatrixBackgroundColourColorbox_X, MFMEScraperConstants.kPropertiesEpochMatrixBackgroundColourColorbox_Y));
            extractEpochMatrix.BackgroundColour = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractEpochMatrix);
        }

        public static void ProcessBarcrestBwbVideo(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentBarcrestBwbVideo extractBarcrestBwbVideo = new ExtractComponentBarcrestBwbVideo(componentStandardData);

            extractBarcrestBwbVideo.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBarcrestBwbVideoNumber_X, MFMEScraperConstants.kPropertiesBarcrestBwbVideoNumber_Y));

            extractBarcrestBwbVideo.LeftSkew = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBarcrestBwbVideoLeftSkew_X, MFMEScraperConstants.kPropertiesBarcrestBwbVideoLeftSkew_Y));

            extractBarcrestBwbVideo.RightSkew = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBarcrestBwbVideoRightSkew_X, MFMEScraperConstants.kPropertiesBarcrestBwbVideoRightSkew_Y));

            Extractor.Layout.Components.Add(extractBarcrestBwbVideo);
        }

        public static void ProcessBfmVideo(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentBfmVideo extractBfmVideo = new ExtractComponentBfmVideo(componentStandardData);

            extractBfmVideo.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBfmVideoNumber_X, MFMEScraperConstants.kPropertiesBfmVideoNumber_Y));

            if (MfmeScraper.GetRadioButtonValue(
                MFMEScraperConstants.kPropertiesBfmVideo600x800VRadioButton_TopLeftX, MFMEScraperConstants.kPropertiesBfmVideo600x800VRadioButton_TopLeftY))
            {
                extractBfmVideo.Resolution = ExtractComponentBfmVideo.ResolutionType._600x800V;
            }
            else if (MfmeScraper.GetRadioButtonValue(
                MFMEScraperConstants.kPropertiesBfmVideo480x640VRadioButton_TopLeftX, MFMEScraperConstants.kPropertiesBfmVideo480x640VRadioButton_TopLeftY))
            {
                extractBfmVideo.Resolution = ExtractComponentBfmVideo.ResolutionType._480x640V;
            }
            else if (MfmeScraper.GetRadioButtonValue(
                MFMEScraperConstants.kPropertiesBfmVideo800x600HRadioButton_TopLeftX, MFMEScraperConstants.kPropertiesBfmVideo800x600HRadioButton_TopLeftY))
            {
                extractBfmVideo.Resolution = ExtractComponentBfmVideo.ResolutionType._800x600H;
            }
            else if (MfmeScraper.GetRadioButtonValue(
                MFMEScraperConstants.kPropertiesBfmVideo640x480HRadioButton_TopLeftX, MFMEScraperConstants.kPropertiesBfmVideo640x480HRadioButton_TopLeftY))
            {
                extractBfmVideo.Resolution = ExtractComponentBfmVideo.ResolutionType._640x480H;
            }
            else
            {
                OutputLog.LogError("Couldn't find a set radio button!");
            }

            Extractor.Layout.Components.Add(extractBfmVideo);
        }

        public static void ProcessAceVideo(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentAceVideo extractAceVideo = new ExtractComponentAceVideo(componentStandardData);

            // this component has no properties outside of ComponentStandardData

            Extractor.Layout.Components.Add(extractAceVideo);
        }

        public static void ProcessMaygayVideo(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentMaygayVideo extractMaygayVideo = new ExtractComponentMaygayVideo(componentStandardData);

            extractMaygayVideo.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesMaygayVideoNumber_X, MFMEScraperConstants.kPropertiesMaygayVideoNumber_Y));

            extractMaygayVideo.Vertical = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesMaygayVideoVerticalCheckbox_X, MFMEScraperConstants.kPropertiesMaygayVideoVerticalCheckbox_Y);

            string qualityDropdownCharacters = DelphiFontScraper.GetDropdownCharacters(
                MFMEScraperConstants.kPropertiesMaygayVideoQualityDropdown_X, MFMEScraperConstants.kPropertiesMaygayVideoQualityDropdown_Y);
            extractMaygayVideo.Quality = qualityDropdownCharacters;

            Extractor.Layout.Components.Add(extractMaygayVideo);
        }

        //        public static void ProcessPrismLamp(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentPrismLamp extractPrismLamp = new ExtractComponentPrismLamp(componentStandardData);

        //            extractPrismLamp.Lamp1NumberAsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesPrismLampLamp1Number_X, MFMEScraperConstants.kPropertiesPrismLampLamp1Number_Y);

        //            extractPrismLamp.Lamp2NumberAsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesPrismLampLamp2Number_X, MFMEScraperConstants.kPropertiesPrismLampLamp2Number_Y);

        //            extractPrismLamp.HorzSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesPrismLampHorzSpacing_X, MFMEScraperConstants.kPropertiesPrismLampHorzSpacing_Y));

        //            extractPrismLamp.VertSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesPrismLampVertSpacing_X, MFMEScraperConstants.kPropertiesPrismLampVertSpacing_Y));

        //            extractPrismLamp.Tilt = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesPrismLampTilt_X, MFMEScraperConstants.kPropertiesPrismLampTilt_Y));

        //            extractPrismLamp.Style = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesPrismLampStyleCheckbox_X, MFMEScraperConstants.kPropertiesPrismLampStyleCheckbox_Y);

        //            extractPrismLamp.Horizontal = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesPrismLampHorizontalCheckbox_X, MFMEScraperConstants.kPropertiesPrismLampHorizontalCheckbox_Y);

        //            extractPrismLamp.CentreLine = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesPrismLampCentreLineCheckbox_X, MFMEScraperConstants.kPropertiesPrismLampCentreLineCheckbox_Y);

        //            // TODO images:
        //            //public string Lamp1ImageBmpFilename;
        //            //public string Lamp1MaskBmpFilename;
        //            //public string Lamp2ImageBmpFilename;
        //            //public string Lamp2MaskBmpFilename;
        //            //public string OffImageBmpFilename;

        //            

        //            Extractor.Layout.Components.Add(extractPrismLamp);
        //        }

        //        public static void ProcessBitmap(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentBitmap extractBitmap = new ExtractComponentBitmap(componentStandardData);

        //            extractBitmap.Transparent = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesBitmapTransparentCheckbox_X, MFMEScraperConstants.kPropertiesBitmapTransparentCheckbox_Y);

        //            if (MfmeScraper.GetRadioButtonValue(
        //                MFMEScraperConstants.kPropertiesBitmapStretchFilterNearestRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterNearestRadioButton_Y))
        //            {
        //                extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Nearest;
        //            }
        //            else if (MfmeScraper.GetRadioButtonValue(
        //                MFMEScraperConstants.kPropertiesBitmapStretchFilterDraftRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterDraftRadioButton_Y))
        //            {
        //                extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Draft;
        //            }
        //            else if (MfmeScraper.GetRadioButtonValue(
        //                MFMEScraperConstants.kPropertiesBitmapStretchFilterLinearRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterLinearRadioButton_Y))
        //            {
        //                extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Linear;
        //            }
        //            else if (MfmeScraper.GetRadioButtonValue(
        //                MFMEScraperConstants.kPropertiesBitmapStretchFilterCosineRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterCosineRadioButton_Y))
        //            {
        //                extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Cosine;
        //            }
        //            else if (MfmeScraper.GetRadioButtonValue(
        //                MFMEScraperConstants.kPropertiesBitmapStretchFilterSplineRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterSplineRadioButton_Y))
        //            {
        //                extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Spline;
        //            }
        //            else if (MfmeScraper.GetRadioButtonValue(
        //                MFMEScraperConstants.kPropertiesBitmapStretchFilterLanczosRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterLanczosRadioButton_Y))
        //            {
        //                extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Lanczos;
        //            }
        //            else if (MfmeScraper.GetRadioButtonValue(
        //                MFMEScraperConstants.kPropertiesBitmapStretchFilterMitchellRadioButton_X, MFMEScraperConstants.kPropertiesBitmapStretchFilterMitchellRadioButton_Y))
        //            {
        //                extractBitmap.StretchFilter = ExtractComponentBitmap.StretchFilterType.Mitchell;
        //            }

        //            // TODO image:
        //            //public string ImageBmpFilename;

        //            

        //            Extractor.Layout.Components.Add(extractBitmap);
        //        }

        public static void ProcessSevenSegmentBlock(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentSevenSegmentBlock extractSevenSegmentBlock = new ExtractComponentSevenSegmentBlock(componentStandardData);

            extractSevenSegmentBlock.Width = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockWidth_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockWidth_Y));

            extractSevenSegmentBlock.Height = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockHeight_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockHeight_Y));

            extractSevenSegmentBlock.Columns = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockColumns_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockColumns_Y));

            extractSevenSegmentBlock.Rows = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockRows_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockRows_Y));

            extractSevenSegmentBlock.RowSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockRowSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockRowSpacing_Y));

            extractSevenSegmentBlock.ColumnSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockColumnSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockColumnSpacing_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockOnColourColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockOnColourColorbox_Y));
            extractSevenSegmentBlock.OnColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockOffColourColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockOffColourColorbox_Y));
            extractSevenSegmentBlock.OffColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockBackColourColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockBackColourColorbox_Y));
            extractSevenSegmentBlock.BackColour = new ColorJSON(color);

            extractSevenSegmentBlock.TypeAsString = DelphiFontScraper.GetDropdownCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockTypeDropdown_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockTypeDropdown_Y);

            extractSevenSegmentBlock.DPRight = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDPRightCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockDPRightCheckbox_Y);

            extractSevenSegmentBlock.FourteenSegment = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlock14SegmentCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentBlock14SegmentCheckbox_Y);

            extractSevenSegmentBlock.Thickness = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockThickness_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockThickness_Y));

            extractSevenSegmentBlock.Spacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockSpacing_Y));

            extractSevenSegmentBlock.HorzSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockHorzSizePercent_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockHorzSizePercent_Y));

            extractSevenSegmentBlock.VertSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockVertSizePercent_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockVertSizePercent_Y));

            extractSevenSegmentBlock.Offset = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockOffset_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockOffset_Y));

            extractSevenSegmentBlock.Angle = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockAngle_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockAngle_Y));

            extractSevenSegmentBlock.Slant = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockSlant_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockSlant_Y));

            extractSevenSegmentBlock.Chop = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockChop_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockChop_Y));

            extractSevenSegmentBlock.Centre = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockCenter_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockCenter_Y));

            extractSevenSegmentBlock.Centre = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockCenter_X, MFMEScraperConstants.kPropertiesSevenSegmentBlockCenter_Y));

            int digitCount = extractSevenSegmentBlock.Rows * extractSevenSegmentBlock.Columns;
            int currentDigitIndex = 0;
            int remainingDigits;
            do
            {
                // this click on the left arrow is necessary to work around MFME bug.  If previous component is a 7seg block with > 4,
                // then have scrolled to the right by clicking right arrows, on th next properties page for the nest
                // component, clicking right arrow jumps from 1 -> 3 instead of 1 -> 2:
                if (currentDigitIndex == 0)
                {
                    MFMEAutomation.LeftClickAtPosition(inputSimulator,
                        MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitTabLeftArrowCenter_X,
                        MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitTabLeftArrowCenter_Y,
                        MFMEAutomation.kLongDelay);
                }

                ProcessSevenSegmentBlockReadDigit(inputSimulator, extractSevenSegmentBlock.DigitElements[currentDigitIndex]);
                ++currentDigitIndex;

                remainingDigits = digitCount - currentDigitIndex;

                int xClickPosition = 0;
                int yClickPosition = 0;
                if (remainingDigits > 3)
                {
                    xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitTabRightArrowCenter_X;
                    yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitTabRightArrowCenter_Y;

                    MFMEAutomation.LeftClickAtPosition(inputSimulator, 
                        xClickPosition, yClickPosition, MFMEAutomation.kLongDelay);

                    // and now need to click on tab number 1 to make it active
                    xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit1TabCenter_X;
                    yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit1TabCenter_Y;
                }
                else if (remainingDigits == 3)
                {
                    xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit2TabCenter_X;
                    yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit2TabCenter_Y;
                }
                else if (remainingDigits == 2)
                {
                    xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit3TabCenter_X;
                    yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit3TabCenter_Y;
                }
                else if (remainingDigits == 1)
                {
                    xClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit4TabCenter_X;
                    yClickPosition = MFMEScraperConstants.kPropertiesSevenSegmentBlockDigit4TabCenter_Y;
                }

                if (remainingDigits > 0)
                {
                    MFMEAutomation.LeftClickAtPosition(inputSimulator,
                        xClickPosition, yClickPosition, MFMEAutomation.kLongDelay);
                }
            }
            while (remainingDigits > 0);

            

            Extractor.Layout.Components.Add(extractSevenSegmentBlock);
        }

        public static void ProcessSevenSegmentBlockReadDigit(InputSimulator inputSimulator,
            ExtractComponentSevenSegmentBlock.DigitElement digitElement)
        {
            digitElement.NumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitNumber_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitNumber_Y);

            digitElement.Programmable = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableCheckbox_Y);

            digitElement.Visible = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitVisibleCheckbox_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitVisibleCheckbox_Y);

            digitElement.DPOn = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitDPOnCheckbox_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitDPOnCheckbox_Y);

            digitElement.DPOff = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitDPOffCheckbox_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitDPOffCheckbox_Y);

            digitElement.AutoDP = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitAutoDPCheckbox_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitAutoDPCheckbox_Y);

            digitElement.ZeroOn = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitZeroOnCheckbox_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitZeroOnCheckbox_Y);

            digitElement.ProgrammableSegment1LampNumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment1Lamp_Y);

            digitElement.ProgrammableSegment2LampNumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment2Lamp_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment2Lamp_Y);

            digitElement.ProgrammableSegment3LampNumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment3Lamp_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment3Lamp_Y);

            digitElement.ProgrammableSegment4LampNumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment4Lamp_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment4Lamp_Y);

            digitElement.ProgrammableSegment5LampNumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment5Lamp_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment5Lamp_Y);

            digitElement.ProgrammableSegment6LampNumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment6Lamp_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment6Lamp_Y);

            digitElement.ProgrammableSegment7LampNumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment7Lamp_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment7Lamp_Y);

            digitElement.ProgrammableSegment8LampNumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment8Lamp_X,
                MFMEScraperConstants.kPropertiesSevenSegmentBlockDigitProgrammableSegment8Lamp_Y);
        }

        public static void ProcessBorder(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentBorder extractBorder = new ExtractComponentBorder(componentStandardData);

            extractBorder.BorderWidth = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBorderBorderWidth_X, MFMEScraperConstants.kPropertiesBorderBorderWidth_Y));

            extractBorder.Spacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesBorderSpacing_X, MFMEScraperConstants.kPropertiesBorderSpacing_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesBorderOuterColorColorbox_X, MFMEScraperConstants.kPropertiesBorderOuterColorColorbox_Y));
            extractBorder.OuterColour = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesBorderInnerColorColorbox_X, MFMEScraperConstants.kPropertiesBorderInnerColorColorbox_Y));
            extractBorder.InnerColour = new ColorJSON(color);

            extractBorder.Outer = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesBorderOuterCheckbox_X, MFMEScraperConstants.kPropertiesBorderOuterCheckbox_Y);

            extractBorder.Inner = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesBorderInnerCheckbox_X, MFMEScraperConstants.kPropertiesBorderInnerCheckbox_Y);

            Extractor.Layout.Components.Add(extractBorder);
        }

        public static void ProcessSevenSegment(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentSevenSegment extractSevenSegment = new ExtractComponentSevenSegment(componentStandardData);

            extractSevenSegment.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentNumber_X, MFMEScraperConstants.kPropertiesSevenSegmentNumber_Y));

            extractSevenSegment.DPRight = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentDPRightCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentDPRightCheckbox_Y);

            extractSevenSegment.Alpha = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentAlphaCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentAlphaCheckbox_Y);

            extractSevenSegment.DPOff = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentDPOffCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentDPOffCheckbox_Y);

            extractSevenSegment.DPOn = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentDPOnCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentDPOnCheckbox_Y);

            extractSevenSegment.AutoDP = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentAutoDPCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentAutoDPCheckbox_Y);

            extractSevenSegment.SixteenSegment = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentSixteenSegmentCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentSixteenSegmentCheckbox_Y);

            extractSevenSegment.ZeroOn = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentZeroOnCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentZeroOnCheckbox_Y);

            extractSevenSegment.TypeAsString = DelphiFontScraper.GetDropdownCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentTypeDropdown_X, MFMEScraperConstants.kPropertiesSevenSegmentTypeDropdown_Y);

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentSegmentOnColorColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentSegmentOnColorColorbox_Y));
            extractSevenSegment.SegmentOnColor = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentSegmentOffColorColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentSegmentOffColorColorbox_Y));
            extractSevenSegment.SegmentOffColor = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentSegmentBackgroundColorColorbox_X, MFMEScraperConstants.kPropertiesSevenSegmentSegmentBackgroundColorColorbox_Y));
            extractSevenSegment.SegmentBackgroundColor = new ColorJSON(color);

            extractSevenSegment.Thickness = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentThickness_X, MFMEScraperConstants.kPropertiesSevenSegmentThickness_Y));

            extractSevenSegment.Spacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentSpacing_Y));

            extractSevenSegment.HorzSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentHorzSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentHorzSpacing_Y));

            extractSevenSegment.VertSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentVertSpacing_X, MFMEScraperConstants.kPropertiesSevenSegmentVertSpacing_Y));

            extractSevenSegment.Offset = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentOffset_X, MFMEScraperConstants.kPropertiesSevenSegmentOffset_Y));

            extractSevenSegment.Angle = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentAngle_X, MFMEScraperConstants.kPropertiesSevenSegmentAngle_Y));

            extractSevenSegment.Slant = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentSlant_X, MFMEScraperConstants.kPropertiesSevenSegmentSlant_Y));

            extractSevenSegment.Chop = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentChop_X, MFMEScraperConstants.kPropertiesSevenSegmentChop_Y));

            extractSevenSegment.Centre = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentCentre_X, MFMEScraperConstants.kPropertiesSevenSegmentCentre_Y));

            extractSevenSegment.LampsProgrammable = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesSevenSegmentLampsProgrammableCheckbox_X, MFMEScraperConstants.kPropertiesSevenSegmentLampsProgrammableCheckbox_Y);

            extractSevenSegment.Lamps1AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps1_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps1_Y);

            extractSevenSegment.Lamps2AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps2_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps2_Y);

            extractSevenSegment.Lamps3AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps3_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps3_Y);

            extractSevenSegment.Lamps4AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps4_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps4_Y);

            extractSevenSegment.Lamps5AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps5_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps5_Y);

            extractSevenSegment.Lamps6AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps6_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps6_Y);

            extractSevenSegment.Lamps7AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps7_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps7_Y);

            extractSevenSegment.Lamps8AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps8_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps8_Y);

            extractSevenSegment.Lamps9AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps9_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps9_Y);

            extractSevenSegment.Lamps10AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps10_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps10_Y);

            extractSevenSegment.Lamps11AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps11_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps11_Y);

            extractSevenSegment.Lamps12AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps12_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps12_Y);

            extractSevenSegment.Lamps13AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps13_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps13_Y);

            extractSevenSegment.Lamps14AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps14_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps14_Y);

            extractSevenSegment.Lamps15AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps15_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps15_Y);

            extractSevenSegment.Lamps16AsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesSevenSegmentLamps16_X, MFMEScraperConstants.kPropertiesSevenSegmentLamps16_Y);

            Extractor.Layout.Components.Add(extractSevenSegment);
        }

        public static void ProcessAlphaNew(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentAlphaNew extractAlphaNew = new ExtractComponentAlphaNew(componentStandardData);

            extractAlphaNew.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesAlphaNewNumber_X, MFMEScraperConstants.kPropertiesAlphaNewNumber_Y));

            // charset
            if (MfmeScraper.GetRadioButtonValue(
                MFMEScraperConstants.kPropertiesAlphaNewOldCharset_X, MFMEScraperConstants.kPropertiesAlphaNewOldCharset_Y))
            {
                extractAlphaNew.CharacterSet = MFMECharacterSetType.OldCharset;
            }
            else if (MfmeScraper.GetRadioButtonValue(
                MFMEScraperConstants.kPropertiesAlphaNewOKI1937Charset_X, MFMEScraperConstants.kPropertiesAlphaNewOKI1937Charset_Y))
            {
                extractAlphaNew.CharacterSet = MFMECharacterSetType.OKI1937;
            }
            else if (MfmeScraper.GetRadioButtonValue(
                MFMEScraperConstants.kPropertiesAlphaNewBFMCharset_X, MFMEScraperConstants.kPropertiesAlphaNewBFMCharset_Y))
            {
                extractAlphaNew.CharacterSet = MFMECharacterSetType.BFMCharset;
            }
            else
            {
                OutputLog.LogError("ERROR could not find a radio button set for Segment Alpha charset type!");
                extractAlphaNew.CharacterSet = MFMECharacterSetType.OKI1937;
            }

            // 16 seg
            extractAlphaNew.SixteenSegment = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesAlphaNew16SegCheckbox_X, MFMEScraperConstants.kPropertiesAlphaNew16SegCheckbox_Y);

            // reversed
            extractAlphaNew.Reversed = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesAlphaNewReversedCheckbox_X, MFMEScraperConstants.kPropertiesAlphaNewReversedCheckbox_Y);

            // alpha on color
            Color onColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesAlphaNewOnColourbox_X, MFMEScraperConstants.kPropertiesAlphaNewOnColourbox_Y));
            extractAlphaNew.OnColor = new ColorJSON(onColor);

            Extractor.Layout.Components.Add(extractAlphaNew);
        }

        //        public static void ProcessAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentAlpha extractAlpha = new ExtractComponentAlpha(componentStandardData);

        //            extractAlpha.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesAlphaNumber_X, MFMEScraperConstants.kPropertiesAlphaNumber_Y));

        //            extractAlpha.Reversed = MfmeScraper.GetCheckboxValue(
        //                MFMEScraperConstants.kPropertiesAlphaReversedCheckbox_X, MFMEScraperConstants.kPropertiesAlphaReversedCheckbox_Y);

        //            Color color = new Color(MfmeScraper.GetColorboxValue(
        //                MFMEScraperConstants.kPropertiesAlphaOnColourbox_X, MFMEScraperConstants.kPropertiesAlphaOnColourbox_Y);
        //            extractAlpha.Color = new ColorJSON(color);

        //            extractAlpha.DigitWidth = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesAlphaDigitWidth_X, MFMEScraperConstants.kPropertiesAlphaDigitWidth_Y));

        //            extractAlpha.Columns = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesAlphaColumns_X, MFMEScraperConstants.kPropertiesAlphaColumns_Y));

        //            // save bmp of alpha:
        //            string alphaImagefilename = "alpha_"
        //                + extractAlpha.Number
        //                + ".bmp";

        //            string alphaImageSaveFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, alphaImagefilename);
        //            alphaImageSaveFullPath = alphaImageSaveFullPath.Replace("/", "\\");

        //            // save bmp image from MFME
        //            if (!File.Exists(alphaImageSaveFullPath) && !MachineConfiguration.ClassicForMAME)
        //            {
        //                FileHelper.DeleteFileAndMetafileIfFound(alphaImageSaveFullPath);

        //                // save alpha image
        //                MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper,
        //                    MFMEScraperConstants.kPropertiesAlphaImage_CenterX, MFMEScraperConstants.kPropertiesAlphaImage_CenterY);

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

        //                GUIUtility.systemCopyBuffer = alphaImageSaveFullPath;

        //                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
        //                yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

        //                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
        //            }

        //            extractAlpha.BmpImageFilename = alphaImagefilename;

        //            Extractor.Layout.Components.Add(extractAlpha);

        //            
        //        }

        public static void ProcessDotAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentDotAlpha extractDotAlpha = new ExtractComponentDotAlpha(componentStandardData);

            extractDotAlpha.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesDotAlphaNumber_X, MFMEScraperConstants.kPropertiesDotAlphaNumber_Y));

            extractDotAlpha.XSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesDotAlphaXSize_X, MFMEScraperConstants.kPropertiesDotAlphaXSize_Y));

            extractDotAlpha.YSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesDotAlphaYSize_X, MFMEScraperConstants.kPropertiesDotAlphaYSize_Y));

            extractDotAlpha.DotSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesDotAlphaDotSpacing_X, MFMEScraperConstants.kPropertiesDotAlphaDotSpacing_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotAlphaOnColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaOnColourbox_Y));
            extractDotAlpha.OnColor = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotAlphaOffColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaOffColourbox_Y));
            extractDotAlpha.OffColor = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotAlphaBackgroundColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaBackgroundColourbox_Y));
            extractDotAlpha.BackgroundColor = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractDotAlpha);
        }

        public static void ProcessMatrixAlpha(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentMatrixAlpha extractMatrixAlpha = new ExtractComponentMatrixAlpha(componentStandardData);

            extractMatrixAlpha.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesMatrixAlphaNumber_X, MFMEScraperConstants.kPropertiesMatrixAlphaNumber_Y));

            extractMatrixAlpha.XSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesMatrixAlphaXSize_X, MFMEScraperConstants.kPropertiesMatrixAlphaXSize_Y));

            extractMatrixAlpha.YSize = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesMatrixAlphaYSize_X, MFMEScraperConstants.kPropertiesMatrixAlphaYSize_Y));

            extractMatrixAlpha.DotSpacing = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesMatrixAlphaDotSpacing_X, MFMEScraperConstants.kPropertiesMatrixAlphaDotSpacing_Y));

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotAlphaOnColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaOnColourbox_Y));
            extractMatrixAlpha.OnColor = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotAlphaOffColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaOffColourbox_Y));
            extractMatrixAlpha.OffColor = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesDotAlphaBackgroundColourbox_X, MFMEScraperConstants.kPropertiesDotAlphaBackgroundColourbox_Y));
            extractMatrixAlpha.BackgroundColor = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractMatrixAlpha);
        }

        //        public static void ProcessLamp(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentLamp extractComponentLamp = new ExtractComponentLamp(componentStandardData);

        //            extractComponentLamp.NoOutline = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampNoOutlineCheckbox_X, MFMEScraperConstants.kPropertiesLampNoOutlineCheckbox_Y);

        //            extractComponentLamp.Graphic = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampGraphicCheckbox_X, MFMEScraperConstants.kPropertiesLampGraphicCheckbox_Y);

        //            extractComponentLamp.Transparent = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampTransparentCheckbox_X, MFMEScraperConstants.kPropertiesLampTransparentCheckbox_Y);

        //            extractComponentLamp.Blend = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampBlendCheckbox_X, MFMEScraperConstants.kPropertiesLampBlendCheckbox_Y);

        //            extractComponentLamp.Inverted = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampInvertedCheckbox_X, MFMEScraperConstants.kPropertiesLampInvertedCheckbox_Y);

        //            extractComponentLamp.ClickAll = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampClickAllCheckbox_X, MFMEScraperConstants.kPropertiesLampClickAllCheckbox_Y);

        //            extractComponentLamp.LED = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampLEDCheckbox_X, MFMEScraperConstants.kPropertiesLampLEDCheckbox_Y);

        //            extractComponentLamp.LockOut = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampLockOutCheckbox_X, MFMEScraperConstants.kPropertiesLampLockOutCheckbox_Y);

        //            extractComponentLamp.RGB = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampRGBCheckbox_X, MFMEScraperConstants.kPropertiesLampRGBCheckbox_Y);

        //            extractComponentLamp.PreserveAspectRatio = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesLampPreserveAspectRatioCheckbox_X, MFMEScraperConstants.kPropertiesLampPreserveAspectRatioCheckbox_Y);


        //            extractComponentLamp.ButtonNumberAsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesLampButtonNumber_X, MFMEScraperConstants.kPropertiesLampButtonNumber_Y);

        //            extractComponentLamp.CoinNote = DelphiFontScraper.GetDropdownCharacters(
        //                MFMEScraperConstants.kPropertiesLampCoinNote_X, MFMEScraperConstants.kPropertiesLampCoinNote_Y);

        //            extractComponentLamp.Effect = DelphiFontScraper.GetDropdownCharacters(
        //                MFMEScraperConstants.kPropertiesLampEffect_X, MFMEScraperConstants.kPropertiesLampEffect_Y);

        //            extractComponentLamp.InhibitLampAsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesLampInhibitLamp_X, MFMEScraperConstants.kPropertiesLampInhibitLamp_Y);

        //            extractComponentLamp.Shortcut1 = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesLampShortcut1_X, MFMEScraperConstants.kPropertiesLampShortcut1_Y);

        //            extractComponentLamp.Shortcut2 = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesLampShortcut2_X, MFMEScraperConstants.kPropertiesLampShortcut2_Y);

        //            Color color = new Color(MfmeScraper.GetColorboxValue(
        //                MFMEScraperConstants.kPropertiesLampTextColourColourbox_X, MFMEScraperConstants.kPropertiesLampTextColourColourbox_Y);
        //            extractComponentLamp.TextColor = new ColorJSON(color);

        //            color = new Color(MfmeScraper.GetColorboxValue(
        //                MFMEScraperConstants.kPropertiesLampOutlineColourColourbox_X, MFMEScraperConstants.kPropertiesLampOutlineColourColourbox_Y);
        //            extractComponentLamp.OutlineColor = new ColorJSON(color);

        //            extractComponentLamp.XOff = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesLampXOff_X, MFMEScraperConstants.kPropertiesLampXOff_Y));

        //            extractComponentLamp.YOff = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesLampYOff_X, MFMEScraperConstants.kPropertiesLampYOff_Y));

        //            extractComponentLamp.Shape = DelphiFontScraper.GetDropdownCharacters(
        //                    MFMEScraperConstants.kPropertiesLampShape_X, MFMEScraperConstants.kPropertiesLampShape_Y);

        //            extractComponentLamp.ShapeParameter1 = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesLampShapeParameter1_X, MFMEScraperConstants.kPropertiesLampShapeParameter1_Y);

        //            extractComponentLamp.ShapeParameter2 = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesLampShapeParameter2_X, MFMEScraperConstants.kPropertiesLampShapeParameter2_Y);

        //            color = new Color(MfmeScraper.GetColorboxValue(
        //                        MFMEScraperConstants.kPropertiesLampOffImageColourbox_X, MFMEScraperConstants.kPropertiesLampOffImageColourbox_Y);
        //            extractComponentLamp.OffImageColor = new ColorJSON(color);

        //            // TODO only scraping the first 4 lamps for now:
        //            //for(int lampElementIndex = 0; lampElementIndex < ExtractComponentLamp.kLampElementCount; ++lampElementIndex)
        //            for (int lampElementIndex = 0; lampElementIndex < 4; ++lampElementIndex)
        //            {
        //                yield return ProcessLampElement(inputSimulator, componentStandardData, lampElementIndex, extractComponentLamp);
        //            }

        //            Extractor.Layout.Components.Add(extractComponentLamp);

        //            
        //        }

        //        public static void ProcessLampElement(InputSimulator inputSimulator, ComponentStandardData componentStandardData,
        //            int lampElementIndex, ExtractComponentLamp extractComponentLamp)
        //        {
        //            int lampIndexX;
        //            int lampIndexY;

        //            int lampOnColorboxX;
        //            int lampOnColorboxY;

        //            int lampImageCenterX;
        //            int lampImageCenterY;
        //            int lampImageTopLeftX;
        //            int lampImageTopLeftY;

        //            int lampMaskImageCenterX;
        //            int lampMaskImageCenterY;
        //            int lampMaskImageTopLeftX;
        //            int lampMaskImageTopLeftY;

        //            GetLampData(lampElementIndex, out lampIndexX, out lampIndexY, out lampOnColorboxX, out lampOnColorboxY,
        //                out lampImageCenterX, out lampImageCenterY, out lampImageTopLeftX, out lampImageTopLeftY);

        //            GetLampMaskData(lampElementIndex, out lampMaskImageCenterX, out lampMaskImageCenterY, out lampMaskImageTopLeftX, out lampMaskImageTopLeftY);

        //            extractComponentLamp.LampElements[lampElementIndex].NumberAsText = DelphiFontScraper.GetFieldCharacters( lampIndexX, lampIndexY);

        //            int lampIndex;
        //            try
        //            {
        //                lampIndex = int.Parse(extractComponentLamp.LampElements[lampElementIndex].NumberAsText);
        //            }
        //            catch (Exception)
        //            {
        //                // not a valid number in the lamp index field, skip this lamp
        //                yield break;
        //            }

        //            Color color = new Color(MfmeScraper.GetColorboxValue( lampOnColorboxX, lampOnColorboxY);
        //            extractComponentLamp.LampElements[lampElementIndex].OnColor = new ColorJSON(color);

        //            if (MfmeScraper.IsImageBoxBlank( lampImageTopLeftX, lampImageTopLeftY,
        //                MFMEScraperConstants.kPropertiesLampImage_Width, MFMEScraperConstants.kPropertiesLampImage_Height, false))
        //            {
        //                yield break; // TOIMPROVE later, will need to implement blended lamps that do the technique of reusing image from 1st (or earlier?) spot
        //            }

        //            // TOIMPROVE - maybe doing away with the On1, On2 etc terminology?  LEDs are Red, Green, Blue etc, plus never use Off lamp type
        //            Converter.MFMELampType lampType = (Converter.MFMELampType)(lampElementIndex + 1);

        //            string lampImagefilename = "lamp_"
        //                + lampIndex + "_"
        //                + componentStandardData.Position.x + "_"
        //                + componentStandardData.Position.y + "_"
        //                + componentStandardData.Size.x + "_"
        //                + componentStandardData.Size.y + "_"
        //                + lampType.ToString()
        //                + ".bmp";

        //            string lampMaskImageFilename = Path.GetFileNameWithoutExtension(lampImagefilename) + "_mask.bmp";

        //            string saveFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, lampImagefilename);
        //            saveFullPath = saveFullPath.Replace("/", "\\");

        //            string saveMaskFullPath = Path.Combine(Converter.kMFMEConverterOutputPath, OutputDirectoryName, lampMaskImageFilename);
        //            saveMaskFullPath = saveMaskFullPath.Replace("/", "\\");

        //            // save lamp bmp image from MFME
        //            if ((DontUseExistingLamps || !File.Exists(saveFullPath)) && !MachineConfiguration.ClassicForMAME)
        //            {
        //                FileHelper.DeleteFileAndMetafileIfFound(saveFullPath);

        //                MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper, lampImageCenterX, lampImageCenterY);

        //                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_S);
        //                yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                // TIMPROVE: DO this on all other right click menus where shortcut exists!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        //                //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //                //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                //yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                // wait for file requester to intialise
        //                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);

        //                GUIUtility.systemCopyBuffer = saveFullPath;

        //                inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
        //                yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

        //                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
        //            }

        //            // TODO a check will be needed when more work is done on this, as we can sometimes have no lamp image, just 
        //            // a lamp mask image etc... for now assuming always a lamp
        //            extractComponentLamp.LampElements[lampElementIndex].BmpImageFilename = lampImagefilename;

        //            // save lamp mask bmp image from MFME if present
        //            if (!MfmeScraper.IsImageBoxBlank( lampMaskImageTopLeftX, lampMaskImageTopLeftY,
        //                MFMEScraperConstants.kPropertiesLampImage_Width, MFMEScraperConstants.kPropertiesLampImage_Height, false))
        //            {
        //                extractComponentLamp.LampElements[lampElementIndex].BmpMaskImageFilename = lampMaskImageFilename;

        //                if ((DontUseExistingLamps || !File.Exists(saveMaskFullPath)) && !MachineConfiguration.ClassicForMAME)
        //                {
        //                    FileHelper.DeleteFileAndMetafileIfFound(saveMaskFullPath);

        //                    MFMEAutomation.RightClickAtPosition(inputSimulator, this, EmulatorScraper, lampMaskImageCenterX, lampMaskImageCenterY);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_S);
        //                    yield return new WaitForSeconds(MFMEAutomation.kShortDelay);

        //                    // wait for file requester to intialise
        //                    yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);

        //                    GUIUtility.systemCopyBuffer = saveMaskFullPath;

        //                    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_V);
        //                    yield return new WaitForSeconds(MFMEAutomation.kMediumDelay);

        //                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //                    yield return new WaitForSeconds(MFMEAutomation.kVeryLongDelay);
        //                }
        //            }

        //            
        //        }

        //        private void GetLampData(int lampElementIndex, out int lampIndexX, out int lampIndexY,
        //            out int lampOnColorboxX, out int lampOnColorboxY,
        //            out int lampImageCenterX, out int lampImageCenterY, out int lampImageTopLeftX, out int lampImageTopLeftY)
        //        {
        //            const int kXShiftPixels = 152;
        //            const int kYShiftPixels = 196;

        //            lampIndexX = MFMEScraperConstants.kPropertiesLamp1Index_X;
        //            lampIndexY = MFMEScraperConstants.kPropertiesLamp1Index_Y;
        //            lampOnColorboxX = MFMEScraperConstants.kPropertiesLamp1OnColourbox_X;
        //            lampOnColorboxY = MFMEScraperConstants.kPropertiesLamp1OnColourbox_Y;
        //            lampImageCenterX = MFMEScraperConstants.kPropertiesLamp1Image_CenterX;
        //            lampImageCenterY = MFMEScraperConstants.kPropertiesLamp1Image_CenterY;
        //            lampImageTopLeftX = MFMEScraperConstants.kPropertiesLamp1Image_TopLeftX;
        //            lampImageTopLeftY = MFMEScraperConstants.kPropertiesLamp1Image_TopLeftY;

        //            switch (lampElementIndex)
        //            {
        //                case 0:
        //                case 4:
        //                case 8:
        //                    // no offset required, base reference scrape coordinates are for On1 lamp 
        //                    break;
        //                case 1:
        //                case 5:
        //                case 9:
        //                    lampIndexX += kXShiftPixels;
        //                    lampImageCenterX += kXShiftPixels;
        //                    lampImageTopLeftX += kXShiftPixels;
        //                    break;
        //                case 2:
        //                case 6:
        //                case 10:
        //                    lampIndexX -= 1; // This is because the On3 panel is incorrectly aligned in MFME - it's 1px to the left of where it should be
        //                    lampIndexY += kYShiftPixels;
        //                    lampImageCenterX -= 1; // This is because the On3 panel is incorrectly aligned in MFME - it's 1px to the left of where it should be
        //                    lampImageTopLeftX -= 1; // This is because the On3 panel is incorrectly aligned in MFME - it's 1px to the left of where it should be
        //                    lampImageCenterY += kYShiftPixels + 2;// this is because lower lamp image boxes are offset down by a further 2 pixels due to MFME mistake
        //                    lampImageTopLeftY += kYShiftPixels + 2;// this is because lower lamp image boxes are offset down by a further 2 pixels due to MFME mistake
        //                    break;
        //                case 3:
        //                case 7:
        //                case 11:
        //                    lampIndexX += kXShiftPixels;
        //                    lampImageCenterX += kXShiftPixels;
        //                    lampImageTopLeftX += kXShiftPixels;
        //                    lampIndexY += kYShiftPixels;
        //                    lampImageCenterY += kYShiftPixels;
        //                    lampImageTopLeftY += kYShiftPixels;
        //                    break;
        //                default:
        //                    UnityEngine.Debug.LogError("Lamp element index out of range");
        //                    break;
        //            }
        //        }

        //        private void GetLampMaskData(int lampElementIndex, /*out int lampIndexX, out int lampIndexY,*/
        //            out int lampMaskImageCenterX, out int lampMaskImageCenterY, out int lampMaskImageTopLeftX, out int lampMaskImageTopLeftY)
        //        {
        //            GetLampData(lampElementIndex, out int lampIndexX, out int lampIndexY, out int _, out int _,
        //                out lampMaskImageCenterX, out lampMaskImageCenterY,
        //                out lampMaskImageTopLeftX, out lampMaskImageTopLeftY);

        //            // correct image coordinates to mask image box
        //            switch (lampElementIndex)
        //            {
        //                case 0:
        //                case 4:
        //                case 8:
        //                    lampMaskImageCenterY += 70;
        //                    lampMaskImageTopLeftY += 70;
        //                    break;
        //                case 1:
        //                case 5:
        //                case 9:
        //                    lampMaskImageCenterY += 70;
        //                    lampMaskImageTopLeftY += 70;
        //                    break;
        //                case 2:
        //                case 6:
        //                case 10:
        //                    lampMaskImageCenterY += 68;
        //                    lampMaskImageTopLeftY += 68;
        //                    break;
        //                case 3:
        //                case 7:
        //                case 11:
        //                    lampMaskImageCenterY += 70;
        //                    lampMaskImageTopLeftY += 70;
        //                    break;
        //                default:
        //                    UnityEngine.Debug.LogError("Lamp element index out of range");
        //                    break;
        //            }
        //        }

        public static void ProcessCheckbox(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentCheckbox extractCheckbox = new ExtractComponentCheckbox(componentStandardData);

            extractCheckbox.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesCheckboxNumber_X, MFMEScraperConstants.kPropertiesCheckboxNumber_Y));

            Color textColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesCheckboxTextColourbox_X, MFMEScraperConstants.kPropertiesCheckboxTextColourbox_Y));
            extractCheckbox.TextColor = new ColorJSON(textColor);

            // TODO needs to be redon with the MS Sans Serif scraper:
            //if (!SkipCopyingLabelTextToIncreaseSpeed)
            //{
            //    MFMEAutomation.GetTextCoroutine(inputSimulator, this, EmulatorScraper,
            //        MFMEScraperConstants.kComponentTextBox_X, MFMEScraperConstants.kComponentTextBox_Y);
            //    extractCheckbox.Text = GUIUtility.systemCopyBuffer;
            //}

            const int kReferenceFixedMinimumWidth = 15; // use this with width to caculate X offset to scrape checkbox state on/off pixel from
            const int kReferenceTopLeftCheckboxPreviewXOffset = 641;
            const int kReferenceTopLeftCheckboxPreviewYOffset = 224;

            int checkboxWidth = extractCheckbox.Size.X;
            int widthDifferenceToReference = checkboxWidth - kReferenceFixedMinimumWidth;
            int xOffsetFromReference = (int)Math.Round((float)widthDifferenceToReference / 2, 0, MidpointRounding.AwayFromZero);
            int topLeftInteriorPixelX = kReferenceTopLeftCheckboxPreviewXOffset - xOffsetFromReference;

            extractCheckbox.State = MfmeScraper.GetCheckboxPropertiesResultPanePreviewValue(
                topLeftInteriorPixelX, kReferenceTopLeftCheckboxPreviewYOffset);

            Extractor.Layout.Components.Add(extractCheckbox);
        }

        public static void ProcessRgbLed(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentRgbLed extractRgbLed = new ExtractComponentRgbLed(componentStandardData);

            extractRgbLed.Number = int.Parse(DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesRGBLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedNumber_Y));

            extractRgbLed.RedLedNumberAsText = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesRGBLedRedLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedRedLedNumber_Y);

            extractRgbLed.GreenLedNumberAsText = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesRGBLedGreenLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedGreenLedNumber_Y);

            extractRgbLed.BlueLedNumberAsText = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesRGBLedBlueLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedBlueLedNumber_Y);

            extractRgbLed.WhiteLedNumberAsText = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesRGBLedWhiteLedNumber_X, MFMEScraperConstants.kPropertiesRGBLedWhiteLedNumber_Y);

            extractRgbLed.MuxLED = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesRGBLedMuxLEDCheckbox_X, MFMEScraperConstants.kPropertiesRGBLedMuxLEDCheckbox_Y);

            extractRgbLed.NoOutline = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesRGBLedNoOutlineCheckbox_X, MFMEScraperConstants.kPropertiesRGBLedNoOutlineCheckbox_Y);

            extractRgbLed.NoShadow = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesRGBLedNoShadowCheckbox_X, MFMEScraperConstants.kPropertiesRGBLedNoShadowCheckbox_Y);

            extractRgbLed.Style = DelphiFontScraper.GetDropdownCharacters(
                MFMEScraperConstants.kPropertiesRGBLedStyleDropdown_X, MFMEScraperConstants.kPropertiesRGBLedStyleDropdown_Y);

            Color adjustedColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesRGBLedAdjustedOffColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedOffColourbox_Y));
            extractRgbLed.AdjustedColorOff = new ColorJSON(adjustedColor);

            adjustedColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesRGBLedAdjustedRedColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedRedColourbox_Y));
            extractRgbLed.AdjustedColorRed = new ColorJSON(adjustedColor);

            adjustedColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesRGBLedAdjustedGreenColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedGreenColourbox_Y));
            extractRgbLed.AdjustedColorGreen = new ColorJSON(adjustedColor);

            adjustedColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesRGBLedAdjustedRedGreenColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedRedGreenColourbox_Y));
            extractRgbLed.AdjustedColorRedGreen = new ColorJSON(adjustedColor);

            adjustedColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesRGBLedAdjustedBlueColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedBlueColourbox_Y));
            extractRgbLed.AdjustedColorBlue = new ColorJSON(adjustedColor);

            adjustedColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesRGBLedAdjustedRedBlueColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedRedBlueColourbox_Y));
            extractRgbLed.AdjustedColorRedBlue = new ColorJSON(adjustedColor);

            adjustedColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesRGBLedAdjustedGreenBlueColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedGreenBlueColourbox_Y));
            extractRgbLed.AdjustedColorGreenBlue = new ColorJSON(adjustedColor);

            adjustedColor = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesRGBLedAdjustedRedGreenBlueColourbox_X, MFMEScraperConstants.kPropertiesRGBLedAdjustedRedGreenBlueColourbox_Y));
            extractRgbLed.AdjustedColorRedGreenBlue = new ColorJSON(adjustedColor);

            Extractor.Layout.Components.Add(extractRgbLed);
        }

        public static void ProcessLed(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentLed extractLed = new ExtractComponentLed(componentStandardData);

            extractLed.NumberAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesLedNumber_X, MFMEScraperConstants.kPropertiesLedNumber_Y);

            extractLed.Led = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesLedLedCheckbox_X, MFMEScraperConstants.kPropertiesLedLedCheckbox_Y);

            extractLed.DigitAsString = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesLedDigit_X, MFMEScraperConstants.kPropertiesLedDigit_Y);

            extractLed.Segment = DelphiFontScraper.GetDropdownCharacters(
                MFMEScraperConstants.kPropertiesLedSegmentDropdown_X, MFMEScraperConstants.kPropertiesLedSegmentDropdown_Y);

            Color color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesLedOnColourbox_X, MFMEScraperConstants.kPropertiesLedOnColourbox_Y));
            extractLed.OnColor = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                MFMEScraperConstants.kPropertiesLedOffColourbox_X, MFMEScraperConstants.kPropertiesLedOffColourbox_Y));
            extractLed.OffColor = new ColorJSON(color);

            extractLed.NoOutline = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesLedNoOutlineCheckbox_X, MFMEScraperConstants.kPropertiesLedNoOutlineCheckbox_Y);

            extractLed.NoShadow = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesLedNoShadowCheckbox_X, MFMEScraperConstants.kPropertiesLedNoShadowCheckbox_Y);

            extractLed.Style = DelphiFontScraper.GetDropdownCharacters(
                MFMEScraperConstants.kPropertiesLedStyleDropdown_X, MFMEScraperConstants.kPropertiesLedStyleDropdown_Y);

            Extractor.Layout.Components.Add(extractLed);
        }

        public static void ProcessFrame(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentFrame extractFrame = new ExtractComponentFrame(componentStandardData);

            extractFrame.ShapeAsString = DelphiFontScraper.GetDropdownCharacters(
                MFMEScraperConstants.kPropertiesFrameShapeDropdown_X, MFMEScraperConstants.kPropertiesFrameShapeDropdown_Y);

            extractFrame.BevelAsString = DelphiFontScraper.GetDropdownCharacters(
                MFMEScraperConstants.kPropertiesFrameBevelDropdown_X, MFMEScraperConstants.kPropertiesFrameBevelDropdown_Y);

            Extractor.Layout.Components.Add(extractFrame);
        }

        public static void ProcessLabel(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        {
            ExtractComponentLabel extractLabel = new ExtractComponentLabel(componentStandardData);

            extractLabel.LampNumberAsText = DelphiFontScraper.GetFieldCharacters(
                MFMEScraperConstants.kPropertiesLabelLampNumber_X, MFMEScraperConstants.kPropertiesLabelLampNumber_Y);

            extractLabel.Transparent = MfmeScraper.GetCheckboxValue(
                MFMEScraperConstants.kPropertiesLabelTransparentCheckbox_X, MFMEScraperConstants.kPropertiesLabelTransparentCheckbox_Y);

            Color color = new Color(MfmeScraper.GetColorboxValue(
                        MFMEScraperConstants.kPropertiesLabelTextColourbox_X, MFMEScraperConstants.kPropertiesLabelTextColourbox_Y));
            extractLabel.TextColor = new ColorJSON(color);

            color = new Color(MfmeScraper.GetColorboxValue(
                        MFMEScraperConstants.kPropertiesLabelBackgroundColourbox_X, MFMEScraperConstants.kPropertiesLabelBackgroundColourbox_Y));
            extractLabel.BackgroundColor = new ColorJSON(color);

            Extractor.Layout.Components.Add(extractLabel);
        }

        //        public static void ProcessButton(InputSimulator inputSimulator, ComponentStandardData componentStandardData)
        //        {
        //            ExtractComponentButton extractButton = new ExtractComponentButton(componentStandardData);

        //            // TODO Extract the lamp image and mask image if present

        //            extractButton.LampElements[0].NumberAsText = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesButtonLampElement0LampNumber_X, MFMEScraperConstants.kPropertiesButtonLampElement0LampNumber_Y);

        //            Color color = new Color(MfmeScraper.GetColorboxValue(
        //                MFMEScraperConstants.kPropertiesButtonLampElement0Color_X, MFMEScraperConstants.kPropertiesButtonLampElement0Color_Y);
        //            extractButton.LampElements[0].OnColor = new ColorJSON(color);

        //            extractButton.LampElements[1].NumberAsText = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesButtonLampElement1LampNumber_X, MFMEScraperConstants.kPropertiesButtonLampElement1LampNumber_Y);

        //            color = new Color(MfmeScraper.GetColorboxValue(
        //                MFMEScraperConstants.kPropertiesButtonLampElement1Color_X, MFMEScraperConstants.kPropertiesButtonLampElement1Color_Y);
        //            extractButton.LampElements[1].OnColor = new ColorJSON(color);

        //            extractButton.ButtonNumberAsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesButtonButtonNumber_X, MFMEScraperConstants.kPropertiesButtonButtonNumber_Y);

        //            extractButton.CoinNote = DelphiFontScraper.GetDropdownCharacters(
        //                MFMEScraperConstants.kPropertiesButtonCoinNote_X, MFMEScraperConstants.kPropertiesButtonCoinNote_Y);

        //            extractButton.Effect = DelphiFontScraper.GetDropdownCharacters(
        //                MFMEScraperConstants.kPropertiesButtonEffect_X, MFMEScraperConstants.kPropertiesButtonEffect_Y);

        //            extractButton.InhibitLampAsString = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesButtonInhibitLamp_X, MFMEScraperConstants.kPropertiesButtonInhibitLamp_Y);

        //            extractButton.Shortcut1 = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesButtonShortcut1_X, MFMEScraperConstants.kPropertiesButtonShortcut1_Y);

        //            extractButton.Shortcut2 = DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesButtonShortcut2_X, MFMEScraperConstants.kPropertiesButtonShortcut2_Y);

        //            extractButton.Graphic = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesButtonGraphicCheckbox_X, MFMEScraperConstants.kPropertiesButtonGraphicCheckbox_Y);

        //            extractButton.Inverted = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesButtonInvertedCheckbox_X, MFMEScraperConstants.kPropertiesButtonInvertedCheckbox_Y);

        //            extractButton.Split = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesButtonSplitCheckbox_X, MFMEScraperConstants.kPropertiesButtonSplitCheckbox_Y);

        //            extractButton.LockOut = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesButtonLockOutCheckbox_X, MFMEScraperConstants.kPropertiesButtonLockOutCheckbox_Y);

        //            extractButton.LED = MfmeScraper.GetCheckboxValue(
        //                EmulatorScraper, MFMEScraperConstants.kPropertiesButtonLEDCheckbox_X, MFMEScraperConstants.kPropertiesButtonLEDCheckbox_Y);

        //            extractButton.XOff = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesButtonXOff_X, MFMEScraperConstants.kPropertiesButtonXOff_Y));

        //            extractButton.YOff = int.Parse(DelphiFontScraper.GetFieldCharacters(
        //                MFMEScraperConstants.kPropertiesButtonYOff_X, MFMEScraperConstants.kPropertiesButtonYOff_Y));

        //            color = new Color(MfmeScraper.GetColorboxValue(
        //                        MFMEScraperConstants.kPropertiesButtonTextColourbox_X, MFMEScraperConstants.kPropertiesButtonTextColourbox_Y);
        //            extractButton.TextColor = new ColorJSON(color);

        //            extractButton.ShapeAsString = DelphiFontScraper.GetDropdownCharacters(
        //                MFMEScraperConstants.kPropertiesButtonShapeDropdown_X, MFMEScraperConstants.kPropertiesButtonShapeDropdown_Y);

        //            color = new Color(MfmeScraper.GetColorboxValue(
        //                        MFMEScraperConstants.kPropertiesButtonOffImageColourbox_X, MFMEScraperConstants.kPropertiesButtonOffImageColourbox_Y);
        //            extractButton.OffImageColor = new ColorJSON(color);

        //            Extractor.Layout.Components.Add(extractButton);

        //            
        //        }
    }
}
