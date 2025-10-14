using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.UI
{
    public class BoundInputField : MonoBehaviour
    {
        public delegate bool OnValueChangedDelegate(BoundInputField source, string input);

        public OnValueChangedDelegate OnValueChanged;
        public OnValueChangedDelegate OnValueSubmitted;

        private InputField _inputField = null;

        public InputField InputField
        {
            get
            {
                if(_inputField == null)
                {
                    _inputField = GetComponent<InputField>();
                }

                return _inputField;
            }
        }

        public string Text
        {
            get
            {
                return InputField.text;
            }
            set
            {
                // Simple pass through for the moment:
                InputField.text = value;
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
            InputField.onValueChanged.AddListener(OnInputFieldValueChanged);
            InputField.onEndEdit.AddListener(OnInputFieldEndEdit);
        }

        private void RemoveListeners()
        {
            InputField.onValueChanged.RemoveListener(OnInputFieldValueChanged);
            InputField.onEndEdit.RemoveListener(OnInputFieldEndEdit);
        }

        // Very basic for now, just pass through, ignore bool return value:
        private void OnInputFieldValueChanged(string value)
        {
            OnValueChanged?.Invoke(this, value);
        }

        // Very basic for now, just pass through, ignore bool return value:
        private void OnInputFieldEndEdit(string value)
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
