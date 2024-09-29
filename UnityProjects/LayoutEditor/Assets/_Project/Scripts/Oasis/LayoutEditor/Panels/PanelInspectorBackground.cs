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
    public class PanelInspectorBackground : PanelInspector
    {
        public EditorComponentBackground EditorComponentBackground
        {
            get
            {
                return (EditorComponentBackground)EditorComponent;
            }
        }

        public Layout.ComponentBackground ComponentBackground
        {
            get
            {
                return (Layout.ComponentBackground)Component;
            }
        }

        public FieldColor Color;

        protected override void AddListeners()
        {
            base.AddListeners();

            Color.Input.OnValueChanged += OnColorValueChanged;
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            Color.Input.OnValueChanged -= OnColorValueChanged;
        }

        protected override void Initialise()
        {
        }

        protected override void Populate()
        {
            base.Populate();

            // TODO this is just test code for now!
            if (Editor.Instance.SelectionController.SelectedEditorComponents.Count > 0)
            {
                EditorComponent firstSelectedEditorComponent =
                    Editor.Instance.SelectionController.SelectedEditorComponents[0];

                if (firstSelectedEditorComponent.GetType() == typeof(EditorComponentBackground))
                {
                    EditorComponentBackground editorComponentBackground = (EditorComponentBackground)firstSelectedEditorComponent;

                    Color.Input.Color = editorComponentBackground.ComponentBackground.Color;
                }
            }
        }

        protected override void OnSelectionChange()
        {
            // TODO this may not be needed if it's all dealt with by the InspectorController
        }

        private bool OnColorValueChanged(BoundColorBox source, Color color)
        {
            ComponentBackground.Color = color;

            return true;
        }

    }
}
