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
    public class PanelEditorPreferencesGeneral : PanelBase
    {
        public FieldString ServerAddress;
        public FieldString ProjectsFolder;

        protected override void AddListeners()
        {
            ProjectsFolder.Input.OnValueChanged += OnProjectsFolderValueChanged;
            ProjectsFolder.Input.OnValueSubmitted += OnProjectsFolderValueChanged;
        }

        protected override void RemoveListeners()
        {
            ProjectsFolder.Input.OnValueChanged -= OnProjectsFolderValueChanged;
            ProjectsFolder.Input.OnValueSubmitted -= OnProjectsFolderValueChanged;
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
            ProjectsFolder.Input.Text = Editor.Instance.Preferences.ProjectsFolder;
        }

        private bool OnProjectsFolderValueChanged(BoundInputField source, string value)
        {
            Editor.Instance.Preferences.ProjectsFolder = value;

            return true;
        }
    }

}
