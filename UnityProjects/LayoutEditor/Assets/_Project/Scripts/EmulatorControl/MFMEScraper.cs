//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//public class MFMEScraper
//{
//    public static readonly int kOffsetToMatchToPixelFromCroppedScreenshotsX = 8;
//    public static readonly int kOffsetToMatchToPixelFromCroppedScreenshotsY = 1;

//    public static readonly int kStartOffsetFromTopLeftInteriorPixelX = 2;
//    public static readonly int kStartOffsetFromTopLeftInteriorPixelY = 3;

//    public static readonly char kFirstChar = ' ';
//    public static readonly int kLastChar = 156; // can't do '£' as Unity evaluates £ as 163, not 156 ASCII

//    public static readonly Color32 kMFMEImageBoxYellow = new Color32(255, 255, 0, 255);
//    public static readonly Color32 kMFMEImageBoxWhite = new Color32(255, 255, 255, 255);
//    public static readonly Color32 kMFMEImageBoxGrey = new Color32(192, 192, 192, 255);

//    private const int kRectPixelsWorkArraySize = 1024; // using work array to avoid the slowness of repeatedly newing arrays
//    private const int kRectPixelsIntWorkArraySize = 1024; // using work array to avoid the slowness of repeatedly newing arrays




//    public static int CharacterHeight = 0;
//    public static List<int> CharacterWidths = new List<int>();
//    public static List<int> CharacterXOffsets = new List<int>();
//    public static List<int[,]> CharacterPixelData = new List<int[,]>();

//    private static Color32[] _rectPixels = new Color32[kRectPixelsWorkArraySize];
//    private static int[] _rectPixelsInt = new int[kRectPixelsIntWorkArraySize];

//    public static int CharacterWidthMaximum
//    {
//        get
//        {
//            return CharacterWidths.Max();
//        }
//    }

//    public static void Initialise()
//    {
//        int first = kFirstChar;
//        int last = kLastChar;




//        // load main source font image
//        string fontImagePath = "C:\\projects\\Arcade\\Assets\\_FMRenderer\\Textures\\Converter\\DelphiFontScraper_sourceImage.bmp";

////for(int i = 0; i < fontImageData.Length; ++i)
////        {
////            Debug.Log("i = " + i + "   " + fontImageData[i]);
////        }

//        ConverterImage fontConverterImage = new ConverterImage(fontImagePath, null, false);
//        fontConverterImage.FlipY();

//        CharacterHeight = fontConverterImage.Height - 1; // -1 as the red/green widths row is not used

//        // extract character widths
//        Color32 currentWidthColor = fontConverterImage.ImageData[0]; //top-left most pixel
//        int currentCharacterWidth = 0;
//        for (int readCharacterX = 0; readCharacterX < fontConverterImage.Width; ++readCharacterX)
//        {
//            //Debug.Log("readCharacterX == " + readCharacterX + "    " + fontConverterImage.ImageData[readCharacterX]);

//            if (fontConverterImage.ImageData[readCharacterX].r != currentWidthColor.r)
//            {
//                CharacterWidths.Add(currentCharacterWidth);
//                CharacterXOffsets.Add(readCharacterX - currentCharacterWidth);
//                currentCharacterWidth = 1;
//                currentWidthColor = fontConverterImage.ImageData[readCharacterX];
//            }
//            else if (readCharacterX == fontConverterImage.Width - 1)
//            {
//                ++currentCharacterWidth; 
//                CharacterWidths.Add(currentCharacterWidth);
//                CharacterXOffsets.Add(readCharacterX - currentCharacterWidth + 1);
//            }
//            else
//            {
//                ++currentCharacterWidth;
//            }
//        }

//        // create font characters as 0/1 int arrays
//        for (int characterIndex = 0; characterIndex < CharacterWidths.Count; ++characterIndex)
//        {
//            int characterWidth = CharacterWidths[characterIndex];
//            CharacterPixelData.Add(new int[characterWidth, CharacterHeight]);

//            for (int writeX = 0; writeX < characterWidth; ++writeX)
//            {
//                for (int writeY = 0; writeY < CharacterHeight; ++writeY)
//                {
//                    int readX = CharacterXOffsets[characterIndex] + writeX;
//                    int readY = writeY + 1; // +1 to skip the red/green widths row
//                    Color32 readPixelColor = fontConverterImage.ImageData[(readY * fontConverterImage.Width) + readX];

//                    CharacterPixelData[characterIndex][writeX, writeY] = readPixelColor.r == 0 ? 1 : 0; // black pixels equate to 1
//                }
//            }
//        }
//    }

//    // when scrapng the GameConfiguration window, x coords need to be shifted left, compared to Properties window:
//    public static int GetGameConfigurationX(int x)
//    {
//        return x - 5;
//    }

//    public static string GetDropdownCharacters(EmulatorScraper emulatorScraper, 
//        int topLeftInteriorPixelX, int topLeftInteriorPixelY, int maximumCharacters = 16, bool trim = true)
//    {
//        int dropdownAdjustedTopLeftInteriorPixelX = topLeftInteriorPixelX + 1;
//        int dropdownAdjustedTopLeftInteriorPixelY = topLeftInteriorPixelY + 1;

//        return GetFieldCharacters(emulatorScraper, 
//            dropdownAdjustedTopLeftInteriorPixelX, dropdownAdjustedTopLeftInteriorPixelY, maximumCharacters, trim);
//    }

//    public static string GetFieldCharacters(EmulatorScraper emulatorScraper, 
//        int topLeftInteriorPixelX, int topLeftInteriorPixelY, int maximumCharacters = 16, bool trim = true)
//    {
//        int readX = kOffsetToMatchToPixelFromCroppedScreenshotsX
//            + kStartOffsetFromTopLeftInteriorPixelX
//            + topLeftInteriorPixelX;

//        int readY = kOffsetToMatchToPixelFromCroppedScreenshotsY
//            + kStartOffsetFromTopLeftInteriorPixelY
//            + topLeftInteriorPixelY;

//        string characterString = "";
//        string detectedCharacter;
//        bool currentAndPreviousCharactersWereSpace;

//        do
//        {
//            int foundCharacterWidth = 0;

//            detectedCharacter = GetCharacterInRect(emulatorScraper, readX, readY, ref foundCharacterWidth, true);

//            // bodgy workaround because I (uppercase i) and l (lowercase L) are the same, but lowercase L has an extra column of blank pixels
//            // to the right for its fixed kerning.
//            if(detectedCharacter == "I")
//            {
//                int foundCharacterWidthIFirst = 0;
//                int foundCharacterWidthLFirst = 0;

//                string detectedCharacterScrapeIFirst = GetCharacterInRect(emulatorScraper, readX, readY, ref foundCharacterWidthIFirst, true);
//                string detectedCharacterScrapeLFirst = GetCharacterInRect(emulatorScraper, readX, readY, ref foundCharacterWidthLFirst, false);

//                int foundNextCharacterWidthFromIFirst = 0;
//                int foundNextCharacterWidthFromLFirst = 0;

//                string detectedNextCharacterFromScrapeIFirst = GetCharacterInRect(
//                    emulatorScraper, readX + foundCharacterWidthIFirst, readY, ref foundNextCharacterWidthFromIFirst, true);
//                string detectedNextCharacterFromScrapeLFirst = GetCharacterInRect(
//                    emulatorScraper, readX + foundCharacterWidthLFirst, readY, ref foundNextCharacterWidthFromLFirst, false);

//                if(detectedNextCharacterFromScrapeIFirst != null
//                    && detectedNextCharacterFromScrapeLFirst == null)
//                {
//                    detectedCharacter = detectedCharacterScrapeIFirst;
//                    foundCharacterWidth = foundCharacterWidthIFirst;
//                }
//                else if(detectedNextCharacterFromScrapeLFirst != null
//                    && detectedNextCharacterFromScrapeIFirst == null)
//                {
//                    detectedCharacter = detectedCharacterScrapeLFirst;
//                    foundCharacterWidth = foundCharacterWidthLFirst;
//                }
//                else
//                {
//                    Debug.LogError("Suspect we've hit an i/L - breaking to return what we have so far");
//                    break;
//                    //return null;
//                    //Debug.Break();
//                }
//            }

//            if (detectedCharacter != null)
//            {
//                characterString += detectedCharacter;
//                readX += foundCharacterWidth;
//            }

//            currentAndPreviousCharactersWereSpace = characterString.Length > 1 
//                && characterString[characterString.Length - 1] == ' '
//                && characterString[characterString.Length - 2] == ' ';
//        }
//        while (detectedCharacter != null && characterString.Length < maximumCharacters && !currentAndPreviousCharactersWereSpace);

//        if(trim)
//        {
//            characterString = characterString.Trim();
//        }

//        return characterString;
//    }

//    // TODO will need to detect list of matches, then return widest character, otherwise this will return 'I' when it is really an 'L', as the left
//    // side will match and trigger the code to return
//    private static string GetCharacterInRect(EmulatorScraper emulatorScraper, int x, int y, ref int foundCharacterWidth, bool scanForwards)
//    {
//        Color32[] rectPixelsUpsideDown = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixels(
//            x, y, CharacterWidthMaximum, CharacterHeight);

//        int rectPixelCount = rectPixelsUpsideDown.Length;

//        // this needs vertically flipping, the pixel array comes back from UwcWindow.GetPixels function upside-down:
//        //Color32[] rectPixels = new Color32[rectPixelsUpsideDown.Length];
//        for (int row = 0; row < CharacterHeight; ++row)
//        {
//            for (int column = 0; column < CharacterWidthMaximum; ++column)
//            {
//                _rectPixels[(row * CharacterWidthMaximum) + column]
//                    = rectPixelsUpsideDown[((CharacterHeight - row - 1) * CharacterWidthMaximum) + column];
//            }
//        }

//        // make int 0/1 version for clear comparison
//        //int[] rectPixelsInt = new int[rectPixelCount];
//        for (int pixelIndex = 0; pixelIndex < rectPixelCount; ++pixelIndex)
//        {
//            _rectPixelsInt[pixelIndex] = _rectPixels[pixelIndex].r < 128 ? 1 : 0; // darker pixels to 1, brighter pixels to 0
//        }

//        const bool kDebugOutput = false;
//        if(kDebugOutput)
//        {
//            string output = "";
//            for (int row = 0; row < CharacterHeight; ++row)
//            {
//                for (int column = 0; column < CharacterWidthMaximum; ++column)
//                {
//                    output += _rectPixelsInt[(row * CharacterWidthMaximum) + column];
//                }

//                output += "\n";
//            }
//            Debug.LogError(output);
//        }

//        // scan forwards or backwards through the character bitmap lookup for match
//        int checkCharacterIndexIncrement = scanForwards ? 1 : -1;

//        int checkCharacterIndex;
//        for (checkCharacterIndex = scanForwards ? 0 : kLastChar - kFirstChar - 1;
//            scanForwards ? checkCharacterIndex < kLastChar - kFirstChar + 1 : checkCharacterIndex >= 0;
//            checkCharacterIndex += checkCharacterIndexIncrement)
//        {
//            if (CheckCharacterMatch(_rectPixelsInt, checkCharacterIndex))
//            {
//                foundCharacterWidth = CharacterWidths[checkCharacterIndex];

//                if(checkCharacterIndex > 96)
//                {
//                    return "£";
//                }

//                return char.ConvertFromUtf32(checkCharacterIndex + kFirstChar);
//            }
//        }

//        return null;
//    }

//    private static bool CheckCharacterMatch(int[] readPixelsInt, int characterIndex)
//    {
//        for (int row = 0; row < CharacterHeight; ++row)
//        {
//            for (int column = 0; column < CharacterWidths[characterIndex]; ++column)
//            {
//                if (CharacterPixelData[characterIndex][column, row] != readPixelsInt[(row * CharacterWidthMaximum) + column])
//                {
//                    return false;
//                }
//            }
//        }

//        return true;
//    }

//    public static bool GetCheckboxValue(EmulatorScraper emulatorScraper, int topLeftInteriorPixelX, int topLeftInteriorPixelY)
//    {
//        // these aren't quite right, need to understand why they work!
//        const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = 10;
//        const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = 9;

//        Color32 scrapedPixelColor32 =
//            emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixel(
//                topLeftInteriorPixelX + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX, 
//                topLeftInteriorPixelY + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY);

//        return scrapedPixelColor32.r != 255;
//    }

//    public static bool GetCheckboxPropertiesResultPanePreviewValue(EmulatorScraper emulatorScraper, int topLeftInteriorPixelX, int topLeftInteriorPixelY)
//    {
//        // these aren't quite right, need to understand why they work!
//        const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = 9;
//        const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = 6;

//        Color32 scrapedPixelColor32 =
//            emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixel(
//                topLeftInteriorPixelX + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX,
//                topLeftInteriorPixelY + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY);

//        return scrapedPixelColor32.r == 0;
//    }

//    public static Color32 GetColorboxValue(EmulatorScraper emulatorScraper, int topLeftInteriorPixelX, int topLeftInteriorPixelY)
//    {
//        const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = 10 + (19 / 2);
//        const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = 9 + (19 / 2);

//        Color32 scrapedPixelColor32 =
//            emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixel(
//                topLeftInteriorPixelX + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX,
//                topLeftInteriorPixelY + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY);

//        return scrapedPixelColor32;
//    }

//    public static bool GetRadioButtonValue(EmulatorScraper emulatorScraper, int topLeftInteriorPixelX, int topLeftInteriorPixelY)
//    {
//        const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX = 12;
//        const int kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY = 8;

//        Color32 scrapedPixelColor32 =
//            emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixel(
//                topLeftInteriorPixelX + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsX,
//                topLeftInteriorPixelY + kOffsetToDetectPixelFromTopLeftAsMeasuredFromScreenshotsY);

//        return scrapedPixelColor32.r < 127;
//    }

//    public static bool IsImageBoxBlank(
//        EmulatorScraper emulatorScraper, int topLeftInteriorPixelX, int topLeftInteriorPixelY, int width, int height, bool checkerboard, bool blankIsYellow = true)
//    {
//        // I don't know why these all keep coming out differently?
//        const int kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsX = 8;
//        const int kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsY = 1;

//        int readX = topLeftInteriorPixelX + kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsX;
//        int readY = topLeftInteriorPixelY + kImageBoxOffsetToMatchToPixelFromCroppedScreenshotsY;

//        Color32[] imageBoxPixels = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixels(readX, readY, width, height);

//        for (int y = 0; y < height; ++y)
//        {
//            for (int x = 0; x < width; ++x)
//            {
//                //Color32 scrapedPixelColor32 = emulatorScraper.CurrentUwcWindowTextureBeingScraped.window.GetPixel(readX + x, readY + y);
//                Color32 scrapedPixelColor32 = imageBoxPixels[(y * width) + x];
//                if (checkerboard)
//                {
//                    if ((scrapedPixelColor32.r != kMFMEImageBoxWhite.r && scrapedPixelColor32.r != kMFMEImageBoxGrey.r)
//                        || (scrapedPixelColor32.g != kMFMEImageBoxWhite.g && scrapedPixelColor32.g != kMFMEImageBoxGrey.g)
//                        || (scrapedPixelColor32.b != kMFMEImageBoxWhite.b && scrapedPixelColor32.b != kMFMEImageBoxGrey.b))
//                    {
//                        return false;
//                    }
//                }
//                else
//                {
//                    Color32 blankColor = blankIsYellow ? kMFMEImageBoxYellow : kMFMEImageBoxWhite;

//                    if (scrapedPixelColor32.r != blankColor.r
//                        || scrapedPixelColor32.g != blankColor.g
//                        || scrapedPixelColor32.b != blankColor.b)
//                    {
//                        return false;
//                    }
//                }

//                // useful for debugging sraping:
//                //Debug.Log("x,y == " + x + "," + y + " == " + scrapedPixelColor32.r);
//            }
//        }

//        return true;
//    }

//    public static KeyCode GetKeyCode(string scrapedCharacters)
//    {
//        string scrapedCharactersTrimmed = scrapedCharacters.TrimEnd(' ');
//        switch (scrapedCharactersTrimmed)
//        {
//            // TODO worth doing escape / f0 - f9, insert, delete?

//            case "SPACE":
//                return KeyCode.Space;
//            case "1":
//                return KeyCode.Alpha1;
//            case "2":
//                return KeyCode.Alpha2;
//            case "3":
//                return KeyCode.Alpha3;
//            case "4":
//                return KeyCode.Alpha4;
//            case "5":
//                return KeyCode.Alpha5;
//            case "6":
//                return KeyCode.Alpha6;
//            case "7":
//                return KeyCode.Alpha7;
//            case "8":
//                return KeyCode.Alpha8;
//            case "9":
//                return KeyCode.Alpha9;
//            case "0":
//                return KeyCode.Alpha0;
//            case "`":
//                return KeyCode.BackQuote;
//            case "-":
//                return KeyCode.Minus;
//            case "=":
//                return KeyCode.Equals;
//            case "A":
//                return KeyCode.A;
//            case "B":
//                return KeyCode.B;
//            case "C":
//                return KeyCode.C;
//            case "D":
//                return KeyCode.D;
//            case "E":
//                return KeyCode.E;
//            case "F":
//                return KeyCode.F;
//            case "G":
//                return KeyCode.G;
//            case "H":
//                return KeyCode.H;
//            case "I":
//                return KeyCode.I;
//            case "J":
//                return KeyCode.J;
//            case "K":
//                return KeyCode.K;
//            case "L":
//                return KeyCode.L;
//            case "M":
//                return KeyCode.M;
//            case "N":
//                return KeyCode.N;
//            case "O":
//                return KeyCode.O;
//            case "P":
//                return KeyCode.P;
//            case "Q":
//                return KeyCode.Q;
//            case "R":
//                return KeyCode.R;
//            case "S":
//                return KeyCode.S;
//            case "T":
//                return KeyCode.T;
//            case "U":
//                return KeyCode.U;
//            case "V":
//                return KeyCode.V;
//            case "W":
//                return KeyCode.W;
//            case "X":
//                return KeyCode.X;
//            case "Y":
//                return KeyCode.Y;
//            case "Z":
//                return KeyCode.Z;
//            case "[":
//                return KeyCode.LeftBracket;
//            case "]":
//                return KeyCode.RightBracket;
//            case ";":
//                return KeyCode.Semicolon;
//            case "'":
//                return KeyCode.Quote;
//            case "#":
//                return KeyCode.Hash;
//            case "SHIFT":
//                return KeyCode.LeftShift;
//            case "\\":
//                return KeyCode.Backslash;
//            case ",":
//                return KeyCode.Comma;
//            case ".":
//                return KeyCode.Period;
//            case "/":
//                return KeyCode.Slash;
//            case "CTRL":
//                return KeyCode.LeftControl;
//            case "ALT":
//                return KeyCode.LeftAlt;
//            case "UP":
//                return KeyCode.UpArrow;
//            case "DOWN":
//                return KeyCode.DownArrow;
//            case "LEFT":
//                return KeyCode.LeftArrow;
//            case "RIGHT":
//                return KeyCode.RightArrow;
//            default:
//                return KeyCode.None;
//        }
//    }

//    public static WindowsInput.Native.VirtualKeyCode GetVirtualKeyCode(KeyCode unityKeycode)
//    {
//        // JP: these are UK codes to work with my keyboard
//        switch (unityKeycode)
//        {
//            case KeyCode.Space:
//                return WindowsInput.Native.VirtualKeyCode.SPACE;
//            case KeyCode.Alpha1:
//                return WindowsInput.Native.VirtualKeyCode.VK_1;
//            case KeyCode.Alpha2:
//                return WindowsInput.Native.VirtualKeyCode.VK_2;
//            case KeyCode.Alpha3:
//                return WindowsInput.Native.VirtualKeyCode.VK_3;
//            case KeyCode.Alpha4:
//                return WindowsInput.Native.VirtualKeyCode.VK_4;
//            case KeyCode.Alpha5:
//                return WindowsInput.Native.VirtualKeyCode.VK_5;
//            case KeyCode.Alpha6:
//                return WindowsInput.Native.VirtualKeyCode.VK_6;
//            case KeyCode.Alpha7:
//                return WindowsInput.Native.VirtualKeyCode.VK_7;
//            case KeyCode.Alpha8:
//                return WindowsInput.Native.VirtualKeyCode.VK_8;
//            case KeyCode.Alpha9:
//                return WindowsInput.Native.VirtualKeyCode.VK_9;
//            case KeyCode.Alpha0:
//                return WindowsInput.Native.VirtualKeyCode.VK_0;
//            case KeyCode.BackQuote:
//                return WindowsInput.Native.VirtualKeyCode.OEM_3;
//            case KeyCode.Minus:
//                return WindowsInput.Native.VirtualKeyCode.OEM_MINUS; 
//            case KeyCode.Equals:
//                return WindowsInput.Native.VirtualKeyCode.OEM_PLUS;
//            case KeyCode.A:
//                return WindowsInput.Native.VirtualKeyCode.VK_A;
//            case KeyCode.B:
//                return WindowsInput.Native.VirtualKeyCode.VK_B;
//            case KeyCode.C:
//                return WindowsInput.Native.VirtualKeyCode.VK_C;
//            case KeyCode.D:
//                return WindowsInput.Native.VirtualKeyCode.VK_D;
//            case KeyCode.E:
//                return WindowsInput.Native.VirtualKeyCode.VK_E;
//            case KeyCode.F:
//                return WindowsInput.Native.VirtualKeyCode.VK_F;
//            case KeyCode.G:
//                return WindowsInput.Native.VirtualKeyCode.VK_G;
//            case KeyCode.H:
//                return WindowsInput.Native.VirtualKeyCode.VK_H;
//            case KeyCode.I:
//                return WindowsInput.Native.VirtualKeyCode.VK_I;
//            case KeyCode.J:
//                return WindowsInput.Native.VirtualKeyCode.VK_J;
//            case KeyCode.K:
//                return WindowsInput.Native.VirtualKeyCode.VK_K;
//            case KeyCode.L:
//                return WindowsInput.Native.VirtualKeyCode.VK_L;
//            case KeyCode.M:
//                return WindowsInput.Native.VirtualKeyCode.VK_M;
//            case KeyCode.N:
//                return WindowsInput.Native.VirtualKeyCode.VK_N;
//            case KeyCode.O:
//                return WindowsInput.Native.VirtualKeyCode.VK_O;
//            case KeyCode.P:
//                return WindowsInput.Native.VirtualKeyCode.VK_P;
//            case KeyCode.Q:
//                return WindowsInput.Native.VirtualKeyCode.VK_Q;
//            case KeyCode.R:
//                return WindowsInput.Native.VirtualKeyCode.VK_R;
//            case KeyCode.S:
//                return WindowsInput.Native.VirtualKeyCode.VK_S;
//            case KeyCode.T:
//                return WindowsInput.Native.VirtualKeyCode.VK_T;
//            case KeyCode.U:
//                return WindowsInput.Native.VirtualKeyCode.VK_U;
//            case KeyCode.V:
//                return WindowsInput.Native.VirtualKeyCode.VK_V;
//            case KeyCode.W:
//                return WindowsInput.Native.VirtualKeyCode.VK_W;
//            case KeyCode.X:
//                return WindowsInput.Native.VirtualKeyCode.VK_X;
//            case KeyCode.Y:
//                return WindowsInput.Native.VirtualKeyCode.VK_Y;
//            case KeyCode.Z:
//                return WindowsInput.Native.VirtualKeyCode.VK_Z;
//            case KeyCode.LeftBracket:
//                return WindowsInput.Native.VirtualKeyCode.OEM_4;
//            case KeyCode.RightBracket:
//                return WindowsInput.Native.VirtualKeyCode.OEM_6;
//            case KeyCode.Semicolon:
//                return WindowsInput.Native.VirtualKeyCode.OEM_1;
//            case KeyCode.Quote:
//                return WindowsInput.Native.VirtualKeyCode.OEM_3;
//            case KeyCode.Hash:
//                return WindowsInput.Native.VirtualKeyCode.OEM_7;
//            case KeyCode.LeftShift:
//                return WindowsInput.Native.VirtualKeyCode.SHIFT;
//            case KeyCode.Backslash:
//                return WindowsInput.Native.VirtualKeyCode.OEM_5;
//            case KeyCode.Comma:
//                return WindowsInput.Native.VirtualKeyCode.OEM_COMMA;
//            case KeyCode.Period:
//                return WindowsInput.Native.VirtualKeyCode.OEM_PERIOD;
//            case KeyCode.Slash:
//                return WindowsInput.Native.VirtualKeyCode.OEM_2;
//            case KeyCode.LeftControl:
//                return WindowsInput.Native.VirtualKeyCode.CONTROL;
//            case KeyCode.LeftAlt:
//                return WindowsInput.Native.VirtualKeyCode.MENU;
//            case KeyCode.UpArrow:
//                return WindowsInput.Native.VirtualKeyCode.UP;
//            case KeyCode.DownArrow:
//                return WindowsInput.Native.VirtualKeyCode.DOWN;
//            case KeyCode.LeftArrow:
//                return WindowsInput.Native.VirtualKeyCode.LEFT;
//            case KeyCode.RightArrow:
//                return WindowsInput.Native.VirtualKeyCode.RIGHT;
//            case KeyCode.None:
//                return WindowsInput.Native.VirtualKeyCode.NONAME; // to be ignored by my logic
//            default:
//                Debug.LogError("Shouldn't get here! Undefined unity key code");
//                return WindowsInput.Native.VirtualKeyCode.NONAME; // to be ignored by my logic
//        }
//    }

//    public static Converter.MFMELampShape GetLampShape(string scrapedCharacters)
//    {
//        string scrapedCharactersTrimmed = scrapedCharacters.TrimEnd(' ');

//        switch (scrapedCharactersTrimmed)
//        {
//            case "Rectangle":
//            case "Square":
//            case "Rect Round":
//            case "Square Round":
//            case "Diamond":
//            case "Star":
//            case "Polygon":
//            case "Triangle Left":
//            case "Triangle Right":
//            case "Triangle Up":
//            case "Triangle Down":
//            case "Semicircle Left":
//            case "Semicircle Right":
//            case "Semicircle Up":
//            case "Semicircle Down":
//            case "Pie":
//                return Converter.MFMELampShape.Rectangle;

//            case "Ellipse":
//            case "Circle": 
//                return Converter.MFMELampShape.Circle;

//            default:
//                Debug.LogError("Couldn't match lamp shape text: " + scrapedCharactersTrimmed);
//                return Converter.MFMELampShape.Rectangle;
//        }

//    }
//}
