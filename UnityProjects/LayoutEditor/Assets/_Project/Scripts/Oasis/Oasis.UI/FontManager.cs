using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;
using Microsoft.Win32;
using System.IO;
using Oasis.MFME.Data;

namespace Oasis.UI
{
    public class FontManager : MonoBehaviour
    {
        public List<Font> MfmeFonts;

        public FontImportDefinitions FontImportDefinitions;

        public Material TextMeshProMaterial;

        private FontCache _fontCache = null;
        private TmpFontAssetCache _fontAssetCache = null;

        // TOIMPROVE could pull this out to scriptable object
        private static readonly Dictionary<string, string> kSubstituteFonts = new Dictionary<string, string>
        {
            { "Termina", "Courier" }, // missing ending 'l' due to crappy scraper I/l issue, also not available
        };


        private void Awake()
        {
            Initialise();
        }

        public static FontStyle GetFontStyle(string fontStyle)
        {
            // TODO need to look into this, there MUST be a somewhat better way to map from
            // font style style text to the Unity font styles?!
            switch(fontStyle)
            {
                case "Bold":
                    return FontStyle.Bold;
                case "Italic":
                case "Oblique":
                    return FontStyle.Italic;
                case "Bold Italic":
                case "Bold Oblique":
                    return FontStyle.BoldAndItalic;
                case "Regular":
                case "Normal":
                default:
                    return FontStyle.Normal;
            }
        }

        public static string GetOSStyleName(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Normal:
                    return "";
                case FontStyle.Bold:
                    return "Bold";
                case FontStyle.Italic:
                    return "Italic";
                case FontStyle.BoldAndItalic:
                    return "Bold Italic";
                default:
                    Debug.LogError("Calling with invalid style");
                    return null;
            }
        }

        public Font GetFont(string name, FontStyle style)
        {
            if(_fontCache.ContainsFont(name, style))
            {
                return _fontCache.GetFont(name, style);
            }

            foreach(Font mfmeFont in MfmeFonts)
            {
                if(mfmeFont.fontNames[0] == name)
                {
                    _fontCache.TryAddFont(mfmeFont, name, style);
                    return mfmeFont;
                }
            }

            //string[] fontPaths = Font.GetPathsToOSFonts();
            const string kWindowsFontPath = "C://Windows//Fonts"; // TODO extract font path from 1st entry in Font.GetPathsToOSFonts()
            string fontFileName = GetFontFileName(name, style);

            if (fontFileName != null)
            {
                string fontFilePath = Path.Combine(kWindowsFontPath, fontFileName);
                Font dynamicFont = new Font(fontFilePath);
                _fontCache.TryAddFont(dynamicFont, name, style);
                return dynamicFont;
            }

            // TODO - an Oasis-defined fallback that works better than Unity's Arial 12 point only font fallback

            return null;
        }

        public static string GetFontFileName(string name, FontStyle style)
        {
            // Define the registry key for system fonts
            string fontsRegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";

            // Open the registry key
            using (RegistryKey fontsKey = Registry.LocalMachine.OpenSubKey(fontsRegistryPath))
            {
                if (fontsKey != null)
                {
                    foreach (string fontRegistryName in fontsKey.GetValueNames())
                    {
                        if(!fontRegistryName.Contains("TrueType"))
                        {
                            continue;
                        }

                        // TOIMPROVE - get fonts that are specific bold or italic fonts, and then
                        // don't use TMP bold system etc


                        string searchName = name;
                        if (kSubstituteFonts.ContainsKey(name))
                        {
                            searchName = kSubstituteFonts[name];
                        }

                        string searchNameMSStripped = searchName.Replace("MS", "");
                        string fontRegistryNameMSStripped = fontRegistryName.Replace("MS", "");

                        if (fontRegistryName.StartsWith(searchName, StringComparison.OrdinalIgnoreCase))
                        {
                            return fontsKey.GetValue(fontRegistryName).ToString(); // This is the filename of the font
                        }
                        // TODO this can go wrong, for instance Serif source name could return Sans Serif, 
                        // so needs some extra code to check there are no 'non-present words' such as Sans
                        // kludgy workaround since some of the MS fonts are not in a usable format
                        else if (fontRegistryNameMSStripped.Contains(searchNameMSStripped, StringComparison.OrdinalIgnoreCase))
                        {
                            return fontsKey.GetValue(fontRegistryNameMSStripped).ToString(); // This is the filename of the font
                        }
                    }
                }
            }
            return null; // Return null if not found
        }

        public TMP_FontAsset GetTmpFontAsset(Font font)
        {
            if (_fontAssetCache.ContainsFontAsset(font))
            {
                return _fontAssetCache.GetFontAsset(font);
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset != null)
            {
                // setting boldSpacing to zero appears to fix the per font inconsistent character spacing issue
                fontAsset.boldSpacing = 0f;

                // initial test, this may need to be done differently:
                //fontAsset.material = new Material(fontAsset.material);

                _fontAssetCache.TryAddFontAsset(font, fontAsset);
            }

            return fontAsset;
        }

        public FontImportDefinition GetFontImportDefinition(string name)
        {
            return (FontImportDefinition)FontImportDefinitions.GetDefinition(name);
        }

        private void Initialise()
        {
            _fontCache = new FontCache();
            _fontAssetCache = new TmpFontAssetCache();
        }

    }

}
