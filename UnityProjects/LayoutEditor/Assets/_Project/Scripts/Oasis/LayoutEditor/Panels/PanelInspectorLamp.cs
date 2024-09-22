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
    public class PanelInspectorLamp : PanelInspector
    {
        public EditorComponentLamp EditorComponentLamp
        {
            get
            {
                return (EditorComponentLamp)EditorComponent;
            }
        }

        public Layout.ComponentLamp ComponentLamp
        {
            get
            {
                return (Layout.ComponentLamp)Component;
            }
        }

        public FieldString Number;

        protected override void AddListeners()
        {
            Number.Input.OnValueChanged += OnNumberValueChanged;
            Number.Input.OnValueSubmitted += OnNumberEndEdit;
        }

        protected override void RemoveListeners()
        {
            Number.Input.OnValueChanged -= OnNumberValueChanged;
            Number.Input.OnValueSubmitted -= OnNumberEndEdit;
        }

        protected override void Initialise()
        {
        }

        protected override void Populate()
        {
        }

        private bool OnNumberValueChanged(BoundInputField source, string value)
        {
            ComponentLamp.Number = int.Parse(value);
            return true;
        }

        private bool OnNumberEndEdit(BoundInputField source, string value)
        {
            ComponentLamp.Number = int.Parse(value);
            return true;
        }

    }
}
