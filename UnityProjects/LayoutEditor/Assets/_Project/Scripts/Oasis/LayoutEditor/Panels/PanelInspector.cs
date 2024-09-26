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
    public abstract class PanelInspector : PanelBase
    {
        public EditorComponent EditorComponent
        {
            get;
            set;
        }

        public Layout.Component Component
        {
            get
            {
                return EditorComponent.Component;
            }
        }

        public FieldVector2 Position;
        public FieldVector2 Size;
        public FieldString Text;

        protected override void Populate()
        {
            if (Editor.Instance.SelectionController.SelectedEditorComponents.Count > 0)
            {
                EditorComponent firstSelectedEditorComponent =
                    Editor.Instance.SelectionController.SelectedEditorComponents[0];

                Vector2Int componentPosition = firstSelectedEditorComponent.Component.Position;
                Position.InputX.Text = componentPosition.x.ToString();
                Position.InputY.Text = componentPosition.y.ToString();

                Vector2Int componentSize = firstSelectedEditorComponent.Component.Size;
                Size.InputX.Text = componentSize.x.ToString();
                Size.InputY.Text = componentSize.y.ToString();

                if(Text != null)
                {
                    Text.Input.Text = firstSelectedEditorComponent.Component.Text;
                }
            }
        }

        protected override void AddListeners()
        {
            Editor.Instance.SelectionController.OnSelectionChange.AddListener(OnSelectionChange);

            Position.InputX.OnValueChanged += OnPositionXValueChanged;
            Position.InputX.OnValueSubmitted += OnPositionXEndEdit;
            Position.InputY.OnValueChanged += OnPositionYValueChanged;
            Position.InputY.OnValueSubmitted += OnPositionYEndEdit;

            Size.InputX.OnValueChanged += OnSizeXValueChanged;
            Size.InputX.OnValueSubmitted += OnSizeXEndEdit;
            Size.InputY.OnValueChanged += OnSizeYValueChanged;
            Size.InputY.OnValueSubmitted += OnSizeYEndEdit;

            if(Text != null)
            {
                Text.Input.OnValueChanged += OnTextValueChanged;
                Text.Input.OnValueSubmitted += OnTextEndEdit;
            }
        }

        protected override void RemoveListeners()
        {
            Editor.Instance.SelectionController.OnSelectionChange.RemoveListener(OnSelectionChange);

            Position.InputX.OnValueChanged -= OnPositionXValueChanged;
            Position.InputX.OnValueSubmitted -= OnPositionXEndEdit;
            Position.InputY.OnValueChanged -= OnPositionYValueChanged;
            Position.InputY.OnValueSubmitted -= OnPositionYEndEdit;

            Size.InputX.OnValueChanged -= OnSizeXValueChanged;
            Size.InputX.OnValueSubmitted -= OnSizeXEndEdit;
            Size.InputY.OnValueChanged -= OnSizeYValueChanged;
            Size.InputY.OnValueSubmitted -= OnSizeYEndEdit;

            if (Text != null)
            {
                Text.Input.OnValueChanged -= OnTextValueChanged;
                Text.Input.OnValueSubmitted -= OnTextEndEdit;
            }
        }

        protected abstract void OnSelectionChange();

        private bool OnPositionXValueChanged(BoundInputField source, string value)
        {
            ProcessPositionXEdit(value);
            return true;
        }

        private bool OnPositionXEndEdit(BoundInputField source, string value)
        {
            ProcessPositionXEdit(value);
            return true;
        }

        private bool OnPositionYValueChanged(BoundInputField source, string value)
        {
            ProcessPositionYEdit(value);
            return true;
        }

        private bool OnPositionYEndEdit(BoundInputField source, string value)
        {
            ProcessPositionYEdit(value);
            return true;
        }

        private bool OnSizeXValueChanged(BoundInputField source, string value)
        {
            ProcessSizeXEdit(value);
            return true;
        }

        private bool OnSizeXEndEdit(BoundInputField source, string value)
        {
            ProcessSizeXEdit(value);
            return true;
        }

        private bool OnSizeYValueChanged(BoundInputField source, string value)
        {
            ProcessSizeYEdit(value);
            return true;
        }

        private bool OnSizeYEndEdit(BoundInputField source, string value)
        {
            ProcessSizeYEdit(value);
            return true;
        }

        private bool OnTextValueChanged(BoundInputField source, string value)
        {
            ProcessTextEdit(value);
            return true;
        }

        private bool OnTextEndEdit(BoundInputField source, string value)
        {
            ProcessTextEdit(value);
            return true;
        }

        private void ProcessPositionXEdit(string value)
        {
            if (int.TryParse(value, out int result))
            {
                Vector2Int position = Component.Position;
                position.x = result;
                Component.Position = position;
            }
        }

        private void ProcessPositionYEdit(string value)
        {
            if (int.TryParse(value, out int result))
            {
                Vector2Int position = Component.Position;
                position.y = result;
                Component.Position = position;
            }
        }

        private void ProcessSizeXEdit(string value)
        {
            if (int.TryParse(value, out int result))
            {
                Vector2Int size = Component.Size;
                size.x = result;
                Component.Size = size;
            }
        }

        private void ProcessSizeYEdit(string value)
        {
            if (int.TryParse(value, out int result))
            {
                Vector2Int size = Component.Size;
                size.y = result;
                Component.Size = size;
            }
        }

        private void ProcessTextEdit(string value)
        {
            Component.Text = value;
        }



        // suspect there will be some generic inspector stuff that can go in here, equivalent 
        // to Unity's gameObject name, tag, layer etc
    }
}
