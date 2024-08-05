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

        public Font GetFont(string name)
        {
            foreach(Font mfmeFont in MfmeFonts)
            {
                if(mfmeFont.fontNames[0] == name)
                {
                    return mfmeFont;
                }
            }

            // TODO try get OS font by name

            return null;
        }
    }

}
