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
        public override string HierarchyPseudoSceneName => "Backgrounds";
        public override string HierarchyName => "Background";

        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;

        protected override void Awake()
        {
            base.Awake();

            _image = GetComponent<Image>();
        }

        public override void Initialise(Layout.Component component)
        {
        // XXX TODO temp workaround, there are issues where the background image is larger
        // than the MFME window size, as then image gets squashed a little to fit on the texture, so
        // then all the lamps are misaligned - quick hack workaround:
        // THIS HACK will break if there's no background image!

        int originalWidth = component.Size.x;
int originalHeight = component.Size.x;
            component.Position = new Vector2Int(0, 0);
            
            component.Size = new Vector2Int(
                ((ComponentBackground)component).OasisImage.Width,
                ((ComponentBackground)component).OasisImage.Height);
                



            base.Initialise(component);

            ComponentBackground componentBackground = (ComponentBackground)component;

            OasisImage oasisImage = componentBackground.OasisImage;

            _texture2d = oasisImage.GetTexture2dCopy(true);
            _texture2d.filterMode = FilterMode.Point;

            _sprite = Sprite.Create(_texture2d,
                new Rect(0, 0, oasisImage.Width, oasisImage.Height), Vector2.zero);

            _image.sprite = _sprite;
        }

        protected override void UpdateStateFromEmulation()
        {
            // nothing to update
        }
    }

}

