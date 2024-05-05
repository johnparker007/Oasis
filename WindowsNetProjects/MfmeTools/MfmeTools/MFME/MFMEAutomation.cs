using Oasis.MfmeTools.UnityWrappers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WindowsInput;

namespace Oasis.MfmeTools.Shared.Mfme
{
    public static class MFMEAutomation
    {
        public static readonly int kVeryShortDelay = (int)(0.05f * 1000);
        public static readonly int kShortDelay = (int)(0.1f * 1000);
        public static readonly int kMediumDelay = (int)(0.25f * 1000);
        public static readonly int kLongDelay = (int)(0.5f * 1000);
        public static readonly int kVeryLongDelay = (int)(1.0f * 1000);

        // TODO these need to be worked out exactly:
        public static readonly int kXOffsetToGetToTopLeftOfLayout = 12;
        public static readonly int kYOffsetToGetToTopLeftOfLayout = 90;

        public static readonly int kPixelOffsetForTextInput = 4;


        public static readonly int kAdjustSize_X = 89;
        public static readonly int kAdjustSizeX_Y = 66;
        public static readonly int kAdjustSizeY_Y = 97;

        public static bool PreviousComponentNavigationTimedOut
        {
            get;
            private set;
        }

        //public static void CenterMouseOnScreen(InputSimulator inputSimulator)
        //{
        //    int clickPositionPixelX = 1920 / 2;
        //    int clickPositionPixelY = 1080 / 2;
        //    double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
        //    double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
        //    inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
        //    Thread.Sleep(kShortDelay);
        //}

        public static double GetMouseCoordinateX(int screenPixelX)
        {
            const int kScreenWidth = 1920; //  TODO find way to get actual display width (not Game window width)
            return ((float)screenPixelX / kScreenWidth) * 65535;
        }

        public static double GetMouseCoordinateY(int screenPixelY)
        {
            const int kScreenHeight = 1080; //  TODO find way to get actual display height (not Game window height)
            return ((float)screenPixelY / kScreenHeight) * 65535;
        }

        //public static void StopEmulationFromDebugMenu(InputSimulator inputSimulator)
        //{
        //    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_E);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_M);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.F4);
        //    Thread.Sleep(kShortDelay);
        //}

        public static void ToggleEditMode(InputSimulator inputSimulator)
        {
            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.CONTROL, WindowsInput.Native.VirtualKeyCode.VK_E);
            Thread.Sleep(kShortDelay);
        }

        //public static void DeleteAllOverlaysFromDesignMenu(InputSimulator inputSimulator)
        //{
        //    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_D);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_T);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_E);
        //    Thread.Sleep(kShortDelay);
        //}

        //public static void DragDownFromTopLeftCornerToMoveUnwantedCheckboxEtc(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
        //{
        //    int windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
        //    int windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
        //    int clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout;
        //    int clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout;
        //    double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
        //    double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
        //    inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Mouse.LeftButtonDown();
        //    Thread.Sleep(kShortDelay);

        //    windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
        //    windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
        //    clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout;
        //    clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout + 16; // move any checkbox etc down out the way of future right mouse clicks
        //    mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
        //    mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
        //    inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Mouse.LeftButtonUp();
        //    Thread.Sleep(kShortDelay);
        //}

        //public static void KillMFMEProcessIfNotExited(Process mfmeProcess)
        //{
        //    if (mfmeProcess != null && !mfmeProcess.HasExited)
        //    {
        //        mfmeProcess.Kill();
        //    }
        //}

        public static void CopyOffLampsToBackground(InputSimulator inputSimulator)
        {
            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_D);
            Thread.Sleep(kShortDelay);

            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_T);
            Thread.Sleep(kShortDelay);

            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_P);
            Thread.Sleep(kShortDelay);
        }

        public static void OpenPropertiesWindow(InputSimulator inputSimulator,
            bool clickTopLeftOfLayoutInsteadOfCurrentPosition)
        {
            if (clickTopLeftOfLayoutInsteadOfCurrentPosition)
            {
                RightClickOnTopLeftCornerOfLayout(inputSimulator);
            }
            else
            {
                inputSimulator.Mouse.RightButtonClick();
                Thread.Sleep(kShortDelay);
            }

            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
            Thread.Sleep(kShortDelay);

            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
            Thread.Sleep(kShortDelay);

            // Can't do this as on certain components, 'P' selects a new Lam[p]s menu option (see Dennis the Menace classic layout)
            //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_P);
            //Thread.Sleep(kShortDelay);

// TODO will need alt version of this for new MfmeTools scrape system:
//            emulatorScraper.SetScrapeChildIfFound(true, "Properties");

// TODO CAN THIS BE REMOVED AS WE'RE GOING TO SCRAPE VIA MY DELPHI PIXEL FONT SCRAPER?
            // move current selection from tab to text box in lower right so it doesn't mess with OCRing first component type
            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LSHIFT, WindowsInput.Native.VirtualKeyCode.TAB);
            Thread.Sleep(kShortDelay);
        }

        //public static void LeftClickOnEmptyAreaOfDataLayout(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
        //{
        //    int windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
        //    int windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
        //    int clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout + MFMEScraperConstants.kDataLayoutEmptyArea_X;
        //    int clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout + MFMEScraperConstants.kDataLayoutEmptyArea_Y;
        //    double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
        //    double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
        //    inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Mouse.LeftButtonClick();
        //    Thread.Sleep(kShortDelay);
        //}

        public static void RightClickOnTopLeftCornerOfLayout(InputSimulator inputSimulator)
        {
            //int windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
            //int windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
            int windowPixelX = MfmeScraper.MainForm.Rect.X;
            int windowPixelY = MfmeScraper.MainForm.Rect.Y;
            int clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout;
            int clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout;
            // fudge so that at least for the extractor, we can extract narrow window layouts where the menu bar spills across two rows
            clickPositionPixelY += 100; // should always be enough even for exceptionally narrow layouts

            double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
            double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
            inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
            Thread.Sleep(kShortDelay);

            inputSimulator.Mouse.RightButtonClick();
            Thread.Sleep(kShortDelay);
        }

        //public static void GetTextCoroutine(InputSimulator inputSimulator, EmulatorScraper emulatorScraper,
        //    int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY)
        //{
        //    GUIUtility.systemCopyBuffer = "";

        //    LeftClickAtPosition(inputSimulator, emulatorScraper, mouseCoordinateWithinWindowX, mouseCoordinateWithinWindowY);

        //    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

        //    Thread.Sleep(kVeryShortDelay);

        //    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_C);

        //    Thread.Sleep(kVeryShortDelay);
        //}

        public static void LeftClickAtPosition(InputSimulator inputSimulator, 
            int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, int? overrideDelay = null)
        {
            ClickAtPosition(true, inputSimulator, mouseCoordinateWithinWindowX, mouseCoordinateWithinWindowY, overrideDelay);
        }

        public static void RightClickAtPosition(InputSimulator inputSimulator, 
            int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, int? overrideDelay = null)
        {
            ClickAtPosition(false, inputSimulator, mouseCoordinateWithinWindowX, mouseCoordinateWithinWindowY, overrideDelay);
        }

        public static void ClickAtPosition(bool leftClick, InputSimulator inputSimulator, 
            int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, int? overrideDelay = null)
        {
// XXX TEMP HACK!  TO BE REMOVED!!!
mouseCoordinateWithinWindowX += 5;


            inputSimulator.Mouse.MoveMouseTo(
                 GetMouseCoordinateWithinWindowX(mouseCoordinateWithinWindowX),
                 GetMouseCoordinateWithinWindowY(mouseCoordinateWithinWindowY));

            Thread.Sleep(kShortDelay);

            if (leftClick)
            {
                inputSimulator.Mouse.LeftButtonClick();
            }
            else
            {
                inputSimulator.Mouse.RightButtonClick();
            }

            if (overrideDelay.HasValue)
            {
                Thread.Sleep(overrideDelay.Value);
            }
            else
            {
                Thread.Sleep(kShortDelay);
            }
        }

        public static double GetMouseCoordinateWithinWindowX(int withinWindowPixelX)
        {
            int screenPixelX = GetScreenPixelX(withinWindowPixelX);
            return GetMouseCoordinateX(screenPixelX);
        }

        public static double GetMouseCoordinateWithinWindowY(int withinWindowPixelY)
        {
            int screenPixelY = GetScreenPixelY(withinWindowPixelY);
            return GetMouseCoordinateY(screenPixelY);
        }

        public static int GetScreenPixelX(int withinWindowPixelX)
        {
            int windowPixelX = MfmeScraper.CurrentWindow.Rect.X;
            int screenPixelX = windowPixelX + withinWindowPixelX;

            return screenPixelX;
        }

        public static int GetScreenPixelY(int withinWindowPixelY)
        {
            int windowPixelY = MfmeScraper.CurrentWindow.Rect.Y;
            int screenPixelY = windowPixelY + withinWindowPixelY;

            return screenPixelY;
        }

        public static MFMEConstants.MFMEComponentType GetMFMEComponentType(string scrapedComponentName)
        {
            switch (scrapedComponentName)
            {
                case "Background":
                    return MFMEConstants.MFMEComponentType.Background;
                case "Matrix Alpha":
                    return MFMEConstants.MFMEComponentType.MatrixAlpha;
                case "Seven Segment":
                    return MFMEConstants.MFMEComponentType.SevenSegment;
                case "Seven Segment Block":
                    return MFMEConstants.MFMEComponentType.SevenSegmentBlock;
                case "Ree": // current Delphi font scraper can't scrap final 'l' ("Reel")
                    return MFMEConstants.MFMEComponentType.Reel;
                case "Lamp":
                    return MFMEConstants.MFMEComponentType.Lamp;
                case "CheckBox":
                    return MFMEConstants.MFMEComponentType.Checkbox;
                case "Labe": // current Delphi font scraper can't scrap final 'l' ("Label")
                    return MFMEConstants.MFMEComponentType.Label;
                case "Button":
                    return MFMEConstants.MFMEComponentType.Button;
                case "LED":
                    return MFMEConstants.MFMEComponentType.Led;
                case "RGB Led": 
                    return MFMEConstants.MFMEComponentType.RgbLed;
                case "Dot Alpha":
                    return MFMEConstants.MFMEComponentType.DotAlpha;
                case "Alpha New":
                    return MFMEConstants.MFMEComponentType.AlphaNew;
                case "Alpha":
                    return MFMEConstants.MFMEComponentType.Alpha;
                case "Frame":
                    return MFMEConstants.MFMEComponentType.Frame;
                case "Band Ree": // current Delphi font scraper can't scrap final 'l' ("Band Reel")
                    return MFMEConstants.MFMEComponentType.BandReel;
                case "Disc Ree": // current Delphi font scraper can't scrap final 'l' ("Disc Reel")
                    return MFMEConstants.MFMEComponentType.DiscReel;
                case "FlipRee": // current Delphi font scraper can't scrap final 'l' ("FlipReel")
                    return MFMEConstants.MFMEComponentType.FlipReel;
                case "Reel Bonus Ree": // current Delphi font scraper can't scrap final 'l' ("Reel Bonus Reel")
                    return MFMEConstants.MFMEComponentType.JpmBonusReel;
                case "BFM Alpha":
                    return MFMEConstants.MFMEComponentType.BfmAlpha;
                case "Proconn Matrix":
                    return MFMEConstants.MFMEComponentType.ProconnMatrix;
                case "Epoch Alpha":
                    return MFMEConstants.MFMEComponentType.EpochAlpha;
                case "IGT VFD": 
                    return MFMEConstants.MFMEComponentType.IgtVfd;
                case "Plasma":
                    return MFMEConstants.MFMEComponentType.Plasma;
                case "Dot Matrix":
                    return MFMEConstants.MFMEComponentType.DotMatrix;
                case "BFM Led":
                    return MFMEConstants.MFMEComponentType.BfmLed;
                case "BFMColourLed":
                    return MFMEConstants.MFMEComponentType.BfmColourLed;
                case "Ace Matrix":
                    return MFMEConstants.MFMEComponentType.AceMatrix;
                case "Epoch Matrix":
                    return MFMEConstants.MFMEComponentType.EpochMatrix;
                case "Video": 
                    return MFMEConstants.MFMEComponentType.BarcrestBwbVideo;
                case "BFM Video":
                    return MFMEConstants.MFMEComponentType.BfmVideo;
                case "ACE Video":
                    return MFMEConstants.MFMEComponentType.AceVideo;
                case "Maygay Video":
                    return MFMEConstants.MFMEComponentType.MaygayVideo;
                case "Prism Lamp":
                    return MFMEConstants.MFMEComponentType.PrismLamp;
                case "Bitmap":
                    return MFMEConstants.MFMEComponentType.Bitmap;
                case "Border":
                    return MFMEConstants.MFMEComponentType.Border;
                default:
                    OutputLog.LogError("Component not found!  Scraped component name: " + scrapedComponentName);
                    return MFMEConstants.MFMEComponentType.None;
            }
        }

        public static void DoWorkaroundFixForMFMEComponentHeightBugAfterAlphaNewComponent(
            InputSimulator inputSimulator)
        {
            // enter something in the angle field as we have already scraped that for this component
            // (doesn't interfere with the rest of our scraping) - just to get the Undo button available
            SetTextToNumber(inputSimulator,
                MFMEScraperConstants.kComponentPositionAngle_X, MFMEScraperConstants.kComponentPositionAngle_Y + 4, 0);

            // click undo
            LeftClickAtPosition(inputSimulator,
                MFMEScraperConstants.kPropertiesUndoButton_X, MFMEScraperConstants.kPropertiesUndoButton_Y, kLongDelay);

            // now the Component Height value will be correct, ready for scraping, instead of '50'.
            // Fix for MFME bug where components before/after AlphaNew component will have height of '50' in the Component Height field,
            // completely unrelated to their actual height.
        }


        public static void SetTextToNumber(
            InputSimulator inputSimulator, 
            int mouseCoordinateWithinWindowX,
            int mouseCoordinateWithinWindowY,
            int number,
            int offsetFromTopLeftInteriorPixels = 4)
        {
            LeftClickAtPosition(inputSimulator, 
                mouseCoordinateWithinWindowX + offsetFromTopLeftInteriorPixels, mouseCoordinateWithinWindowY + offsetFromTopLeftInteriorPixels);

            inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

            string numberString = number.ToString();
            for (int characterIndex = 0; characterIndex < numberString.Length; ++characterIndex)
            {
                string singleNumberString = numberString.Substring(characterIndex, 1);
                if (singleNumberString == "-")
                {
                    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.OEM_MINUS);
                }
                else
                {
                    int characterValue = int.Parse(singleNumberString);
                    int virtualKeyCode = (int)WindowsInput.Native.VirtualKeyCode.VK_0 + characterValue;

                    inputSimulator.Keyboard.KeyPress((WindowsInput.Native.VirtualKeyCode)virtualKeyCode);
                }

                Thread.Sleep(kVeryShortDelay);
            }
        }

        //public static void SetDropdownToValue(
        //    InputSimulator inputSimulator, EmulatorScraper emulatorScraper,
        //    int dropdownPositionX, int dropdownPositionY, string value, bool resetToTopOfList)
        //{
        //    LeftClickAtPosition(inputSimulator, emulatorScraper,
        //        dropdownPositionX + kPixelOffsetForTextInput,
        //        dropdownPositionY + kPixelOffsetForTextInput);

        //    if (resetToTopOfList)
        //    {
        //        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.HOME);
        //        Thread.Sleep(kLongDelay);
        //    }

        //    while (MFMEScraper.GetDropdownCharacters(emulatorScraper, dropdownPositionX, dropdownPositionY).TrimEnd() != value)
        //    {
        //        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //        Thread.Sleep(kShortDelay);
        //    }

        //    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //    Thread.Sleep(kShortDelay);
        //}

        public static void ClickPropertiesComponentPreviousUntilOnFirstComponent(InputSimulator inputSimulator)
        {
            PreviousComponentNavigationTimedOut = false;

            do
            {
                ClickPropertiesComponentPrevious(inputSimulator);
            }
            while (PreviousComponentNavigationTimedOut == false);

            OutputLog.Log("Reached zeroth component");
        }

        public static void ClickPropertiesComponentNext(InputSimulator inputSimulator)
        {
            ClickPropertiesComponentNavigationArrow(inputSimulator, true);
        }

        public static void ClickPropertiesComponentPrevious(InputSimulator inputSimulator)
        {
            ClickPropertiesComponentNavigationArrow(inputSimulator, false);
        }

        private static void ClickPropertiesComponentNavigationArrow(
            InputSimulator inputSimulator, bool clickNext, float timeout = 1.0f)
        {
            MfmeScraper.CurrentWindow.UpdateCapture();

            // capture initial 'Z order:' area pixels, click with no post delay, loop capturing pixels until they change from the initial zorder pixels 
            Color32[] initialZOrderPixels =
                MfmeScraper.CurrentWindow.GetPixels(
                MFMEScraperConstants.kPropertiesZOrder_X, MFMEScraperConstants.kPropertiesZOrder_Y,
                MFMEScraperConstants.kPropertiesZOrder_Width, MFMEScraperConstants.kPropertiesZOrder_Height);

            if (clickNext)
            {
                LeftClickAtPosition(inputSimulator,
                    MFMEScraperConstants.kPropertiesNextButton_X, MFMEScraperConstants.kPropertiesNextButton_Y, 0);
            }
            else
            {
                LeftClickAtPosition(inputSimulator,
                    MFMEScraperConstants.kPropertiesPreviousButton_X, MFMEScraperConstants.kPropertiesPreviousButton_Y, 0);
            }

            float elapsed = 0f;
            const int kFixedDeltaSleepMilliseconds = 17;
            bool currentZOrderPixelsChanged = false;
            do
            {
                Thread.Sleep(kFixedDeltaSleepMilliseconds);
                elapsed += kFixedDeltaSleepMilliseconds / 1000f;

                MfmeScraper.CurrentWindow.UpdateCapture();

                Color32[] currentZOrderPixels = MfmeScraper.CurrentWindow.GetPixels(
                                MFMEScraperConstants.kPropertiesZOrder_X, MFMEScraperConstants.kPropertiesZOrder_Y,
                                MFMEScraperConstants.kPropertiesZOrder_Width, MFMEScraperConstants.kPropertiesZOrder_Height);

                for (int zOrderPixelIndex = 0;
                    zOrderPixelIndex < MFMEScraperConstants.kPropertiesZOrder_Width * MFMEScraperConstants.kPropertiesZOrder_Height;
                    ++zOrderPixelIndex)
                {
                    // only need to check a single channel to detect change to Zorder text
                    if (currentZOrderPixels[zOrderPixelIndex].r != initialZOrderPixels[zOrderPixelIndex].r)
                    {
                        currentZOrderPixelsChanged = true;
                    }
                }
            }
            while (!currentZOrderPixelsChanged && elapsed < timeout);

            PreviousComponentNavigationTimedOut = elapsed >= timeout;
        }

    }
}
