using Oasis.LayoutEditor.Tools;
using Oasis.MAME;
using Oasis.UI;
using Oasis.UI.Fields;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelEditorPreferencesMame : PanelBase
    {
        public FieldInt MameVersion;

        protected override void AddListeners()
        {
            MameVersion.Input.OnValueChanged += OnMameVersionValueChanged;
            MameVersion.Input.OnValueSubmitted += OnMameVersionValueChanged;
        }

        protected override void RemoveListeners()
        {
            MameVersion.Input.OnValueChanged -= OnMameVersionValueChanged;
            MameVersion.Input.OnValueSubmitted -= OnMameVersionValueChanged;
        }

        protected override void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;
        }

        protected override void Populate()
        {
            MameVersion.Input.Text = Editor.Instance.Preferences.MameVersion.ToString();
        }

        private bool OnMameVersionValueChanged(BoundInputField source, string value)
        {
            Editor.Instance.Preferences.MameVersion = int.Parse(value);

            return true;
        }
    }

}
