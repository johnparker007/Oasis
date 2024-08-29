using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;
using Oasis.UI;
using TMPro;

namespace Oasis.LayoutEditor
{
    public class EditorComponentLamp : EditorComponent2D
    {
        public override string HierarchyPseudoSceneName => "Lamps";
        public override string HierarchyName => "Lamp";

        private int? _number = null;
        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;
        private Text _text = null;
        private TMPro.TMP_Text _tmpText = null;
        private Outline _outline = null;

        protected ComponentLamp ComponentLamp
        {
            get
            {
                return (ComponentLamp)Component;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            _image = GetComponent<Image>();
            _text = GetComponentInChildren<Text>();
            _tmpText = GetComponentInChildren<TMPro.TMP_Text>();
            _outline = GetComponent<Outline>();
        }

        protected override void Refresh()
        {
            base.Refresh();

            _number = ComponentLamp.Number;

            _text.text = ComponentLamp.Text;
            _text.color = ComponentLamp.TextColor;

            _tmpText.text = ComponentLamp.Text;
            _tmpText.color = ComponentLamp.TextColor;


            // TEMP hack test for these fonts just to get things going!

            FontStyle fontStyle = FontManager.GetFontStyle(ComponentLamp.FontStyle);
            // Seems to be a discrepancy, so e.g: 36 in Mfme needs to be 48 in Unity
            const float kMfmeFontScale = 1.3333333333f; 
            int fontSize = Mathf.RoundToInt(ComponentLamp.FontSize * kMfmeFontScale);
            Font font = FontManager.Instance.GetFont(ComponentLamp.FontName, fontStyle, fontSize);
            if (font != null)
            {
                _text.font = font;
                _text.fontSize = fontSize;
                _text.fontStyle = fontStyle; // TODO do we not need this if baked into the font we gewnerated?

                // more work to be done, can get the Unispace font to kinda work, provided don't use Italic 
                //_text.fontStyle = FontStyle.Normal;

                // otherwise we may end up with 'double-bold' fonts or double-italic fonts.


                // *** New TMP version:
                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
                _tmpText.font = fontAsset;
                //float fontSizeFloat = ComponentLamp.FontSize * kMfmeFontScale;
// test round down to int:
float fontSizeFloat = (int)(ComponentLamp.FontSize * kMfmeFontScale);
                _tmpText.fontSize = fontSizeFloat;

                // settings to give a less blurry/soft font at smaller sizes
                // (in time this may want to vary with font size, but these are a good start)
                _tmpText.font.material.SetFloat("_Sharpness", 1f);
                _tmpText.font.material.SetFloat("_GradientScale", 15f);


                //_tmpText.fontStyle = fontStyle;
            }

            // TODO THERE ARE POTENTIALLY IMAGE-RELATED MEMORY LEAKS TO FIX HERE!
            OasisImage oasisImage = ComponentLamp.OasisImage;
            if(oasisImage != null)
            {
                _texture2d = oasisImage.GetTexture2dCopy(true);
                _texture2d.filterMode = FilterMode.Point;

                _sprite = Sprite.Create(_texture2d,
                    new Rect(0, 0, oasisImage.Width, oasisImage.Height), Vector2.zero);

                _image.sprite = _sprite;
            }

            _outline.enabled = ComponentLamp.Outline;

            SetLampBrightness(0f);
        }

        protected override void UpdateStateFromEmulation()
        {
            if (!_number.HasValue)
            {
                return;
            }

            // hack for now, until we implement variable brightness lamps in MAME - lamp is always full on or full off
            float lampBrightness = MameController.LampValues[(int)_number] == 1 ? 1f : 0f;
            SetLampBrightness(lampBrightness);
        }

        protected override void ShowDisplayElements(bool text)
        {
            base.ShowDisplayElements(text);

            if (text)
            {
                _image.sprite = null;
                _text.enabled = true;
            }
            else
            {
                _image.sprite = _sprite;
                _text.enabled = false;
            }

            SetLampBrightness(0f);
        }

        protected void SetLampBrightness(float brightness)
        {
            if(Editor.Instance.DisplayText)
            {
                _image.color = Color.Lerp(ComponentLamp.OffColor, ComponentLamp.OnColor, brightness);
            }
            else
            {
                _image.color = Color.white * brightness;
            }
        }

    }

}

