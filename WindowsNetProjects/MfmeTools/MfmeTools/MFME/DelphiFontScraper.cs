using Oasis.MfmeTools.Extensions;
using Oasis.MfmeTools.UnityWrappers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Oasis.MfmeTools.Shared.Mfme
{
    public static class DelphiFontScraper
    {
        public static readonly int kStartOffsetFromTopLeftInteriorPixelX = 2;
        public static readonly int kStartOffsetFromTopLeftInteriorPixelY = 3;

        public static readonly char kFirstChar = ' ';
        public static readonly int kLastChar = 156; // can't do '£' as Unity evaluates £ as 163, not 156 ASCII

        private const int kRectPixelsWorkArraySize = 1024; // using work array to avoid the slowness of repeatedly newing arrays
        private const int kRectPixelsIntWorkArraySize = 1024; // using work array to avoid the slowness of repeatedly newing arrays

        public static int CharacterHeight = 0;
        public static List<int> CharacterWidths = new List<int>();
        public static List<int> CharacterXOffsets = new List<int>();
        public static List<int[,]> CharacterPixelData = new List<int[,]>();

        private static Color32[] _rectPixels = new Color32[kRectPixelsWorkArraySize];
        private static int[] _rectPixelsInt = new int[kRectPixelsIntWorkArraySize];

        public static int CharacterWidthMaximum
        {
            get
            {
                return CharacterWidths.Max();
            }
        }


        public static void Initialise()
        {
            Bitmap delphiFontImage = new Bitmap(Properties.Resources.DelphiFontScraper_sourceImage);
            Color32[] delphiFontImageData = delphiFontImage.GetImageData();

            CharacterHeight = delphiFontImage.Height - 1; // -1 as the red/green widths row is not used

            InitialiseCharacterWidths(delphiFontImage, delphiFontImageData);
            InitialiseCharacterPixelData(delphiFontImage, delphiFontImageData);
        }

        private static void InitialiseCharacterWidths(
            Bitmap delphiFontImage, Color32[] delphiFontImageData)
        {
            Color32 currentWidthColor = delphiFontImageData[0]; //top-left most pixel
            int currentCharacterWidth = 0;
            for (int readCharacterX = 0; readCharacterX < delphiFontImage.Width; ++readCharacterX)
            {
                if (delphiFontImageData[readCharacterX].r != currentWidthColor.r)
                {
                    CharacterWidths.Add(currentCharacterWidth);
                    CharacterXOffsets.Add(readCharacterX - currentCharacterWidth);
                    currentCharacterWidth = 1;
                    currentWidthColor = delphiFontImageData[readCharacterX];
                }
                else if (readCharacterX == delphiFontImage.Width - 1)
                {
                    ++currentCharacterWidth;
                    CharacterWidths.Add(currentCharacterWidth);
                    CharacterXOffsets.Add(readCharacterX - currentCharacterWidth + 1);
                }
                else
                {
                    ++currentCharacterWidth;
                }
            }
        }

        private static void InitialiseCharacterPixelData(
            Bitmap delphiFontImage, Color32[] delphiFontImageData)
        {
            for (int characterIndex = 0; characterIndex < CharacterWidths.Count; ++characterIndex)
            {
                int characterWidth = CharacterWidths[characterIndex];
                CharacterPixelData.Add(new int[characterWidth, CharacterHeight]);

                for (int writeX = 0; writeX < characterWidth; ++writeX)
                {
                    for (int writeY = 0; writeY < CharacterHeight; ++writeY)
                    {
                        int readX = CharacterXOffsets[characterIndex] + writeX;
                        int readY = writeY + 1; // +1 to skip the red/green widths row
                        Color32 readPixelColor = delphiFontImageData[(readY * delphiFontImage.Width) + readX];

                        CharacterPixelData[characterIndex][writeX, writeY] = readPixelColor.r == 0 ? 1 : 0; // black pixels equate to 1
                    }
                }
            }
        }

        public static string GetDropdownCharacters(
            int topLeftInteriorPixelX, int topLeftInteriorPixelY, int maximumCharacters = 16, bool trim = true)
        {
            int dropdownAdjustedTopLeftInteriorPixelX = topLeftInteriorPixelX + 1;
            int dropdownAdjustedTopLeftInteriorPixelY = topLeftInteriorPixelY + 1;

            return GetFieldCharacters(
                dropdownAdjustedTopLeftInteriorPixelX, dropdownAdjustedTopLeftInteriorPixelY, maximumCharacters, trim);
        }

        public static string GetFieldCharacters(
            int topLeftInteriorPixelX, int topLeftInteriorPixelY, int maximumCharacters = 16, bool trim = true)
        {
            int readX = kStartOffsetFromTopLeftInteriorPixelX + topLeftInteriorPixelX;
            int readY = kStartOffsetFromTopLeftInteriorPixelY + topLeftInteriorPixelY;

            string characterString = "";
            string detectedCharacter;
            bool currentAndPreviousCharactersWereSpace;

            do
            {
                int foundCharacterWidth = 0;

                detectedCharacter = GetCharacterInRect(readX, readY, ref foundCharacterWidth, true);

                // bodgy workaround because I (uppercase i) and l (lowercase L) are the same, but lowercase L has an extra column of blank pixels
                // to the right for its fixed kerning.
                if (detectedCharacter == "I")
                {
                    int foundCharacterWidthIFirst = 0;
                    int foundCharacterWidthLFirst = 0;

                    string detectedCharacterScrapeIFirst = GetCharacterInRect(readX, readY, ref foundCharacterWidthIFirst, true);
                    string detectedCharacterScrapeLFirst = GetCharacterInRect(readX, readY, ref foundCharacterWidthLFirst, false);

                    int foundNextCharacterWidthFromIFirst = 0;
                    int foundNextCharacterWidthFromLFirst = 0;

                    string detectedNextCharacterFromScrapeIFirst = GetCharacterInRect(
                        readX + foundCharacterWidthIFirst, readY, ref foundNextCharacterWidthFromIFirst, true);
                    string detectedNextCharacterFromScrapeLFirst = GetCharacterInRect(
                        readX + foundCharacterWidthLFirst, readY, ref foundNextCharacterWidthFromLFirst, false);

                    if (detectedNextCharacterFromScrapeIFirst != null
                        && detectedNextCharacterFromScrapeLFirst == null)
                    {
                        detectedCharacter = detectedCharacterScrapeIFirst;
                        foundCharacterWidth = foundCharacterWidthIFirst;
                    }
                    else if (detectedNextCharacterFromScrapeLFirst != null
                        && detectedNextCharacterFromScrapeIFirst == null)
                    {
                        detectedCharacter = detectedCharacterScrapeLFirst;
                        foundCharacterWidth = foundCharacterWidthLFirst;
                    }
                    else
                    {
                        Console.WriteLine("Suspect we've hit an i/L - breaking to return what we have so far");
                        break;
                    }
                }

                if (detectedCharacter != null)
                {
                    characterString += detectedCharacter;
                    readX += foundCharacterWidth;
                }

                currentAndPreviousCharactersWereSpace = characterString.Length > 1
                    && characterString[characterString.Length - 1] == ' '
                    && characterString[characterString.Length - 2] == ' ';
            }
            while (detectedCharacter != null && characterString.Length < maximumCharacters && !currentAndPreviousCharactersWereSpace);

            if (trim)
            {
                characterString = characterString.Trim();
            }

            return characterString;
        }

        // TODO will need to detect list of matches, then return widest character, otherwise this will return 'I' when it is really an 'L', as the left
        // side will match and trigger the code to return
        private static string GetCharacterInRect(int x, int y, ref int foundCharacterWidth, bool scanForwards)
        {
            _rectPixels = MfmeScraper.CurrentWindow.GetPixels(x, y, CharacterWidthMaximum, CharacterHeight);

            // make int 0/1 version for clear comparison
            for (int pixelIndex = 0; pixelIndex < _rectPixels.Length; ++pixelIndex)
            {
                _rectPixelsInt[pixelIndex] = _rectPixels[pixelIndex].r < 128 ? 1 : 0; // darker pixels to 1, brighter pixels to 0
            }

            const bool kDebugOutput = true;
            if (kDebugOutput)
            {
                string output = "";
                for (int row = 0; row < CharacterHeight; ++row)
                {
                    for (int column = 0; column < CharacterWidthMaximum; ++column)
                    {
                        output += _rectPixelsInt[(row * CharacterWidthMaximum) + column];
                    }

                    output += "\n";
                }
                Console.WriteLine(output);
            }

            // scan forwards or backwards through the character bitmap lookup for match
            int checkCharacterIndexIncrement = scanForwards ? 1 : -1;

            int checkCharacterIndex;
            for (checkCharacterIndex = scanForwards ? 0 : kLastChar - kFirstChar - 1;
                scanForwards ? checkCharacterIndex < kLastChar - kFirstChar + 1 : checkCharacterIndex >= 0;
                checkCharacterIndex += checkCharacterIndexIncrement)
            {
                if (CheckCharacterMatch(_rectPixelsInt, checkCharacterIndex))
                {
                    foundCharacterWidth = CharacterWidths[checkCharacterIndex];

                    if (checkCharacterIndex > 96)
                    {
                        return "£";
                    }

                    return char.ConvertFromUtf32(checkCharacterIndex + kFirstChar);
                }
            }

            return null;
        }

        private static bool CheckCharacterMatch(int[] readPixelsInt, int characterIndex)
        {
            for (int row = 0; row < CharacterHeight; ++row)
            {
                for (int column = 0; column < CharacterWidths[characterIndex]; ++column)
                {
                    if (CharacterPixelData[characterIndex][column, row] != readPixelsInt[(row * CharacterWidthMaximum) + column])
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
