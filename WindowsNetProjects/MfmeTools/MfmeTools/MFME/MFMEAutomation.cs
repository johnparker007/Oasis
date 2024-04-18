using MfmeTools.UnityStructWrappers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WindowsInput;

namespace MfmeTools.Mfme
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

        //public static bool PreviousComponentNavigationTimedOut
        //{
        //    get;
        //    private set;
        //}

        //public static void CenterMouseOnScreen(InputSimulator inputSimulator)
        //{
        //    int clickPositionPixelX = 1920 / 2;
        //    int clickPositionPixelY = 1080 / 2;
        //    double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
        //    double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
        //    inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
        //    Thread.Sleep(kShortDelay);
        //}

        //public static double GetMouseCoordinateX(int screenPixelX)
        //{
        //    const int kScreenWidth = 1920; //  TODO find way to get actual display width (not Game window width)
        //    return ((float)screenPixelX / kScreenWidth) * 65535;
        //}

        //public static double GetMouseCoordinateY(int screenPixelY)
        //{
        //    const int kScreenHeight = 1080; //  TODO find way to get actual display height (not Game window height)
        //    return ((float)screenPixelY / kScreenHeight) * 65535;
        //}

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

        public static void EnableEditModeFromDesignMenu(InputSimulator inputSimulator)
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

        //public static void OpenPropertiesWindow(InputSimulator inputSimulator,
        //    EmulatorScraper emulatorScraper, bool clickTopLeftOfLayoutInsteadOfCurrentPosition)
        //{
        //    if (clickTopLeftOfLayoutInsteadOfCurrentPosition)
        //    {
        //        RightClickOnTopLeftCornerOfLayout(inputSimulator, emulatorScraper);
        //    }
        //    else
        //    {
        //        inputSimulator.Mouse.RightButtonClick();
        //        Thread.Sleep(kShortDelay);
        //    }

        //    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
        //    Thread.Sleep(kShortDelay);

        //    // Can't do this as on certain components, 'P' selects a new Lam[p]s menu option (see Dennis the Menace classic layout)
        //    //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_P);
        //    //Thread.Sleep(kShortDelay);

        //    emulatorScraper.SetScrapeChildIfFound(true, "Properties");

        //    // move current selection from tab to text box in lower right so it doesn't mess with OCRing first component type
        //    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LSHIFT, WindowsInput.Native.VirtualKeyCode.TAB);
        //    Thread.Sleep(kShortDelay);
        //}

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

        //public static void RightClickOnTopLeftCornerOfLayout(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
        //{
        //    int windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
        //    int windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
        //    int clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout;
        //    int clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout;
        //    // fudge so that at least for the extractor, we can extract narrow window layouts where the menu bar spills across two rows
        //    clickPositionPixelY += 100; // should always be enough even for exceptionally narrow layouts

        //    double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
        //    double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
        //    inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
        //    Thread.Sleep(kShortDelay);

        //    inputSimulator.Mouse.RightButtonClick();
        //    Thread.Sleep(kShortDelay);
        //}

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

        //public static void LeftClickAtPosition(InputSimulator inputSimulator, EmulatorScraper emulatorScraper,
        //    int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, int? overrideDelay = null)
        //{
        //    ClickAtPosition(true, inputSimulator, emulatorScraper, mouseCoordinateWithinWindowX, mouseCoordinateWithinWindowY, overrideDelay);
        //}

        //public static void RightClickAtPosition(InputSimulator inputSimulator, EmulatorScraper emulatorScraper,
        //    int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, int? overrideDelay = null)
        //{
        //    ClickAtPosition(false, inputSimulator, emulatorScraper, mouseCoordinateWithinWindowX, mouseCoordinateWithinWindowY, overrideDelay);
        //}

        //public static void ClickAtPosition(bool leftClick, InputSimulator inputSimulator, EmulatorScraper emulatorScraper,
        //    int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, int? overrideDelay = null)
        //{
        //    inputSimulator.Mouse.MoveMouseTo(
        //         GetMouseCoordinateWithinWindowX(mouseCoordinateWithinWindowX, emulatorScraper),
        //         GetMouseCoordinateWithinWindowY(mouseCoordinateWithinWindowY, emulatorScraper));

        //    Thread.Sleep(kShortDelay);

        //    if (leftClick)
        //    {
        //        inputSimulator.Mouse.LeftButtonClick();
        //    }
        //    else
        //    {
        //        inputSimulator.Mouse.RightButtonClick();
        //    }

        //    if (overrideDelay.HasValue)
        //    {
        //        Thread.Sleep(overrideDelay.Value);
        //    }
        //    else
        //    {
        //        Thread.Sleep(kShortDelay);
        //    }
        //}

        //public static double GetMouseCoordinateWithinWindowX(int withinWindowPixelX, EmulatorScraper emulatorScraper)
        //{
        //    int screenPixelX = GetScreenPixelX(withinWindowPixelX, emulatorScraper);
        //    return GetMouseCoordinateX(screenPixelX);
        //}

        //public static double GetMouseCoordinateWithinWindowY(int withinWindowPixelY, EmulatorScraper emulatorScraper)
        //{
        //    int screenPixelY = GetScreenPixelY(withinWindowPixelY, emulatorScraper);
        //    return GetMouseCoordinateY(screenPixelY);
        //}

        //public static int GetScreenPixelX(int withinWindowPixelX, EmulatorScraper emulatorScraper)
        //{
        //    int windowPixelX = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.x;
        //    int sreenPixelX = windowPixelX + withinWindowPixelX;

        //    return sreenPixelX;
        //}

        //public static int GetScreenPixelY(int withinWindowPixelY, EmulatorScraper emulatorScraper)
        //{
        //    int windowPixelY = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.y;
        //    int sreenPixelY = windowPixelY + withinWindowPixelY;

        //    return sreenPixelY;
        //}

        //public static MFMEConstants.MFMEComponentType GetMFMEComponentType(EmulatorScraper emulatorScraper, string scrapedComponentNameOCR)
        //{
        //    string scrapedComponentNameTrimmed = scrapedComponentNameOCR.TrimEnd(' ', ':');

        //    //UnityEngine.Debug.LogError("scrapedComponentNameTrimmed: " + scrapedComponentNameTrimmed);

        //    string mfmeComponentTypeTextDelphiScrape;
        //    switch (scrapedComponentNameTrimmed)
        //    {
        //        case "Background":
        //            return MFMEConstants.MFMEComponentType.Background;
        //        case "Matrix Alpha":
        //            return MFMEConstants.MFMEComponentType.MatrixAlpha;
        //        case "Seven Segment":
        //            mfmeComponentTypeTextDelphiScrape = MFMEScraper.GetFieldCharacters(emulatorScraper, 9, 31, 20);
        //            if (mfmeComponentTypeTextDelphiScrape == "Seven Segment Block")
        //            {
        //                return MFMEConstants.MFMEComponentType.SevenSegmentBlock;
        //            }

        //            return MFMEConstants.MFMEComponentType.SevenSegment;
        //        case "Reel":
        //            return MFMEConstants.MFMEComponentType.Reel;
        //        case "Lamp":
        //            return MFMEConstants.MFMEComponentType.Lamp;
        //        case "CheckBox":
        //            return MFMEConstants.MFMEComponentType.Checkbox;
        //        case "Label":
        //            return MFMEConstants.MFMEComponentType.Label;
        //        case "Button":
        //            return MFMEConstants.MFMEComponentType.Button;
        //        case "LED":
        //            return MFMEConstants.MFMEComponentType.Led;
        //        case "AGB Led": // the OCR incorrectly scrapes this as 'AGB' insted of 'RGB'
        //            return MFMEConstants.MFMEComponentType.RgbLed;
        //        case "Dot Alpha":
        //            return MFMEConstants.MFMEComponentType.DotAlpha;
        //        case "Alpha New":
        //            return MFMEConstants.MFMEComponentType.AlphaNew;
        //        case "Alpha":
        //            mfmeComponentTypeTextDelphiScrape = MFMEScraper.GetFieldCharacters(emulatorScraper, 9, 31, 15);
        //            if (mfmeComponentTypeTextDelphiScrape == "BFM Alpha")
        //            {
        //                return MFMEConstants.MFMEComponentType.BfmAlpha;
        //            }

        //            return MFMEConstants.MFMEComponentType.Alpha;
        //        case "Frame":
        //            return MFMEConstants.MFMEComponentType.Frame;
        //        case "Band Reel":
        //            return MFMEConstants.MFMEComponentType.BandReel;
        //        case "Disc Reel":
        //            return MFMEConstants.MFMEComponentType.DiscReel;
        //        case "FlipReel":
        //            return MFMEConstants.MFMEComponentType.FlipReel;
        //        case "Reel Bonus Ree": // the OCR doesn't pick up the final 'l' character
        //            return MFMEConstants.MFMEComponentType.JpmBonusReel;
        //        //case "BFM Alpha": // Dealt with above, the OCR can't scrape this correctly
        //        //    return MFMEConstants.MFMEComponentType.BFMAlpha;
        //        case "Proconn Matrix":
        //            return MFMEConstants.MFMEComponentType.ProconnMatrix;
        //        case "Epoch Alpha":
        //            return MFMEConstants.MFMEComponentType.EpochAlpha;
        //        case "IGT": // the OCR doesn't pick up the final 'VFD' characters
        //            return MFMEConstants.MFMEComponentType.IgtVfd;
        //        case "Plasma":
        //            return MFMEConstants.MFMEComponentType.Plasma;
        //        case "Dot Matrix":
        //            return MFMEConstants.MFMEComponentType.DotMatrix;
        //        case "Led": // the OCR doesn't pick up the 'BFM' prefix, should be "BFM Led"
        //            return MFMEConstants.MFMEComponentType.BfmLed;
        //        case "BFMColourLed":
        //            return MFMEConstants.MFMEComponentType.BfmColourLed;
        //        case "Ace Matrix":
        //            return MFMEConstants.MFMEComponentType.AceMatrix;
        //        case "Epoch Matrix":
        //            return MFMEConstants.MFMEComponentType.EpochMatrix;
        //        case "I Video": // OCR scrapes leading 'I', should be 'Video', which represents 'Barcrest/BWB Video' 
        //            return MFMEConstants.MFMEComponentType.BarcrestBwbVideo;
        //        case "BFM Video":
        //            return MFMEConstants.MFMEComponentType.BfmVideo;
        //        case "ACE Video":
        //            return MFMEConstants.MFMEComponentType.AceVideo;
        //        case "Maygay Video":
        //            return MFMEConstants.MFMEComponentType.MaygayVideo;
        //        case "Prism Lamp":
        //            return MFMEConstants.MFMEComponentType.PrismLamp;
        //        case "Bitmap":
        //            return MFMEConstants.MFMEComponentType.Bitmap;
        //        case "Border":
        //            return MFMEConstants.MFMEComponentType.Border;

        //        default:
        //            OutputLog.LogError("Component not found!  Scraped trimmed component name: " + scrapedComponentNameTrimmed);
        //            return MFMEConstants.MFMEComponentType.None;
        //    }
        //}

        //public static void DoWorkaroundFixForMFMEComponentHeightBugAfterAlphaNewComponent(
        //    InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
        //{
        //    // enter something in the angle field as wedon't scrape from that (doesn't interfere with the rest of our scraping) - just to get the Undo button available
        //    SetTextToNumber(inputSimulator, emulatorScraper,
        //        MFMEScraperConstants.kComponentPositionAngle_X, MFMEScraperConstants.kComponentPositionAngle_Y + 4, 0);

        //    // click undo
        //    LeftClickAtPosition(inputSimulator, 
        //        emulatorScraper, MFMEScraperConstants.kPropertiesUndoButton_X, MFMEScraperConstants.kPropertiesUndoButton_Y, kLongDelay);

        //    // now the Component Height value will be correct, ready for scraping, instead of '50'.
        //    // Fix for MFME bug where components before/after AlphaNew component will have height of '50' in the Component Height field,
        //    // completely unrelated to their actual height.
        //}


        //public static void SetTextToNumber(
        //    InputSimulator inputSimulator, EmulatorScraper emulatorScraper,
        //    int mouseCoordinateWithinWindowX,
        //    int mouseCoordinateWithinWindowY,
        //    int number,
        //    int offsetFromTopLeftInteriorPixels = 4)
        //{
        //    LeftClickAtPosition(
        //        inputSimulator, emulatorScraper,
        //        mouseCoordinateWithinWindowX + offsetFromTopLeftInteriorPixels, mouseCoordinateWithinWindowY + offsetFromTopLeftInteriorPixels);

        //    inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

        //    string numberString = number.ToString();
        //    for (int characterIndex = 0; characterIndex < numberString.Length; ++characterIndex)
        //    {
        //        string singleNumberString = numberString.Substring(characterIndex, 1);
        //        if (singleNumberString == "-")
        //        {
        //            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.OEM_MINUS);
        //        }
        //        else
        //        {
        //            int characterValue = int.Parse(singleNumberString);
        //            int virtualKeyCode = (int)WindowsInput.Native.VirtualKeyCode.VK_0 + characterValue;

        //            inputSimulator.Keyboard.KeyPress((WindowsInput.Native.VirtualKeyCode)virtualKeyCode);
        //        }

        //        Thread.Sleep(kVeryShortDelay);
        //    }
        //}

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

        //public static void ClickPropertiesComponentPreviousUntilOnFirstComponent(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
        //{
        //    PreviousComponentNavigationTimedOut = false;

        //    do
        //    {
        //        ClickPropertiesComponentPrevious(inputSimulator, emulatorScraper);
        //    }
        //    while (PreviousComponentNavigationTimedOut == false);
        //}

        //public static void ClickPropertiesComponentNext(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
        //{
        //    yield return ClickPropertiesComponentNavigationArrow(inputSimulator, emulatorScraper, true);
        //}

        //public static void ClickPropertiesComponentPrevious(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
        //{
        //    yield return ClickPropertiesComponentNavigationArrow(inputSimulator, emulatorScraper, false);
        //}

        //private static IEnumerator ClickPropertiesComponentNavigationArrow(
        //    InputSimulator inputSimulator, EmulatorScraper emulatorScraper, bool clickNext, float timeout = 1.0f)
        //{
        //    // capture initial 'Z order:' area pixels, click with no post delay, loop capturing pixels until they change from the initial zorder pixels 
        //    Color32[] initialZOrderPixels = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixels(
        //        MFMEScraperConstants.kPropertiesZOrder_X, MFMEScraperConstants.kPropertiesZOrder_Y,
        //        MFMEScraperConstants.kPropertiesZOrder_Width, MFMEScraperConstants.kPropertiesZOrder_Height);

        //    if (clickNext)
        //    {
        //        LeftClickAtPosition(inputSimulator, emulatorScraper,
        //            MFMEScraperConstants.kPropertiesNextButton_X, MFMEScraperConstants.kPropertiesNextButton_Y, 0f);
        //    }
        //    else
        //    {
        //        LeftClickAtPosition(inputSimulator, emulatorScraper,
        //            MFMEScraperConstants.kPropertiesPreviousButton_X, MFMEScraperConstants.kPropertiesPreviousButton_Y, 0f);
        //    }

        //    float elapsed = 0f;
        //    bool currentZOrderPixelsChanged = false;
        //    do
        //    {
        //        yield return null;
        //        elapsed += Time.deltaTime;

        //        Color32[] currentZOrderPixels = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixels(
        //                        MFMEScraperConstants.kPropertiesZOrder_X, MFMEScraperConstants.kPropertiesZOrder_Y,
        //                        MFMEScraperConstants.kPropertiesZOrder_Width, MFMEScraperConstants.kPropertiesZOrder_Height);

        //        for (int zOrderPixelIndex = 0;
        //            zOrderPixelIndex < MFMEScraperConstants.kPropertiesZOrder_Width * MFMEScraperConstants.kPropertiesZOrder_Height;
        //            ++zOrderPixelIndex)
        //        {
        //            // only need to check a single channel to detect change to Zorder text
        //            if (currentZOrderPixels[zOrderPixelIndex].r != initialZOrderPixels[zOrderPixelIndex].r)
        //            {
        //                currentZOrderPixelsChanged = true;
        //            }
        //        }
        //    }
        //    while (!currentZOrderPixelsChanged && elapsed < timeout);

        //    PreviousComponentNavigationTimedOut = elapsed >= timeout;
        //}

    }
}
