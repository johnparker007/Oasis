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
        private TMP_Text _tmpText = null;
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
            _tmpText = GetComponentInChildren<TMPro.TMP_Text>();
            _outline = GetComponent<Outline>();
        }

        protected override void Refresh()
        {
            base.Refresh();

            _number = ComponentLamp.Number;

            _tmpText.text = ComponentLamp.Text;
            _tmpText.color = ComponentLamp.TextColor;



            FontStyle fontStyle = FontManager.GetFontStyle(ComponentLamp.FontStyle);
            Font font = FontManager.Instance.GetFont(ComponentLamp.FontName, fontStyle);
            if (font != null)
            {
                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);

                if(fontAsset != null)
                {
                    // setting boldSpacing to zero appears to fix the per font inconsistent character spacing issue
                    fontAsset.boldSpacing = 0f;
                }


                _tmpText.font = fontAsset;
                const float kMfmeFontScale = 1.33333f;
                float fontSizeFloat = ComponentLamp.FontSize * kMfmeFontScale;
                _tmpText.fontSize = fontSizeFloat;

                _tmpText.font.material.SetFloat("_Sharpness", 1f);
                _tmpText.font.material.SetFloat("_GradientScale", 15f);

                // TODO font styles shold only apply if style isn't baked into font
                switch(fontStyle)
                {
                    case FontStyle.Normal:
                        break;
                    case FontStyle.Bold:
                        _tmpText.fontStyle = FontStyles.Bold;
                        break;
                    case FontStyle.Italic:
                        _tmpText.fontStyle = FontStyles.Italic;
                        break;
                    case FontStyle.BoldAndItalic:
                        _tmpText.fontStyle |= FontStyles.Bold;
                        _tmpText.fontStyle |= FontStyles.Italic;
                        break;
                }
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
            else
            {
                _sprite = null;
            }

            _outline.enabled = ComponentLamp.Outline;

            SetLampBrightness(0f);

            ShowDisplayElements(_sprite == null);
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

        protected override void ShowDisplayElements(bool forceText)
        {
            base.ShowDisplayElements(forceText);

            if (_sprite == null || forceText)
            {
                _image.sprite = null;
                _tmpText.enabled = true;
            }
            else
            {
                _image.sprite = _sprite;
                _tmpText.enabled = false;
            }

            SetLampBrightness(0f);
        }

        protected void SetLampBrightness(float brightness)
        {
            if(_image.sprite == null || Editor.Instance.DisplayText)
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

