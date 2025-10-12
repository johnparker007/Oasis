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

        public ComponentBackground ComponentBackground
        {
            get
            {
                return (ComponentBackground)Component; 
            }
        }

        protected override void Awake()
        {
            base.Awake();

            _image = GetComponent<Image>();
        }

        public override void Initialise(Layout.Component component)
        {
            ComponentBackground componentBackground = (ComponentBackground)component;
            OasisImage oasisImage = componentBackground.OasisImage;

            component.Position = new Vector2Int(0, 0);

            // TODO temp workaround, there are issues where the background image is larger
            // than the MFME window size, as then image gets squashed a little to fit on the texture, so
            // then all the lamps are misaligned - quick hack workaround:
            if (oasisImage != null)
            {
                component.Size = new Vector2Int(oasisImage.Width, oasisImage.Height);
            }

            base.Initialise(component);

            if(oasisImage != null)
            {
                _texture2d = oasisImage.GetTexture2dCopy();
                _texture2d.filterMode = FilterMode.Point;

                _sprite = Sprite.Create(_texture2d,
                    new Rect(0, 0, oasisImage.Width, oasisImage.Height), Vector2.zero);

                _image.sprite = _sprite;

                _image.color = Color.white;
            }
            else
            {
                //TODO: Address the real cause of this being null
                if (ComponentBackground != null) {
                    _image.color = ComponentBackground.Color;
                }
            }
        }

        protected override void Refresh()
        {
            base.Refresh();

            if(_sprite == null)
            {
                _image.color = ComponentBackground.Color;
            }
            else
            {
                _image.color = Color.white;
            }
        }

        protected override void UpdateStateFromEmulation()
        {
            // nothing to update
        }

        protected override void ShowDisplayElements(bool text)
        {
            base.ShowDisplayElements(text);

            if (text)
            {
                _image.sprite = null;
            }
            else
            {
                _image.sprite = _sprite;
            }
        }
    }

}

