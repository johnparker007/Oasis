using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;
using Oasis.MAME;

namespace Oasis.LayoutEditor
{
    public class EditorComponentReel : EditorComponent2D
    {
        public override string HierarchyPseudoSceneName => "Reels";
        public override string HierarchyName => "Reel";

        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;
        private Material _material = null;

        public ComponentReel ComponentReel
        {
            get
            {
                return (ComponentReel)Component;
            }
        }
        

        protected override void Awake()
        {
            base.Awake();

            _image = GetComponent<Image>();

            _material = new Material(_image.material);
            _image.material = _material;
        }

        public override void Initialise(Layout.Component component)
        {
            base.Initialise(component);

            OasisImage bandOasisImage = ComponentReel.BandOasisImage;

            _texture2d = bandOasisImage.GetTexture2dCopy(true);
            _texture2d.filterMode = FilterMode.Point;
            // TODO this would be different for horizontal UV scrolling reel!
            _texture2d.wrapModeU = TextureWrapMode.Clamp;
            _texture2d.wrapModeV = TextureWrapMode.Repeat;

            _sprite = Sprite.Create(_texture2d,
                new Rect(0, 0, bandOasisImage.Width, bandOasisImage.Height), Vector2.zero);

            _image.sprite = _sprite;
            _image.preserveAspect = false;

            // set y scale TODO this would be x scale on horizontal reel

            float xScale = 1f; // TODO MFME has the 'border width' stuff, maybe factor that in?

            //float yScale = _rectTransform.rect.height / bandOasisImage.Height;
float yScale = ComponentReel.VisibleScale2D;

            _material.mainTextureScale = new Vector2(xScale, yScale);
        }

        protected override void UpdateStateFromEmulation()
        {
            if (!ComponentReel.Number.HasValue)
            {
                return;
            }

            // TODO do UV scrolling for horizontal/vertical reels
            int reelPosition = Editor.Instance.MameController.ReelValues[(int)ComponentReel.Number];
            // TODO hardcoded at 96 steps for now, just to get working with JPM impact popeye layout test
            const int kTEMPReelYPositionCount = 96;
            float normalisedOffset = (float)reelPosition / kTEMPReelYPositionCount;

            // TODO will need something better/ shared for this; on Impact, reels need to be reversed, but not on MPU4
            switch(Editor.Instance.MameController.DebugPlatformType)
            {
                case MameController.PlatformType.Impact:
                    normalisedOffset = 1f - normalisedOffset; 
                    break;
                case MameController.PlatformType.MPU4:
                default:
                    break;
            }

            if(ComponentReel.Reversed)
            {
                normalisedOffset = 1f - normalisedOffset;
            }

            // only tested for the 2d mfme style reels so far to test:
            float bandOffsetNormalisedToCorrectRendering = 0f;
            switch (Editor.Instance.MameController.DebugPlatformType)
            {
                case MameController.PlatformType.Impact:
                    // correct for JPM Impact (I think - Popeye):
                    bandOffsetNormalisedToCorrectRendering = -0.11f;
                    break;
                case MameController.PlatformType.MPU4:
                    switch(ComponentReel.Stops)
                    {
                        case 12:
                            bandOffsetNormalisedToCorrectRendering = -0.102f;
                            break;
                        case 16:
                            bandOffsetNormalisedToCorrectRendering = -0.153f;
                            break;
                        case 24:
                            break;
                        case 25:
                            break;
                    }
                    break;
                default:
                    break;
            }

            normalisedOffset += bandOffsetNormalisedToCorrectRendering;

            // TODO don't new Vector each time
            _material.mainTextureOffset = new Vector2(0f, normalisedOffset);
        }
    }

}

