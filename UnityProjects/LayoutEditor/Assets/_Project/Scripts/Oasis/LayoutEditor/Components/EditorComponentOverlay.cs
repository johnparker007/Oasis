using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponentOverlay : EditorComponent2D
    {
        public override string HierarchyPseudoSceneName => "Overlays";
        public override string HierarchyName => "Overlay";

        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;

        protected override void Awake()
        {
            base.Awake();

            _image = GetComponent<Image>();
        }

        protected override void Refresh()
        {
            base.Refresh();

            ComponentReel componentReel = (ComponentReel)Component;

            // TODO THERE ARE POTENTIALLY IMAGE-RELATED MEMORY LEAKS TO FIX HERE!
            OasisImage oasisImage = componentReel.OverlayOasisImage;
            if(oasisImage != null)
            {
                _texture2d = oasisImage.GetTexture2dCopy();
                _texture2d.filterMode = FilterMode.Point;

                _sprite = Sprite.Create(_texture2d,
                    new Rect(0, 0, oasisImage.Width, oasisImage.Height), Vector2.zero);

                _image.sprite = _sprite;
            }
        }

        protected override void UpdateStateFromEmulation()
        {
            // n/a - just using that for an MFME overlay holding structure for now
        }

    }

}

