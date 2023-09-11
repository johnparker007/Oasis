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
        private int _number = -1;
        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;

        protected override void Awake()
        {
            _image = GetComponent<Image>();
        }

        protected void Update()
        {
            // TOIMPROVE using a -1 for this stuff is crappy code!
            if(_number == -1)
            {
                return;
            }

            if(LayoutEditor.MameController.LampValues[_number] == 1)
            {
                _image.color = Color.white;
            }
            else
            {
                _image.color = Color.clear;
            }
        }

        public override void Initialise(
            Layout.Component component, Editor layoutEditor)
        {
            base.Initialise(component, layoutEditor);

            ComponentLamp componentLamp = (ComponentLamp)component;

            _number = componentLamp.Number;

            OasisImage oasisImage = componentLamp.OasisImage;

            _texture2d = oasisImage.GetTexture2dCopy(true);
            _texture2d.filterMode = FilterMode.Point;

            _sprite = Sprite.Create(_texture2d,
                new Rect(0, 0, oasisImage.Width, oasisImage.Height), Vector2.zero);

            _image.sprite = _sprite;
        }

    }

}

