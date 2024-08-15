using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;

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

        public Font GetFont(string name, FontStyle style, int size)
        {
            foreach(Font mfmeFont in MfmeFonts)
            {
                if(mfmeFont.fontNames[0] == name)
                {
                    return mfmeFont;
                }
            }

            // There is some issues with some OS fonts, for instance Unispace Bold is found in the list of OS
            // fonts, but upon creating, it appears to be falling back to Arial.  This may be a bug/shortcoming of the Unity
            // inbuilt CreateDynamicFontFromOSFont functionality.

            string nameWithOsStyleName = name;
            if(style != FontStyle.Normal)
            {
                nameWithOsStyleName += " " + GetOSStyleName(style);
            }
            
            string fallbackNameWithOsBold = name + " " + GetOSStyleName(FontStyle.Bold);
            string fallbackNameWithOsItalic = name + " " + GetOSStyleName(FontStyle.Italic);
            string fallbackNameWithOsBoldItalic = name + " " + GetOSStyleName(FontStyle.BoldAndItalic);

            string osFontNameToCreate = null;
            string[] installedFontNames = Font.GetOSInstalledFontNames();
            if (installedFontNames.Contains(nameWithOsStyleName))
            {
                osFontNameToCreate = nameWithOsStyleName;
            }
            else if(installedFontNames.Contains(name))
            {
                osFontNameToCreate = name;
            }
            // further fallback:
            else if(installedFontNames.Contains(fallbackNameWithOsBold))
            {
                osFontNameToCreate = fallbackNameWithOsBold;
            }
            else if (installedFontNames.Contains(fallbackNameWithOsItalic))
            {
                osFontNameToCreate = fallbackNameWithOsItalic;
            }
            else if (installedFontNames.Contains(fallbackNameWithOsBoldItalic))
            {
                osFontNameToCreate = fallbackNameWithOsBoldItalic;
            }

            if (osFontNameToCreate != null)
            {
                Font dynamicFont = Font.CreateDynamicFontFromOSFont(osFontNameToCreate, size);
                if (dynamicFont != null)
                {
                    return dynamicFont;
                }
            }

            // TODO - an Oasis-defined fallback that works better than Unity's Arial 12 point only font fallback

            return null;
        }
    }

}
