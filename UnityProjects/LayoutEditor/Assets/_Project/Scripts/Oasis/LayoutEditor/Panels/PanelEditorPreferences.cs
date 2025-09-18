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
        public FieldInt MameVersion;

        protected override void AddListeners()
        {
            ProjectsFolder.Input.OnValueChanged += OnProjectsFolderValueChanged;
            ProjectsFolder.Input.OnValueSubmitted += OnProjectsFolderValueChanged;

            MameVersion.Input.OnValueChanged += OnMameVersionValueChanged;
            MameVersion.Input.OnValueSubmitted += OnMameVersionValueChanged;

        }

        protected override void RemoveListeners()
        {
            ProjectsFolder.Input.OnValueChanged -= OnProjectsFolderValueChanged;
            ProjectsFolder.Input.OnValueSubmitted -= OnProjectsFolderValueChanged;

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
            ProjectsFolder.Input.Text = Editor.Instance.Preferences.ProjectsFolder;
            MameVersion.Input.Text = Editor.Instance.Preferences.MameVersion.ToString();
        }

        private bool OnProjectsFolderValueChanged(BoundInputField source, string value)
        {
            Editor.Instance.Preferences.ProjectsFolder = value;

            return true;
        }

        private bool OnMameVersionValueChanged(BoundInputField source, string value)
        {
            Editor.Instance.Preferences.MameVersion = int.Parse(value);

            return true;
        }
    }

}
