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
        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;

        protected override void Awake()
        {
            _image = GetComponent<Image>();
        }

        protected void Update()
        {
// HACK TEST to flicker lamps
            if(Random.value < 0.5f)
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

            OasisImage oasisImage = componentLamp.OasisImage;

            _texture2d = oasisImage.GetTexture2dCopy(true);
            _texture2d.filterMode = FilterMode.Point;

            _sprite = Sprite.Create(_texture2d,
                new Rect(0, 0, oasisImage.Width, oasisImage.Height), Vector2.zero);

            _image.sprite = _sprite;
        }
    }

}

