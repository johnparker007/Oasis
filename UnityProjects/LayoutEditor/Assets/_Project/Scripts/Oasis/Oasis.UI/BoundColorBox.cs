using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.UI
{
    public class BoundColorBox : MonoBehaviour
    {
        public delegate bool OnValueChangedDelegate(BoundColorBox source, Color color);

        public OnValueChangedDelegate OnValueChanged;
        public OnValueChangedDelegate OnValueSubmitted;

        private ColorBox _colorBox = null;

        public ColorBox ColorBox
        {
            get
            {
                if(_colorBox == null)
                {
                    _colorBox = GetComponent<ColorBox>();
                }

                return _colorBox;
            }
        }

        public Color Color
        {
            get
            {
                return ColorBox.Color;
            }
            set
            {
                // Simple pass through for the moment:
                ColorBox.Color = value;
            }
        }

        private void Awake()
        {
            Initialise();
        }

        private void OnDestroy()
        {
            RemoveListeners();
        }

        private void Initialise()
        {
            AddListeners();
        }

        private void AddListeners()
        {
            ColorBox.onValueChanged.AddListener(OnColorBoxValueChanged);
            ColorBox.onEndEdit.AddListener(OnColorBoxEndEdit);
        }

        private void RemoveListeners()
        {
            ColorBox.onValueChanged.RemoveListener(OnColorBoxValueChanged);
            ColorBox.onEndEdit.RemoveListener(OnColorBoxEndEdit);
        }

        // Very basic for now, just pass through, ignore bool return value:
        private void OnColorBoxValueChanged(Color value)
        {
            OnValueChanged?.Invoke(this, value);
        }

        // Very basic for now, just pass through, ignore bool return value:
        private void OnColorBoxEndEdit(Color value)
        {
            if(OnValueSubmitted != null)
            {
                OnValueSubmitted.Invoke(this, value);
            }
            else
            {
                // future use
            }
        }

    }
}
