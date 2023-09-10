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

// HACK TEST to flicker lamps
            //if(Random.value < 0.5f)
            //{
            //    _image.color = Color.white;
            //}
            //else
            //{
            //    _image.color = Color.clear;
            //}
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

// TEMP - not sure this wants to be here, just putting in now for a basic test of the
// mame controller data:
//layoutEditor.MameController.OnLampChanged.AddListener(OnLampChanged);
        }

        // THIS DIDNT WORK - due to blocking 
//        private void OnLampChanged(int lampNumber, int lampValue)
//        {
//            if (lampNumber != _number)
//            {
//                return;
//            }

//// TODO THIS IS TEMP JUST TO TEST!  need to make a better system that doesn't block etc

//            lock (this)
//            {
//                if (lampValue == 1)
//                {
//                    _image.color = Color.white;
//                }
//                else
//                {
//                    _image.color = Color.clear;
//                }
//            }
//        }
    }

}

