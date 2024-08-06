using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;
using Oasis.UI;

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
            _outline = GetComponent<Outline>();
        }

        protected override void Refresh()
        {
            base.Refresh();

            _number = ComponentLamp.Number;

            _text.text = ComponentLamp.Text;
            _text.color = ComponentLamp.TextColor;

            // TEMP hack test just to get things going!
            Font font = FontManager.Instance.GetFont(ComponentLamp.FontName);
            if (font != null)
            {
                _text.font = font;

                // Seems to be a discrepancy, so e.g: 36 in Mfme needs to be 48 in Unity
                const float kMfmeFontScale = 1.3333333333f;
                _text.fontSize = Mathf.RoundToInt(ComponentLamp.FontSize * kMfmeFontScale);

                _text.fontStyle = FontManager.GetFontStyle(ComponentLamp.FontStyle);
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

