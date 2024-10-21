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
    public class PanelEditorPreferences : PanelBase
    {
        public FieldString ServerAddress;
        public FieldString ProjectsFolder;

        protected override void AddListeners()
        {
            ProjectsFolder.Input.OnValueChanged += OnProjectsFolderValueChanged;
            ProjectsFolder.Input.OnValueSubmitted += OnProjectsFolderEndEdit;
        }

        protected override void RemoveListeners()
        {
            ProjectsFolder.Input.OnValueChanged -= OnProjectsFolderValueChanged;
            ProjectsFolder.Input.OnValueSubmitted -= OnProjectsFolderEndEdit;
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
            ProjectsFolder.Input.Text = "TODO";
        }

        private bool OnProjectsFolderValueChanged(BoundInputField source, string value)
        {
            Debug.LogError("TODO store ProjectsFolder: " + value);

            return true;
        }

        private bool OnProjectsFolderEndEdit(BoundInputField source, string value)
        {
            Debug.LogError("TODO store ProjectsFolder: " + value);

            return true;
        }
    }

}
