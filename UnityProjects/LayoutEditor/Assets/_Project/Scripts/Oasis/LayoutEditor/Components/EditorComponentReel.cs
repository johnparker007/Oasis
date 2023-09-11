using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;

namespace Oasis.LayoutEditor
{
    public class EditorComponentReel : EditorComponent2D
    {
        private int _number = -1;
        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;
        private Material _material = null;

        protected override void Awake()
        {
            _image = GetComponent<Image>();

            _material = new Material(_image.material);
            _image.material = _material;
        }

        protected void Update()
        {
            // TOIMPROVE using a -1 for this stuff is crappy code!
            if(_number == -1)
            {
                return;
            }

            // TODO do UV scrolling for horizontal/vertical reels
            int reelPosition = LayoutEditor.MameController.ReelValues[_number];
            // TODO hardcoded at 96 steps for now, just to get working with JPM impact popeye layout test
            const int kTEMPReelYPositionCount = 96;
            float normalisedOffset = (float)reelPosition / kTEMPReelYPositionCount;
            normalisedOffset = 1f - normalisedOffset; // this is reversed prob simple to to texture coord system being flipped

            const float kTEMPBandOffsetNormalisedToCorrectRendering = -0.11f; // not sure if this will be same for all techs from MAME
            normalisedOffset += kTEMPBandOffsetNormalisedToCorrectRendering;

            // TODO don't new Vector each time
            _material.mainTextureOffset = new Vector2(0f, normalisedOffset);
        }

        public override void Initialise(
            Layout.Component component, Editor layoutEditor)
        {
            base.Initialise(component, layoutEditor);

            ComponentReel componentReel = (ComponentReel)component;

            _number = componentReel.Number;

            OasisImage bandOasisImage = componentReel.BandOasisImage;

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
            float yScale = _rectTransform.rect.height / bandOasisImage.Height;
            _material.mainTextureScale = new Vector2(xScale, yScale);
        }

    }

}

