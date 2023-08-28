using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponentBackground : EditorComponent
    {
        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;

        protected void Awake()
        {
            _image = GetComponent<Image>();
        }

        public override void Initialise(
            Layout.Component component, Editor layoutEditor)
        {
            base.Initialise(component, layoutEditor);

            ComponentBackground componentBackground = (ComponentBackground)component;

            OasisImage oasisImage = componentBackground.OasisImage;

            _texture2d = oasisImage.GetTexture2dCopy(true);
            _texture2d.filterMode = FilterMode.Point;

            _sprite = Sprite.Create(_texture2d,
                new Rect(0, 0, oasisImage.Width, oasisImage.Height), Vector2.zero);

            _image.sprite = _sprite;
        }
    }

}

