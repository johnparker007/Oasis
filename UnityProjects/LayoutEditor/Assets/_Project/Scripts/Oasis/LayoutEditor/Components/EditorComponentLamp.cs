using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

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

        protected override void Awake()
        {
            base.Awake();

            _image = GetComponent<Image>();
            _text = GetComponentInChildren<Text>();
        }

        protected override void Refresh()
        {
            base.Refresh();

            ComponentLamp componentLamp = (ComponentLamp)Component;

            _number = componentLamp.Number;

            _text.text = componentLamp.Text;

            // TODO THERE ARE POTENTIALLY IMAGE-RELATED MEMORY LEAKS TO FIX HERE!
            OasisImage oasisImage = componentLamp.OasisImage;
            if(oasisImage != null)
            {
                _texture2d = oasisImage.GetTexture2dCopy(true);
                _texture2d.filterMode = FilterMode.Point;

                _sprite = Sprite.Create(_texture2d,
                    new Rect(0, 0, oasisImage.Width, oasisImage.Height), Vector2.zero);

                _image.sprite = _sprite;
            }
        }

        protected override void UpdateStateFromEmulation()
        {
            if (!_number.HasValue)
            {
                return;
            }

            if (Editor.Instance.MameController.LampValues[(int)_number] == 1)
            {
                _image.color = Color.white;
            }
            else
            {
                _image.color = Color.clear;
            }
        }

        protected override void ShowDisplayElements(bool text)
        {
            base.ShowDisplayElements(text);

            _image.enabled = !text;
            _text.enabled = text;
        }


    }

}

