using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Oasis.Layout;
using Oasis.Graphics;
using Oasis.MAME;

namespace Oasis.LayoutEditor
{
    public class EditorComponentBandReel : EditorComponent2D
    {
        private Image _image = null;
        private Sprite _sprite = null;
        private Texture2D _texture2d = null;
        private Material _material = null;

        public ComponentBandReel ComponentBandReel
        {
            get
            {
                return (ComponentBandReel)Component;
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

            OasisImage bandOasisImage = ComponentBandReel.BandOasisImage;
            if (bandOasisImage != null) 
            {
                OasisImage bandOasisImageHorizontal = bandOasisImage.ConvertToHorizontalReelBand((int)ComponentBandReel.Stops);
                _texture2d = bandOasisImageHorizontal.GetTexture2dCopy();
                _texture2d.filterMode = FilterMode.Point;
                // TODO this would be different for horizontal UV scrolling reel!
                _texture2d.wrapModeU = TextureWrapMode.Repeat;
                _texture2d.wrapModeV = TextureWrapMode.Clamp;

                _sprite = Sprite.Create(_texture2d,
                    new Rect(0, 0, bandOasisImageHorizontal.Width, bandOasisImageHorizontal.Height), Vector2.zero);

                _image.sprite = _sprite;
                _image.preserveAspect = false;
            }
            
            // set y scale TODO this would be x scale on horizontal reel

            float xScale = ComponentBandReel.VisibleScale2D; // TODO MFME has the 'border width' stuff, maybe factor that in?

            //float yScale = _rectTransform.rect.height / bandOasisImage.Height;
            float yScale = 1f;

            _material.mainTextureScale = new Vector2(xScale, yScale);
        }

        protected override void UpdateStateFromEmulation()
        {
            if (!ComponentBandReel.Number.HasValue)
            {
                return;
            }

            // TODO do UV scrolling for horizontal/vertical reels
            int reelPosition = Editor.Instance.MameController.ReelValues[(int)ComponentBandReel.Number];
            // TODO hardcoded at 96 steps for now, just to get working with JPM impact popeye layout test
            const int kTEMPReelYPositionCount = 96;
            float normalisedOffset = (float)reelPosition / kTEMPReelYPositionCount;

            // TODO will need something better/ shared for this; on Impact, reels need to be reversed, but not on MPU4
            switch (Editor.Instance.Project.Settings.FruitMachine.Platform)
            {
                case MameController.PlatformType.Impact:
                case MameController.PlatformType.Scorpion4:
                    normalisedOffset = 1f - normalisedOffset;
                    break;
                case MameController.PlatformType.MPU4:
                default:
                    break;
            }

            if (ComponentBandReel.Reversed)
            {
                normalisedOffset = 1f - normalisedOffset;
            }

            // only tested for the 2d mfme style reels so far to test:
            // TODO pull these values out to ScriptableObjects or something, if can realtime adjust
            // during dev without needing restart that will make adding new techs far less painful!
            float bandOffsetNormalisedToCorrectRendering = 
                GetNormalisedBandOffsetToCorrectRendering(Editor.Instance.Project.Settings.FruitMachine.Platform, ComponentBandReel);

            normalisedOffset += bandOffsetNormalisedToCorrectRendering;

            // TODO don't new Vector each time
            _material.mainTextureOffset = new Vector2(normalisedOffset, 0f);
        }

        private static float GetNormalisedBandOffsetToCorrectRendering(
            MameController.PlatformType platformType, ComponentBandReel componentBandReel)
        {
            float bandOffsetNormalisedToCorrectRendering = 0f;

            switch (platformType)
            {
                case MameController.PlatformType.Impact:
                    // correct for JPM Impact (I think - Popeye):
                    bandOffsetNormalisedToCorrectRendering = -0.11f;
                    break;
                case MameController.PlatformType.MPU4:
                    // correct I think for Andy Capp
                    switch (componentBandReel.Stops)
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
                case MameController.PlatformType.Scorpion4:
                    // correct for Full Throttle
                    switch (componentBandReel.Stops)
                    {
                        case 12:
                            bandOffsetNormalisedToCorrectRendering = 0.2f + ((1f/12f) * 0.4f) - (1f / 12f);
                            break;
                        case 16:
                            bandOffsetNormalisedToCorrectRendering = 0.1305f;
                            break;
                        case 24:
                            break;
                        case 25:
                            break;
                    }
                    break;
            }

            return bandOffsetNormalisedToCorrectRendering;
        }
    }

}

