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
            base.AddListeners();

            Number.Input.OnValueChanged += OnNumberValueChanged;
            Number.Input.OnValueSubmitted += OnNumberEndEdit;
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            Number.Input.OnValueChanged -= OnNumberValueChanged;
            Number.Input.OnValueSubmitted -= OnNumberEndEdit;
        }

        protected override void Initialise()
        {
        }

        protected override void Populate()
        {
            // TODO this is just test code for now!
            if (Editor.Instance.SelectionController.SelectedEditorComponents.Count > 0)
            {
                EditorComponent firstSelectedEditorComponent =
                    Editor.Instance.SelectionController.SelectedEditorComponents[0];

                if (firstSelectedEditorComponent.GetType() == typeof(EditorComponentLamp))
                {
                    EditorComponentLamp editorComponentLamp = (EditorComponentLamp)firstSelectedEditorComponent;
                    if(editorComponentLamp.ComponentLamp.Number.HasValue)
                    {
                        Number.Input.Text = editorComponentLamp.ComponentLamp.Number.ToString();
                    }
                    else
                    {
                        Number.Input.Text = "";
                    }
                }
            }
        }

        protected override void OnSelectionChange()
        {
            // TODO this is just test code for now!
            if(Editor.Instance.SelectionController.SelectedEditorComponents.Count > 0)
            {
                EditorComponent firstSelectedEditorComponent =
                    Editor.Instance.SelectionController.SelectedEditorComponents[0];

                if(firstSelectedEditorComponent.GetType() == typeof(EditorComponentLamp))
                {
                    // not sure if this would be doing the enable/disable of the Inspector type...
                    // for now as a test, just populate with lamp values:

                    EditorComponent = firstSelectedEditorComponent; // TODO this is just to get started, need to develop 'multi-edit' feature

                    Populate();
                }
            }
        }

        private bool OnNumberValueChanged(BoundInputField source, string value)
        {
            ProcessNumberEdit(value);

            return true;
        }

        private bool OnNumberEndEdit(BoundInputField source, string value)
        {
            ProcessNumberEdit(value);

            return true;
        }

        private void ProcessNumberEdit(string value)
        {
            if (int.TryParse(value, out int result))
            {
                ComponentLamp.Number = result;
            }
        }
    }
}
