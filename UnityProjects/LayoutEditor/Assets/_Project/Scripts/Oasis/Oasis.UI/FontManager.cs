using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;
using Microsoft.Win32;
using System.IO;

namespace Oasis.UI
{


    public class FontManager : MonoBehaviour
    {
        public List<Font> MfmeFonts;

        public static FontManager Instance
        {
            get;
            private set;
        } = null;


        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(Instance);
            }
            else if (this != Instance)
            {
                Destroy(this);
                return;
            }
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
            foreach(Font mfmeFont in MfmeFonts)
            {
                if(mfmeFont.fontNames[0] == name)
                {
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
                        string fontFile = fontsKey.GetValue(fontRegistryName).ToString();
                        // TOIMPROVE - get fonts that are specific bold or italic fonts, and then
                        // don't use TMP bold system etc
                        if (fontRegistryName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return fontFile; // This is the filename of the font
                        }
                    }
                }
            }
            return null; // Return null if not found
        }
    }

}
