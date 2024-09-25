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

        protected override void AddListeners()
        {
            base.AddListeners();
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();
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

                if (firstSelectedEditorComponent.GetType() == typeof(EditorComponentBackground))
                {
                    EditorComponentBackground editorComponentBackground = (EditorComponentBackground)firstSelectedEditorComponent;

                    // TODO

                }
            }
        }

        protected override void OnSelectionChange()
        {
            // TODO this may not be needed if it's all dealt with by the InspectorController
        }

    }
}
