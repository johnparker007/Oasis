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
    public class PanelInspectorReel : PanelInspector
    {
        public EditorComponentReel EditorComponentReel
        {
            get
            {
                return (EditorComponentReel)EditorComponent;
            }
        }

        public Layout.ComponentReel ComponentReel
        {
            get
            {
                return (Layout.ComponentReel)Component;
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

                if (firstSelectedEditorComponent.GetType() == typeof(EditorComponentReel))
                {
                    EditorComponentReel editorComponentReel = (EditorComponentReel)firstSelectedEditorComponent;
                    if(editorComponentReel.ComponentReel.Number.HasValue)
                    {
                        Number.Input.Text = editorComponentReel.ComponentReel.Number.ToString();
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
            // TODO this may not be needed if it's all dealt with by the InspectorController
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
                ComponentReel.Number = result;
            }
        }
    }
}
