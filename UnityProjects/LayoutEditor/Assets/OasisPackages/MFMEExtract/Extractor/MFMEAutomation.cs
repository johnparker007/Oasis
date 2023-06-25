//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Diagnostics;
//using UnityEngine;
//using WindowsInput;

//public static class MFMEAutomation
//{
//    public static readonly float kVeryShortDelay = 0.05f;
//    public static readonly float kShortDelay = 0.1f;
//    public static readonly float kMediumDelay = 0.25f;
//    public static readonly float kLongDelay = 0.5f;
//    public static readonly float kVeryLongDelay = 1.0f;

//    // TODO these need to be worked out exactly:
//    public static readonly int kXOffsetToGetToTopLeftOfLayout = 12;
//    public static readonly int kYOffsetToGetToTopLeftOfLayout = 90;

//    public static readonly int kPixelOffsetForTextInput = 4; 


//    public static readonly int kAdjustSize_X = 89;
//    public static readonly int kAdjustSizeX_Y = 66;
//    public static readonly int kAdjustSizeY_Y = 97;

//    public static bool PreviousComponentNavigationTimedOut
//    {
//        get;
//        private set;
//    }


//    public static IEnumerator MinimiseUnity(InputSimulator inputSimulator)
//    {
//        // give Unity chance to get running stably:
//        yield return new WaitForSeconds(kVeryLongDelay);
//        yield return new WaitForSeconds(kVeryLongDelay);
//        yield return new WaitForSeconds(kVeryLongDelay);
//        yield return new WaitForSeconds(kVeryLongDelay);

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LWIN, WindowsInput.Native.VirtualKeyCode.VK_M);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator RestoreUnityEditorWindow(InputSimulator inputSimulator)
//    {
//        yield return new WaitForSeconds(kVeryLongDelay);

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.TAB);
//    }

//    public static IEnumerator ClearStartupPopups(InputSimulator inputSimulator)
//    {
//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.SPACE);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.SPACE);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator CenterMouseOnScreen(InputSimulator inputSimulator)
//    {
//        int clickPositionPixelX = 1920 / 2;
//        int clickPositionPixelY = 1080 / 2;
//        double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
//        double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
//        inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static double GetMouseCoordinateX(int screenPixelX)
//    {
//        const int kScreenWidth = 1920; //  TODO find way to get actual display width (not Game window width)
//        return ((float)screenPixelX / kScreenWidth) * 65535;
//    }

//    public static double GetMouseCoordinateY(int screenPixelY)
//    {
//        const int kScreenHeight = 1080; //  TODO find way to get actual display height (not Game window height)
//        return ((float)screenPixelY / kScreenHeight) * 65535;
//    }

//    public static IEnumerator StopEmulationFromDebugMenu(InputSimulator inputSimulator)
//    {
//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_E);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_M);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.F4);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator EnableEditModeFromDesignMenu(InputSimulator inputSimulator)
//    {
//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_D);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_E);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator DeleteAllOverlaysFromDesignMenu(InputSimulator inputSimulator)
//    {
//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_D);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_T);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_E);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator DragDownFromTopLeftCornerToMoveUnwantedCheckboxEtc(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
//    {
//        int windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
//        int windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
//        int clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout;
//        int clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout;
//        double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
//        double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
//        inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Mouse.LeftButtonDown();
//        yield return new WaitForSeconds(kShortDelay);

//        windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
//        windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
//        clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout;
//        clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout + 16; // move any checkbox etc down out the way of future right mouse clicks
//        mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
//        mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
//        inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Mouse.LeftButtonUp();
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static void KillMFMEProcessIfNotExited(Process mfmeProcess)
//    {
//        if (mfmeProcess != null && !mfmeProcess.HasExited)
//        {
//            mfmeProcess.Kill();
//        }
//    }

//    public static IEnumerator CopyOffLampsToBackground(InputSimulator inputSimulator)
//    {
//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.VK_D);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_T);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_P);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator OpenPropertiesWindow(InputSimulator inputSimulator,
//        MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper, bool clickTopLeftOfLayoutInsteadOfCurrentPosition)
//    {
//        if (clickTopLeftOfLayoutInsteadOfCurrentPosition)
//        {
//            yield return monoBehaviour.StartCoroutine(RightClickOnTopLeftCornerOfLayout(inputSimulator, emulatorScraper));
//        }
//        else
//        {
//            inputSimulator.Mouse.RightButtonClick();
//            yield return new WaitForSeconds(kShortDelay);
//        }

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//        yield return new WaitForSeconds(kShortDelay);

//        // Can't do this as on certain components, 'P' selects a new Lam[p]s menu option (see Dennis the Menace classic layout)
//        //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_P);
//        //yield return new WaitForSeconds(kShortDelay);

//        emulatorScraper.SetScrapeChildIfFound(true, "Properties");

//        // move current selection from tab to text box in lower right so it doesn't mess with OCRing first component type
//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LSHIFT, WindowsInput.Native.VirtualKeyCode.TAB);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator LeftClickOnEmptyAreaOfDataLayout(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
//    {
//        int windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
//        int windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
//        int clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout + MFMEScraperConstants.kDataLayoutEmptyArea_X;
//        int clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout + MFMEScraperConstants.kDataLayoutEmptyArea_Y;
//        double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
//        double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
//        inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Mouse.LeftButtonClick();
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator RightClickOnTopLeftCornerOfLayout(InputSimulator inputSimulator, EmulatorScraper emulatorScraper)
//    {
//        int windowPixelX = emulatorScraper.UwcWindowTexture.window.x;
//        int windowPixelY = emulatorScraper.UwcWindowTexture.window.y;
//        int clickPositionPixelX = windowPixelX + kXOffsetToGetToTopLeftOfLayout;
//        int clickPositionPixelY = windowPixelY + kYOffsetToGetToTopLeftOfLayout;
//        // fudge so that at least for the extractor, we can extract narrow window layouts where the menu bar spills across two rows
//        clickPositionPixelY += 100; // should always be enough even for exceptionally narrow layouts

//        double mousePositionX = GetMouseCoordinateX(clickPositionPixelX);
//        double mousePositionY = GetMouseCoordinateY(clickPositionPixelY);
//        inputSimulator.Mouse.MoveMouseTo(mousePositionX, mousePositionY);
//        yield return new WaitForSeconds(kShortDelay);

//        inputSimulator.Mouse.RightButtonClick();
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator GetTextCoroutine(InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper,
//        int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY)
//    {
//        GUIUtility.systemCopyBuffer = "";

//        yield return monoBehaviour.StartCoroutine(
//            LeftClickAtPosition(inputSimulator, monoBehaviour, emulatorScraper, mouseCoordinateWithinWindowX, mouseCoordinateWithinWindowY));

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

//        yield return new WaitForSeconds(kVeryShortDelay);

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_C);

//        yield return new WaitForSeconds(kVeryShortDelay);
//    }

//    public static IEnumerator LeftClickAtPosition(InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper,
//        int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, float? overrideDelay = null)
//    {
//        yield return monoBehaviour.StartCoroutine(
//            ClickAtPosition(true, inputSimulator, emulatorScraper, mouseCoordinateWithinWindowX, mouseCoordinateWithinWindowY, overrideDelay));
//    }

//    public static IEnumerator RightClickAtPosition(InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper,
//        int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, float? overrideDelay = null)
//    {
//        yield return monoBehaviour.StartCoroutine(
//            ClickAtPosition(false, inputSimulator, emulatorScraper, mouseCoordinateWithinWindowX, mouseCoordinateWithinWindowY, overrideDelay));
//    }

//    public static IEnumerator ClickAtPosition(bool leftClick, InputSimulator inputSimulator, EmulatorScraper emulatorScraper,
//        int mouseCoordinateWithinWindowX, int mouseCoordinateWithinWindowY, float? overrideDelay = null)
//    {
//        inputSimulator.Mouse.MoveMouseTo(
//             GetMouseCoordinateWithinWindowX(mouseCoordinateWithinWindowX, emulatorScraper),
//             GetMouseCoordinateWithinWindowY(mouseCoordinateWithinWindowY, emulatorScraper));

//        yield return new WaitForSeconds(kShortDelay);

//        if (leftClick)
//        {
//            inputSimulator.Mouse.LeftButtonClick();
//        }
//        else
//        {
//            inputSimulator.Mouse.RightButtonClick();
//        }

//        if (overrideDelay.HasValue)
//        {
//            yield return new WaitForSeconds(overrideDelay.Value);
//        }
//        else
//        {
//            yield return new WaitForSeconds(kShortDelay);
//        }
//    }

//    public static double GetMouseCoordinateWithinWindowX(int withinWindowPixelX, EmulatorScraper emulatorScraper)
//    {
//        int screenPixelX = GetScreenPixelX(withinWindowPixelX, emulatorScraper);
//        return GetMouseCoordinateX(screenPixelX);
//    }

//    public static double GetMouseCoordinateWithinWindowY(int withinWindowPixelY, EmulatorScraper emulatorScraper)
//    {
//        int screenPixelY = GetScreenPixelY(withinWindowPixelY, emulatorScraper);
//        return GetMouseCoordinateY(screenPixelY);
//    }

//    public static int GetScreenPixelX(int withinWindowPixelX, EmulatorScraper emulatorScraper)
//    {
//        int windowPixelX = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.x;
//        int sreenPixelX = windowPixelX + withinWindowPixelX;

//        return sreenPixelX;
//    }

//    public static int GetScreenPixelY(int withinWindowPixelY, EmulatorScraper emulatorScraper)
//    {
//        int windowPixelY = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.y;
//        int sreenPixelY = windowPixelY + withinWindowPixelY;

//        return sreenPixelY;
//    }

//    public static string GetText(TesseractDriver tesseractDriver, EmulatorScraper emulatorScraper, int x, int y, int width, int height)
//    {
//        Color32[] scrapedPixelBlockColor32 =
//            emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixels(x, y, width, height);

//        Color[] scrapedPixelBlockColor = new Color[scrapedPixelBlockColor32.Length];
//        for (int i = 0; i < scrapedPixelBlockColor32.Length; ++i)
//        {
//            scrapedPixelBlockColor[i] = scrapedPixelBlockColor32[i];
//        }

//        Texture2D _testTexture = new Texture2D(width, height, TextureFormat.BGRA32, false);

//        _testTexture.SetPixels(scrapedPixelBlockColor);
//        _testTexture.Apply();

//        //DebugMeshRenderer.material.mainTexture = _testTexture; // may want this debug view functionality back one day

//        //UnityEngine.Debug.Log("AI OCR - MFME Component Name: " + tesseractDriver.Recognize(_testTexture));
//        string recognizedText = tesseractDriver.Recognize(_testTexture);

//        UnityEngine.Object.Destroy(_testTexture); // TODO check this still is working in this new static class...

//        return recognizedText;
//    }

//    public static Converter.MFMEComponentType GetMFMEComponentType(EmulatorScraper emulatorScraper, string scrapedComponentNameOCR)
//    {
//        string scrapedComponentNameTrimmed = scrapedComponentNameOCR.TrimEnd(' ', ':');

//        //UnityEngine.Debug.LogError("scrapedComponentNameTrimmed: " + scrapedComponentNameTrimmed);

//        string mfmeComponentTypeTextDelphiScrape;
//        switch (scrapedComponentNameTrimmed)
//        {
//            case "Background":
//                return Converter.MFMEComponentType.Background;
//            case "Matrix Alpha":
//                return Converter.MFMEComponentType.MatrixAlpha;
//            case "Seven Segment":
//                mfmeComponentTypeTextDelphiScrape = MFMEScraper.GetFieldCharacters(emulatorScraper, 9, 31, 20);
//                if (mfmeComponentTypeTextDelphiScrape == "Seven Segment Block")
//                {
//                    return Converter.MFMEComponentType.SevenSegmentBlock;
//                }

//                return Converter.MFMEComponentType.SevenSegment;
//            case "Reel":
//                return Converter.MFMEComponentType.Reel;
//            case "Lamp":
//                return Converter.MFMEComponentType.Lamp;
//            case "CheckBox":
//                return Converter.MFMEComponentType.Checkbox;
//            case "Label":
//                return Converter.MFMEComponentType.Label;
//            case "Button":
//                return Converter.MFMEComponentType.Button;
//            case "LED":
//                return Converter.MFMEComponentType.Led;
//            case "AGB Led": // the OCR incorrectly scrapes this as 'AGB' insted of 'RGB'
//                return Converter.MFMEComponentType.RgbLed;
//            case "Dot Alpha":
//                return Converter.MFMEComponentType.DotAlpha;
//            case "Alpha New":
//                return Converter.MFMEComponentType.AlphaNew;
//            case "Alpha":
//                mfmeComponentTypeTextDelphiScrape = MFMEScraper.GetFieldCharacters(emulatorScraper, 9, 31, 15);
//                if(mfmeComponentTypeTextDelphiScrape == "BFM Alpha")
//                {
//                    return Converter.MFMEComponentType.BfmAlpha;
//                }

//                return Converter.MFMEComponentType.Alpha;
//            case "Frame":
//                return Converter.MFMEComponentType.Frame;
//            case "Band Reel":
//                return Converter.MFMEComponentType.BandReel;
//            case "Disc Reel":
//                return Converter.MFMEComponentType.DiscReel;
//            case "FlipReel":
//                return Converter.MFMEComponentType.FlipReel;
//            case "Reel Bonus Ree": // the OCR doesn't pick up the final 'l' character
//                return Converter.MFMEComponentType.JpmBonusReel;
//            //case "BFM Alpha": // Dealt with above, the OCR can't scrape this correctly
//            //    return Converter.MFMEComponentType.BFMAlpha;
//            case "Proconn Matrix":
//                return Converter.MFMEComponentType.ProconnMatrix;
//            case "Epoch Alpha":
//                return Converter.MFMEComponentType.EpochAlpha;
//            case "IGT": // the OCR doesn't pick up the final 'VFD' characters
//                return Converter.MFMEComponentType.IgtVfd;
//            case "Plasma":
//                return Converter.MFMEComponentType.Plasma;
//            case "Dot Matrix":
//                return Converter.MFMEComponentType.DotMatrix;
//            case "Led": // the OCR doesn't pick up the 'BFM' prefix, should be "BFM Led"
//                return Converter.MFMEComponentType.BfmLed;
//            case "BFMColourLed":
//                return Converter.MFMEComponentType.BfmColourLed;
//            case "Ace Matrix":
//                return Converter.MFMEComponentType.AceMatrix;
//            case "Epoch Matrix":
//                return Converter.MFMEComponentType.EpochMatrix;
//            case "I Video": // OCR scrapes leading 'I', should be 'Video', which represents 'Barcrest/BWB Video' 
//                return Converter.MFMEComponentType.BarcrestBwbVideo;
//            case "BFM Video":
//                return Converter.MFMEComponentType.BfmVideo;
//            case "ACE Video":
//                return Converter.MFMEComponentType.AceVideo;
//            case "Maygay Video":
//                return Converter.MFMEComponentType.MaygayVideo;
//            case "Prism Lamp":
//                return Converter.MFMEComponentType.PrismLamp;
//            case "Bitmap":
//                return Converter.MFMEComponentType.Bitmap;
//            case "Border":
//                return Converter.MFMEComponentType.Border;

//            default:
//                UnityEngine.Debug.LogError("Component not found!  Scraped trimmed component name: " + scrapedComponentNameTrimmed);
//                return Converter.MFMEComponentType.None;
//        }
//    }

//    public static IEnumerator DoWorkaroundFixForMFMEComponentHeightBugAfterAlphaNewComponent(
//        InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper)
//    {
//        // enter something in the angle field as wedon't scrape from that (doesn't interfere with the rest of our scraping) - just to get the Undo button available
//        yield return monoBehaviour.StartCoroutine(SetTextToNumber(inputSimulator, monoBehaviour, emulatorScraper,
//            MFMEScraperConstants.kComponentPositionAngle_X, MFMEScraperConstants.kComponentPositionAngle_Y + 4, 0));

//        // click undo
//        yield return LeftClickAtPosition(inputSimulator, monoBehaviour, 
//            emulatorScraper, MFMEScraperConstants.kPropertiesUndoButton_X, MFMEScraperConstants.kPropertiesUndoButton_Y, kLongDelay);

//        // now the Component Height value will be correct, ready for scraping, instead of '50'.
//        // Fix for MFME bug where components before/after AlphaNew component will have height of '50' in the Component Height field,
//        // completely unrelated to their actual height.
//    }


//    public static IEnumerator SetTextToNumber(
//        InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper,
//        int mouseCoordinateWithinWindowX,
//        int mouseCoordinateWithinWindowY,
//        int number,
//        int offsetFromTopLeftInteriorPixels = 4)
//    {
//        yield return monoBehaviour.StartCoroutine(LeftClickAtPosition(
//            inputSimulator, monoBehaviour, emulatorScraper, 
//            mouseCoordinateWithinWindowX + offsetFromTopLeftInteriorPixels, mouseCoordinateWithinWindowY + offsetFromTopLeftInteriorPixels));

//        inputSimulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

//        string numberString = number.ToString();
//        for (int characterIndex = 0; characterIndex < numberString.Length; ++characterIndex)
//        {
//            string singleNumberString = numberString.Substring(characterIndex, 1);
//            if (singleNumberString == "-")
//            {
//                inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.OEM_MINUS);
//            }
//            else
//            {
//                int characterValue = int.Parse(singleNumberString);
//                int virtualKeyCode = (int)WindowsInput.Native.VirtualKeyCode.VK_0 + characterValue;

//                inputSimulator.Keyboard.KeyPress((WindowsInput.Native.VirtualKeyCode)virtualKeyCode);
//            }

//            yield return new WaitForSeconds(kVeryShortDelay);
//        }
//    }

//    public static IEnumerator SetDropdownToValue(
//        InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper,
//        int dropdownPositionX, int dropdownPositionY, string value, bool resetToTopOfList)
//    {
//        yield return monoBehaviour.StartCoroutine(LeftClickAtPosition(inputSimulator, monoBehaviour, emulatorScraper,
//            dropdownPositionX + kPixelOffsetForTextInput,
//            dropdownPositionY + kPixelOffsetForTextInput));

//        if(resetToTopOfList)
//        {
//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.HOME);
//            yield return new WaitForSeconds(kLongDelay);
//        }

//        while (MFMEScraper.GetDropdownCharacters(emulatorScraper, dropdownPositionX, dropdownPositionY).TrimEnd() != value)
//        {
//            inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.DOWN);
//            yield return new WaitForSeconds(kShortDelay);
//        }

//        inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
//        yield return new WaitForSeconds(kShortDelay);
//    }

//    public static IEnumerator ClickPropertiesComponentPreviousUntilOnFirstComponent(InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper)
//    {
//        PreviousComponentNavigationTimedOut = false;

//        do
//        {
//            yield return ClickPropertiesComponentPrevious(inputSimulator, monoBehaviour, emulatorScraper);
//        }
//        while (PreviousComponentNavigationTimedOut == false);
//    }

//    public static IEnumerator ClickPropertiesComponentNext(InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper)
//    {
//        yield return ClickPropertiesComponentNavigationArrow(inputSimulator, monoBehaviour, emulatorScraper, true);
//    }

//    public static IEnumerator ClickPropertiesComponentPrevious(InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper)
//    {
//        yield return ClickPropertiesComponentNavigationArrow(inputSimulator, monoBehaviour, emulatorScraper, false);
//    }

//    private static IEnumerator ClickPropertiesComponentNavigationArrow(
//        InputSimulator inputSimulator, MonoBehaviour monoBehaviour, EmulatorScraper emulatorScraper, bool clickNext, float timeout = 1.0f)
//    {
//        // capture initial 'Z order:' area pixels, click with no post delay, loop capturing pixels until they change from the initial zorder pixels 
//        Color32[] initialZOrderPixels = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixels(
//            MFMEScraperConstants.kPropertiesZOrder_X, MFMEScraperConstants.kPropertiesZOrder_Y,
//            MFMEScraperConstants.kPropertiesZOrder_Width, MFMEScraperConstants.kPropertiesZOrder_Height);

//        if(clickNext)
//        {
//            yield return LeftClickAtPosition(inputSimulator, monoBehaviour, emulatorScraper,
//                MFMEScraperConstants.kPropertiesNextButton_X, MFMEScraperConstants.kPropertiesNextButton_Y, 0f);
//        }
//        else
//        {
//            yield return LeftClickAtPosition(inputSimulator, monoBehaviour, emulatorScraper,
//                MFMEScraperConstants.kPropertiesPreviousButton_X, MFMEScraperConstants.kPropertiesPreviousButton_Y, 0f);
//        }

//        float elapsed = 0f;
//        bool currentZOrderPixelsChanged = false;
//        do
//        {
//            yield return null;
//            elapsed += Time.deltaTime;

//            Color32[] currentZOrderPixels = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixels(
//                            MFMEScraperConstants.kPropertiesZOrder_X, MFMEScraperConstants.kPropertiesZOrder_Y,
//                            MFMEScraperConstants.kPropertiesZOrder_Width, MFMEScraperConstants.kPropertiesZOrder_Height);

//            for (int zOrderPixelIndex = 0;
//                zOrderPixelIndex < MFMEScraperConstants.kPropertiesZOrder_Width * MFMEScraperConstants.kPropertiesZOrder_Height;
//                ++zOrderPixelIndex)
//            {
//                // only need to check a single channel to detect change to Zorder text
//                if (currentZOrderPixels[zOrderPixelIndex].r != initialZOrderPixels[zOrderPixelIndex].r)
//                {
//                    currentZOrderPixelsChanged = true;
//                }
//            }
//        }
//        while (!currentZOrderPixelsChanged && elapsed < timeout);

//        PreviousComponentNavigationTimedOut = elapsed >= timeout;
//    }

//}
