using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponentBackground : EditorComponent2D
    {
        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;

        protected override void Awake()
        {
            _image = GetComponent<Image>();
        }

        public override void Initialise(
            Layout.Component component, Editor layoutEditor)
        {
// XXX TODO temp workaround, there are issues where the background image is larger
// than the MFME window size, as then image gets squashed a little to fit on the texture, so
// then all the lamps are misaligned - quick hack workaround:
// THIS HACK will break if there's no background image!

int originalWidth = component.RectInt.size.x;
int originalHeight = component.RectInt.size.x;
component.RectInt = new RectInt(
    0, 0,
    ((ComponentBackground)component).OasisImage.Width,
    ((ComponentBackground)component).OasisImage.Height);
                



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

